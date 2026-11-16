namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Says a fork of the map's shape as the player walks into it, once an area: which ways go on, which are dead ends a look
    ///     shows whole, which are explored; never the way the player came by, and nothing when no way is new (the owner,
    ///     2026-11-16; <see cref="ForkTracker"/>, docs/radar.md "Forks").
    /// </summary>
    public sealed class ForkNarrator
    {
        private const int PollMs = 250;

        /// <summary>Where the player came from is taken this far back along the walk (cells), past the fork's own ground.</summary>
        private const float TrailCells = 20f;

        /// <summary>A fork is walked into within its radius and this much more (cells).</summary>
        private const float EnterSlackCells = 2f;

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly ForkTracker tracker = new();
        private MapTopology? shape;
        private Vector2 trailPoint;
        private Vector2 cameFrom;
        private long lastPoll = long.MinValue / 2;

        public ForkNarrator(EntityCatalog catalog, IAccessibilityOutputService output)
        {
            this.catalog = catalog;
            this.output = output;
        }

        /// <summary>A new area: every fork is news again.</summary>
        public void Reset()
        {
            this.tracker.Reset();
            this.shape = null;
            this.trailPoint = default;
            this.cameFrom = default;
        }

        /// <summary>Per frame; <paramref name="quiet"/> (a fight) holds a fork back until it is over, if the player is still in it.</summary>
        public void Tick(bool playing, bool speak, bool quiet)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (!playing || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            if (Vector2.Distance(here, this.trailPoint) > TrailCells)
            {
                this.cameFrom = this.trailPoint - here;
                this.trailPoint = here;
            }

            if (this.catalog.Topology() is not { } topology)
            {
                return;
            }

            if (!ReferenceEquals(topology, this.shape))
            {
                this.tracker.Reset(); // node ids belong to one build
                this.shape = topology;
            }

            if (!speak || quiet)
            {
                return;
            }

            var near = topology.NodeNear(here, EnterSlackCells);
            var node = near is { } id && topology.Nodes[id].Kind != TopologyNodeKind.End ? near : null;
            if (this.tracker.Enter(node) is not { } fork)
            {
                return;
            }

            var ways = topology.Ways(fork).Where(way => !way.Nook).ToList();
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (ways.Count < 3 || toWorld <= 0)
            {
                return; // a bend or a corridor through a room is no fork
            }

            var back = ForkTracker.WayBack(ways.Select(way => way.Direction).ToList(), this.cameFrom + (here - topology.Nodes[fork].Centre));
            var told = new List<(string Side, ForkWay State)>();
            for (var i = 0; i < ways.Count; i++)
            {
                if (i == back)
                {
                    continue;
                }

                var way = ways[i];
                var state = topology.IsDeadEndWay(fork, way, out var end) && EntityCatalog.SeenWhole(end) ? ForkWay.DeadEnd
                    : this.catalog.WasSeen(way.Ahead) && this.catalog.WasSeen(topology.Nodes[way.To].Centre) ? ForkWay.Explored
                    : ForkWay.GoesOn;
                told.Add((Announcements.HintSides[ScreenProjection.Octant(player.WorldPosition, way.Direction * toWorld)], state));
            }

            if (ForkTracker.Tell(told) is { } line)
            {
                this.output.Say(SpeechKind.Places, Announcements.Fork(topology.Nodes[fork].Kind == TopologyNodeKind.Room, line));
            }
        }
    }
}
