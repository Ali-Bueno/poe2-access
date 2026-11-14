namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Says what a sighted player sees of a Trial of Chaos round: which kind of round its pieces make, a blood circle
    ///     filling, the core taken in hand, and the Ruin counted over the head (docs/research/trial-of-chaos.md).
    /// </summary>
    public sealed class ChaosTrialWatch
    {
        private const int PollMs = 500;

        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly ChaosRoundNews news = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private readonly List<(uint Id, ChaosPiece Piece)> pieces = new();
        private long lastPoll;

        public ChaosTrialWatch(IAccessibilityOutputService output, PluginLog log)
        {
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: nothing is known of its rounds.</summary>
        public void Reset() => this.news.Reset();

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            this.pieces.Clear();
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (!entity.IsValid || ChaosTrial.PieceOf(entity.Path ?? string.Empty) is not { } piece)
                {
                    continue;
                }

                var states = entity.TryGetComponent<StateMachine>(out var machine)
                    ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                    : new List<(string Name, long Value)>();
                this.LogOnce(entity.Path!, states);
                this.pieces.Add((entity.Id, piece));
                if (piece == ChaosPiece.BloodCircle && this.news.Filled(entity.Id, ChaosTrial.IsFull(states)))
                {
                    this.output.Say(SpeechKind.Quest, Announcements.ChaosCircleFilled);
                }
            }

            if (this.news.Began(this.pieces) is { } round)
            {
                this.output.Say(SpeechKind.Quest, Announcements.ChaosRoundBegan(round));
            }

            if (!Seats.Followed.TryGetComponent<Buffs>(out var buffs))
            {
                return;
            }

            if (this.news.Core(buffs.StatusEffects.ContainsKey(ChaosTrial.HoldingCoreBuff)) == true)
            {
                this.output.Say(SpeechKind.Quest, Announcements.ChaosCoreTaken);
            }

            var ruin = buffs.StatusEffects.TryGetValue(ChaosTrial.RuinBuff, out var effect) ? PlayerCharges.Held(effect.Charges) : 0;
            if (this.news.Ruin(ruin) is { } count)
            {
                var line = Announcements.ChaosRuin(count, ChaosTrial.RuinToFail);
                this.log.Info($"Ruin: {count} (buff charges {effect.Charges})");
                if (count >= ChaosTrial.RuinToFail - 1)
                {
                    this.output.Urge(line);
                }
                else
                {
                    this.output.Say(SpeechKind.StatusBegins, line);
                }
            }
        }

        private void LogOnce(string path, List<(string Name, long Value)> states)
        {
            var line = $"{path}: {string.Join(", ", states.Select(state => $"{state.Name}={state.Value}"))}";
            if (this.logged.Add(line))
            {
                this.log.Info($"Chaos trial piece seen: {line}");
            }
        }
    }
}
