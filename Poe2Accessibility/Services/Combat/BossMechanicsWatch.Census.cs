namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     The census's mechanics (docs/research/boss-mechanics.md §14): a boss's helpers that strike for it, what its states
    ///     and buffs say of the arena and of what protects it, the places of the fight, and what the fight puts on the player.
    /// </summary>
    public sealed partial class BossMechanicsWatch
    {
        /// <summary>Adds that empower the boss are said again no sooner than this (ms): a wave comes in over a few seconds.</summary>
        private const int FeedersGapMs = 8000;

        /// <summary>
        ///     With no fought boss seen this long (ms) the fight is over and its places and buffs are forgotten: one poll
        ///     without the boss (out of the watched range, a torn read) is not its end.
        /// </summary>
        private const int FightOverMs = 3000;

        private readonly List<(Boss Boss, BossSignal Signal)> signals = new();
        private readonly Dictionary<uint, BossSignalTracker> signalTrackers = new();
        private readonly List<string> calm = new();
        private readonly BossPlayerBuffs playerBuffs = new();
        private long lastFightAt = long.MinValue / 2;
        private readonly HashSet<string> described = new(StringComparer.Ordinal);
        private readonly HashSet<(uint, long)> grabWorded = new();
        private readonly Dictionary<uint, string?> helpers = new();
        private readonly HashSet<uint> feedersSeen = new();
        private long feedersSaidAt = long.MinValue / 2;
        private BossPlacesWatch? places;
        private EntityPlaces? positions;

        /// <summary>Where entities stand, drawn or not (shared with the dev command that checks it).</summary>
        public EntityPlaces Positions => this.positions ??= new EntityPlaces(this.memory, this.log);

        /// <summary>Whether a harmful buff is named to the player elsewhere as it begins (<see cref="HarmfulBuffs"/>): its name is not said twice.</summary>
        public Func<string, bool> NamedElsewhere { get; set; } = _ => false;

        /// <summary>A skill's danger area from the game's tables (set by <see cref="BossWatch"/>, which reads them).</summary>
        public Func<string, AttackArea?> AreaOf { get; set; } = _ => null;

        /// <summary>
        ///     Plays the dodge warning from a place (world): a helper's danger has no attack of the boss's to carry it. False
        ///     when the dodge warnings are off: then nothing is said of it either.
        /// </summary>
        public Func<Vector2, bool>? HelperCue { get; set; }

        /// <summary>Where the heartbeat leads in a boss fight: a hiding boss, the way out of its walls, a place of its arena; null when nowhere.</summary>
        public (Vector2 At, float Radius)? LeadTo => this.Hiding ?? (this.WayOut is { } wayOut ? (wayOut, 0f) : this.places?.Lead);

        /// <summary>
        ///     Whether an entity's attacks are a fought boss's own even while the game hides it: the boss itself, out of play, or
        ///     an invisible helper that strikes for it (<see cref="BossMechanicsCatalog.IsDaemon(string, string)"/>).
        /// </summary>
        public bool StrikesForBoss(Entity entity)
        {
            if (this.bosses.TryGetValue(entity.Id, out var boss))
            {
                return boss.Engaged;
            }

            if (this.Catalog.Daemons == 0 || !this.bosses.Values.Any(other => other.Engaged))
            {
                return false;
            }

            if (!this.helpers.TryGetValue(entity.Id, out var helper))
            {
                var monster = BossMechanicsCatalog.MonsterId(entity.Path);
                this.helpers[entity.Id] = helper = this.Catalog.IsDaemon(monster) ? monster : null;
            }

            return helper != null && this.bosses.Values.Any(other => other.Engaged && this.Catalog.IsDaemon(other.Monster, helper));
        }

        /// <summary>Whether the files mark a skill as impossible to dodge-roll, evade or block.</summary>
        public bool IsUnavoidable(string skill) => this.Catalog.IsUnavoidable(skill);

        /// <summary>The game reset a boss's encounter: what its fight said once is said again.</summary>
        private void RestartCensus(uint boss)
        {
            this.signalTrackers.Remove(boss);
            this.described.Clear();
        }

        /// <summary>The poll's lines that are true only now (a count, a thing to use): said after the urgent ones, never cutting them.</summary>
        private void SayCalm()
        {
            foreach (var text in this.calm.Distinct(StringComparer.Ordinal))
            {
                this.output.Say(SpeechKind.BossLife, text, happened: true);
                this.log.Info($"Boss note: {text}");
            }

            this.calm.Clear();
        }

        private void ResetCensus()
        {
            this.signals.Clear();
            this.signalTrackers.Clear();
            this.calm.Clear();
            this.lastFightAt = long.MinValue / 2;
            this.playerBuffs.Reset();
            this.described.Clear();
            this.grabWorded.Clear();
            this.helpers.Clear();
            this.feedersSeen.Clear();
            this.places?.Reset();
        }

        /// <summary>A boss's states and buffs this poll: what changed is kept for the poll's end.</summary>
        private void NoteSignals(Entity entity, Boss boss, long now)
        {
            var states = entity.TryGetComponent<StateMachine>(out var machine)
                ? machine.States.Select(state => (state.Name, state.Value)).ToList()
                : new List<(string, long)>();
            var buffs = entity.TryGetComponent<Buffs>(out var effects)
                ? effects.StatusEffects.Keys.SelectMany(key => new[] { key, BuffIds.Base(key) }).Distinct().ToList()
                : new List<string>();
            // Kept by entity, not with the boss's record: that record is made anew whenever a read of the boss fails.
            if (!this.signalTrackers.TryGetValue(entity.Id, out var tracker))
            {
                this.signalTrackers[entity.Id] = tracker = new BossSignalTracker();
            }

            foreach (var signal in tracker.Update(this.Catalog, boss.Monster, states, buffs, boss.Engaged, now))
            {
                this.signals.Add((boss, signal));
            }
        }

        /// <summary>The poll's end: the bosses' signals, the fight's places and what it put on the player, as lines.</summary>
        private void EndCensusPoll(IReadOnlyList<Boss> fighting, bool multi, long now, List<string> said)
        {
            var changed = this.signals.ToList();
            this.signals.Clear();
            foreach (var (boss, signal) in changed)
            {
                this.log.Info($"Boss signal: {boss.Monster} {signal.Kind} {signal.Key} {signal.Value}");
                if (this.SignalLine(boss, signal, multi) is { Length: > 0 } line)
                {
                    (signal.Kind == BossSignalKind.Progress ? this.calm : said).Add(line);
                }
            }

            this.Positions.Learn();
            if (fighting.Count == 0)
            {
                if (now - this.lastFightAt >= FightOverMs)
                {
                    this.playerBuffs.Reset();
                    this.places?.Reset();
                }

                return;
            }

            this.lastFightAt = now;
            this.NoteFeeders(fighting, now, said);
            this.places ??= new BossPlacesWatch(this.elements, this.memory, this.Positions, this.log);
            this.places.AreaOf = this.AreaOf;
            foreach (var news in this.playerBuffs.Update(this.Catalog, PlayerBuffs(), this.places.RefugeUp, now))
            {
                this.log.Info($"Boss fight on the player: {news.Kind} {news.Buff} {news.Count}");
                this.SayPlayerNews(news, said);
            }

            said.AddRange(this.places.Poll(this.Catalog, fighting.Select(boss => (boss.Monster, boss.At)).ToList(), this.playerBuffs.Sheltered, this.Heartbeat(), now));
            this.calm.AddRange(this.places.Told);
            foreach (var (at, area) in this.places.HelperNews)
            {
                this.WarnOfHelper(at, area, now);
            }
        }

        /// <summary>A helper's danger reached the player: the dodge warning from its side and, with time between words, the way out of its area.</summary>
        private void WarnOfHelper(Vector2 at, AttackArea? area, long now)
        {
            if (this.HelperCue?.Invoke(at) == false || now - this.lastWordAt < WordGapMs || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var answer = area is { Shape.Length: > 0 } shape
                ? BossCounterplay.Of(shape, at, BossHelperWarnings.AimOf(shape, at, here), here, player.ModelBounds.X, AttackAim.MarginWorld)
                : null;
            var kind = answer?.Kind ?? CounterplayKind.Move;
            var octant = answer is { Kind: CounterplayKind.Step } step ? ScreenProjection.Octant(player.WorldPosition, step.Way * ProbeWorld) : 0;
            var text = Announcements.Counterplay(kind, octant);
            this.lastWordAt = now;
            this.output.Urge(text, WordHoldMs);
            this.log.Info($"Boss helper: danger from {at.X:0},{at.Y:0}, {Vector2.Distance(at, here):0} world units from the player ({area?.Shape ?? "-"}) -> {text}");
        }

        /// <summary>The adds in play whose skill changes a fought boss: said as a wave of them comes, not for each one.</summary>
        private void NoteFeeders(IReadOnlyList<Boss> fighting, long now, List<string> said)
        {
            var fresh = this.others.Where(other => fighting.Any(boss => this.Catalog.FeedsBoss(boss.Monster, other.Monster)))
                .Count(other => this.feedersSeen.Add(other.Id));
            if (fresh > 0 && now - this.feedersSaidAt >= FeedersGapMs)
            {
                this.feedersSaidAt = now;
                said.Add(Announcements.BossFed());
            }
        }

        private string? SignalLine(Boss boss, in BossSignal signal, bool multi)
        {
            var name = multi ? boss.Name : null;
            switch (signal.Kind)
            {
                case BossSignalKind.Arena:
                    return Announcements.BossArena(signal.Key);
                case BossSignalKind.Element:
                    return Announcements.BossElement(signal.Key);
                case BossSignalKind.Progress:
                    return Announcements.BossProgress(signal.Value, signal.Max);
                case BossSignalKind.Empowered:
                    return Announcements.BossEmpowered(name, this.Catalog.BuffText(signal.Key, Localization.Language) is (var title, var description) ? BossTexts.Line(title, description) : string.Empty);
                default:
                    return this.Catalog.BuffText(signal.Key, Localization.Language) is { Name.Length: > 0 } text
                        ? Announcements.ResourceEnded(text.Name)
                        : Announcements.BossEmpowerEnded(name);
            }
        }

        private void SayPlayerNews(in PlayerFightNews news, List<string> said)
        {
            var text = this.Catalog.BuffText(news.Buff, Localization.Language);
            switch (news.Kind)
            {
                case PlayerFightNewsKind.Sheltered:
                    said.Add(Announcements.BossSheltered(true));
                    break;
                case PlayerFightNewsKind.ShelterLost:
                    said.Add(Announcements.BossSheltered(false));
                    break;
                case PlayerFightNewsKind.Grabbed when !this.NamedElsewhere(news.Buff):
                    this.output.Urge(Announcements.BossGrabbed(), WordHoldMs);
                    break;
                case PlayerFightNewsKind.Stack:
                    this.calm.Add(Announcements.BossStacks(text?.Name ?? string.Empty, news.Count));
                    break;
                case PlayerFightNewsKind.Zone when text is (var name, var description):
                    // Its description once a fight; its name each time, unless the harmful buffs' news already names it.
                    var first = this.described.Add(news.Buff);
                    var line = this.NamedElsewhere(news.Buff) ? (first ? description : string.Empty) : first ? BossTexts.Line(name, description) : name;
                    if (line.Length > 0)
                    {
                        said.Add(line);
                    }

                    break;
            }
        }

        /// <summary>The followed player's buffs, each id whole and without its instance suffix, with its stacks.</summary>
        private static List<(string Id, int Stacks)> PlayerBuffs()
        {
            var buffs = new Dictionary<string, int>(StringComparer.Ordinal);
            if (Seats.Followed.TryGetComponent<Buffs>(out var effects))
            {
                foreach (var (key, effect) in effects.StatusEffects)
                {
                    var stacks = PlayerCharges.Held(effect.Charges);
                    foreach (var id in new[] { key, BuffIds.Base(key) })
                    {
                        buffs[id] = Math.Max(buffs.GetValueOrDefault(id), stacks);
                    }
                }
            }

            return buffs.Select(pair => (pair.Key, pair.Value)).ToList();
        }

        /// <summary>The word for an attack with which the boss seizes the player, once per attack.</summary>
        private void GrabWord(uint id, long startedAt, in DodgeDecision decision, string? skill, long now)
        {
            if (skill == null || decision.Verdict == DodgeVerdict.Silent || !this.bosses.TryGetValue(id, out var boss) || !boss.Engaged ||
                !this.Catalog.IsGrab(boss.Monster, skill) || !this.grabWorded.Add((id, startedAt)))
            {
                return;
            }

            this.lastWordAt = now;
            var text = Announcements.BossGrab();
            this.output.Urge(text, WordHoldMs);
            this.log.Debug($"Boss grab: monster {id} skill {skill} ({text})");
        }
    }
}
