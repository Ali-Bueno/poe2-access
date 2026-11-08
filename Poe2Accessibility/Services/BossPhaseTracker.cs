namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>A change in a boss fight worth one line.</summary>
    public enum BossNews
    {
        /// <summary>The boss left the fight for a while: hidden by its script, untargetable, set aside.</summary>
        Gone,

        Back,
        Invulnerable,
        Vulnerable,
        Enraged,

        /// <summary>Its phase state rose: the fight's music turned to its next part.</summary>
        NextPhase,
    }

    /// <summary>What memory shows of one boss this poll (<see cref="BossMechanicsWatch"/>).</summary>
    /// <param name="Engaged">The fight is on: its life has fallen below full at least once.</param>
    /// <param name="Gone">Not in play (hidden, untargetable) or in a state its script draws nothing for.</param>
    /// <param name="Invulnerable">A buff the files mark as unhurtable is on it.</param>
    /// <param name="Enraged">An enrage buff or enrage state is on it.</param>
    /// <param name="Phase">The value of its phase state; null without one.</param>
    public readonly record struct BossFacts(bool Engaged, bool Gone, bool Invulnerable, bool Enraged, long? Phase);

    /// <summary>
    ///     One boss's fight turns, each said once when it has held <see cref="SettleMs"/> (a teleport's hide or a flicker of the
    ///     flag is part of an attack the dodge cues tell); the return is said only after the leaving was. Before the fight is
    ///     on nothing is said (a boss emerging is no news). Pure.
    /// </summary>
    public sealed class BossPhaseTracker
    {
        /// <summary>A change this long-lived is news (ms): longer than a teleport slam's vanish, shorter than a burrow.</summary>
        public const int SettleMs = 800;

        /// <summary>
        ///     The boss back in play after its leaving was said is news this soon (ms). The Devourer's flag held at least 1.9 s on
        ///     each of its five returns (Mud Burrow, 2026-11-08), while waiting <see cref="SettleMs"/> said "back" a second after it
        ///     had already risen and struck.
        /// </summary>
        public const int BackSettleMs = 400;

        /// <summary>Phase rises this soon after the fight began are its start (ms): the music turns on with the boss's aggro.</summary>
        public const int EngageGraceMs = 3000;

        private readonly int goneSettleMs;
        private long? engagedAt;
        private Flag gone;
        private Flag invulnerable;
        private bool enragedSaid;
        private long? phase;

        /// <param name="goneSettleMs">How long a leaving must hold: longer for a boss that turns into another (its next form says the turn).</param>
        public BossPhaseTracker(int goneSettleMs = SettleMs) => this.goneSettleMs = goneSettleMs;

        /// <summary>Set when its next form took over: it says nothing more.</summary>
        public bool Replaced { get; set; }

        public IReadOnlyList<BossNews> Update(in BossFacts facts, long now)
        {
            var news = new List<BossNews>();
            if (this.Replaced)
            {
                return news;
            }

            if (this.gone.Step(facts.Gone, facts.Engaged, now, this.goneSettleMs, BackSettleMs) is { } left)
            {
                news.Add(left ? BossNews.Gone : BossNews.Back);
            }

            if (this.invulnerable.Step(facts.Invulnerable, facts.Engaged, now, SettleMs, SettleMs) is { } shielded)
            {
                news.Add(shielded ? BossNews.Invulnerable : BossNews.Vulnerable);
            }

            if (facts.Enraged && facts.Engaged && !this.enragedSaid)
            {
                news.Add(BossNews.Enraged);
            }

            this.enragedSaid |= facts.Enraged;
            if (facts.Engaged && this.engagedAt == null)
            {
                this.engagedAt = now;
            }

            if (facts.Phase is { } value)
            {
                // The music's first step comes with the first hits (aggro): a rise that early is the fight starting, not its next part.
                if (facts.Engaged && now - this.engagedAt >= EngageGraceMs && this.phase is { } before && value > before)
                {
                    news.Add(BossNews.NextPhase);
                }

                this.phase = value;
            }

            return news;
        }

        /// <summary>A condition, when it last changed, and the value last said (or taken as the baseline).</summary>
        private struct Flag
        {
            private bool value;
            private long changedAt;
            private bool said;

            /// <summary>The value to say now, if it changed and held (<paramref name="settleMs"/> set, <paramref name="clearMs"/> cleared); null otherwise.</summary>
            public bool? Step(bool now, bool engaged, long at, int settleMs, int clearMs)
            {
                if (now != this.value)
                {
                    (this.value, this.changedAt) = (now, at);
                }

                if (!engaged)
                {
                    this.said = false; // the fight starts with the boss hittable: the player has just hit it
                    return null;
                }

                if (this.value == this.said || at - this.changedAt < (this.value ? settleMs : clearMs))
                {
                    return null;
                }

                this.said = this.value;
                return this.value;
            }
        }
    }
}
