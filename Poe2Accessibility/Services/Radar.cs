namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Natives;

    /// <summary>
    ///     Navigation radar (D2R design, docs/radar.md): four rays along the screen's up, left, right and down, each
    ///     reading open floor, wall or exit from the walkable grid. A ray sounds only when its reading changes and is
    ///     confirmed; cues play one at a time, walls first.
    /// </summary>
    public sealed class Radar
    {
        private const int PollMs = 50;

        /// <summary>A longer gap between polls (a hitch, a menu) starts again from a silent baseline.</summary>
        private const int MaxPollGapMs = 250;

        private const int ExitRefreshMs = 1000;
        private const int CueGapMs = 50;
        private const float PanStrength = 0.9f;

        /// <summary>
        ///     World units, scaled from D2R's cells by walking speed (PoE2 walks ~400 units a second): a wall within
        ///     half a second of walking, open beyond ~0.6 s, the corridor proved to twice that.
        /// </summary>
        private const float OpenWorld = 250f;
        private const float CloseWorld = 200f;
        private const float ConfirmWorld = 30f;
        private const float JumpWorld = 800f;

        /// <summary>The lane either side of a ray, in cells: the staircase of a diagonal line plus a body's width.</summary>
        private const float LaneCells = 2f;

        private const long StillMs = 200;
        private const long StillConfirmMs = 100;
        private const long OpeningIntervalMs = 500;

        private const float ProbeWorld = 100f;
        private const string TransitionPathMarker = "AreaTransition";

        private static readonly RadarSynth.Direction[] Directions = Enum.GetValues<RadarSynth.Direction>();

        private readonly AudioCueService audio;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly Dictionary<RadarSynth.Direction, RadarSensor> sensors = new();
        private readonly Dictionary<RadarSynth.Direction, RadarReading> pending = new();
        private readonly Dictionary<(RadarReading, RadarSynth.Direction), float[]> sounds = new();
        private List<Vector2> exits = new();
        private float scale;
        private string area = string.Empty;
        private Vector2 lastPosition;
        private long lastPoll = long.MinValue / 2;
        private long lastExitRefresh = long.MinValue / 2;
        private long busyUntil;
        private int turn;
        private bool silent = true;

        public Radar(AudioCueService audio, Func<string> areaId, PluginLog log)
        {
            this.audio = audio;
            this.areaId = areaId;
            this.log = log;
            foreach (var direction in Directions)
            {
                foreach (var reading in new[] { RadarReading.Open, RadarReading.Exit, RadarReading.Wall })
                {
                    this.sounds[(reading, direction)] = RadarSynth.Cue(reading, direction, AudioCueService.SampleRate);
                }
            }
        }

        /// <summary>Cue volume, a fraction of the synthesized level.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Per frame; <paramref name="active"/> false silences and forgets every reading.</summary>
        public void Tick(bool active)
        {
            if (!active)
            {
                if (!this.silent)
                {
                    this.Rebase();
                    this.silent = true;
                }

                return;
            }

            this.silent = false;
            var now = Environment.TickCount64;
            if (now - this.lastPoll >= PollMs)
            {
                this.Poll(now);
            }

            this.PlayNext(now);
        }

        private void Poll(long now)
        {
            var gap = now - this.lastPoll;
            this.lastPoll = now;
            var areaInstance = Core.States.InGameStateObject.CurrentAreaInstance;
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var cellSize = areaInstance.WorldToGridConvertor;
            var grid = areaInstance.GridWalkableData;
            var row = areaInstance.TerrainMetadata.BytesPerRow;
            if (cellSize <= 0 || grid.Length == 0 || !areaInstance.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var origin = player.WorldPosition;
            var position = new Vector2(origin.X, origin.Y) / cellSize;
            var currentArea = this.areaId();
            if (currentArea != this.area || gap > MaxPollGapMs || cellSize != this.scale ||
                Vector2.Distance(position, this.lastPosition) * cellSize >= JumpWorld)
            {
                this.Rebase();
                this.area = currentArea;
                this.ApplyScale(cellSize);
            }

            this.lastPosition = position;
            if (now - this.lastExitRefresh >= ExitRefreshMs)
            {
                this.exits = Transitions(cellSize);
                this.lastExitRefresh = now;
            }

            var here = world.WorldToScreen(origin);
            var alongX = world.WorldToScreen(Offset(origin, ProbeWorld, 0)) - here;
            var alongY = world.WorldToScreen(Offset(origin, 0, ProbeWorld)) - here;
            var open = OpenWorld / cellSize;
            foreach (var direction in Directions)
            {
                if (GridAxis(direction, alongX, alongY) is not { } axis)
                {
                    continue;
                }

                var scan = PassageScan.Scan(grid, row, position, axis, LaneCells, 2 * open, open);
                var exitAhead = this.exits.Any(exit => IsAhead(exit - position, axis, scan.Depth, open));
                if (this.sensors[direction].Update(now, position, axis, scan, exitAhead) is { } reading)
                {
                    this.pending[direction] = reading;
                    this.log.Debug($"Radar {direction}: {reading} (depth {scan.Depth * cellSize:0} units, at {position.X:0.00},{position.Y:0.00}, axis {axis.X:0.000},{axis.Y:0.000})");
                }
            }
        }

        /// <summary>The most urgent cue still true (walls, then exits, then floor; equals take turns), one at a time.</summary>
        private void PlayNext(long now)
        {
            if (now < this.busyUntil || this.pending.Count == 0)
            {
                return;
            }

            foreach (var stale in this.pending.Where(cue => this.sensors[cue.Key].Confirmed != cue.Value).Select(cue => cue.Key).ToList())
            {
                this.pending.Remove(stale);
            }

            if (this.pending.Count == 0)
            {
                return;
            }

            var urgency = this.pending.Values.Max();
            var due = Directions.Where(direction => this.pending.TryGetValue(direction, out var reading) && reading == urgency).ToList();
            var direction = due[this.turn++ % due.Count];
            this.pending.Remove(direction);
            var sound = this.sounds[(urgency, direction)];
            this.audio.Play(sound, RadarSynth.Pan(direction) * PanStrength, 1f, this.Gain);
            this.busyUntil = now + (sound.Length * 1000L / AudioCueService.SampleRate) + CueGapMs;
        }

        private void Rebase()
        {
            foreach (var sensor in this.sensors.Values)
            {
                sensor.Reset();
            }

            this.pending.Clear();
            this.busyUntil = 0;
        }

        /// <summary>Cell sizes differ per area, so the thresholds in cells follow the area's grid.</summary>
        private void ApplyScale(float cellSize)
        {
            this.scale = cellSize;
            var limits = new RadarThresholds(OpenWorld / cellSize, CloseWorld / cellSize, ConfirmWorld / cellSize, StillMs, StillConfirmMs, OpeningIntervalMs);
            foreach (var direction in Directions)
            {
                this.sensors[direction] = new RadarSensor(limits);
            }
        }

        /// <summary>An exit counts on a ray when it lies ahead within the open way plus an opening, and near its line.</summary>
        private static bool IsAhead(Vector2 offset, Vector2 axis, float depth, float open)
        {
            var ahead = Vector2.Dot(offset, axis);
            var aside = MathF.Abs(Vector2.Dot(offset, new Vector2(-axis.Y, axis.X)));
            return ahead > 0 && ahead <= depth + open && aside <= open / 2;
        }

        /// <summary>The grid direction that the camera draws as this screen direction.</summary>
        private static Vector2? GridAxis(RadarSynth.Direction direction, Vector2 alongX, Vector2 alongY) =>
            StickDirection.WorldDirection(
                direction switch
                {
                    RadarSynth.Direction.Up => new Vector2(0, -1),
                    RadarSynth.Direction.Left => new Vector2(-1, 0),
                    RadarSynth.Direction.Right => new Vector2(1, 0),
                    _ => new Vector2(0, 1),
                },
                alongX,
                alongY);

        private static List<Vector2> Transitions(float cellSize)
        {
            var found = new List<Vector2>();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && (entity.Path ?? string.Empty).Contains(TransitionPathMarker, StringComparison.Ordinal) &&
                    entity.TryGetComponent<Render>(out var render))
                {
                    found.Add(new Vector2(render.WorldPosition.X, render.WorldPosition.Y) / cellSize);
                }
            }

            return found;
        }

        private static StdTuple3D<float> Offset(StdTuple3D<float> origin, float dx, float dy) =>
            new() { X = origin.X + dx, Y = origin.Y + dy, Z = origin.Z };
    }
}
