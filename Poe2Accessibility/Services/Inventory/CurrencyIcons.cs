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

        /// <summary>
        ///     The currency a quick-use button offers, by its icon: the art catalog where it names the file, else the
        ///     item whose id is the file's stem ("CurrencyUpgradeToRare.dds" is <c>CurrencyUpgradeToRare</c>, the Orb
        ///     of Alchemy, measured on a strongbox's button 2026-11-23). The orbs with Greater and Perfect tiers share
        ///     one art file in the game's tables, so the extractor leaves it unnamed and the stem names only the base
        ///     tier: which tier a button showing that shared file would spend is unverified (strongbox-currency.md
        ///     §4c), so a base stem with a numbered tier sibling is not spoken as any orb. A stem that itself carries
        ///     its tier's number names that concrete item, as the measured Alchemy stem names its item.
        /// </summary>
        public static string? QuickUseName(string key)
        {
            if (!key.StartsWith(GameLayout.Image.CurrencyArtPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            if (FromTextureKey(key) is { Length: > 0 } named)
            {
                return named;
            }

            var end = key.IndexOf(KeyDetailsMark);
            var file = end < 0 ? key : key[..end];
            var slash = file.LastIndexOf('/');
            var stem = slash < 0 ? file : file[(slash + 1)..];
            if (stem.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            {
                stem = stem[..^4];
            }

            if (stem.Length == 0 || ItemNames.Of(stem + "2") != null)
            {
                return null; // tiers share this file: naming the base tier would understate what the button spends
            }

            return ItemNames.Of(stem);
        }
    }
}
