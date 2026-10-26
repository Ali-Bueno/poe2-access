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
    ///     Which flask reads are news: a drink (fewer uses in the same belt slot, same flask), running dry once until it
    ///     refills, a use gained (kills and time refill it), a flask put in a slot. Slots, not item addresses: the game
    ///     makes a new item entity whenever a flask's charges change (a drink moved the life flask 0x2028CB186C0 →
    ///     0x2028CB1ED40, 15 → 5 charges, 2026-10-26). Pure.
    /// </summary>
    public sealed class FlaskDrinks
    {
        private readonly Dictionary<int, (string Name, int Uses)> last = new();
        private readonly HashSet<int> dry = new();

        /// <summary>The flask now in <paramref name="slot"/> and what it tells; the caller keeps an area's first read silent.</summary>
        public FlaskNews Update(int slot, string name, int uses)
        {
            var known = this.last.TryGetValue(slot, out var before);
            this.last[slot] = (name, uses);
            if (uses > 0)
            {
                this.dry.Remove(slot);
            }

            var ranDry = uses <= 0 && this.dry.Add(slot);
            if (!known || before.Name != name)
            {
                return FlaskNews.New;
            }

            return uses < before.Uses || ranDry ? FlaskNews.Drunk
                : uses > before.Uses ? FlaskNews.Refilled
                : FlaskNews.None;
        }

        /// <summary>A new area: nothing is known.</summary>
        public void Clear()
        {
            this.last.Clear();
            this.dry.Clear();
        }
    }
}
