namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper.RemoteObjects.Components;

    /// <summary>
    ///     Raise Shield feedback: a rising clank as the player raises the shield, a falling one as it comes down
    ///     (<see cref="ShieldStance"/>), and the heavy stun bar that blocked and parried hits fill, spoken as it rises
    ///     (<see cref="StunMeter"/>), so the player lowers the guard before it breaks.
    /// </summary>
    public sealed class ShieldWatch
    {
        private const int StunPollMs = 100;

        /// <summary>From this step up the bar's news cuts in when urgent: a blow or two from the stun.</summary>
        private const int UrgentStunPercent = 75;

        private readonly AudioCueService audio;
        private readonly IAccessibilityOutputService output;
        private readonly HostMemoryReader memory;
        private readonly PluginLog log;
        private readonly ShieldStance stance = new();

        /// <summary>Half, three quarters and nine tenths full; re-armed 10 points below.</summary>
        private readonly StunMeter stun = new(10, 50, 75, 90);
        private readonly float[] up = CueSynth.ShieldUp(AudioCueService.SampleRate);
        private readonly float[] down = CueSynth.ShieldDown(AudioCueService.SampleRate);
        private long lastStunPoll;
        private bool stunBarLogged;

        public ShieldWatch(AudioCueService audio, IAccessibilityOutputService output, HostMemoryReader memory, PluginLog log)
        {
            this.audio = audio;
            this.output = output;
            this.memory = memory;
            this.log = log;
        }

        /// <summary>A new area: the shield is down and the bar empty.</summary>
        public void Reset()
        {
            this.stance.Reset();
            this.stun.Reset();
            this.stunBarLogged = false;
        }

        /// <summary>Per frame; off, dead or in a menu the shield counts as down and nothing is heard. The switches are the two settings.</summary>
        public void Tick(bool playing, bool sounds, bool stunBar)
        {
            if (!playing || !Seats.Followed.TryGetComponent<Actor>(out var actor))
            {
                this.stance.Reset();
                return;
            }

            if (sounds)
            {
                this.WatchStance(Enum.GetName(actor.Animation));
            }
            else
            {
                this.stance.Reset();
            }

            if (stunBar)
            {
                this.WatchStun(actor.Address);
            }
        }

        private void WatchStance(string? animation)
        {
            switch (this.stance.Update(animation, Environment.TickCount64))
            {
                case ShieldChange.Raised:
                    this.log.Info($"Shield raised ({animation})");
                    this.audio.Play(this.up, 0f, 1f, 1f, "shield_up");
                    break;
                case ShieldChange.Lowered:
                    this.log.Info($"Shield lowered ({animation})");
                    this.audio.Play(this.down, 0f, 1f, 1f, "shield_down");
                    break;
            }
        }

        /// <summary>"Stun 80 percent" as the bar rises past a step: the player's Actor holds it as a monster's does (<see cref="GameLayout.ActorComponent"/>).</summary>
        private void WatchStun(IntPtr actor)
        {
            var now = Environment.TickCount64;
            if (now - this.lastStunPoll < StunPollMs)
            {
                return;
            }

            this.lastStunPoll = now;
            if (!this.memory.TryRead<int>(actor + GameLayout.ActorComponent.PoiseThresholdOffset, out var threshold) ||
                !this.memory.TryRead<int>(actor + GameLayout.ActorComponent.PoiseOffset, out var buildup))
            {
                return;
            }

            if (!this.stunBarLogged)
            {
                this.stunBarLogged = true;
                this.log.Info($"Player stun bar in this area: {buildup}/{threshold}");
            }

            if (StunMeter.Percent(buildup, threshold) is { } percent && this.stun.Update(percent) is { } step)
            {
                this.log.Info($"Player stun bar {buildup}/{threshold}");
                this.output.Say(SpeechKind.LifeAndMana, Announcements.StunBar(percent), step >= UrgentStunPercent);
            }
        }
    }
}
