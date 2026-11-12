namespace Poe2Accessibility.Services
{
    using System.Linq;

    /// <summary>
    ///     The Trial's honour falling, from the HUD's "Honor: 474/513": it ends a run as life does, and a run ended on it with
    ///     nothing said (user 2026-11-07). And rising: a shrine or a fountain that restores it said nothing of what it gave
    ///     (user 2026-11-12). Pure.
    /// </summary>
    public sealed class HonourWatch
    {
        private static readonly int[] Marks = { 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 };

        // A shrine's or a fountain's worth, not the point or two a relic gives back with each kill.
        private const int RiseToSay = 5;

        private int last = -1;
        private int low = -1;

        /// <summary>
        ///     The percent honour rose to, as of the last <see cref="Heard"/>, when it stands <see cref="RiseToSay"/> points or
        ///     more over its lowest since the last rise said; null otherwise. Nothing rises from none left: the run is over,
        ///     and the next reading is another run's.
        /// </summary>
        public int? Rose { get; private set; }

        /// <summary>
        ///     Another area: its first reading is neither a fall nor a rise. A run left with honour to spare was followed by
        ///     "honour restored" as the next one began whole.
        /// </summary>
        public void Reset() => (this.last, this.low, this.Rose) = (-1, -1, null);

        /// <summary>The percent to say now, or null: when it falls past a mark. A rise, as at a fountain, starts over from there.</summary>
        /// <param name="line">The HUD's honour line.</param>
        public int? Heard(string line)
        {
            this.Rose = null;
            if (TrialHud.HonourOf(line) is not { Most: not 0 } honour)
            {
                return null;
            }

            var before = this.last;
            var percent = honour.Now * 100 / honour.Most;
            this.last = percent;
            if (this.low <= 0 || percent < this.low)
            {
                this.low = percent;
            }
            else if (percent - this.low >= RiseToSay)
            {
                (this.Rose, this.low) = (percent, percent);
            }

            return before >= 0 && Marks.Any(mark => percent <= mark && before > mark) ? percent : null;
        }
    }
}
