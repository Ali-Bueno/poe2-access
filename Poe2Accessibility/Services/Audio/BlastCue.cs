namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     A boss's long blast heard (<see cref="BossMechanicsWatch.BlastCountdown"/>): a countdown beep (<see cref="CueSynth.Blast"/>)
    ///     from where the boss is now, once a second when the blow is far, four times a second at its last moment, silent once the
    ///     player is out of its reach.
    /// </summary>
    public sealed class BlastCue
    {
        /// <summary>From this far (ms) before the blow the beeps come at their slowest…</summary>
        private const int FarMs = 10000;

        /// <summary>…once a second (ms)…</summary>
        private const int FarIntervalMs = 1000;

        /// <summary>…and four times a second at the blow (ms), the beep being 140 ms long.</summary>
        private const int NearIntervalMs = 250;

        private readonly AudioCueService audio;
        private readonly float[] beep = CueSynth.Blast(AudioCueService.SampleRate);
        private long last;

        public BlastCue(AudioCueService audio) => this.audio = audio;

        public float Gain { get; set; } = 1f;

        /// <summary>Where the boss of a pending long blast is, how long is left (ms), and whether the player is in its reach; null when none.</summary>
        public Func<(Vector2 At, int MsLeft, bool InReach)?> Countdown { get; set; } = () => null;

        /// <summary>How often the beep plays with this long left (ms).</summary>
        public static int IntervalMs(int msLeft) =>
            (int)MathF.Round(NearIntervalMs + ((FarIntervalMs - NearIntervalMs) * Math.Clamp(msLeft / (float)FarMs, 0f, 1f)));

        /// <summary>Per frame.</summary>
        public void Tick(bool playing)
        {
            if (!playing || this.Countdown() is not { InReach: true } blast || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now - this.last < IntervalMs(blast.MsLeft))
            {
                return;
            }

            this.last = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, blast.At - here));
            this.audio.Play(this.beep, pan, pitch, this.Gain, CueSynth.BlastSound);
        }
    }
}
