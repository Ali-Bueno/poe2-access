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
    ///     2026-11-16; <see cref="ForkTracker"/>, docs/radar.md "Forks"). Also the named rooms walked into, in the same line
    ///     as a fork in them ("Capilla: sigue…", <see cref="RoomTracker"/>, docs/research/rooms.md).
    /// </summary>
    public sealed class ForkNarrator
    {
        private const int PollMs = 250;

        /// <summary>Where the player came from is taken this far back along the walk (cells), past the fork's own ground.</summary>
        private const float TrailCells = 20f;

        /// <summary>A fork is walked into within its radius and this much more (cells).</summary>
        private const float EnterSlackCells = 2f;

        /// <summary>A map piece this much past a place's radius (cells, about a step) still stands in it.</summary>
        private const float FeatureSlackCells = 10f;

        /// <summary>Which piece names a place, the most telling first: rooms by themselves, then what changes the way, then furniture.</summary>
        private static readonly string[] FeatureOrder =
        {
            "ballroom", "church", "temple", "library", "courtyard", "arena", "forge", "market", "graveyard",
            "stairs_down", "stairs_up", "stairs", "ramp", "bridge", "hole", "chasm", "waterfall", "gate", "door", "walkway", "platform",
            "fireplace", "organ", "statue", "well", "window", "pillar", "cell", "oven", "tomb", "ruins", "tower", "hut", "boat", "dock", "garden", "lake",
        };

        private readonly EntityCatalog catalog;
        private readonly IAccessibilityOutputService output;
        private readonly ForkTracker tracker = new();
        private readonly RoomTracker roomTracker = new();
        private MapTopology? shape;
        private Vector2 trailPoint;
        private Vector2 cameFrom;
        private long lastPoll = long.MinValue / 2;

        public ForkNarrator(EntityCatalog catalog, IAccessibilityOutputService output)
        {
            this.catalog = catalog;
            this.output = output;
        }

        /// <summary>The map pieces the tiles name, in grid cells (<see cref="PlaceNarrator.Pieces"/>).</summary>
        public Func<IReadOnlyList<(Vector2 Grid, string Kind)>>? Pieces { get; set; }

        /// <summary>The named room at a grid cell (<see cref="PlaceNarrator.RoomAt"/>).</summary>
        public Func<Vector2, NamedRoom?>? RoomAt { get; set; }

        /// <summary>A new area: every fork and room is news again.</summary>
        public void Reset()
        {
            this.tracker.Reset();
            this.roomTracker.Reset();
            this.shape = null;
            this.trailPoint = default;
            this.cameFrom = default;
        }

        /// <summary>
        ///     Per frame: says the forks (<paramref name="forks"/>) and the named rooms (<paramref name="rooms"/>) walked into;
        ///     <paramref name="quiet"/> (a fight) holds them back until it is over, if the player is still in them.
        /// </summary>
        public void Tick(bool playing, bool forks, bool rooms, bool quiet)
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

            var topology = forks ? this.catalog.Topology() : null;
            if (topology != null && !ReferenceEquals(topology, this.shape))
            {
                this.tracker.Reset(); // node ids belong to one build
                this.shape = topology;
            }

            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (quiet || toWorld <= 0)
            {
                return;
            }

            var named = rooms ? this.RoomAt?.Invoke(here) : null;
            var roomName = named is { } inRoom ? Announcements.RoomName(inRoom.Label) : null;
            var roomNews = this.roomTracker.Enter(roomName != null ? named : null, here);
            var empty = Array.Empty<(ForkWay State, IReadOnlyList<string> Sides)>();
            var said = topology != null && this.Fork(topology, here, player, toWorld, roomName) is { } fork
                ? RoomTracker.Line(roomName, roomNews, (fork.Name, fork.Again), fork.Ways)
                : RoomTracker.Line(roomName, roomNews, null, empty);
            if (said is { } line)
            {
                this.output.Say(SpeechKind.Places, Announcements.Fork(line.Name, line.Again, line.Ways));
            }
        }

        /// <summary>The fork or room of the map's shape just walked into, when it is news, with the ways to tell; null otherwise.</summary>
        private (string Name, bool Again, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> Ways)? Fork(MapTopology topology, Vector2 here, Render player, float toWorld, string? roomName)
        {
            var near = topology.NodeNear(here, EnterSlackCells);
            var node = near is { } id && topology.Nodes[id].Kind != TopologyNodeKind.End ? near : null;
            var ways = node is { } at ? topology.Ways(at).Where(way => !way.Nook).ToList() : new List<Way>();
            var room = node is { } placed && topology.Nodes[placed].Kind == TopologyNodeKind.Room;

            // A bend or a corridor through a junction is no place; a room is one whatever its ways.
            var place = room || ways.Count >= 3 ? node : null;
            var centre = place is { } p ? topology.Nodes[p].Centre : here;
            if (this.tracker.Enter(place, centre, here, () => Announcements.ForkName(room, this.Feature(topology.Nodes[place!.Value])), roomName) is not { } entered)
            {
                return null;
            }

            var back = ForkTracker.WayBack(ways.Select(way => way.Direction).ToList(), this.cameFrom + (here - centre));
            var told = new List<(string Side, ForkWay State)>();
            for (var i = 0; i < ways.Count; i++)
            {
                if (i == back)
                {
                    continue;
                }

                var way = ways[i];
                var state = topology.IsDeadEndWay(entered.Node, way, out var end) && EntityCatalog.SeenWhole(end) ? ForkWay.DeadEnd
                    : this.catalog.WayExplored(topology, entered.Node, way) ? ForkWay.Explored
                    : ForkWay.GoesOn;
                told.Add((Announcements.HintSides[ScreenProjection.Octant(player.WorldPosition, way.Direction * toWorld)], state));
            }

            IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> line = ways.Count >= 3 ? ForkTracker.Tell(told) : Array.Empty<(ForkWay, IReadOnlyList<string>)>();
            return (entered.Name, entered.Again, line);
        }

        /// <summary>The most telling map piece standing in a place, to name it by; null for none.</summary>
        private string? Feature(TopologyNode place)
        {
            var reach = place.Radius + FeatureSlackCells;
            var kinds = (this.Pieces?.Invoke() ?? Array.Empty<(Vector2 Grid, string Kind)>())
                .Where(piece => Vector2.Distance(piece.Grid, place.Centre) <= reach)
                .Select(piece => piece.Kind).ToHashSet();
            return FeatureOrder.FirstOrDefault(kinds.Contains);
        }
    }
}
