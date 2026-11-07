namespace Poe2Accessibility.Services
{
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     A clock falling on the HUD, such as the Trial's "00:19": its digits alone changing is no news to the screen differ, so
    ///     the crystals' 20 seconds ran out unheard and killed the player (user 2026-11-07). Pure.
    /// </summary>
    public sealed class CountdownWatch
    {
        private static readonly Regex Clock = new(@"^(\d{1,2}):(\d{2})$", RegexOptions.Compiled);
        private static readonly int[] Marks = { 60, 30, 20, 10, 5 };

        private int last = -1;
        private bool said;

        /// <summary>Whether a HUD text is a clock.</summary>
        public static bool IsClock(string text) => Clock.IsMatch(text);

        /// <summary>The seconds to say now, or null: said once the clock is seen to fall, then as it passes each mark.</summary>
        /// <param name="clock">The clock on the HUD, null with none.</param>
        public int? Heard(string? clock)
        {
            if (clock == null || Clock.Match(clock) is not { Success: true } parts)
            {
                (this.last, this.said) = (-1, false);
                return null;
            }

            var before = this.last;
            var left = (int.Parse(parts.Groups[1].Value) * 60) + int.Parse(parts.Groups[2].Value);
            this.last = left;
            if (before < 0 || left > before)
            {
                this.said = false;
                return null;
            }

            if (left == before)
            {
                return null;
            }

            var due = !this.said || Marks.Any(mark => left <= mark && before > mark);
            this.said = true;
            return due ? left : null;
        }
    }
}
