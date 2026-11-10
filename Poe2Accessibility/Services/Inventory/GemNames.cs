namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Skill and support gem names in the game's language by the last part of their BaseItemTypes id ("SkillGemTwister"),
    ///     from the game files' gem catalog (<see cref="GemIndex"/>, made again at every start): a build plan names its gems
    ///     by id, and the ones not owned have no item to read.
    /// </summary>
    public static class GemNames
    {
        private static readonly Regex Prefix = new("^(SkillGem|SupportGem)", RegexOptions.Compiled);
        private static readonly Regex WordStart = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        /// <summary>GGG's mark on the name of a gem that is not in the game ("[DNT-UNUSED] Axe Chop"): no name to say.</summary>
        private const string UnusedMark = "[DNT";

        /// <summary>The place a pattern name leaves for the game to fill in play, with the separator before it ("Spectre: {0}").</summary>
        private static readonly Regex Hole = new(@"\s*[:：]?\s*\{\d+\}", RegexOptions.Compiled);

        /// <summary>A run of spaces, said as one: two of the files' Spanish names carry a double space.</summary>
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>The gem's name in the game's language from the catalog the plugin loaded.</summary>
        public static string Of(string key) => Of(key, GemIndex.Shared, Localization.Language);

        /// <summary>
        ///     The gem's name in the language, English when it has none; a pattern name without its unfilled place
        ///     ("Spectre"); a gem the files lack (newer than the catalog) or mark unused by its id's words
        ///     ("SupportGemFrostNexus" → "Frost Nexus").
        /// </summary>
        public static string Of(string key, GemIndex index, string language) =>
            Said(index.ByBaseItem(key)?.Name(language) ?? string.Empty) is { Length: > 0 } name ? name : WordStart.Replace(Prefix.Replace(key, string.Empty), " ");

        /// <summary>
        ///     What a row of the Skills panel can show for the gem: the names of the skills it grants, which are the game's
        ///     own column and differ from the gem's for a fifth of the skill gems ("Armour Piercing Rounds" grants "Load
        ///     Armour Piercing Rounds"), and the gem's name.
        /// </summary>
        public static IReadOnlyList<string> RowNames(string key) => RowNames(key, GemIndex.Shared, Localization.Language);

        public static IReadOnlyList<string> RowNames(string key, GemIndex index, string language)
        {
            var names = new List<string> { Of(key, index, language) };
            names.AddRange((index.ByBaseItem(key)?.SkillNames(language) ?? Array.Empty<string>()).Select(Said).Where(name => name.Length > 0));
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string Said(string name) =>
            name.StartsWith(UnusedMark, StringComparison.Ordinal) ? string.Empty : Spaces.Replace(Hole.Replace(name, string.Empty), " ").Trim();
    }
}
