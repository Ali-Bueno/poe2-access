namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Natives;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     A boss fight's turns from memory, read through <see cref="BossMechanicsCatalog"/> (docs/research/boss-mechanics.md):
    ///     the boss vanishing and coming back, turning invulnerable (with its adds counted) and vulnerable, enraging, its next
    ///     phase or form, its adds when the enemy news is off; and, for a boss's attack whose danger area holds the player, the
    ///     way out in a word ("¡Sal!", "¡Al centro!", "¡Izquierda!"). Fed by <see cref="BossWatch"/> from the monsters it already walks.
    /// </summary>
    public sealed class BossMechanicsWatch
    {
        /// <summary>A boss that turns into another waits this long before its leaving is said: the new form's "Phase 2" says the turn.</summary>
        private const int FormSettleMs = 3000;

        /// <summary>Phase news this close together are one turn (a form appearing as its phase state rises; a boss of several bodies).</summary>
        private const int PhaseMergeMs = 4000;

        /// <summary>Adds appearing this soon after the first of a wave are counted with it.</summary>
        private const int AddsGatherMs = 600;

        /// <summary>Two counterplay words at least this far apart (ms): two bosses' areas do not pile up.</summary>
        private const int WordGapMs = 800;

        /// <summary>A blow closer than this (ms) leaves no time to hear a word and move: the dodge cue alone sounds.</summary>
        private const int MinWordLeadMs = 250;

        /// <summary>How long a counterplay word holds the speech against other interruptions (ms).</summary>
        private const int WordHoldMs = 700;

        /// <summary>The probe step (world units) that turns a way out into a stick direction on screen.</summary>
        private const float ProbeWorld = 100f;

        private readonly IAccessibilityOutputService output;
        private readonly UiElementReader elements;
        private readonly PluginLog log;
        private readonly HostMemoryReader memory;
        private readonly Dictionary<uint, Boss> bosses = new();
        private readonly List<(uint Id, string Monster)> others = new();
        private readonly HashSet<uint> inPlaySeen = new();
        private readonly HashSet<string> formsSaid = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<uint> addsSaid = new();
        private readonly HashSet<(uint, long)> worded = new();
        private readonly HashSet<string> logged = new(StringComparer.Ordinal);
        private int phase = 1;
        private long? playerDiedAt;
        private long lastPhaseAt = long.MinValue / 2;
        private long lastWordAt = long.MinValue / 2;
        private long addsWaveAt;
        private int addsWave;

        public BossMechanicsWatch(IAccessibilityOutputService output, UiElementReader elements, PluginLog log)
        {
            this.output = output;
            this.elements = elements;
            this.log = log;
            this.memory = new HostMemoryReader(log);
        }

        public BossMechanicsCatalog Catalog { get; set; } = BossMechanicsCatalog.Shared;

        /// <summary>Whether a boss's adds are counted aloud: only while the enemy news (which names them) is off.</summary>
        public Func<bool> SayAdds { get; set; } = () => false;

        public void Reset()
        {
            this.bosses.Clear();
            this.others.Clear();
            this.inPlaySeen.Clear();
            this.formsSaid.Clear();
            this.addsSaid.Clear();
            this.worded.Clear();
            this.phase = 1;
            this.addsWave = 0;
            this.playerDiedAt = null;
        }

        /// <summary>The player died at <paramref name="at"/>: a boss back at full life afterwards starts its fight over.</summary>
        public void PlayerDied(long at) => this.playerDiedAt = at;

        /// <summary>One monster within the boss watch's range, each life poll.</summary>
        public void Observe(Entity entity, Render render, Rarity rarity, long now)
        {
            var monster = BossMechanicsCatalog.MonsterId(entity.Path);
            var isBoss = this.Catalog.Bosses > 0 ? this.Catalog.IsBoss(monster) : rarity == Rarity.Unique;
            if (!isBoss && !this.bosses.Values.Any(boss => boss.Engaged))
            {
                return; // other monsters matter only as an engaged boss's adds
            }

            if (!EntityCatalog.IsAliveEnemy(entity) || !entity.TryGetComponent<Life>(out var life))
            {
                this.bosses.Remove(entity.Id);
                return;
            }

            var inPlay = EntityCatalog.IsInPlay(this.memory, entity);
            var firstInPlay = inPlay && this.inPlaySeen.Add(entity.Id);
            if (!isBoss)
            {
                if (inPlay)
                {
                    this.others.Add((entity.Id, monster));
                }

                return;
            }

            if (!this.bosses.TryGetValue(entity.Id, out var boss))
            {
                var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
                boss = new Boss(monster, name, new BossPhaseTracker(this.Catalog.HasForms(monster) ? FormSettleMs : BossPhaseTracker.SettleMs));
                this.bosses[entity.Id] = boss;
            }

            var percent = life.Health.Total > 0 ? life.Health.CurrentInPercent() : -1; // a zero total reads as 0 percent
            if (BossLifeSteps.FightRestarted(this.playerDiedAt, boss.HurtAt, boss.Engaged, percent))
            {
                // The game reset the encounter: its phases and forms are said from the start again.
                this.log.Info($"Boss mechanics: {boss.Name} fight restarted (full life after the player's death)");
                boss = new Boss(monster, boss.Name, new BossPhaseTracker(this.Catalog.HasForms(monster) ? FormSettleMs : BossPhaseTracker.SettleMs));
                this.bosses[entity.Id] = boss;
                this.phase = 1;
                this.lastPhaseAt = long.MinValue / 2;
                this.formsSaid.Clear();
                this.addsSaid.Clear();
                this.addsWave = 0;
            }

            if (percent is >= 0 and < 100)
            {
                boss.Engaged = true;
                boss.HurtAt = now;
            }
            var facts = this.FactsOf(entity, monster, boss.Engaged, inPlay);
            if (facts != boss.Facts)
            {
                this.LogStates(entity, monster, facts);
            }

            boss.Facts = facts;
            boss.SeenAt = now;
            if (firstInPlay)
            {
                this.NoteForm(boss, now);
            }
        }

        /// <summary>After each poll: what changed is said, once per text.</summary>
        public void EndPoll(long now)
        {
            var said = new List<string>();
            var fighting = this.bosses.Values.Where(boss => boss.SeenAt == now && boss.Engaged).ToList();
            var multi = fighting.Select(boss => boss.Name).Distinct(StringComparer.Ordinal).Count() > 1;
            var adds = this.others.Where(other => fighting.Any(boss => this.Catalog.IsAdd(boss.Monster, other.Monster))).Select(other => other.Id).ToList();
            if (fighting.Count == 0 && this.bosses.Values.All(boss => !boss.Engaged))
            {
                this.phase = 1;
            }

            foreach (var boss in this.bosses.Values.Where(boss => boss.SeenAt == now))
            {
                foreach (var news in boss.Tracker.Update(boss.Facts, now))
                {
                    if (news == BossNews.NextPhase && !this.NextPhase(now))
                    {
                        continue;
                    }

                    said.Add(Announcements.BossTurn(news, multi ? boss.Name : null, news == BossNews.Invulnerable ? adds.Count : 0, this.phase));
                }

                if (boss.FormNews)
                {
                    boss.FormNews = false;
                    said.Add(Announcements.BossTurn(BossNews.NextPhase, boss.Name, phase: this.phase));
                }
            }

            this.Waves(adds, now, said);
            foreach (var text in said.Distinct(StringComparer.Ordinal))
            {
                this.output.Say(SpeechKind.BossLife, text, true);
                this.log.Info($"Boss turn: {text}");
            }

            this.others.Clear();
        }

        /// <summary>
        ///     A boss's attack the dodge gate judged (<see cref="DodgeGate"/>): when it is aimed, in reach and seen, and its skill's
        ///     area holds the player, the way out is said once per attack, if the blow leaves time to act on it.
        /// </summary>
        public void Attack(uint id, bool unique, long startedAt, in DodgeDecision decision, AttackArea? area, Vector2 monster, Vector2 target, Vector2 player,
                           float playerRadius, StdTuple3D<float> origin, int? blowInMs, long now)
        {
            if (!unique || area is not { } shape || decision.Verdict == DodgeVerdict.Silent || blowInMs is > 0 and < MinWordLeadMs ||
                now - this.lastWordAt < WordGapMs || this.worded.Contains((id, startedAt)))
            {
                return;
            }

            if (BossCounterplay.Of(shape, monster, target, player, playerRadius, AttackAim.MarginWorld) is not { } answer)
            {
                return;
            }

            this.worded.Add((id, startedAt));
            this.lastWordAt = now;
            var octant = answer.Kind == CounterplayKind.Step ? ScreenProjection.Octant(origin, answer.Way * ProbeWorld) : 0;
            var text = Announcements.Counterplay(answer.Kind, octant);
            this.output.Urge(text, WordHoldMs);
            this.log.Debug($"Boss counterplay: monster {id} {shape.Shape} -> {answer.Kind} ({text}), blow in {blowInMs?.ToString() ?? "?"} ms");
        }

        private BossFacts FactsOf(Entity entity, string monster, bool engaged, bool inPlay)
        {
            var states = entity.TryGetComponent<StateMachine>(out var machine) ? machine.States : Array.Empty<StateMachineState>();
            var buffs = entity.TryGetComponent<Buffs>(out var effects) ? effects.StatusEffects.Keys.Select(BuffIds.Base).ToList() : new List<string>();
            long? phase = null;
            foreach (var state in states)
            {
                if (this.Catalog.IsPhaseState(monster, state.Name))
                {
                    phase = Math.Max(phase ?? long.MinValue, state.Value);
                }
            }

            var hidden = states.Any(state => this.Catalog.Hides(monster, state.Name, state.Value));
            var enraged = buffs.Any(this.Catalog.IsEnrage) || states.Any(state => state.Value > 0 && this.Catalog.IsEnrageState(monster, state.Name));
            return new BossFacts(engaged, !inPlay || hidden, buffs.Any(this.Catalog.IsUnhurtable), enraged, phase);
        }

        /// <summary>Each distinct reading of a boss's facts, states set and buffs, once: the live protocol's evidence (docs/research/boss-mechanics.md).</summary>
        private void LogStates(Entity entity, string monster, in BossFacts facts)
        {
            var states = entity.TryGetComponent<StateMachine>(out var machine)
                ? string.Join(",", machine.States.Where(state => state.Value != 0).Select(state => $"{state.Name}={state.Value}"))
                : "-";
            var buffs = entity.TryGetComponent<Buffs>(out var effects) ? string.Join(",", effects.StatusEffects.Keys.Select(BuffIds.Base).Distinct()) : "-";
            var line = $"{monster} {facts} states [{states}] buffs [{buffs}]";
            if (this.logged.Add(line))
            {
                this.log.Info($"Boss facts: {line}");
            }
        }

        /// <summary>A boss coming into play that an engaged boss turns into: its fight's next phase, said by the new name.</summary>
        private void NoteForm(Boss boss, long now)
        {
            var from = this.bosses.Values.Where(other => other != boss && other.Engaged && this.Catalog.IsForm(other.Monster, boss.Monster)).ToList();
            if (from.Count == 0 || !this.formsSaid.Add(boss.Monster))
            {
                return;
            }

            foreach (var other in from)
            {
                other.Tracker.Replaced = true;
            }

            boss.Engaged = true;
            this.NextPhase(now);
            boss.FormNews = true; // said even when merged into a phase counted a moment ago: the new name is the news
        }

        /// <summary>Counts the fight's next phase unless one was counted a moment ago; false when merged into it.</summary>
        private bool NextPhase(long now)
        {
            var fresh = now - this.lastPhaseAt >= PhaseMergeMs;
            this.lastPhaseAt = now;
            if (fresh)
            {
                this.phase++;
            }

            return fresh;
        }

        /// <summary>A wave of a boss's adds coming into play, counted once it has gathered, while the enemy news is off.</summary>
        private void Waves(List<uint> adds, long now, List<string> said)
        {
            var fresh = adds.Where(id => !this.addsSaid.Contains(id)).ToList();
            if (fresh.Count > 0 && this.addsWave == 0)
            {
                this.addsWaveAt = now;
            }

            this.addsWave += fresh.Count;
            this.addsSaid.UnionWith(fresh);
            if (this.addsWave > 0 && now - this.addsWaveAt >= AddsGatherMs)
            {
                if (this.SayAdds())
                {
                    said.Add(Announcements.BossAdds(this.addsWave));
                }

                this.addsWave = 0;
            }
        }

        /// <summary>A boss met in this area: what it is, its last facts and its tracker.</summary>
        private sealed class Boss(string monster, string name, BossPhaseTracker tracker)
        {
            public string Monster { get; } = monster;

            public string Name { get; } = name;

            public BossPhaseTracker Tracker { get; } = tracker;

            public bool Engaged { get; set; }

            /// <summary>When it was last seen below full life.</summary>
            public long HurtAt { get; set; }

            public BossFacts Facts { get; set; }

            public long SeenAt { get; set; }

            /// <summary>It came into play as an engaged boss's next form: its "Phase N: name" is due.</summary>
            public bool FormNews { get; set; }
        }
    }
}
