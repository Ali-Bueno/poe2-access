namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The player's own channel of a skill released with Perfect Timing (Perfect Strike, Snipe, Gathering Storm),
    ///     followed by its animation and its clip's clock: a hammer blow the moment its window opens, and each release
    ///     logged with how it ended (docs/research/perfect-timing.md).
    /// </summary>
    public sealed class PerfectTimingWatch
    {
        /// <summary>The cue's glossary key.</summary>
        public const string SoundKey = "perfect_timing";

        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly PerfectTimingChannel channel = new();
        private readonly float[] cue = CueSynth.PerfectTiming(AudioCueService.SampleRate);
        private AnimationReading last;
        private string? clip;

        /// <summary>Whether <see cref="last"/> is a reading of the channel being held.</summary>
        private bool followed;

        public PerfectTimingWatch(AudioCueService audio, PluginLog log)
        {
            this.audio = audio;
            this.log = log;
        }

        /// <summary>The reader the monsters' attacks proved (<see cref="AttackReader.Playing"/>); null until the catalogs are read.</summary>
        public Func<AttackReader?> Reader { get; set; } = () => null;

        /// <summary>A new area: no channel is held.</summary>
        public void Reset()
        {
            this.channel.Reset();
            this.followed = false;
        }

        /// <summary>Per frame while playing; <paramref name="sounds"/> is the setting.</summary>
        public void Tick(bool playing, bool sounds)
        {
            if (!playing || !Seats.Followed.TryGetComponent<Actor>(out var actor))
            {
                this.Reset();
                return;
            }

            var now = Environment.TickCount64;
            var animation = AnimationNames.Of(actor.Animation);
            var animated = Seats.Followed.TryGetComponent<Animated>(out var component) ? component.Address : IntPtr.Zero;
            var playingClip = PerfectTimingChannel.IsChannel(animation) ? this.Reader()?.Playing(Seats.Followed.Id, animated) : null;
            switch (this.channel.Update(animation, now))
            {
                case PerfectTimingChange.Began:
                    this.clip = null;
                    this.followed = false;
                    break;
                case PerfectTimingChange.Perfect:
                case PerfectTimingChange.Plain:
                case PerfectTimingChange.Dropped:
                    this.clip = null;
                    this.followed = false;
                    this.log.Info($"Perfect timing: released as {animation} after {now - this.channel.EndedSince} ms, clip at {this.last.Position:0.000} s of " +
                                  $"{this.last.Start:0.000}-{this.last.End:0.000}, rate {AttackClock.Rate(this.last):0.000}");
                    break;
            }

            if (playingClip is not var (clock, events, name))
            {
                return;
            }

            var opens = PerfectTimingWindow.Opens(events);
            if (name != this.clip || (this.followed && AttackClock.NewSwing(this.last, clock)))
            {
                // A channel can pass through more than one clip: each is told once, with the events that time it.
                this.clip = name;
                this.followed = false;
                this.log.Info($"Perfect timing: {animation}, clip {clock.Clip} {name}, {clock.Start:0.000}-{clock.End:0.000} s at rate {AttackClock.Rate(clock):0.000}, " +
                              $"window from {(opens is { } at ? $"{at:0.000}" : "no event")}; events: " +
                              (events.Count == 0 ? "none" : string.Join(", ", events.Select(clipEvent => $"{clipEvent.Name}@{clipEvent.Time:0.000}").Distinct())));
            }

            if (sounds && this.followed && PerfectTimingWindow.Crossed(opens, this.last.Position, clock.Position))
            {
                this.audio.Play(this.cue, 0f, 1f, 1f, SoundKey);
            }

            this.last = clock;
            this.followed = true;
        }
    }
}
