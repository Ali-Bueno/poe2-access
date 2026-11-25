namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>One item's stat lines against a worn one's: the lines both have with other numbers, those only one has, and the equal ones.</summary>
    /// <param name="Changed">The item's line and the worn item's numbers in it ("Evasión: 127", "81").</param>
    public sealed record ItemDifference(IReadOnlyList<(string Line, string Worn)> Changed, IReadOnlyList<string> OnlyThis, IReadOnlyList<string> OnlyWorn, IReadOnlyList<string> Same);

    /// <summary>What sets an item apart from the worn one, from the lines of their cards in the game's Compare view. Pure.</summary>
    public static class ItemComparison
    {
        private static readonly Regex Number = new(@"[-+]?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        /// <summary>From a line's first number to its last, with its sign and percent: "32-48", "+15%", "7 a 8".</summary>
        private static readonly Regex Numbers = new(@"[-+]?\d(?:.*\d)?%?", RegexOptions.Compiled | RegexOptions.Singleline);

        public static ItemDifference Compare(IReadOnlyList<string> item, IReadOnlyList<string> worn)
        {
            var left = Merged(worn);
            var changed = new List<(string, string)>();
            var only = new List<string>();
            var same = new List<string>();
            foreach (var line in Merged(item))
            {
                // An equal line first: an item may have two lines of one shape.
                var at = left.IndexOf(line);
                at = at >= 0 ? at : left.FindIndex(other => BuildGear.Shape(other) == BuildGear.Shape(line));
                if (at < 0)
                {
                    only.Add(line);
                    continue;
                }

                if (left[at] == line)
                {
                    same.Add(line);
                }
                else
                {
                    changed.Add((line, Numbers.Match(left[at]).Value));
                }

                left.RemoveAt(at);
            }

            return new ItemDifference(changed, only, left, same);
        }

        /// <summary>
        ///     The lines as the plain tooltip adds them up: the advanced view writes a stat once per modifier ("+38 a la vida
        ///     máxima", "+18 a la vida máxima"). Only lines of one number, where a sum is what the game shows.
        /// </summary>
        public static List<string> Merged(IReadOnlyList<string> lines)
        {
            var merged = new List<string>();
            foreach (var line in lines)
            {
                var at = Number.Matches(line).Count == 1 ? merged.FindIndex(other => BuildGear.Shape(other) == BuildGear.Shape(line)) : -1;
                if (at < 0)
                {
                    merged.Add(line);
                    continue;
                }

                var sum = Value(merged[at]) + Value(line);
                merged[at] = Number.Replace(merged[at], match => Written(sum, match.Value), 1);
            }

            return merged;
        }

        private static decimal Value(string line) =>
            decimal.Parse(Number.Match(line).Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture);

        private static string Written(decimal sum, string like)
        {
            var text = sum.ToString("0.##", CultureInfo.InvariantCulture);
            text = like.Contains(',') ? text.Replace('.', ',') : text;
            return like.StartsWith('+') && sum >= 0 ? "+" + text : text;
        }
    }
}
