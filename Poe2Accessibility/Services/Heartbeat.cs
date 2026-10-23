namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Low-life heartbeat (D2R vitals.md): at or under the line a lub-dub loops, one beat every 0.58 s, until life
    ///     climbs 5 points above the line; quiet while dead, paused or in a menu. Its own voice and volume, so it
    ///     carries the danger without speech lagging behind the fight.
    /// </summary>
    public sealed class Heartbeat
    {
        private readonly AudioCueService audio;
        private readonly float[] beat;
        private readonly HeartbeatGate gate = new();
        private long lastBeat;

        public Heartbeat(AudioCueService audio)
        {
            this.audio = audio;
            this.beat = CueSynth.Heartbeat(AudioCueService.SampleRate);
        }

        public float Gain { get; set; } = 1f;

        /// <summary>Life percent at or under which it beats (settings LowLifeBelow).</summary>
        public int Below { get => this.gate.Below; set => this.gate.Below = value; }

        /// <summary>Per frame; <paramref name="lifePercent"/> null when life cannot be read (dead, loading).</summary>
        public void Tick(bool playing, int? lifePercent)
        {
            var beating = this.gate.Update(lifePercent);
            var now = Environment.TickCount64;
            if (!playing || !beating || now - this.lastBeat < CueSynth.HeartbeatPeriodMs)
            {
                return;
            }

            this.lastBeat = now;
            this.audio.Play(this.beat, 0f, 1f, this.Gain);
        }
    }
}
