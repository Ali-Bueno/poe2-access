namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameOffsets.Natives;

    /// <summary>
    ///     A thud when the stick pushes into a wall and the character stops (<see cref="WallBumpDetector"/>), panned to
    ///     the side pushed. The wall is read from the walkable grid along the push. Design: docs/radar.md.
    /// </summary>
    public sealed class WallBump
    {
        /// <summary>LeAccess values: half a push, 200 ms without moving (15 units ≈ 0.04 s of walking), re-armed after
        /// 30 units, a 45° turn or 300 ms without pushing into anything.</summary>
        private static readonly WallBumpLimits Limits = new(0.5f, 200, 15f, 30f, 45f, 300);

        /// <summary>The character stops a body's width short of the blocked cells: look this far ahead (~4 cells).</summary>
        private const float ReachWorld = 45f;

        private const float PanStrength = 0.9f;
        private const float ProbeWorld = 100f;

        private readonly AudioCueService audio;
        private readonly WallBumpDetector detector = new(Limits);
        private readonly float[] sound = RadarSynth.Bump(AudioCueService.SampleRate);

        public WallBump(AudioCueService audio) => this.audio = audio;

        /// <summary>Cue volume, a fraction of the synthesized level.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>Per frame in gameplay, with the stick the game received (x right, y up).</summary>
        public void Tick(float stickX, float stickY)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var cellSize = area.WorldToGridConvertor;
            if (cellSize <= 0 || !area.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var origin = player.WorldPosition;
            var position = new Vector2(origin.X, origin.Y);
            var push = new Vector2(stickX, -stickY);
            var blocked = false;
            if (push.Length() >= Limits.MinPush)
            {
                var here = world.WorldToScreen(origin);
                var alongX = world.WorldToScreen(Offset(origin, ProbeWorld, 0)) - here;
                var alongY = world.WorldToScreen(Offset(origin, 0, ProbeWorld)) - here;
                blocked = StickDirection.WorldDirection(push, alongX, alongY) is { } direction &&
                          BlockedAhead(area.GridWalkableData, area.TerrainMetadata.BytesPerRow, position / cellSize, direction, ReachWorld / cellSize);
            }

            if (this.detector.Update(Environment.TickCount64, push, position, blocked))
            {
                this.audio.Play(this.sound, Math.Clamp(push.X, -1f, 1f) * PanStrength, 1f, this.Gain);
            }
        }

        private static bool BlockedAhead(byte[] grid, int row, Vector2 start, Vector2 direction, float reachCells)
        {
            for (var step = 1f; step <= reachCells; step++)
            {
                var cell = start + (direction * step);
                if (!RoutePlanner.IsWalkable(grid, row, (int)cell.X, (int)cell.Y))
                {
                    return true;
                }
            }

            return false;
        }

        private static StdTuple3D<float> Offset(StdTuple3D<float> origin, float dx, float dy) =>
            new() { X = origin.X + dx, Y = origin.Y + dy, Z = origin.Z };
    }
}
