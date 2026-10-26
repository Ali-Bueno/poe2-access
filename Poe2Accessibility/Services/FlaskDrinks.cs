namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Which flask reads are news: a drink (fewer uses in the same belt slot, same flask) or a flask running dry, once
    ///     until it refills. Slots, not item addresses: the game makes a new item entity whenever a flask's charges change
    ///     (a drink moved the life flask 0x2028CB186C0 → 0x2028CB1ED40, 15 → 5 charges, 2026-10-26). Pure.
    /// </summary>
    public sealed class FlaskDrinks
    {
        private readonly Dictionary<int, (string Name, int Uses)> last = new();
        private readonly HashSet<int> dry = new();

        /// <summary>The flask now in <paramref name="slot"/>; true when its uses left are worth saying.</summary>
        public bool Update(int slot, string name, int uses)
        {
            // Charges fall only when the flask is drunk (kills and time refill it).
            var drunk = this.last.TryGetValue(slot, out var before) && before.Name == name && uses < before.Uses;
            this.last[slot] = (name, uses);
            if (uses > 0)
            {
                this.dry.Remove(slot);
            }

            var ranDry = uses <= 0 && this.dry.Add(slot);
            return drunk || ranDry;
        }

        /// <summary>A new area: nothing is known.</summary>
        public void Clear()
        {
            this.last.Clear();
            this.dry.Clear();
        }
    }
}
