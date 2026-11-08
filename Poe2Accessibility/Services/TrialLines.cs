namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>A HUD counter line, "Round 3/10" or "5 Death Crystals remaining", as its pattern reads it.</summary>
    public sealed record CounterLine(string Text, int Value, int Of);

    /// <summary>The trials' counter lines on the HUD (patterns from <see cref="TrialCatalog"/>). Pure.</summary>
    public static class TrialLines
    {
        /// <summary>The first text the pattern matches, with its numbers; null for none or no pattern.</summary>
        public static CounterLine? Read(Regex? pattern, IEnumerable<string> texts)
        {
            if (pattern == null)
            {
                return null;
            }

            foreach (var text in texts)
            {
                if (pattern.Match(text) is { Success: true } match)
                {
                    var of = match.Groups.Count > 2 ? Number(match.Groups[2].Value) : 0;
                    return new CounterLine(text, Number(match.Groups[1].Value), of);
                }
            }

            return null;
        }

        private static int Number(string digits) => int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    /// <summary>
    ///     A counter whose digits alone change: no news to the screen differ, so a round beginning or a crystal disabled went
    ///     unheard. The line's first sight is the differ's (a text appearing); from then on each change is said. Pure.
    /// </summary>
    public sealed class CounterWatch
    {
        private readonly bool fallsOnly;
        private CounterLine? last;

        /// <param name="fallsOnly">Say it only going down: the Death Crystals left.</param>
        public CounterWatch(bool fallsOnly) => this.fallsOnly = fallsOnly;

        /// <summary>The line to say now, or null; a line gone (the room done, the trial left) starts over.</summary>
        public CounterLine? Heard(CounterLine? line)
        {
            var before = this.last;
            this.last = line;
            if (line == null || before == null || (line.Value == before.Value && line.Of == before.Of))
            {
                return null;
            }

            return this.fallsOnly && line.Value >= before.Value ? null : line;
        }
    }
}
