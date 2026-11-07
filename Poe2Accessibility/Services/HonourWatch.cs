namespace Poe2Accessibility.Services
{
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The Trial's honour falling, from the HUD's "Honor: 474/513": it ends a run as life does, and a run ended on it with
    ///     nothing said (user 2026-11-07). Pure.
    /// </summary>
    public sealed class HonourWatch
    {
        private static readonly Regex Amount = new(@"(\d+)/(\d+)$", RegexOptions.Compiled);
        private static readonly int[] Marks = { 90, 80, 70, 60, 50, 40, 30, 20, 10, 0 };

        private int last = -1;

        /// <summary>The percent to say now, or null: when it falls past a mark. A rise, as at a fountain, starts over from there.</summary>
        /// <param name="line">The HUD's honour line.</param>
        public int? Heard(string line)
        {
            var amount = Amount.Match(line);
            var most = amount.Success ? int.Parse(amount.Groups[2].Value) : 0;
            if (most == 0)
            {
                return null;
            }

            var before = this.last;
            var percent = int.Parse(amount.Groups[1].Value) * 100 / most;
            this.last = percent;
            return before >= 0 && Marks.Any(mark => percent <= mark && before > mark) ? percent : null;
        }
    }
}
