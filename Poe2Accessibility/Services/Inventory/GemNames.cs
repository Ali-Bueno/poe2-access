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

        /// <summary>For tests: the table as the catalog holds it.</summary>
        public static void Set(Dictionary<string, Dictionary<string, string>> table) => names = table;

        /// <summary>
        ///     The gem's name in the game's language (English when that language has none, markup out); a gem newer than the
        ///     catalog is named by its id's words ("SupportGemFrostNexus" → "Frost Nexus").
        /// </summary>
        public static string Of(string key) =>
            Localization.Pick(names, Localization.Language, key) is { Length: > 0 } name && BuildNotes.Plain(name) is { Length: > 0 } plain
                ? plain
                : WordStart.Replace(Prefix.Replace(key, string.Empty), " ");
    }
}
