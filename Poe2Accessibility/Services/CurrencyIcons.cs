namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;

    /// <summary>Names the currency an icon shows: prices and the gold counter are an item image beside a number.</summary>
    public static class CurrencyIcons
    {
        private const char KeyDetailsMark = '|';

        private static Dictionary<string, Dictionary<string, string>> names = new(StringComparer.Ordinal);

        /// <summary>Reads catalog/currency-art.json ({ language: { art file, lower case: item name } }); without it only gold is named.</summary>
        public static void Load(PluginLog log, string file)
        {
            try
            {
                names = File.Exists(file)
                    ? JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(file)) ?? new()
                    : new();
                log.Info($"Currency icons: {names.GetValueOrDefault("en")?.Count ?? 0} named from {file}");
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Currency icons: cannot read {file}: {ex.Message}");
            }
        }

        /// <summary>For tests: the table as the catalog holds it.</summary>
        public static void Set(Dictionary<string, Dictionary<string, string>> table) => names = table;

        /// <summary>Spoken unit for a currency icon's art key; null for other images and currencies the game's tables do not name.</summary>
        public static string? FromTextureKey(string key)
        {
            if (!key.StartsWith(GameLayout.Image.CurrencyArtPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            if (key.Contains(GameLayout.Image.GoldArtMarker, StringComparison.Ordinal))
            {
                return Announcements.Gold;
            }

            var end = key.IndexOf(KeyDetailsMark);
            var art = (end < 0 ? key : key[..end]).ToLowerInvariant();
            return Localization.Pick(names, Localization.Language, art);
        }
    }
}
