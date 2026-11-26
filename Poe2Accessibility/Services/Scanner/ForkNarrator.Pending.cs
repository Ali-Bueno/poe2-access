namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>The ways of the forks walked through that still go on, told when they are left behind (docs/radar.md "Ways left pending").</summary>
    public sealed partial class ForkNarrator
    {
        /// <summary>How often the pending ways are checked against what was seen since.</summary>
        private const int PendingCheckMs = 1000;

        /// <summary>A reminder of the nearest pending way, at a dead end or an exit, at most this often.</summary>
        private const int ReminderGapMs = 20_000;

        /// <summary>Scanner ids of the pending ways, apart from the scanner's own ranges.</summary>
        private const uint PendingIdBase = 0xD0000000;

        private readonly PendingWays pending = new(TrailCells);
        private readonly HashSet<int> remindedAt = new();
        private readonly Dictionary<uint, ScanEntry> exitsTold = new();
        private long lastPendingCheck = long.MinValue / 2;
        private long lastReminder = long.MinValue / 2;

        /// <summary>Which of the pending-way lines are on: on leaving a fork, at a dead end, at an exit of the area.</summary>
        public Func<(bool Leaving, bool DeadEnds, bool Exits)>? PendingLines { get; set; }

        private void ResetPending()
        {
            this.pending.Reset();
            this.remindedAt.Clear();
            this.exitsTold.Clear();
        }

        private void TickPending(MapTopology topology, Vector2 here, Render player, float toWorld, long now)
        {
            var lines = this.PendingLines?.Invoke() ?? (false, false, false);
            if (now - this.lastPendingCheck >= PendingCheckMs)
            {
                this.lastPendingCheck = now;
                this.pending.Drop(way => this.StateOf(topology, way.Node, way.Way) != ForkWay.GoesOn);
            }

            var hidden = this.catalog.OffPlayersPiece();
            if (this.pending.Leave(here, hidden) is { } left && lines.Leaving)
            {
                var sides = left.Select(way => Announcements.HintSides[ScreenProjection.Octant(player.WorldPosition, (way.Way.Ahead - here) * toWorld)]).Distinct().ToList();
                this.output.Say(SpeechKind.Places, Announcements.PendingLeft(sides.Count, sides));
            }

            var shown = this.pending.CountShown(hidden);
            if (shown == 0 || now - this.lastReminder < ReminderGapMs)
            {
                return;
            }

            if (lines.DeadEnds && topology.NodeNear(here, EnterSlackCells) is { } node && !this.remindedAt.Contains(node) && IsStop(topology, node) &&
                topology.Ways(node).Where(way => !way.Nook).All(way => this.StateOf(topology, node, way) != ForkWay.GoesOn) &&
                this.Pending(here, toWorld, player, node, hidden) is { } where)
            {
                this.remindedAt.Add(node);
                this.lastReminder = now;
                this.output.Say(SpeechKind.Places, Announcements.Sentences(new[] { Announcements.PendingNothingHere, where }));
                return;
            }

            if (lines.Exits && this.ExitReached() is { } exit && this.Pending(here, toWorld, player, null, hidden) is { } nearest)
            {
                this.exitsTold[exit.Id] = exit;
                this.lastReminder = now;
                this.output.Say(SpeechKind.Places, Announcements.Sentences(new[] { Announcements.PendingBeforeExit(shown), nearest }));
            }
        }

        /// <summary>A place where a player stops to look for the way on: a dead end, a fork or a room; not a bend on a path walked back along.</summary>
        private static bool IsStop(MapTopology topology, int node) =>
            topology.Nodes[node].Kind is TopologyNodeKind.End or TopologyNodeKind.Room || topology.Ways(node).Count(way => !way.Nook) >= 3;

        /// <summary>How a way out of a place stands now, as the fork's line tells it.</summary>
        private ForkWay StateOf(MapTopology topology, int node, Way way) =>
            topology.IsDeadEndWay(node, way, out var end) && EntityCatalog.SeenWhole(end) ? ForkWay.DeadEnd
            : this.catalog.WayExplored(topology, node, way) ? ForkWay.Explored
            : ForkWay.GoesOn;

        /// <summary>"Camino pendiente en Cruce 3, 60 pasos, abajo 3, luego a la izquierda": the nearest pending way but those of <paramref name="exceptNode"/>.</summary>
        private string? Pending(Vector2 here, float toWorld, Render player, int? exceptNode, Func<Vector2, bool>? hidden)
        {
            if (this.pending.Nearest(here, exceptNode, hidden) is not { } way)
            {
                return null;
            }

            var at = way.Way.Ahead * toWorld;
            var entry = new ScanEntry(PendingIdBase + (uint)way.Way.Edge, ScanCategory.Unexplored, Announcements.PendingWayAt(way.Place), at.X, at.Y, player.WorldPosition.Z);
            return this.catalog.Describe(entry);
        }

        /// <summary>An exit of the area the player has just reached and was not told of since coming near it; null otherwise.</summary>
        private ScanEntry? ExitReached()
        {
            var awayWorld = 2 * ExplorationMap.LookCells * Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            foreach (var (id, told) in this.exitsTold.ToList())
            {
                if (this.catalog.DistanceTo(told) is not { } distance || distance > awayWorld)
                {
                    this.exitsTold.Remove(id);
                }
            }

            return this.catalog.Entries().FirstOrDefault(entry => entry.Category == ScanCategory.Exits && entry.Kind == QuestMatcher.ExitKind &&
                                                                   !this.exitsTold.ContainsKey(entry.Id) && this.catalog.DistanceTo(entry) <= Beacon.ArrivalWorld);
        }
    }
}
