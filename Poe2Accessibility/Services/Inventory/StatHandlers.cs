namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The handlers a stat description line names after its text (data/statdescriptions/stat_descriptions.csd, 0.5.x:
    ///     44 names; docs/research/item-mods.md, "Transforms"): each turns a stat's value into the number shown. The
    ///     extractor's `mod-stats` step fails on a name that is not here, so a new one is never guessed. Pure.
    /// </summary>
    internal static class StatHandlers
    {
        /// <summary>The handler that takes no stat number after it.</summary>
        public const string CanonicalLine = "canonical_line";

        private static readonly Regex DecimalsPart = new(@"_(\d)dp(_if_required)?$", RegexOptions.Compiled);

        /// <summary>What each arithmetic handler does, by its name without the decimals part: shown = value × Factor + Offset.</summary>
        public static readonly IReadOnlyDictionary<string, (double Factor, double Offset)> Arithmetic = new Dictionary<string, (double Factor, double Offset)>(StringComparer.Ordinal)
        {
            ["negate"] = (-1, 0), ["negate_and_double"] = (-2, 0), ["double"] = (2, 0), ["times_twenty"] = (20, 0),
            ["divide_by_one_hundred"] = (1 / 100.0, 0), ["divide_by_one_hundred_and_negate"] = (-1 / 100.0, 0),
            ["per_minute_to_per_second"] = (1 / 60.0, 0), ["milliseconds_to_seconds"] = (1 / 1000.0, 0), ["deciseconds_to_seconds"] = (1 / 10.0, 0),
            ["divide_by_ten"] = (1 / 10.0, 0), ["divide_by_two"] = (1 / 2.0, 0), ["divide_by_three"] = (1 / 3.0, 0), ["divide_by_four"] = (1 / 4.0, 0),
            ["divide_by_five"] = (1 / 5.0, 0), ["divide_by_fifteen"] = (1 / 15.0, 0), ["divide_by_fifty"] = (1 / 50.0, 0),
            ["divide_by_twenty_then_double"] = (1 / 10.0, 0), ["add_one"] = (0, 1), ["subtract_one"] = (0, -1), ["plus_two_hundred"] = (0, 200),
        };

        /// <summary>Arithmetic whose name leaves open what is rounded between its two steps: applied as written above, never taken for the tooltip's number.</summary>
        public static readonly IReadOnlySet<string> Unsettled = new HashSet<string>(StringComparer.Ordinal) { "divide_by_twenty_then_double" };

        /// <summary>
        ///     The decimals a handler without a decimals part was seen to show (352 a minute read "5.9", 104 "1.7":
        ///     docs/research/evidence/item-mods-2026-11-10.txt). One not here was never seen with a fraction.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> SeenDecimals = new Dictionary<string, int>(StringComparer.Ordinal) { ["per_minute_to_per_second"] = 1 };

        /// <summary>
        ///     Handlers that state no decimals and were seen to write a number that has this many decimals as it is (140
        ///     hundredths read "+1.4%" on a rare spear's critical hit chance, live check 2026-11-10). A number with more
        ///     decimals is still not known: rounded or cut.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> SeenExact = new Dictionary<string, int>(StringComparer.Ordinal) { ["divide_by_one_hundred"] = 1 };

        /// <summary>Handlers that change no number: which line stands for the stat elsewhere in the game.</summary>
        public static readonly IReadOnlySet<string> Marks = new HashSet<string>(StringComparer.Ordinal) { "canonical_stat", "reminderstring", CanonicalLine };

        /// <summary>Handlers that show something looked up by the value (a passive, a skill, an item class), not a number: a line with one is not written here.</summary>
        public static readonly IReadOnlySet<string> Lookups = new HashSet<string>(StringComparer.Ordinal)
        {
            "affliction_reward_type", "display_indexable_support", "mages_legacy_index", "mod_value_to_item_class", "passive_hash",
            "passive_keystone_index", "specific_skill", "tree_expansion_jewel_passive", "ultimatum_wager_type_hash",
        };

        /// <summary>A handler's name without its decimals part ("divide_by_ten_1dp_if_required" → "divide_by_ten", 1, as required).</summary>
        public static (string Name, int? Decimals, bool IfRequired) Split(string handler)
        {
            var decimals = DecimalsPart.Match(handler);
            return decimals.Success ? (handler[..decimals.Index], decimals.Groups[1].Value[0] - '0', decimals.Groups[2].Success) : (handler, null, false);
        }

        /// <summary>Whether the name is one this table knows what to do with: apply, pass over, or leave the line unwritten.</summary>
        public static bool IsKnown(string handler) =>
            Marks.Contains(handler) || Lookups.Contains(handler) || Arithmetic.ContainsKey(Split(handler).Name);

        /// <summary>The handler names after a line's text, in order.</summary>
        public static IEnumerable<string> NamesIn(string line)
        {
            var words = line[(line.LastIndexOf('"') + 1)..].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (var at = 0; at < words.Length; at += words[at] == CanonicalLine ? 1 : 2)
            {
                yield return words[at];
            }
        }
    }
}
