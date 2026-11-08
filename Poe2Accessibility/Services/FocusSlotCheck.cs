namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which slot of the UI manager holds the focused element, judged by watching the slots around it while the player
    ///     moves through a menu: the focus takes each element first, the previous-focus slot gets it a step later, so the
    ///     slot that most often holds an element no slot held before is the focus (docs/compatibility.md).
    /// </summary>
    public sealed class FocusSlotCheck
    {
        /// <summary>Moves of the focus that must agree before a slot is judged.</summary>
        public const int MovesToJudge = 3;

        private readonly int measured;
        private readonly Dictionary<int, long> last = new();
        private readonly Dictionary<int, int> leads = new();
        private readonly HashSet<long> seen = new();
        private bool looked;

        /// <summary>Initializes a new instance of the <see cref="FocusSlotCheck"/> class around the focus slot as it reads now.</summary>
        public FocusSlotCheck(int measured)
        {
            this.measured = measured;
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

            var fresh = new List<int>();
            foreach (var (offset, element) in slots)
            {
                if (element != 0 && this.last.GetValueOrDefault(offset) != element)
                {
                    this.last[offset] = element;
                    if (!this.seen.Contains(element))
                    {
                        fresh.Add(offset);
                    }
                }
            }

            // What the slots hold at the first look was not moved to: it only becomes known.
            foreach (var offset in fresh)
            {
                this.seen.Add(this.last[offset]);
                this.leads[offset] = this.leads.GetValueOrDefault(offset) + (this.looked ? 1 : 0);
            }

            this.looked = true;

            // The measured slot keeps its place once it led; another takes it only while the measured one never led.
            var atMeasured = this.leads.GetValueOrDefault(this.measured);
            this.Judged = atMeasured >= MovesToJudge ? this.measured
                : atMeasured > 0 ? null
                : this.leads.Where(lead => lead.Value >= MovesToJudge).OrderByDescending(lead => lead.Value)
                    .ThenBy(lead => Math.Abs(lead.Key - this.measured)).Select(lead => (int?)lead.Key).FirstOrDefault();
            return this.Judged;
        }
    }
}
