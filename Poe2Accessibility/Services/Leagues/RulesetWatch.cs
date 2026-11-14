namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using GameReader.RemoteObjects.Components;

    /// <summary>
    ///     Says what the client's rulesets hold that a sighted player sees drawn (<see cref="RulesetReader"/>): a Breach's ring
    ///     growing and the player walking out of it, the Trial of Chaos' round. It also keeps the Expedition's count of charges for
    ///     the league watch.
    /// </summary>
    public sealed class RulesetWatch
    {
        private const int PollMs = 400;

        /// <summary>How often the interface is walked for the Trial of Chaos' elements.</summary>
        private const int TrialHudWalkMs = 3000;

        private readonly RulesetReader reader;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly RulesetNews news = new();
        private readonly ChaosTrialHudReader trialHud;
        private readonly ChaosTrialHudNews trialHudNews = new();
        private readonly HashSet<string> trialHudLogged = new(StringComparer.Ordinal);
        private long trialHudFound = long.MinValue / 2;
        private bool areaStatsLogged;
        private long lastPoll;
        private string lastLogged = string.Empty;

        public RulesetWatch(RulesetReader reader, ChaosTrialHudReader trialHud, IAccessibilityOutputService output, PluginLog log)
        {
            this.reader = reader;
            this.trialHud = trialHud;
            this.output = output;
            this.log = log;
        }

        /// <summary>Gets or sets the element every layer of the game's interface hangs from.</summary>
        public Func<IntPtr> Container { get; set; } = () => IntPtr.Zero;

        /// <summary>Gets the charges of the Expedition site nearest the player as last read: placed, and how many there are.</summary>
        public (int Placed, int Total)? Charges { get; private set; }

        /// <summary>Research (dev command "rulesets"): where a session could be, to the log.</summary>
        public void Probe()
        {
            foreach (var line in this.reader.Probe())
            {
                this.log.Info(line);
            }

            this.log.Info(this.reader.Describe());
        }

        /// <summary>A new area: nothing has been read.</summary>
        public void Reset()
        {
            this.reader.Reset();
            this.news.Reset();
            this.trialHudNews.Reset();
            this.trialHud.Reset();
            this.areaStatsLogged = false;
            this.trialHudFound = long.MinValue / 2;
            (this.Charges, this.lastLogged) = (null, string.Empty);
        }

        /// <summary>Per frame; <paramref name="playing"/> false while dead, paused or in a menu.</summary>
        public void Tick(bool playing, bool leagues, bool trials)
        {
            var now = Environment.TickCount64;
            if (!playing || now - this.lastPoll < PollMs || !Seats.Followed.TryGetComponent<Render>(out var render))
            {
                return;
            }

            this.lastPoll = now;
            var player = new Vector2(render.GridPosition.X, render.GridPosition.Y);
            var ring = this.reader.BreachNear(player);
            this.Charges = this.reader.ExpeditionNear(player);
            var round = this.reader.TrialRound();
            this.LogChange(ring, round, player);
            if (!this.areaStatsLogged)
            {
                this.areaStatsLogged = true;
                this.log.Info(this.reader.AreaStats());
            }

            foreach (var line in this.news.Breach(ring, player, now))
            {
                if (!leagues)
                {
                    continue;
                }

                if (line == RulesetLine.OutsideBreach)
                {
                    this.output.Urge(Announcements.Ruleset(line, 0, 0));
                }
                else
                {
                    this.output.Say(SpeechKind.Quest, Announcements.Ruleset(line, 0, 0));
                }
            }

            if (this.news.Round(round?.Round) is { } begun && trials && round is { } trial)
            {
                this.output.Say(SpeechKind.Quest, Announcements.Ruleset(RulesetLine.TrialRound, begun, trial.Total));
            }

            var objective = round == null ? null : this.reader.TrialObjective();
            if (objective is { } kind && this.trialHudLogged.Add($"objective {kind.Kind}"))
            {
                this.log.Info($"Trial objective: {kind.Kind} = \"{kind.Text}\""); // confirms the room's row and its kind's text
            }

            if (this.news.Objective(objective?.Text) is { } asked && trials)
            {
                this.output.Say(SpeechKind.Quest, asked);
            }

            if (this.news.Progress(round == null ? null : this.reader.TrialProgress()) is { } percent && trials)
            {
                this.output.Say(SpeechKind.Quest, Announcements.TrialProgress(percent));
            }

            // Inside a Trial of Chaos only (its ruleset is there). The whole interface is walked every few seconds for the Trial's
            // elements; between walks only those are looked at.
            if (round != null)
            {
                if (now - this.trialHudFound >= TrialHudWalkMs)
                {
                    this.trialHudFound = now;
                    this.trialHud.Discover(this.Container());
                }

                var shown = this.trialHud.Visible();
                foreach (var (name, text) in shown)
                {
                    if (this.trialHudLogged.Add($"{name} = {System.Text.RegularExpressions.Regex.Replace(text, @"\d+", "#")}"))
                    {
                        this.log.Info($"Trial HUD: {name} = \"{text}\""); // which names the game's elements really have, and what they hold
                    }
                }

                foreach (var line in this.trialHudNews.Heard(shown, now))
                {
                    // The objective is said from the ruleset: the same words on screen are not said twice.
                    if (trials && line != objective?.Text)
                    {
                        this.output.Say(SpeechKind.Quest, line);
                    }
                }
            }
        }

        /// <summary>
        ///     How the fields are confirmed from a session's log: the ring by its state and quarter, the charges and the round as
        ///     they change, with the player's distance from the ring's centre beside its radius.
        /// </summary>
        private void LogChange(BreachRing? ring, (int Round, int Total)? round, Vector2 player)
        {
            var breach = ring is { } open
                ? $"breach state {open.State}, quarter {BreachRings.QuarterOf(open.Value)}, centre ({open.Centre.X:F0},{open.Centre.Y:F0})"
                : "no breach";
            var line = $"{breach}; charges {(this.Charges is { } charges ? $"{charges.Placed}/{charges.Total}" : "none")}; trial round {(round is { } trial ? $"{trial.Round}/{trial.Total}" : "none")}";
            if (line == this.lastLogged)
            {
                return;
            }

            this.lastLogged = line;
            var detail = ring is { } seen ? $" (value {seen.Value:F1}, radius {seen.Radius:F0}, player {Vector2.Distance(player, seen.Centre):F0} from its centre)" : string.Empty;
            this.log.Info($"Rulesets read: {line}{detail}");
        }
    }
}
