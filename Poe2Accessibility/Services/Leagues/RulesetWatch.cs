namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
        private bool fogRan;

        private const int TrialHudWalkMs = 3000;

        /// <summary>Once its elements are known the walk is only for new ones (a popup's labels).</summary>
        private const int TrialHudRewalkMs = 8000;

        private readonly RulesetReader reader;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly RulesetNews news = new();
        private readonly FogReader fog;
        private readonly FogNews fogNews = new();
        private string fogWhy = string.Empty;
        private long fogLogged = long.MinValue / 2;
        private readonly ChaosTrialHudReader trialHud;
        private readonly ChaosTrialHudNews trialHudNews = new();
        private readonly HashSet<string> trialHudLogged = new(StringComparer.Ordinal);
        private long trialHudFound = long.MinValue / 2;
        private bool areaStatsLogged;
        private long lastPoll;
        private string lastLogged = string.Empty;

        public RulesetWatch(RulesetReader reader, FogReader fog, ChaosTrialHudReader trialHud, IAccessibilityOutputService output, PluginLog log)
        {
            this.reader = reader;
            this.fog = fog;
            this.trialHud = trialHud;
            this.output = output;
            this.log = log;
        }

        /// <summary>
        ///     Gets a value indicating whether this area runs a Trial of Chaos (its rules hold a round). Its HUD shows no round line
        ///     (never seen, 2026-11-19), so the trial is known by its rules, not by the screen.
        /// </summary>
        public bool InChaosTrial { get; private set; }

        /// <summary>Gets or sets how many rounds the Trial of Chaos here has (the game's table by area level); null when not known.</summary>
        public Func<int?> TrialRounds { get; set; } = () => null;

        /// <summary>Gets or sets what follows a Trial of Chaos room completed, with the round won.</summary>
        public Action<int>? RoundWon { get; set; }

        /// <summary>Gets or sets the element every layer of the game's interface hangs from.</summary>
        public Func<IntPtr> Container { get; set; } = () => IntPtr.Zero;

        /// <summary>
        ///     Gets whether a Delirium encounter with a front and a rear runs: false only once one that ran in this area is over,
        ///     null when unknown. Rules that are here and never ran say nothing: a map may be fogged by other rules.
        /// </summary>
        public bool? FogRunning { get; private set; }

        /// <summary>Gets a value indicating whether the fog's edges are being spoken of from the computation, the player's depth known.</summary>
        public bool FogSpeaks { get; private set; }

        /// <summary>Gets the charges of the Expedition site nearest the player as last read: placed, and how many there are.</summary>
        public (int Placed, int Total)? Charges { get; private set; }

        /// <summary>
        ///     The area's modifier "Minimap is Revealed" (stat <c>map_minimap_revealed</c>, a map's or a fixed seed's): a sighted player
        ///     sees the whole layout from the start (<see cref="EntityCatalog.LayoutRevealed"/>). Read once per area.
        /// </summary>
        public bool MinimapRevealed { get; private set; }

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
            this.fogNews.Reset();
            (this.fogWhy, this.fogLogged, this.FogRunning, this.FogSpeaks, this.fogRan) = (string.Empty, long.MinValue / 2, null, false, false);
            this.trialHudNews.Reset();
            this.trialHud.Reset();
            this.areaStatsLogged = false;
            this.MinimapRevealed = false;
            this.InChaosTrial = false;
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
            this.InChaosTrial = round != null;
            this.LogChange(ring, round, player);
            if (!this.areaStatsLogged)
            {
                this.areaStatsLogged = true;
                this.log.Info(this.reader.AreaStats());
                var revealed = GameIds.Current.Stat(nameof(GameReader.RemoteEnums.GameStats.map_minimap_revealed), (int)GameReader.RemoteEnums.GameStats.map_minimap_revealed);
                this.MinimapRevealed = this.reader.AreaStat(revealed) > 0;
                if (this.MinimapRevealed)
                {
                    this.log.Info("Minimap revealed by the area's modifier: its layout counts as seen");
                }
            }

            this.Fog(render, now, leagues);
            foreach (var line in this.news.Breach(ring, player, now))
            {
                if (!leagues)
                {
                    continue;
                }

                if (line == RulesetLine.OutsideBreach)
                {
                    this.output.Urge(Announcements.Ruleset(line));
                }
                else
                {
                    this.output.Say(SpeechKind.Quest, Announcements.Ruleset(line));
                }
            }

            // The client counts 10 rounds in every trial; how many this one has is the game's table by the area's level.
            var rounds = round == null ? null : this.TrialRounds();
            if (this.news.RoundWon(round?.Round) is { } won)
            {
                this.log.Info($"Trial round won: {won} of {rounds?.ToString() ?? "?"} (the client's count {round?.Total})");
                this.RoundWon?.Invoke(won);
                if (trials)
                {
                    this.output.Say(SpeechKind.Quest, Announcements.TrialRoundWon(won, rounds));
                }
            }

            var objective = round == null ? null : this.reader.TrialObjective();
            if (objective is { } kind && this.trialHudLogged.Add($"objective {kind.Kind}"))
            {
                this.log.Info($"Trial objective: {kind.Kind} = \"{kind.Text}\""); // confirms the room's row and its kind's text
            }

            if (this.news.Objective(objective?.Room ?? IntPtr.Zero, objective?.Text) is { } asked && trials && round is { } standing)
            {
                var line = standing.Round <= (rounds ?? int.MaxValue) ? Announcements.TrialRoundAsks(standing.Round, rounds, asked) : asked;
                var explained = Announcements.ChaosObjectiveExplained(objective!.Value.Kind);
                this.output.Say(SpeechKind.Quest, explained.Length > 0 ? $"{line}. {explained}" : line);
            }

            if (this.news.Progress(round == null ? null : this.reader.TrialProgress()) is { } percent && trials)
            {
                this.output.Say(SpeechKind.Quest, Announcements.TrialProgress(percent));
            }

            // Inside a Trial of Chaos only (its ruleset is there). The whole interface is walked every few seconds for the Trial's
            // elements; between walks only those are looked at.
            if (round != null)
            {
                // Until an element is found, every few seconds; after, rarely: a new popup's labels are found at the next walk.
                if (now - this.trialHudFound >= (this.trialHud.Found == 0 ? TrialHudWalkMs : TrialHudRewalkMs))
                {
                    this.trialHudFound = now;
                    this.trialHud.Discover(this.Container());
                }

                var shown = this.trialHud.Visible();
                foreach (var (_, name, text) in shown)
                {
                    if (this.trialHudLogged.Add($"{name} = {System.Text.RegularExpressions.Regex.Replace(text, @"\d+", "#")}"))
                    {
                        this.log.Info($"Trial HUD: {name} = \"{text}\""); // which names the game's elements really have, and what they hold
                    }
                }

                foreach (var line in this.trialHudNews.Heard(shown.Select(element => (element.Id, element.Text)).ToList(), now))
                {
                    // The objective is said from the ruleset: the same words on screen are not said twice.
                    if (trials && line != objective?.Text)
                    {
                        this.output.Say(SpeechKind.Quest, line);
                    }
                }
            }
        }

        /// <summary>How often the fog's numbers go to the log while an encounter runs.</summary>
        private const int FogLogMs = 5000;

        /// <summary>
        ///     The Delirium fog's edges: which way it went when the player is out of it, and a word before the rear or the front
        ///     reaches them. Only once the computation has agreed with the game's own stat (<see cref="FogNews"/>).
        /// </summary>
        private void Fog(Render render, long now, bool leagues)
        {
            var sample = this.fog.Read((int)render.GridPosition.X, (int)render.GridPosition.Y);
            this.fogRan |= this.fog.Running == true;
            (this.FogRunning, this.FogSpeaks) = (this.fog.Running == true ? true : this.fog.Running == false && this.fogRan ? false : null, false);
            if (this.fog.Why != this.fogWhy)
            {
                this.fogWhy = this.fog.Why;
                if (this.fogWhy != "no ruleset")
                {
                    this.log.Info($"Delirium fog: {this.fogWhy}{(this.reader.Ruleset(GameLayout.Fog.MapRulesName) != null ? "; the map's own rules are here too" : string.Empty)}");
                }
            }

            if (sample is not { } read)
            {
                if (this.fogWhy is "not running" or "no ruleset")
                {
                    this.fogNews.Reset(); // each encounter earns its voice anew
                }

                return;
            }

            bool? inFog = PlayerStats.Of(Seats.Followed, LeaguePieces.FogStat) is { } stat ? stat > 0 : null;
            var line = read.Depth is { } depth ? this.fogNews.Heard(depth, read.Front, read.Rear, read.Most, inFog, now) : null;
            this.FogSpeaks = read.Depth != null && this.fogNews.Trusted;
            if (now - this.fogLogged >= FogLogMs)
            {
                // How the port is judged from a session's log: its band beside the game's own word, and their record.
                this.fogLogged = now;
                var (agreed, disagreed) = this.fogNews.Record;
                this.log.Info($"Delirium fog: front {read.Front}, rear {read.Rear}, player {read.Depth?.ToString() ?? "off the map"} of {read.Most}" +
                              $"{(read.Paused ? ", paused" : string.Empty)}; the game says {(inFog is { } said ? (said ? "in" : "out") : "nothing")}; agreed {agreed}, not {disagreed}{(this.fogNews.Trusted ? ", trusted" : string.Empty)}");
            }

            if (line is not { } heard || !leagues)
            {
                return;
            }

            var way = heard is FogLine.LeftBehind or FogLine.RearNear ? read.Deeper : heard == FogLine.Outran ? read.Shallower : null;
            var text = Announcements.Fog(heard, SideOf(render, way));
            if (heard is FogLine.LeftBehind or FogLine.Outran)
            {
                this.output.Urge(text);
            }
            else
            {
                this.output.Say(SpeechKind.Quest, text);
            }
        }

        /// <summary>A way over the ground as the side of the screen it goes ("arriba a la derecha"); empty without one.</summary>
        private static string SideOf(Render render, Vector2? way)
        {
            if (way is not { } step)
            {
                return string.Empty;
            }

            var (alongX, alongY) = ScreenProjection.Axes(render.WorldPosition);
            return Announcements.HintSides[StickDirection.Octant(alongX, alongY, step.X, step.Y)];
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
