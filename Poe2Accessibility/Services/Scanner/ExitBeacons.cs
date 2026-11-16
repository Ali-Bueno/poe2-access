namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The exits of the rooms and forks near the player as ambient sounds placed in their doorways, as a game's ambient
    ///     emitters are: wind blowing in from a desert exit, wood creaking on a bridge, water toward a dock, louder as the player
    ///     nears them (the owner, 2026-11-16, lost in Ogham Manor: "me cuesta encontrar salidas"; then "que sonasen más en loop,
    ///     como si de un sonido ambiente se tratasen… landmarks inmersivos, pero que a la vez ayuden al jugador"). What sounds
    ///     tells the exit's material or setting; an explored exit is quieter and muffled; a dead end a look shows whole is
    ///     silent. The nearest few are heard; a fight lowers them, menus and the option off stop them.
    ///     docs/radar.md "Exits that sound".
    /// </summary>
    public sealed class ExitBeacons
    {
        private const int PollMs = 100;

        /// <summary>How often each heard exit's state (explored, dead end) is judged again.</summary>
        private const int StateMs = 1000;

        /// <summary>An exit is heard within this many cells (about two seconds of walking), fading with distance.</summary>
        private const float ReachCells = 80f;

        /// <summary>At most this many exits are heard at once, the nearest.</summary>
        private const int MostHeard = 4;

        /// <summary>An exit sounds this far past the place's edge (cells): its doorway, not the middle of the room.</summary>
        private const float DoorwayCells = 6f;

        /// <summary>A junction's exits sound at least this far out (cells), where the corridor starts.</summary>
        private const float MinExitCells = 18f;

        /// <summary>A map piece this far along an exit (cells) gives it its material: a bridge, stairs, a door.</summary>
        private const float MaterialReachCells = 30f;

        /// <summary>Two exits of one material this near (cells) are one doorway: the two ends of a short corridor.</summary>
        private const float SameDoorwayCells = 20f;

        /// <summary>Distance (world units) at which an exit is fully to its side; nearer, it draws to the centre.</summary>
        private const float CentreWorld = 250f;

        /// <summary>An explored exit's level beside a new one's (its loop is also the muffled one).</summary>
        private const float ExploredGain = 0.45f;

        /// <summary>
        ///     The exits sit under the game's own ambience: heard when listened for, never covering it (the owner, 2026-11-16:
        ///     "tienen que ser algo sutiles"); 0.5 at first was "algo bajos" in the Manor (same day).
        /// </summary>
        private const float SubtleGain = 1f;

        /// <summary>The level during a fight: the exits stay, under the fight's own sounds.</summary>
        private const float FightGain = 0.35f;

        private readonly EntityCatalog catalog;
        private readonly AudioCueService audio;
        private readonly Dictionary<(int Node, int Edge), Exit> exits = new();
        private readonly Dictionary<(int Node, int Edge, bool Explored), Emitter> beds = new();
        private MapTopology? shape;
        private long lastPoll = long.MinValue / 2;

        public ExitBeacons(EntityCatalog catalog, AudioCueService audio)
        {
            this.catalog = catalog;
            this.audio = audio;
        }

        /// <summary>The materials' beds and events (the game's own recordings); null or empty: the exits are silent.</summary>
        public ExitSoundSet? Sounds { get; set; }

        /// <summary>The kind of map piece at a grid cell (<see cref="PlaceMap.KindAt"/>).</summary>
        public Func<Vector2, string?>? KindAt { get; set; }

        /// <summary>The setting of the ground at a grid cell (<see cref="PlaceMap.BiomeAt"/>).</summary>
        public Func<Vector2, string?>? BiomeAt { get; set; }

        /// <summary>Volume, a fraction of the recorded level.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>A new area: every exit goes.</summary>
        public void Reset()
        {
            this.StopAll();
            this.exits.Clear();
            this.shape = null;
        }

        /// <summary>Per frame; <paramref name="quiet"/> (a fight) lowers the exits.</summary>
        public void Tick(bool playing, bool enabled, bool quiet)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (!playing || !enabled || this.Sounds is not { IsEmpty: false } sounds || toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player) ||
                this.catalog.Topology() is not { } topology)
            {
                this.StopAll();
                return;
            }

            if (!ReferenceEquals(topology, this.shape))
            {
                this.Reset();
                this.shape = topology;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var heard = new List<(Exit Exit, float Distance)>();
            foreach (var node in topology.Nodes)
            {
                if (node.Kind == TopologyNodeKind.End || Vector2.Distance(node.Centre, here) > ReachCells + node.Radius + MinExitCells)
                {
                    continue;
                }

                var ways = topology.Ways(node.Id).Where(way => !way.Nook).ToList();
                if (node.Kind != TopologyNodeKind.Room && ways.Count < 3)
                {
                    continue; // a bend in a corridor has no doorways
                }

                foreach (var way in ways)
                {
                    var exit = this.ExitOf(topology, node, way);
                    if (now - exit.JudgedAt >= StateMs)
                    {
                        exit.JudgedAt = now;
                        exit.DeadEnd = topology.IsDeadEndWay(node.Id, way, out var end) && EntityCatalog.SeenWhole(end);
                        exit.Explored = this.catalog.WayExplored(topology, node.Id, way);
                    }

                    var distance = Vector2.Distance(exit.At, here);
                    if (!exit.DeadEnd && distance <= ReachCells)
                    {
                        heard.Add((exit, distance));
                    }
                }
            }

            // The nearest few; of two of one material at one doorway (a short corridor's two ends), the nearer.
            var chosen = new List<(Exit Exit, float Distance)>();
            foreach (var candidate in heard.OrderBy(pair => pair.Distance))
            {
                if (chosen.Count < MostHeard && !chosen.Any(pair => pair.Exit.Material == candidate.Exit.Material &&
                                                                   Vector2.Distance(pair.Exit.At, candidate.Exit.At) < SameDoorwayCells))
                {
                    chosen.Add(candidate);
                }
            }

            var playingNow = new HashSet<(int, int, bool)>();
            var origin = player.WorldPosition;
            foreach (var (exit, distance) in chosen)
            {
                var key = (exit.Node, exit.Edge, exit.Explored);
                if (!this.beds.TryGetValue(key, out var emitter))
                {
                    // One bed of the material per exit, from anywhere in it: two exits of one material must not play in step.
                    var bedSound = sounds.Bed(exit.Material, exit.Explored, Random.Shared.Next());
                    var (min, max) = ExitMaterial.EventGapMs(exit.Material);
                    emitter = this.beds[key] = new Emitter(
                        bedSound == null ? null : this.audio.AddBed(bedSound, $"exit_{exit.Material}", Random.Shared.Next(bedSound.Length)),
                        new ExitScatter(min, max, now, Random.Shared));
                }

                var offset = (exit.At - here) * toWorld;
                var (pan, pitch) = ScreenProjection.Voice(origin, offset, offset.Length(), CentreWorld);
                // Linear with distance: squared, an exit half the reach away was a quarter as loud and lost under the game.
                var fade = 1f - (distance / ReachCells);
                var gain = this.Gain * SubtleGain * fade * (exit.Explored ? ExploredGain : 1f) * (quiet ? FightGain : 1f);
                emitter.Bed?.Set(pan, gain, pitch);
                if (emitter.Scatter.Next(now, sounds.EventCount(exit.Material)) is { } next)
                {
                    this.audio.Play(sounds.Event(exit.Material, exit.Explored, next.Index), pan, pitch * next.Pitch, gain * next.Gain, $"exit_{exit.Material}");
                }

                playingNow.Add(key);
            }

            foreach (var key in this.beds.Keys.Where(key => !playingNow.Contains(key)).ToList())
            {
                this.beds[key].Bed?.Stop();
                this.beds.Remove(key);
            }
        }

        private void StopAll()
        {
            foreach (var emitter in this.beds.Values)
            {
                emitter.Bed?.Stop();
            }

            this.beds.Clear();
        }

        /// <summary>A place's exit along a way, its doorway and material worked out once.</summary>
        private Exit ExitOf(MapTopology topology, TopologyNode node, Way way)
        {
            if (this.exits.TryGetValue((node.Id, way.Edge), out var known))
            {
                return known;
            }

            var at = node.Centre + (way.Direction * MathF.Max(node.Radius + DoorwayCells, MinExitCells));
            string? piece = null;
            for (var step = 0f; step <= MaterialReachCells && piece == null; step += 3f)
            {
                piece = this.KindAt?.Invoke(at + (way.Direction * step)) is { } kind && ExitMaterial.Pieces.ContainsKey(kind) ? kind : null;
            }

            var narrow = topology.Narrows.Any(passage => passage.Edge == way.Edge);
            var material = ExitMaterial.Of(piece, narrow, this.BiomeAt?.Invoke(at + (way.Direction * MaterialReachCells)));
            return this.exits[(node.Id, way.Edge)] = new Exit(node.Id, way.Edge, at, material);
        }

        /// <summary>An exit heard: its bed (null when the material has none) and when its next event comes.</summary>
        private sealed record Emitter(AudioCueService.Bed? Bed, ExitScatter Scatter);

        private sealed class Exit
        {
            public Exit(int node, int edge, Vector2 at, string material)
            {
                this.Node = node;
                this.Edge = edge;
                this.At = at;
                this.Material = material;
                this.JudgedAt = long.MinValue / 2;
            }

            public int Node { get; }

            public int Edge { get; }

            public Vector2 At { get; }

            public string Material { get; }

            public long JudgedAt { get; set; }

            public bool DeadEnd { get; set; }

            public bool Explored { get; set; }
        }
    }
}
