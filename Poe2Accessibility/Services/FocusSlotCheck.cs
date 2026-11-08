namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which slot of the UI manager holds the focused element, judged by watching the slots around it while the player
    ///     moves through a menu: the focus takes each element first and the previous-focus slot, a fixed step after it, gets
    ///     that element when the focus moves on. A slot counts a move only when both happen, so the hovered slot (a free
    ///     cursor's elements, with no previous-focus slot following it) is never taken for the focus (docs/compatibility.md).
    /// </summary>
    public sealed class FocusSlotCheck
    {
        /// <summary>Moves of the focus that must be seen before a slot is judged.</summary>
        public const int MovesToJudge = 3;

        private readonly int measured;
        private readonly int lag;
        private readonly Dictionary<int, long> last = new();
        private readonly Dictionary<int, HashSet<long>> led = new();
        private readonly Dictionary<int, int> moves = new();
        private readonly HashSet<long> seen = new();
        private bool looked;

        /// <summary>Initializes a new instance of the <see cref="FocusSlotCheck"/> class.</summary>
        /// <param name="measured">the focus slot as it reads now.</param>
        /// <param name="lag">how far after the focus the previous-focus slot sits.</param>
        public FocusSlotCheck(int measured, int lag)
        {
            this.measured = measured;
            this.lag = lag;
        }

        /// <summary>Gets the judged focus slot; null until the player moved enough.</summary>
        public int? Judged { get; private set; }

        /// <summary>One look at the slots: each offset with the element of this manager it holds (zero for none); the judged slot once known.</summary>
        public int? See(IReadOnlyDictionary<int, long> slots)
        {
            if (this.Judged != null)
            {
                return this.Judged;
            }

            var changed = slots.Where(slot => slot.Value != 0 && this.last.GetValueOrDefault(slot.Key) != slot.Value).ToList();
            foreach (var (offset, element) in changed)
            {
                this.last[offset] = element;

                // What the slots hold at the first look was not moved to: it only becomes known.
                if (this.looked && !this.seen.Contains(element))
                {
                    this.Led(offset).Add(element);
                }

                // The previous-focus slot takes what the slot a step before it led with: that slot's focus moved on.
                if (this.led.TryGetValue(offset - this.lag, out var before) && before.Remove(element))
                {
                    this.moves[offset - this.lag] = this.moves.GetValueOrDefault(offset - this.lag) + 1;
                }
            }

            this.seen.UnionWith(changed.Select(slot => slot.Value));
            this.looked = true;

            // The measured slot keeps its place once it moved; another takes it only while the measured one never did.
            var atMeasured = this.moves.GetValueOrDefault(this.measured);
            this.Judged = atMeasured >= MovesToJudge ? this.measured
                : atMeasured > 0 ? null
                : this.moves.Where(move => move.Value >= MovesToJudge).OrderByDescending(move => move.Value)
                    .ThenBy(move => Math.Abs(move.Key - this.measured)).Select(move => (int?)move.Key).FirstOrDefault();
            return this.Judged;
        }

        private HashSet<long> Led(int offset)
        {
            if (!this.led.TryGetValue(offset, out var elements))
            {
                elements = new HashSet<long>();
                this.led[offset] = elements;
            }

            return elements;
        }
    }
}
