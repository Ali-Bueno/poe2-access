namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The exits of the room or fork the player stands in, all sounding together, each from where it leaves, as a sighted player
    ///     sees a room's doorways at a glance (the owner, 2026-11-16, lost in Ogham Manor: "me cuesta encontrar salidas").
    ///     What sounds tells the exit's material or setting ("madera crujiendo para un puente, viento en salidas de desiertos");
    ///     how it sounds tells its state: a way not walked full, an explored one quieter and muffled, a dead end a look shows
    ///     whole not at all. Silent in fights, menus and when the option is off. docs/radar.md "Exits that sound".
    /// </summary>
    public sealed class ExitBeacons
    {
        private const int PollMs = 50;

        /// <summary>An exit sounds this far past the place's edge (cells): its doorway, not the middle of the room.</summary>
        private const float DoorwayCells = 6f;

        /// <summary>A junction's exits sound at least this far out (cells), where the corridor starts.</summary>
        private const float MinExitCells = 18f;

        /// <summary>A map piece this far along an exit (cells) gives it its material: a bridge, stairs, a door.</summary>
        private const float MaterialReachCells = 30f;

        /// <summary>Walked into from this much past its radius (cells): a little before the fork's centre.</summary>
        private const float EnterSlackCells = 4f;

        /// <summary>Distance (world units) at which an exit is fully to its side; nearer, it draws to the centre.</summary>
        private const float CentreWorld = 250f;

        /// <summary>An explored exit's level beside a new one's.</summary>
        private const float ExploredGain = 0.45f;

        private readonly EntityCatalog catalog;
        private readonly AudioCueService audio;
        private readonly ExitRound round = new(RoundEveryMs);
        private long lastPoll = long.MinValue / 2;

        public ExitBeacons(EntityCatalog catalog, AudioCueService audio)
        {
            this.catalog = catalog;
            this.audio = audio;
        }

        /// <summary>The exits sound again this often while the player stays in the place (ms).</summary>
        public const long RoundEveryMs = 5000;

        /// <summary>The material's sound: full for a way not walked, muffled for an explored one; null: that material is silent.</summary>
        public Func<string, bool, float[]?>? SoundOf { get; set; }

        /// <summary>The kind of map piece at a grid cell (<see cref="PlaceMap.KindAt"/>).</summary>
        public Func<Vector2, string?>? KindAt { get; set; }

        /// <summary>The setting of the ground at a grid cell (<see cref="PlaceMap.BiomeAt"/>).</summary>
        public Func<Vector2, string?>? BiomeAt { get; set; }

        /// <summary>Volume, a fraction of the recorded level.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>A new area: the next place starts a round at once.</summary>
        public void Reset() => this.round.Reset();

        /// <summary>Per frame; <paramref name="quiet"/> (a fight) silences the round.</summary>
        public void Tick(bool playing, bool enabled, bool quiet)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (!playing || !enabled || quiet || this.SoundOf == null || !Seats.Followed.TryGetComponent<Render>(out var player) ||
                this.catalog.Topology() is not { } topology)
            {
                this.round.Reset();
                return;
            }

            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var near = topology.NodeNear(here, EnterSlackCells);
            var ways = new List<Way>();
            int? place = null;
            if (near is { } id && topology.Nodes[id].Kind != TopologyNodeKind.End)
            {
                ways = topology.Ways(id).Where(way => !way.Nook).ToList();
                place = topology.Nodes[id].Kind == TopologyNodeKind.Room || ways.Count >= 3 ? id : null;
            }

            // The exits are worked out only when they sound: their state changes as the player walks.
            if (toWorld <= 0 || !this.round.Due(now, place) || place is not { } at)
            {
                return;
            }

            var node = topology.Nodes[at];
            var exits = new List<(Vector2 At, string Material, bool Explored)>();
            foreach (var way in ways)
            {
                if (topology.IsDeadEndWay(at, way, out var end) && EntityCatalog.SeenWhole(end))
                {
                    continue;
                }

                var spot = node.Centre + (way.Direction * MathF.Max(node.Radius + DoorwayCells, MinExitCells));
                exits.Add((spot, this.MaterialOf(topology, way, spot), this.catalog.WayExplored(topology, at, way)));
            }

            if (exits.Count == 0)
            {
                return;
            }

            // All together, each from its doorway; the level shared so a room of four is no louder than one exit.
            var origin = player.WorldPosition;
            var share = 1f / MathF.Sqrt(exits.Count);
            foreach (var (spot, material, explored) in exits)
            {
                if (this.SoundOf(material, explored) is not { } sound)
                {
                    continue;
                }

                var offset = (spot - here) * toWorld;
                var (pan, pitch) = ScreenProjection.Voice(origin, offset, offset.Length(), CentreWorld);
                this.audio.Play(sound, pan, pitch, this.Gain * share * (explored ? ExploredGain : 1f), $"exit_{material}");
            }
        }

        private string MaterialOf(MapTopology topology, Way way, Vector2 at)
        {
            string? piece = null;
            for (var step = 0f; step <= MaterialReachCells && piece == null; step += 3f)
            {
                piece = this.KindAt?.Invoke(at + (way.Direction * step)) is { } kind && ExitMaterial.Pieces.ContainsKey(kind) ? kind : null;
            }

            var narrow = topology.Narrows.Any(passage => passage.Edge == way.Edge);
            return ExitMaterial.Of(piece, narrow, this.BiomeAt?.Invoke(at + (way.Direction * MaterialReachCells)));
        }
    }
}
