namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     What a boss must be made to throw at (<see cref="BossThrowWatch"/>): its metal (<see cref="CueSynth.Scaffold"/>) from
    ///     where it stands, quicker while the boss holds the throw, and struck twice while a throw at the player flies at it.
    /// </summary>
    public sealed class BossThrowCue
    {
        public const string Sound = "boss_scaffold";

        /// <summary>It sounds this often (ms) while the boss holds nothing…</summary>
        public const int IdleIntervalMs = 1600;

        /// <summary>…and this often while it does: the throw comes about 1.5 s after the boulder is up (2026-11-25).</summary>
        public const int CarryingIntervalMs = 600;

        private readonly AudioCueService audio;
        private readonly float[] strike = CueSynth.Scaffold(AudioCueService.SampleRate);
        private readonly float[] twice = CueSynth.ScaffoldTwice(AudioCueService.SampleRate);
        private long last;

        public BossThrowCue(AudioCueService audio) => this.audio = audio;

        public float Gain { get; set; } = 1f;

        /// <summary>The target to use now; null when none.</summary>
        public Func<BossThrowAim?> Aim { get; set; } = () => null;

        public static int IntervalMs(bool carrying) => carrying ? CarryingIntervalMs : IdleIntervalMs;

        /// <summary>Per frame.</summary>
        public void Tick(bool playing)
        {
            if (!playing || this.Aim() is not { } aim || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var now = Environment.TickCount64;
            if (now - this.last < IntervalMs(aim.Carrying))
            {
                return;
            }

            this.last = now;
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(player.WorldPosition, aim.At - here));
            this.audio.Play(aim.LinedUp ? this.twice : this.strike, pan, pitch, this.Gain, Sound);
        }
    }
}
