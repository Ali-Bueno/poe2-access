namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     The dodge warning from where a Trial's trap is about to strike (<see cref="TrialTraps"/>): spikes under the rumbling
    ///     ground, a laser firing, a mine primed. Every trap and state met is logged once, for the first run through them.
    /// </summary>
    public sealed class TrapWatch
    {
        private const int PollMs = 100;

        /// <summary>Near enough to be caught; a first guess, no trap has been timed live.</summary>
        private const float WarnWorld = 400f;

        /// <summary>A row of spikes rumbles at once: one warning for it, not one each.</summary>
        private const int MinGapMs = 300;

        private readonly AudioCueService audio;
        private readonly PluginLog log;
        private readonly float[] warning = CueSynth.Dodge(AudioCueService.SampleRate);
        private readonly TrapStrikes strikes = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private long lastPoll;
        private long lastWarned;

        public TrapWatch(AudioCueService audio, PluginLog log)
        {
            this.audio = audio;
            this.log = log;
        }

        /// <summary>Gain under the master volume.</summary>
        public float Gain { get; set; } = 1f;

        /// <summary>A new area: no trap is known to be firing.</summary>
        public void Reset() => this.strikes.Reset();

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            this.lastPoll = now;
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                var path = entity.Path ?? string.Empty;
                if (!entity.IsValid || TrialTraps.KindOf(path) == null || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                this.LogOnce(path, states);
                var offset = new Vector2(render.WorldPosition.X - player.WorldPosition.X, render.WorldPosition.Y - player.WorldPosition.Y);
                if (this.strikes.Update(entity.Id, TrialTraps.IsFiring(path, states), offset.Length() <= WarnWorld))
                {
                    this.Warn(render, offset, now);
                }
            }
        }

        private void Warn(Render trap, Vector2 offset, long now)
        {
            if (now - this.lastWarned < MinGapMs)
            {
                return;
            }

            this.lastWarned = now;
            var (pan, pitch) = BeaconMath.Voice(ScreenProjection.Direction(trap.WorldPosition, offset));
            this.audio.Play(this.warning, pan, pitch, this.Gain, "dodge");
        }

        private void LogOnce(string path, List<(string Name, long Value)> states)
        {
            var line = $"{path}: {string.Join(", ", states.Where(state => state.Value > 0).Select(state => $"{state.Name}={state.Value}"))}";
            if (this.logged.Add(line))
            {
                this.log.Info($"Trial trap seen: {line}");
            }
        }
    }
}
