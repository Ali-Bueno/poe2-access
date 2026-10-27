namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Presence sounds in gameplay: every living hostile monster on screen and in line of sight (uniques always), and
    ///     the NPCs and other players, is a <see cref="CueTarget"/>; the scheduler says which pulse to play, panned to its side of
    ///     the screen and lower in pitch when it is below the player. Design: docs/enemy-sounds.md.
    /// </summary>
    public sealed class EnemyCues
    {
        /// <summary>Targets are re-read this often; pulses are scheduled every frame from the latest read.</summary>
        private const int RefreshMs = 100;

        /// <summary>D2R defaults: four streams, 100 ms between starts, 150 ms at contact to 600 ms at the screen's corner.</summary>
        private const int MaxStreams = 4;
        private const long MinGapMs = 100;
        private const long NearIntervalMs = 150;
        private const long FarIntervalMs = 600;

        /// <summary>A screen corner is √2 half-screens away; nearness falls to zero there.</summary>
        private static readonly float CornerDistance = MathF.Sqrt(2f);

        private const float PanStrength = 0.9f;

        /// <summary>Pitch ratio at the bottom edge; at or above the player the cue plays as synthesized.</summary>
        private const float LowestPitch = 0.8f;

        /// <summary>Line-of-sight cells skipped at each end: the player and the monster stand on the edge of what they block.</summary>
        private const int SightEndCells = 2;

        /// <summary>
        ///     Cells in a row that may block sight before the target counts as hidden: on the sight layer only the
        ///     staircase of a diagonal past a corner; on the walkable fallback ~90 units, thicker than props.
        /// </summary>
        private const int MaxBlockedSightCells = 2;
        private const int MaxBlockedWalkCells = 8;

        /// <summary>How often the log gets a summary of what played (never one line per pulse).</summary>
        private const int ReportMs = 5000;

        /// <summary>The reach ping is armed again once every enemy is this far beyond the reach (world units).</summary>
        private const float ReleaseWorld = 25f;

        private readonly AudioCueService audio;
        private readonly HostMemoryReader memory;
        private byte[] sightGrid = Array.Empty<byte>();
        private IntPtr sightGridSource;
        private readonly PluginLog log;
        private long lastReport;
        private int pulses;
        private int mostTargets;
        private readonly EnemyCueScheduler scheduler = new(MaxStreams, MinGapMs, NearIntervalMs, FarIntervalMs);
        private readonly Dictionary<CueSynth.Layer, float[]> sounds = new();
        private List<CueTarget> targets = new();
        private readonly ReachGate reachGate = new();
        private readonly float[] reachPing = CueSynth.ReachPing(AudioCueService.SampleRate);
        private float? nearestEdge;
        private float nearestPan;
        private long lastRefresh = long.MinValue / 2; // halved: "now - lastRefresh" must not overflow
        private bool silent = true;

        public EnemyCues(AudioCueService audio, HostMemoryReader memory, PluginLog log)
        {
            this.audio = audio;
            this.memory = memory;
            this.log = log;
            foreach (var layer in Enum.GetValues<CueSynth.Layer>())
            {
                this.sounds[layer] = CueSynth.Enemy(layer, AudioCueService.SampleRate);
            }
        }

        /// <summary>Cue volume, a fraction of the synthesized level.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Growls for enemies (setting EnemySounds).</summary>
        public bool Enemies { get; set; } = true;

        /// <summary>Taps for the nearest NPC or other player (setting NpcSounds).</summary>
        public bool Friends { get; set; } = true;

        /// <summary>The D2R reach ping: two rising notes when an enemy in sight comes within melee reach.</summary>
        public bool Reach { get; set; } = true;

        /// <summary>Melee reach: gap between the two bodies (ModelBounds) at which the ping fires, world units.</summary>
        public float ReachWorld { get; set; } = 40f;

        /// <summary>Per frame; <paramref name="active"/> false (menu, dialogue, loading, window in the back) silences at once.</summary>
        public void Tick(bool active)
        {
            if (!active)
            {
                if (!this.silent)
                {
                    this.audio.StopAll();
                    this.scheduler.Reset();
                    this.targets.Clear();
                    this.reachGate.Reset();
                    this.silent = true;
                }

                return;
            }

            this.silent = false;
            var now = Environment.TickCount64;
            if (now - this.lastRefresh >= RefreshMs)
            {
                this.targets = this.Collect();
                this.lastRefresh = now;
                if (!this.Reach)
                {
                    this.reachGate.Reset();
                }
                else if (this.reachGate.Update(this.nearestEdge, this.ReachWorld, ReleaseWorld))
                {
                    this.audio.Play(this.reachPing, this.nearestPan * PanStrength, 1f, this.Gain);
                    this.log.Debug($"Reach ping: gap {this.nearestEdge:F0}");
                }
            }

            if (this.scheduler.Next(now, this.targets) is { } target)
            {
                var pitch = 1f - ((1f - LowestPitch) * Math.Clamp(target.ScreenY, 0f, 1f));
                this.audio.Play(this.sounds[target.Layer], target.ScreenX * PanStrength, pitch, this.Gain);
                this.pulses++;
            }

            this.mostTargets = Math.Max(this.mostTargets, this.targets.Count);
            if (now - this.lastReport >= ReportMs)
            {
                if (this.pulses > 0)
                {
                    this.log.Debug($"Enemy cues: {this.pulses} pulses, up to {this.mostTargets} enemies in the last {ReportMs / 1000} s");
                }

                this.lastReport = now;
                this.pulses = 0;
                this.mostTargets = 0;
            }
        }

        private List<CueTarget> Collect()
        {
            var found = new List<CueTarget>();
            this.nearestEdge = null;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var window = Core.Process.WindowArea;
            if (window.Width <= 0 || window.Height <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return found;
            }

            var half = new Vector2(window.Width / 2f, window.Height / 2f);
            var here = world.WorldToScreen(player.WorldPosition);
            var (grid, maxBlocked) = this.SightGrid(area);
            var row = area.TerrainMetadata.BytesPerRow;
            var toGrid = area.WorldToGridConvertor;
            var from = new Vector2(player.WorldPosition.X, player.WorldPosition.Y) / toGrid;
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                // Not in play: still buried or dormant (or a story variant of an NPC), nothing a sighted player sees. A
                // player down is heard off screen and behind walls too (user 2026-10-26: the body was hard to find).
                var layer = this.LayerOf(entity);
                var down = layer == CueSynth.Layer.PlayerDown;
                if (!down && !EntityCatalog.IsInPlay(this.memory, entity))
                {
                    continue;
                }

                var enemy = this.Reach && entity.EntityType == EntityTypes.Monster && EntityCatalog.IsAliveEnemy(entity);
                if (layer == null && !enemy)
                {
                    continue;
                }

                var screen = world.WorldToScreen(render.WorldPosition);
                if (!down && (screen.X < 0 || screen.Y < 0 || screen.X > window.Width || screen.Y > window.Height))
                {
                    continue;
                }

                var to = new Vector2(render.WorldPosition.X, render.WorldPosition.Y) / toGrid;
                if (!down && layer != CueSynth.Layer.Boss && grid.Length > 0 && !InSight(grid, row, from, to, maxBlocked))
                {
                    continue;
                }

                var offset = (screen - here) / half;
                if (enemy)
                {
                    var gap = Vector2.Distance(new Vector2(render.WorldPosition.X, render.WorldPosition.Y), new Vector2(player.WorldPosition.X, player.WorldPosition.Y)) -
                              render.ModelBounds.X - player.ModelBounds.X;
                    if (this.nearestEdge is not { } nearest || gap < nearest)
                    {
                        (this.nearestEdge, this.nearestPan) = (gap, Math.Clamp(offset.X, -1f, 1f));
                    }
                }

                if (layer is not { } voiced)
                {
                    continue;
                }

                var nearness = 1f - Math.Clamp(offset.Length() / CornerDistance, 0f, 1f);
                found.Add(new CueTarget(entity.Id, voiced, Math.Clamp(offset.X, -1f, 1f), Math.Clamp(offset.Y, -1f, 1f), nearness));
            }

            return found;
        }

        /// <summary>Research (dev command "enemies"): why each awake monster is or is not voiced.</summary>
        public string Diagnose()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var window = Core.Process.WindowArea;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return "Enemy cues: no player";
            }

            var toGrid = area.WorldToGridConvertor;
            var from = new Vector2(player.WorldPosition.X, player.WorldPosition.Y) / toGrid;
            var lines = new List<string> { $"Enemy cues: window {window}, player screen {world.WorldToScreen(player.WorldPosition)}, {this.Collect().Count} targets" };
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.EntityType != EntityTypes.Monster || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var to = new Vector2(render.WorldPosition.X, render.WorldPosition.Y) / toGrid;
                var (grid, maxBlocked) = this.SightGrid(area);
                var life = entity.TryGetComponent<Life>(out var vitals) ? $"{vitals.Health.Current}/{vitals.Health.Total}" : "-";
                var animation = entity.TryGetComponent<Actor>(out var actor) ? Enum.GetName(actor.Animation) ?? $"{(int)actor.Animation}" : "-";
                var buffs = entity.TryGetComponent<Buffs>(out var effects) ? string.Join(",", effects.StatusEffects.Keys.Take(12)) : "-";
                lines.Add($"  {entity.Id} {entity.Path}: alive enemy {EntityCatalog.IsAliveEnemy(entity)}, in play {EntityCatalog.IsInPlay(this.memory, entity)}, " +
                          $"screen {world.WorldToScreen(render.WorldPosition)}, in sight {InSight(grid, area.TerrainMetadata.BytesPerRow, from, to, maxBlocked)}, " +
                          $"life {life}, animation {animation}, buffs [{buffs}]");
            }

            return string.Join(Environment.NewLine, lines.Take(40));
        }

        /// <summary>The sound for this entity, or null when it gets none (or its kind is switched off).</summary>
        private CueSynth.Layer? LayerOf(GameHelper.RemoteObjects.States.InGameStateObjects.Entity entity)
        {
            if (entity.EntityType == EntityTypes.Monster && EntityCatalog.IsAliveEnemy(entity))
            {
                if (!this.Enemies)
                {
                    return null;
                }

                return (entity.TryGetComponent<ObjectMagicProperties>(out var magic) ? magic.Rarity : Rarity.Normal) switch
                {
                    Rarity.Unique => CueSynth.Layer.Boss,
                    Rarity.Magic or Rarity.Rare => CueSynth.Layer.Elite,
                    _ => CueSynth.Layer.None,
                };
            }

            if (!this.Friends)
            {
                return null;
            }

            if (entity.EntityType == EntityTypes.Player)
            {
                // A body to revive calls, the local partner's too (CoopDowns says it fell); alive, the local partner
                // has its own radar.
                if (Seats.IsFollowed(entity))
                {
                    return null;
                }

                if (entity.TryGetComponent<Life>(out var body) && !body.IsAlive)
                {
                    return CueSynth.Layer.PlayerDown;
                }

                return Seats.IsLocal(entity) ? null : CueSynth.Layer.Player;
            }

            // A friendly monster with the NPC component is a character (Mortimer); corpses and minions have none.
            return entity.EntityType == EntityTypes.NPC || entity.TryGetComponent<NPC>(out _) ? CueSynth.Layer.Friend : null;
        }

        /// <summary>
        ///     The terrain layer that blocks sight (GameLayout.Terrain.SightLayer), read once per area; the walkable grid,
        ///     which also blocks at every prop, with a thicker tolerance when that layer cannot be read.
        /// </summary>
        private (byte[] Grid, int MaxBlocked) SightGrid(GameHelper.RemoteObjects.States.InGameStateObjects.AreaInstance area)
        {
            var walkable = area.GridWalkableData;
            var layer = area.TerrainMetadata.GridLandscapeData;
            if (layer.First != this.sightGridSource)
            {
                this.sightGridSource = layer.First;
                this.sightGrid = this.memory.ReadStdVector<byte>(layer, walkable.Length);
            }

            return this.sightGrid.Length == walkable.Length && walkable.Length > 0
                ? (this.sightGrid, MaxBlockedSightCells)
                : (walkable, MaxBlockedWalkCells);
        }

        /// <summary>No run of more than <paramref name="maxBlocked"/> blocking cells on the straight line, ends excepted.</summary>
        private static bool InSight(byte[] grid, int row, Vector2 from, Vector2 to, int maxBlocked)
        {
            var cells = (int)MathF.Ceiling(Vector2.Distance(from, to));
            var run = 0;
            for (var i = SightEndCells; i <= cells - SightEndCells; i++)
            {
                var cell = Vector2.Lerp(from, to, (float)i / cells);
                run = RoutePlanner.IsWalkable(grid, row, (int)cell.X, (int)cell.Y) ? 0 : run + 1;
                if (run > maxBlocked)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
