namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>
    ///     Currency item names in the game's language, by the id inline item art carries ("&lt;&lt;itemvisual:
    ///     CurrencyIdentification&gt;&gt;" is the Scroll of Wisdom), from catalog/item-names.json (tools/Poe2UiExtract
    ///     item-names, regenerated after patches).
    /// </summary>
    public static class ItemNames
    {
        private static Dictionary<string, Dictionary<string, string>> names = new(StringComparer.Ordinal);

        /// <summary>Reads the catalog; a missing or broken one leaves the art unnamed.</summary>
        public static void Load(PluginLog log, string file)
        {
            if (!File.Exists(file))
            {
                log.Warn($"Item names: {file} not found; inline item art stays unnamed");
                return;
            }

            try
            {
                names = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(file)) ?? new();
                log.Info($"Item names: {names.Count} languages from {file}");
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Item names: cannot read {file}: {ex.Message}");
            }
        }

        /// <summary>For tests: the table as the catalog holds it.</summary>
        public static void Set(Dictionary<string, Dictionary<string, string>> table) => names = table;

        /// <summary>The item's name in the game's language (English when that language has none), or null.</summary>
        public static string? Of(string artId) =>
            (names.TryGetValue(Localization.Language, out var local) && local.TryGetValue(artId, out var name)) ||
            (names.TryGetValue("en", out var english) && english.TryGetValue(artId, out name))
                ? name
                : null;
    }
}
