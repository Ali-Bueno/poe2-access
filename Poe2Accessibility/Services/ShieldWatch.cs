namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper.RemoteObjects.Components;

    /// <summary>Raise Shield feedback: a rising clank as the player raises the shield, a falling one as it comes down (<see cref="ShieldStance"/>).</summary>
    public sealed class ShieldWatch
    {
        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly ShieldStance stance = new();
        private readonly float[] up = CueSynth.ShieldUp(AudioCueService.SampleRate);
        private readonly float[] down = CueSynth.ShieldDown(AudioCueService.SampleRate);

        public ShieldWatch(AudioCueService audio, PluginLog log)
        {
            this.audio = audio;
            this.log = log;
        }

        /// <summary>A new area: the shield is down.</summary>
        public void Reset() => this.stance.Reset();

        /// <summary>Per frame; off, dead or in a menu the shield counts as down and nothing sounds.</summary>
        public void Tick(bool playing)
        {
            if (!playing || !Seats.Followed.TryGetComponent<Actor>(out var actor))
            {
                this.stance.Reset();
                return;
            }

            var animation = Enum.GetName(actor.Animation);
            switch (this.stance.Update(animation, Environment.TickCount64))
            {
                case ShieldChange.Raised:
                    this.log.Info($"Shield raised ({animation})");
                    this.audio.Play(this.up, 0f, 1f, 1f);
                    break;
                case ShieldChange.Lowered:
                    this.log.Info($"Shield lowered ({animation})");
                    this.audio.Play(this.down, 0f, 1f, 1f);
                    break;
            }
        }
    }
}
