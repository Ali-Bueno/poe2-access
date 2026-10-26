namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Wall tones (D2R `[wall_tones]`): one soft noise band per screen direction that swells as the walkable ground
    ///     ends within range — level (1 - d / range)², the lanes across the body counted, the farthest reach wins, the
    ///     map's edge a wall; the sides panned hard. A continuous alternative to the radar's knocks; silent outside play.
    /// </summary>
    public sealed class WallTones
    {
        private const int PollMs = 50;

        /// <summary>Half the width scanned either side of a ray, in cells: the body and a lane each side.</summary>
        private const float LaneCells = 1.5f;

        private const float ProbeWorld = 100f;

        private static readonly RadarSynth.Direction[] Directions = Enum.GetValues<RadarSynth.Direction>();

        private readonly Dictionary<RadarSynth.Direction, AudioCueService.Bed> beds = new();
        private long lastPoll;
        private bool sounding;

        public WallTones(AudioCueService audio)
        {
            foreach (var direction in Directions)
            {
                this.beds[direction] = audio.AddBed(RadarSynth.WallTone(direction, AudioCueService.SampleRate));
            }
        }

        /// <summary>Volume, a fraction of the synthesized level (follows the radar volume).</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>How far ahead a wall starts to sound, in world units (PoE2 walks ~400 a second).</summary>
        public float RangeWorld { get; set; } = 600f;

        /// <summary>Per frame; <paramref name="active"/> false fades every band out.</summary>
        public void Tick(bool active)
        {
            if (!active)
            {
                if (this.sounding)
                {
                    foreach (var bed in this.beds.Values)
                    {
                        bed.Set(0f, 0f);
                    }

                    this.sounding = false;
                }

                return;
            }

            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var cellSize = area.WorldToGridConvertor;
            var grid = area.GridWalkableData;
            var row = area.TerrainMetadata.BytesPerRow;
            if (cellSize <= 0 || grid.Length == 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var origin = player.WorldPosition;
            var position = new Vector2(origin.X, origin.Y) / cellSize;
            var here = world.WorldToScreen(origin);
            var alongX = world.WorldToScreen(Radar.Offset(origin, ProbeWorld, 0)) - here;
            var alongY = world.WorldToScreen(Radar.Offset(origin, 0, ProbeWorld)) - here;
            var range = this.RangeWorld / cellSize;
            foreach (var direction in Directions)
            {
                var gain = 0f;
                if (Radar.GridAxis(direction, alongX, alongY) is { } axis)
                {
                    var depth = PassageScan.Scan(grid, row, position, axis, LaneCells, range, range).Depth;
                    gain = RadarSynth.WallToneGain(depth, range);
                }

                this.beds[direction].Set(RadarSynth.Pan(direction), gain * this.Gain);
            }

            this.sounding = true;
        }
    }
}
