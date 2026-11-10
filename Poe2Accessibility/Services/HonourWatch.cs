namespace Poe2Accessibility.Services
{
    using System.Linq;

    /// <summary>
    ///     The Trial's honour falling, from the HUD's "Honor: 474/513": it ends a run as life does, and a run ended on it with
    ///     nothing said (user 2026-11-07). Pure.
    /// </summary>
    public sealed class HonourWatch
    {
        private static readonly int[] Marks = { 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 };

        private int last = -1;

        /// <summary>The percent to say now, or null: when it falls past a mark. A rise, as at a fountain, starts over from there.</summary>
        /// <param name="line">The HUD's honour line.</param>
        public int? Heard(string line)
        {
            if (TrialHud.HonourOf(line) is not { Most: not 0 } honour)
            {
                return null;
            }

            var before = this.last;
            var percent = honour.Now * 100 / honour.Most;
            this.last = percent;
            return before >= 0 && Marks.Any(mark => percent <= mark && before > mark) ? percent : null;
        }
    }
}
