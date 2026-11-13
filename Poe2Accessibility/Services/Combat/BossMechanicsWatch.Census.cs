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
        private readonly List<(Boss Boss, BossSignal Signal)> signals = new();
        private readonly BossPlayerBuffs playerBuffs = new();
        private readonly HashSet<string> described = new(StringComparer.Ordinal);
        private readonly HashSet<(uint, long)> grabWorded = new();
        private readonly Dictionary<uint, string?> helpers = new();
        private BossPlacesWatch? places;
        private EntityPlaces? positions;

        /// <summary>Where entities stand, drawn or not (shared with the dev command that checks it).</summary>
        public EntityPlaces Positions => this.positions ??= new EntityPlaces(this.memory, this.log);

        /// <summary>Whether a harmful buff is named to the player elsewhere as it begins (<see cref="HarmfulBuffs"/>): its name is not said twice.</summary>
        public Func<string, bool> NamedElsewhere { get; set; } = _ => false;

        /// <summary>The gap within which the player is at a place (the melee reach setting).</summary>
        public float ReachWorld { get; set; } = 40f;

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

        private void ResetCensus()
        {
            this.signals.Clear();
            this.playerBuffs.Reset();
            this.described.Clear();
            this.grabWorded.Clear();
            this.helpers.Clear();
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
            foreach (var signal in boss.Signals.Update(this.Catalog, boss.Monster, states, buffs, boss.Engaged, now))
            {
                this.signals.Add((boss, signal));
            }
        }

        /// <summary>The poll's end: the bosses' signals, the fight's places and what it put on the player, as lines.</summary>
        private void EndCensusPoll(IReadOnlyList<Boss> fighting, bool multi, long now, List<string> said)
        {
            foreach (var (boss, signal) in this.signals)
            {
                this.log.Info($"Boss signal: {boss.Monster} {signal.Kind} {signal.Key} {signal.Value}");
                if (this.SignalLine(boss, signal, multi) is { Length: > 0 } line)
                {
                    said.Add(line);
                }
            }

            this.signals.Clear();
            this.Positions.Learn();
            if (fighting.Count == 0)
            {
                this.playerBuffs.Reset();
                this.places?.Reset();
                return;
            }

            this.places ??= new BossPlacesWatch(this.elements, this.memory, this.Positions, this.log);
            this.places.ReachWorld = this.ReachWorld;
            foreach (var news in this.playerBuffs.Update(this.Catalog, PlayerBuffs(), this.places.RefugeUp, now))
            {
                this.log.Info($"Boss fight on the player: {news.Kind} {news.Buff} {news.Count}");
                this.SayPlayerNews(news, said);
            }

            said.AddRange(this.places.Poll(this.Catalog, fighting.Select(boss => (boss.Monster, boss.At)).ToList(), this.playerBuffs.Sheltered, this.Heartbeat(), now));
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
                    return this.Catalog.BuffText(signal.Key, Localization.Language) is { Name.Length: > 0 } text ? Announcements.ResourceEnded(text.Name) : null;
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
                    said.Add(Announcements.BossStacks(text?.Name ?? string.Empty, news.Count));
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
            if (skill == null || decision.Verdict == DodgeVerdict.Silent || !this.bosses.TryGetValue(id, out var boss) ||
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
