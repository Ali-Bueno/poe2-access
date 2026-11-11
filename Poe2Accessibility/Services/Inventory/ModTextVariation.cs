namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     One line of a stat description block as the game's file holds it (docs/research/file-catalogs.md, "Modifier
    ///     texts"): a condition per stat, the quoted text with its places ("{0}", "{0:+d}", "{}"), and handlers that turn a
    ///     stat's value into the number shown ("negate 1": a "reduced" line holds a negative stat). Pure.
    /// </summary>
    internal sealed class ModTextVariation
    {
        /// <summary>The mark a number leaves in a text's skeleton, whatever the number.</summary>
        internal const char NumberMark = '#';

        /// <summary>The first of the private characters a place is kept as while markup is taken out (its braces are no tag's).</summary>
        private const char FirstPlace = '', LastPlace = '';

        private const string NumberPattern = @"\d+(?:[.,]\d+)?";

        private static readonly Regex Place = new(@"\{(\d*)(?::(\+?)d?)?\}", RegexOptions.Compiled);
        /// <summary>Braces left after the known places that wrap no tag's text.</summary>
        private static readonly Regex OtherPlace = new(@"(?<!>)\{[^{}<>]*\}", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex KeywordLink = new(@"\[([^\]|]+)(?:\|([^\]]+))?\]", RegexOptions.Compiled);

        /// <summary>A number or a place, with the sign written right before it unless that sign joins two numbers ("5-10").</summary>
        private static readonly Regex NumberToken = new(@"(?:(?<![\d-)])(?<sign>[+\-−]))?(?:(?<place>[-])|(?<number>" + NumberPattern + "))", RegexOptions.Compiled);

        /// <summary>A rolled number with its range after it, as an item's tooltip shows it: "40(30-40)".</summary>
        private static readonly Regex RolledWithRange = new(@"(?<=\d)\s?\(\s*-?" + NumberPattern + @"\s*[-–—]\s*-?" + NumberPattern + @"\s*\)", RegexOptions.Compiled);

        /// <summary>A range alone, as a crafting text shows what an item may get: "(11-15)".</summary>
        private static readonly Regex RangeAlone = new(@"\(\s*(" + NumberPattern + @")\s*[-–—]\s*" + NumberPattern + @"\s*\)", RegexOptions.Compiled);

        /// <summary>The decimals a number is shown with when no handler says: as many as it has, two at most.</summary>
        private const int DefaultDecimals = 2;

        /// <summary>From this number on the tooltip may group the digits, which was never seen on a modifier line.</summary>
        private const double FirstGrouped = 1000;

        private const double Tiny = 1e-9;

        private readonly Condition[] conditions;
        private readonly Shown[] shown;
        private readonly Token[] tokens;
        private readonly string plain;
        private readonly int[] shownStats;

        private ModTextVariation(Condition[] conditions, Shown[] shown, Token[] tokens, string plain, string template, string skeleton) =>
            (this.conditions, this.shown, this.tokens, this.plain, this.Template, this.Skeleton, this.shownStats) =
            (conditions, shown, tokens, plain, template, skeleton, tokens.Where(token => token.Stat >= 0).Select(token => token.Stat).Distinct().Order().ToArray());

        /// <summary>Gets the text with markup out and its places as the file writes them: "{0:+d}% to Fire Resistance".</summary>
        public string Template { get; }

        /// <summary>Gets the text as lines are compared: one case, single spaces, every number and place a <see cref="NumberMark"/>.</summary>
        public string Skeleton { get; }

        /// <summary>
        ///     The line, or null for one this reader cannot apply whole: a word before the text that is no condition
        ///     ("table_only"), a place format or a handler it does not know (one that shows a name, not a number).
        /// </summary>
        public static ModTextVariation? Parse(string line, int stats)
        {
            var (first, last) = (line.IndexOf('"'), line.LastIndexOf('"'));
            if (last <= first)
            {
                return null;
            }

            var words = line[..first].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            var conditions = new Condition[stats];
            if (words.Length != stats)
            {
                return null;
            }

            for (var stat = 0; stat < stats; stat++)
            {
                if (Condition.Parse(words[stat]) is not { } condition)
                {
                    return null;
                }

                conditions[stat] = condition;
            }

            var shown = Enumerable.Range(0, stats).Select(_ => new Shown(1, 0, DefaultDecimals, false)).ToArray();
            var handlers = line[(last + 1)..].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (var at = 0; at < handlers.Length; at += 2)
            {
                if (handlers[at] == StatHandlers.CanonicalLine)
                {
                    at--; // the one handler with no stat number after it
                    continue;
                }

                if (StatHandlers.Marks.Contains(handlers[at]))
                {
                    continue;
                }

                if (at + 1 >= handlers.Length || !int.TryParse(handlers[at + 1], out var stat) || stat < 1 || stat > stats || !shown[stat - 1].With(handlers[at], out var changed))
                {
                    return null;
                }

                shown[stat - 1] = changed;
            }

            // Places first, as private characters: the markup cleaner would take their braces for a tag's.
            var places = new List<(int Stat, bool Plus)>();
            var unknownPlace = false;
            var text = Place.Replace(line[(first + 1)..last].Replace("\\n", " "), match =>
            {
                var stat = match.Groups[1].Length == 0 ? places.Count : int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var written) ? written : stats;
                unknownPlace |= stat >= stats || places.Count > LastPlace - FirstPlace;
                places.Add((stat, match.Groups[2].Length > 0));
                return ((char)(FirstPlace + places.Count - 1)).ToString();
            });
            if (unknownPlace || OtherPlace.IsMatch(text))
            {
                return null; // a place written another way ("{0:-d}"): its meaning is not known
            }

            var plain = Clean(text);

            var tokens = new List<Token>();
            var skeleton = Skeletal(plain, tokens, places);
            var template = new StringBuilder(plain.Length + 8);
            foreach (var letter in plain)
            {
                if (letter is >= FirstPlace and <= LastPlace)
                {
                    template.Append('{').Append(places[letter - FirstPlace].Stat).Append(places[letter - FirstPlace].Plus ? ":+d}" : "}");
                }
                else
                {
                    template.Append(letter);
                }
            }

            return new ModTextVariation(conditions, shown, tokens.ToArray(), plain, template.ToString(), skeleton);
        }

        /// <summary>A line as a person or the game wrote it, as it is compared, with the numbers in it in order; a rolled number's range is dropped and a range alone gives its lower end.</summary>
        public static (string Skeleton, IReadOnlyList<double> Numbers) Read(string line)
        {
            var tokens = new List<Token>();
            var skeleton = Skeletal(RangeAlone.Replace(RolledWithRange.Replace(Clean(line), string.Empty), "$1"), tokens, null);
            return (skeleton, tokens.Select(token => token.Number).ToList());
        }

        /// <summary>Whether the stat values meet every condition of the line.</summary>
        public bool Fits(IReadOnlyList<double> values) => !this.conditions.Where((condition, stat) => !condition.Holds(values[stat])).Any();

        /// <summary>
        ///     The stat values a line with these numbers holds, or null when the numbers are not this line's (a literal
        ///     number of the text differs). A stat the text does not show takes the one value its condition allows, else the
        ///     end of its range nearest zero: every value in the range reads as this same line.
        /// </summary>
        public double[]? Values(IReadOnlyList<double> numbers)
        {
            if (numbers.Count != this.tokens.Length)
            {
                return null;
            }

            var values = new double?[this.conditions.Length];
            for (var at = 0; at < this.tokens.Length; at++)
            {
                var token = this.tokens[at];
                if (token.Stat < 0)
                {
                    if (!Same(token.Number, numbers[at]))
                    {
                        return null;
                    }

                    continue;
                }

                // A sign the text writes itself is the text's, not the number's.
                var value = this.shown[token.Stat].Value(token.Sign == 0 ? numbers[at] : numbers[at] * token.Sign);
                if (token.Sign != 0 && Math.Sign(numbers[at]) == -token.Sign)
                {
                    return null;
                }

                if (values[token.Stat] is { } before && !Same(before, value))
                {
                    return null;
                }

                values[token.Stat] = value;
            }

            return values.Select((value, stat) => value ?? this.conditions[stat].Example).ToArray();
        }

        /// <summary>The numbers the line shows for these stat values, one a stat it has a place for, in the stats' order.</summary>
        public double[] Numbers(IReadOnlyList<double> values) => this.shownStats.Select(stat => this.shown[stat].Number(values[stat])).ToArray();

        /// <summary>The stat values behind the numbers <see cref="Numbers"/> gave (a stat not shown as in <see cref="Values"/>); null when they are not as many.</summary>
        public double[]? ValuesOf(IReadOnlyList<double> numbers)
        {
            if (numbers.Count != this.shownStats.Length)
            {
                return null;
            }

            var values = this.conditions.Select(condition => condition.Example).ToArray();
            for (var at = 0; at < this.shownStats.Length; at++)
            {
                values[this.shownStats[at]] = this.shown[this.shownStats[at]].Value(numbers[at]);
            }

            return values;
        }

        /// <summary>The line as the game writes it for these stat values.</summary>
        public string Say(IReadOnlyList<double> values) => this.Written(values, (shown, number) => shown.Digits(number));

        /// <summary>
        ///     The line for these stat values, and whether every number in it is written a way the tooltip was seen to write
        ///     one (docs/research/item-mods.md, "Numbers"): what was not seen (digits grouped, a minus sign, a fraction where
        ///     no handler states the decimals, a fixed decimal that is zero) is written as <see cref="Say"/> does and told.
        /// </summary>
        /// <param name="decimalMarkSeen">Whether a fraction's mark was seen in the language the line is written in.</param>
        public (string Text, bool Certain) Show(IReadOnlyList<double> values, bool decimalMarkSeen)
        {
            var certain = true;
            var text = this.Written(values, (shown, number) =>
            {
                var (digits, seen) = shown.Seen(number, decimalMarkSeen);
                certain &= seen;
                return digits;
            });
            return (text, certain);
        }

        private string Written(IReadOnlyList<double> values, Func<Shown, double, string> digits)
        {
            var text = new StringBuilder();
            var place = 0;
            foreach (var letter in this.plain)
            {
                if (letter is < FirstPlace or > LastPlace)
                {
                    text.Append(letter);
                    continue;
                }

                var token = this.tokens.Where(candidate => candidate.Stat >= 0).ElementAt(place++);
                var shown = this.shown[token.Stat];
                var number = shown.Number(values[token.Stat]);
                text.Append(token.Plus && number >= 0 ? "+" : string.Empty).Append(digits(shown, number));
            }

            return text.ToString();
        }

        private static bool Same(double a, double b) => Math.Abs(a - b) < 1e-6;

        /// <summary>Markup out (keyword links, tags and their braces), spaces single.</summary>
        private static string Clean(string text)
        {
            // Most lines carry no markup at all: the cleaners are regular expressions, and there are 170,000 lines to index.
            var linked = text.Contains('[') ? KeywordLink.Replace(text, link => link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value) : text;
            return linked.AsSpan().IndexOfAny("<>{}") >= 0 ? BuildNotes.Plain(linked) : Spaces.Replace(linked, " ").Trim();
        }

        /// <summary>The text with each number or place as a mark, and what each mark stood for.</summary>
        private static string Skeletal(string plain, List<Token> tokens, List<(int Stat, bool Plus)>? places)
        {
            var marked = NumberToken.Replace(plain, match =>
            {
                var sign = match.Groups["sign"].Success ? (match.Groups["sign"].Value == "+" ? 1 : -1) : 0;
                if (match.Groups["place"].Success && places != null)
                {
                    var (stat, plus) = places[match.Groups["place"].Value[0] - FirstPlace];
                    tokens.Add(new Token(stat, plus, sign, 0));
                }
                else
                {
                    var number = double.Parse(match.Groups["number"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
                    tokens.Add(new Token(-1, false, 0, sign < 0 ? -number : number));
                }

                return NumberMark.ToString();
            });
            return Spaces.Replace(marked, " ").Trim().ToLowerInvariant();
        }

        /// <summary>A number of the text: a stat's place (with the sign the text writes before it: 1, -1 or 0 for none), or for Stat -1 a number written out.</summary>
        private readonly record struct Token(int Stat, bool Plus, int Sign, double Number);

        /// <summary>How a stat's value becomes the number shown, and with how many decimals.</summary>
        /// <param name="Stated">The decimals a handler's name states ("_1dp"), as required or always; null when none does.</param>
        /// <param name="SeenDecimals">The decimals a handler that states none was seen to show; null when none was.</param>
        /// <param name="Unsettled">A handler whose arithmetic is not settled took part.</param>
        private readonly record struct Shown(double Factor, double Offset, int Decimals, bool Fixed, int? Stated = null, int? SeenDecimals = null, bool Unsettled = false, int? SeenExact = null, bool TieSeen = false)
        {
            public double Number(double value) => (value * this.Factor) + this.Offset;

            public double Value(double number) => (number - this.Offset) / this.Factor;

            /// <summary>The number's digits: the stated decimals, else as many as it has, two at most.</summary>
            public string Digits(double number) => this.Fixed
                ? number.ToString("F" + this.Decimals, CultureInfo.InvariantCulture)
                : Math.Round(number, this.Decimals).ToString(this.Decimals == 0 ? "0" : "0." + new string('#', this.Decimals), CultureInfo.InvariantCulture);

            /// <summary>The number's digits, and whether the tooltip was seen to write such a number that way.</summary>
            public (string Digits, bool Seen) Seen(double number, bool decimalMarkSeen)
            {
                var plain = !this.Unsettled && number > -Tiny && number < FirstGrouped;
                if (Math.Abs(number - Math.Round(number)) < Tiny)
                {
                    // "2" or "2.0": a whole number under decimals that are always shown was never seen.
                    var always = this.Stated is > 0 ? this.Fixed : this.Stated == null && this.SeenDecimals is > 0;
                    return (Math.Round(number).ToString("0", CultureInfo.InvariantCulture), plain && !always);
                }

                if ((this.Stated ?? this.SeenDecimals) is not (> 0 and var places))
                {
                    if (this.Stated == null && this.SeenExact is > 0 and var exact)
                    {
                        var parts = Math.Abs(number) * Math.Pow(10, exact);
                        if (Math.Abs(parts - Math.Round(parts)) < 1e-6)
                        {
                            // No more decimals than were seen written out as they are: nothing is rounded or cut.
                            return (number.ToString("F" + exact, CultureInfo.InvariantCulture), plain && decimalMarkSeen);
                        }
                    }

                    return (this.Digits(number), false); // no decimals stated, or none shown: rounded or cut is not known
                }

                var scaled = Math.Abs(number) * Math.Pow(10, places);
                var tie = Math.Abs(scaled - Math.Floor(scaled) - 0.5) < 1e-6;
                // At an exact half the tooltip showed the lower digit (201 a minute read "3.3", not "3.4"): the game's own
                // number is single precision, where 3.35 is 3.3499999. Said for certain only under a handler it was seen with.
                var rounded = tie ? (double)(float)number : number;
                var digits = Math.Round(rounded, places, MidpointRounding.AwayFromZero).ToString("F" + places, CultureInfo.InvariantCulture);
                var single = Math.Abs(rounded) * Math.Pow(10, places);
                var halfStill = tie && Math.Abs(single - Math.Floor(single) - 0.5) < 1e-9; // 2.25 is a half in single precision too: up or to even is not known
                var seen = plain && decimalMarkSeen && (!tie || (this.TieSeen && !halfStill));
                if (!digits.EndsWith('0'))
                {
                    return (digits, seen);
                }

                // A last decimal of zero: dropped where the handler says "if required", not seen where it does not.
                var required = this.Stated != null && !this.Fixed;
                return (required ? digits.TrimEnd('0').TrimEnd('.') : digits, seen && required);
            }

            /// <summary>This with one more handler applied after it; false for a handler that is no arithmetic.</summary>
            public bool With(string handler, out Shown changed)
            {
                var (name, decimals, ifRequired) = StatHandlers.Split(handler);
                if (!StatHandlers.Arithmetic.TryGetValue(name, out var step))
                {
                    changed = this;
                    return false;
                }

                var factor = step.Factor == 0 ? 1 : step.Factor;
                changed = new Shown(this.Factor * factor, (this.Offset * factor) + step.Offset, decimals ?? this.Decimals, decimals != null ? !ifRequired : this.Fixed,
                    decimals ?? this.Stated, StatHandlers.SeenDecimals.TryGetValue(handler, out var seen) ? seen : this.SeenDecimals, this.Unsettled || StatHandlers.Unsettled.Contains(name),
                    StatHandlers.SeenExact.TryGetValue(handler, out var exact) ? exact : this.SeenExact, this.TieSeen || StatHandlers.SeenTies.Contains(handler));
                return true;
            }
        }

        /// <summary>What a stat's value must be for the line: a range (either end open), or anything but one value.</summary>
        private readonly record struct Condition(double Least, double Most, double? Not)
        {
            private const string Any = "#";

            /// <summary>A value that meets it: the only one, else the end of the range nearest zero.</summary>
            public double Example =>
                this.Not is { } not ? (not == 0 ? 1 : 0)
                : this.Least <= 0 && this.Most >= 0 ? 0
                : this.Least > 0 ? this.Least : this.Most;

            public bool Holds(double value) => this.Not is { } not ? !Same(value, not) : value >= this.Least - 1e-6 && value <= this.Most + 1e-6;

            /// <summary>"#", "1", "1|#", "#|-1", "2|5", "!0"; null for anything else.</summary>
            public static Condition? Parse(string word)
            {
                if (word.StartsWith('!'))
                {
                    return Number(word[1..]) is { } not ? new Condition(double.NegativeInfinity, double.PositiveInfinity, not) : null;
                }

                var ends = word.Split('|');
                if (ends.Length > 2)
                {
                    return null;
                }

                var least = ends[0] == Any ? double.NegativeInfinity : Number(ends[0]);
                var most = ends.Length == 1 ? (ends[0] == Any ? double.PositiveInfinity : least) : ends[1] == Any ? double.PositiveInfinity : Number(ends[1]);
                return least is { } from && most is { } to ? new Condition(from, to, null) : null;
            }

            private static double? Number(string word) => int.TryParse(word, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : null;
        }
    }
}
