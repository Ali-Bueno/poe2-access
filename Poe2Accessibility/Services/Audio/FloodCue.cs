namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     A boss arena's flood heard (<see cref="BossFloodWatch"/>): its pulse (<see cref="CueSynth.Flood"/>) from the side of its
    ///     nearest edge, faster as it nears, silent past the first warning and once the player stands in it (the ground alarm then).
    /// </summary>
    public sealed class FloodCue
    {
        public const string Sound = "boss_flood";

        /// <summary>The pulse starts with the first spoken warning, 10 steps (world units).</summary>
        private const float ReachWorld = 1000f;

        /// <summary>At the edge it pulses this often (ms): the pulse is 0.3 s long…</summary>
        private const int NearIntervalMs = 300;

        /// <summary>…and this often at the reach.</summary>
        private const int FarIntervalMs = 1200;

        private readonly AudioCueService audio;
        private readonly float[] pulse = CueSynth.Flood(AudioCueService.SampleRate);
        private long last;

        public FloodCue(AudioCueService audio) => this.audio = audio;

        public float Gain { get; set; } = 1f;

        /// <summary>The flood's nearest edge, how far, and whether the player stands in it; null while none flows.</summary>
        public Func<(Vector2 At, float Distance, bool Inside)?> Edge { get; set; } = () => null;

        /// <summary>How often the pulse plays this far from the edge (ms).</summary>
        public static int IntervalMs(float distance) =>
            (int)MathF.Round(NearIntervalMs + ((FarIntervalMs - NearIntervalMs) * Math.Clamp(distance / ReachWorld, 0f, 1f)));

        /// <summary>Per frame.</summary>
        public void Tick(bool playing)
        {
            if (!playing || this.Edge() is not { Inside: false } edge || edge.Distance > ReachWorld || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now - this.last < IntervalMs(edge.Distance))
            {
                return;
            }

            this.last = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, edge.At - here));
            this.audio.Play(this.pulse, pan, pitch, this.Gain, Sound);
        }
    }
}
