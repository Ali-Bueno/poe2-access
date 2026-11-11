namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The player's own channel of a skill released with Perfect Timing (Perfect Strike, Snipe, Gathering Storm),
    ///     followed by its animation and its clip's clock, and how each release ended. For now it only records them, to
    ///     find where in the clip the window opens (docs/research/perfect-timing.md).
    /// </summary>
    public sealed class PerfectTimingWatch
    {
        private readonly PluginLog log;
        private readonly PerfectTimingChannel channel = new();
        private AnimationReading last;
        private string? clip;

        public PerfectTimingWatch(PluginLog log)
        {
            this.log = log;
        }

        /// <summary>The reader the monsters' attacks proved (<see cref="AttackReader.Playing"/>); null until the catalogs are read.</summary>
        public Func<AttackReader?> Reader { get; set; } = () => null;

        /// <summary>A new area: no channel is held.</summary>
        public void Reset() => this.channel.Reset();

        /// <summary>Per frame while playing.</summary>
        public void Tick(bool playing)
        {
            if (!playing || !Seats.Followed.TryGetComponent<Actor>(out var actor))
            {
                this.channel.Reset();
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
                    this.log.Info($"Perfect timing: {animation} begins");
                    break;
                case PerfectTimingChange.Perfect:
                case PerfectTimingChange.Plain:
                case PerfectTimingChange.Dropped:
                    this.clip = null;
                    this.log.Info($"Perfect timing: released as {animation} after {now - this.channel.EndedSince} ms, clip at {this.last.Position:0.000} s of " +
                                  $"{this.last.Start:0.000}-{this.last.End:0.000}, rate {AttackClock.Rate(this.last):0.000}");
                    break;
            }

            if (playingClip is not var (clock, events, name))
            {
                return;
            }

            if (name != this.clip || AttackClock.NewSwing(this.last, clock))
            {
                // A channel can pass through more than one clip (a wind-up, then a held loop): each is told once.
                this.clip = name;
                this.log.Info($"Perfect timing: clip {clock.Clip} {name}, {clock.Start:0.000}-{clock.End:0.000} s, at {clock.Position:0.000}, speed {clock.Speed:0.000}, " +
                              $"global {clock.GlobalSpeed:0.000}{(clock.GlobalOff ? " off" : string.Empty)}, {now - this.channel.Since} ms in; events: " +
                              (events.Count == 0 ? "none" : string.Join(", ", events.OrderBy(clipEvent => clipEvent.Time).Select(clipEvent => $"{clipEvent.Name}@{clipEvent.Time:0.000}"))));
            }

            this.last = clock;
        }
    }
}
