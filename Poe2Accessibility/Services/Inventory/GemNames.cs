namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Skill and support gem names in the game's language by the last part of their BaseItemTypes id ("SkillGemTwister"),
    ///     from catalog/gem-names.json: a build plan names its gems by id, and the ones not owned have no item to read.
    /// </summary>
    public static class GemNames
    {
        private static readonly Regex Prefix = new("^(SkillGem|SupportGem)", RegexOptions.Compiled);
        private static readonly Regex WordStart = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        private static Dictionary<string, Dictionary<string, string>> names = new(StringComparer.Ordinal);

        /// <summary>Reads the catalog; a missing or broken one leaves the gems named by their ids' words.</summary>
        public static void Load(PluginLog log, string file)
        {
            if (!File.Exists(file))
            {
                log.Warn($"Gem names: {file} not found; a build's gems are named by their ids");
                return;
            }

            try
            {
                names = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(file)) ?? new();
                log.Info($"Gem names: {names.Count} languages from {file}");
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Gem names: cannot read {file}: {ex.Message}");
            }
        }

        /// <summary>GGG's mark on the name of a gem that is not in the game ("[DNT-UNUSED] Axe Chop"): no name to say.</summary>
        private const string UnusedMark = "[DNT";

        /// <summary>The place a pattern name leaves for the game to fill in play, with the separator before it ("Spectre: {0}").</summary>
        private static readonly Regex Hole = new(@"\s*[:：]?\s*\{\d+\}", RegexOptions.Compiled);

        /// <summary>A run of spaces, said as one: two of the files' Spanish names carry a double space.</summary>
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>
        ///     The gem's name from the game files' catalog (<see cref="GemIndex"/>) in the language, English when it has none;
        ///     a pattern name without its unfilled place ("Spectre"); a gem the files lack or mark unused by its id's words.
        /// </summary>
        public static string FromFiles(string key, GemIndex index, string language)
        {
            var name = index.ByBaseItem(key)?.Name(language) ?? string.Empty;
            name = name.StartsWith(UnusedMark, StringComparison.Ordinal) ? string.Empty : Spaces.Replace(Hole.Replace(name, string.Empty), " ").Trim();
            return name.Length > 0 ? name : WordStart.Replace(Prefix.Replace(key, string.Empty), " ");
        }

        /// <summary>For tests: the table as the catalog holds it.</summary>
        public static void Set(Dictionary<string, Dictionary<string, string>> table) => names = table;

        /// <summary>
        ///     The gem's name in the game's language (English when that language has none, markup out); a gem newer than the
        ///     catalog is named by its id's words ("SupportGemFrostNexus" → "Frost Nexus").
        /// </summary>
        public static string Of(string key) => Of(key, Localization.Language);

        /// <summary>The same in a language asked for.</summary>
        public static string Of(string key, string language) =>
            Localization.Pick(names, language, key) is { Length: > 0 } name && BuildNotes.Plain(name) is { Length: > 0 } plain
                ? plain
                : WordStart.Replace(Prefix.Replace(key, string.Empty), " ");
    }
}
