namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The hazard alarm: a loop of grinding stabs (<see cref="CueSynth.HazardAlarm"/>) while the followed player stands in
    ///     harmful ground or an enemy's damaging zone, known by the buff it puts on the player (<see cref="HazardGround"/>);
    ///     silence means safe ground.
    /// </summary>
    public sealed class HazardWatch
    {
        private const int PollMs = 150;

        private readonly AudioCueService.Bed bed;
        private readonly HazardGround hazards;
        private readonly PluginLog log;
        private string logged = string.Empty;
        private long lastPoll;
        private bool inHazard;

        public HazardWatch(AudioCueService audio, HazardGround hazards, PluginLog log)
        {
            this.bed = audio.AddBed(CueSynth.HazardAlarm(AudioCueService.SampleRate), "hazard");
            this.hazards = hazards;
            this.log = log;
        }

        /// <summary>Alarm gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>
        ///     The alarm keeps quiet: a boss fight's shelter is up, so the ground is the boss's storm, which fills the arena and
        ///     touches the player inside the shelter too (Jamanra's sandstorm under Asala's barrier); the barrier's own beat leads.
        /// </summary>
        public Func<bool> Quiet { get; set; } = () => false;

        /// <summary>Per frame; off, dead or in a menu it falls silent.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (playing && now - this.lastPoll >= PollMs)
            {
                this.lastPoll = now;
                var hazard = Seats.Followed.TryGetComponent<Buffs>(out var buffs) ? buffs.StatusEffects.Keys.FirstOrDefault(this.hazards.IsHazard) : null;
                this.inHazard = hazard != null && !this.Quiet();
                if (hazard != null && hazard != this.logged)
                {
                    this.log.Info($"Hazard ground: {hazard}");
                }

                this.logged = hazard ?? string.Empty;
            }

            this.bed.Set(0f, playing && this.inHazard ? this.Gain : 0f);
        }
    }
}
