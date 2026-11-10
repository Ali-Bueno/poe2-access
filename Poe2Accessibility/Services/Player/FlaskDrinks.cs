namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>What a flask read tells: nothing new, a drink (or running dry), a use gained, or a flask new in its slot.</summary>
    public enum FlaskNews
    {
        None,
        Drunk,
        Refilled,
        New,
    }

    /// <summary>
    ///     Which flask reads are news, and what a drink costs: the charges a flask is seen to lose when drunk, since the
    ///     base type's cost ignores the flask's modifiers (docs/research/robustness-audit-2026-11-10.md, stale values).
    ///     Until two drinks agree on the cost the flask has no use count, only charges. Slots, not item addresses: the
    ///     game makes a new item entity whenever a flask's charges change. Pure.
    /// </summary>
    public sealed class FlaskDrinks
    {
        /// <summary>A drop with another this close is a drain's tick (the game has flask-drain auras), not a drink.</summary>
        public const int DrinkAloneMs = 3000;

        private readonly Dictionary<int, (string Name, int Charges)> last = new();
        private readonly Dictionary<(int Slot, string Name), Cost> costs = new();
        private readonly HashSet<int> dry = new();

        /// <summary>The drinks left in the flask; null while what a drink costs has not been seen.</summary>
        public int? Uses(int slot, string name, int charges) =>
            this.costs.TryGetValue((slot, name), out var cost) && cost.PerUse is { } perUse ? charges / perUse : null;

        /// <summary>The charges a drink of this flask was seen to cost; null until two drinks agreed.</summary>
        public int? PerUse(int slot, string name) => this.costs.TryGetValue((slot, name), out var cost) ? cost.PerUse : null;

        /// <summary>
        ///     The flask now in <paramref name="slot"/> and what it tells; the caller keeps an area's first read silent.
        ///     <paramref name="draining"/>: something is draining the player's flasks, so a drop teaches no cost.
        /// </summary>
        public FlaskNews Update(int slot, string name, int charges, long now, bool draining = false)
        {
            var known = this.last.TryGetValue(slot, out var before);
            this.last[slot] = (name, charges);
            var same = known && before.Name == name;
            if (!this.costs.TryGetValue((slot, name), out var cost))
            {
                this.costs[(slot, name)] = cost = new Cost();
            }

            cost.Settle(now);
            var drank = same && charges < before.Charges && cost.Dropped(before.Charges - charges, now, draining);
            var empty = charges <= 0 || (cost.PerUse is { } price && charges < price);
            var filled = !empty && this.dry.Remove(slot);
            var ranDry = empty && this.dry.Add(slot);
            if (!same)
            {
                return FlaskNews.New;
            }

            if (cost.PerUse is not { } perUse)
            {
                // Without a cost there are no uses to count, but an empty flask holding charges again is a refill (the
                // town well filled a flask never drunk and nothing was said, live 2026-11-10).
                return drank || ranDry ? FlaskNews.Drunk : filled ? FlaskNews.Refilled : FlaskNews.None;
            }

            var (uses, usesBefore) = (charges / perUse, before.Charges / perUse);
            return uses < usesBefore || ranDry ? FlaskNews.Drunk
                : uses > usesBefore ? FlaskNews.Refilled
                : FlaskNews.None;
        }

        /// <summary>A new area: no reading is known; what the flasks cost stays, they are the same flasks.</summary>
        public void Clear()
        {
            this.last.Clear();
            this.dry.Clear();
            foreach (var cost in this.costs.Values)
            {
                cost.Interrupt();
            }
        }

        /// <summary>Another character may come with other flasks of the same names: the costs are learnt again.</summary>
        public void ForgetCosts() => this.costs.Clear();

        /// <summary>What one flask's drinks cost, from the drops seen in its charges.</summary>
        private sealed class Cost
        {
            private int unconfirmed;
            private (int Drop, long At)? candidate;
            private long droppedAt = long.MinValue / 2;

            public int? PerUse { get; private set; }

            /// <summary>A drop left alone long enough was a drink; two drinks in a row that agree are the cost.</summary>
            public void Settle(long now)
            {
                if (this.candidate is not var (drop, at) || now - at < DrinkAloneMs)
                {
                    return;
                }

                this.candidate = null;
                if (drop == this.PerUse)
                {
                    this.unconfirmed = 0;
                }
                else if (drop == this.unconfirmed)
                {
                    (this.PerUse, this.unconfirmed) = (drop, 0);
                }
                else
                {
                    this.unconfirmed = drop;
                }
            }

            /// <summary>The charges fell: whether it looks like a drink (none drained, no other drop just before).</summary>
            public bool Dropped(int drop, long now, bool draining)
            {
                var alone = !draining && now - this.droppedAt >= DrinkAloneMs;
                this.droppedAt = now;
                this.candidate = alone ? (drop, now) : null; // a second drop this soon also takes the first one back
                return alone;
            }

            /// <summary>The charges are not followed for a while: a drop waiting to be left alone is not judged.</summary>
            public void Interrupt() => this.candidate = null;
        }
    }
}
