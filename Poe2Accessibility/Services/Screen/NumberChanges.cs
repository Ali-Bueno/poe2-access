namespace Poe2Accessibility.Services
{
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Pure: whether a price or count that changed in the window the player is in is said (a reroll's cost after A). A clock, a
    ///     percentage or a change nobody asked for is not (docs/research/ui-unseen-panels.md).
    /// </summary>
    public static class NumberChanges
    {
        /// <summary>A change this soon after the player's press is the press's answer.</summary>
        public const int RecentInputMs = 3000;

        /// <summary>Fewest milliseconds between two such lines: a counter running on never chatters.</summary>
        public const int MinGapMs = 1500;

        /// <summary>Fewest letters of a label or unit for a number to be a thing said ("Reroll", "gold"); a bare number is not.</summary>
        private const int MinLabelLetters = 3;

        /// <summary>A clock (m:ss), seconds counted ("12s", "12 seconds") or a percentage: running on its own, never news.</summary>
        private static readonly Regex Running = new(@"\d:\d{2}|\d\s?(?:s|sec|secs|seconds?|segundos?)\b|%", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>The text to say for a text whose digits alone changed; null when it is not news.</summary>
        public static string? Said(string before, string after, long sinceInputMs, long sinceSaidMs) =>
            before != after && ScreenDiffer.IsTick(before, after) && sinceInputMs <= RecentInputMs && sinceSaidMs >= MinGapMs &&
            after.Count(char.IsLetter) >= MinLabelLetters && after.Any(char.IsDigit) && !Running.IsMatch(before) && !Running.IsMatch(after)
                ? after
                : null;
    }
}
