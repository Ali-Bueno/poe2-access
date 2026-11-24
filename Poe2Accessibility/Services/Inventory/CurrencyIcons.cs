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

        /// <summary>The number the Greater tier's id ends in ("CurrencyUpgradeToMagic2"); Perfect is the next.</summary>
        private const int SecondTier = 2;

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
        ///     The currency a quick-use button offers, by its icon. Every tier of an orb shows the base tier's icon, so a tiered
        ///     orb is the one tier carried, or its base name with the tier unknown (docs/research/strongbox-currency.md §4f).
        /// </summary>
        /// <param name="key">The icon's texture key.</param>
        /// <param name="carries">Whether the backpack holds the currency of this id (<see cref="ItemNames.CurrencyPrefix"/> left out).</param>
        public static string? QuickUseName(string key, Func<string, bool> carries)
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
            var stem = Path.GetFileNameWithoutExtension(end < 0 ? key : key[..end]);
            if (ItemNames.Of(stem) is not { } baseName)
            {
                return null;
            }

            var tiers = new List<string> { stem };
            for (var tier = SecondTier; !char.IsDigit(stem[^1]) && ItemNames.Of(stem + tier) != null; tier++)
            {
                tiers.Add(stem + tier);
            }

            if (tiers.Count == 1)
            {
                return baseName;
            }

            var held = tiers.FindAll(id => carries(id));
            return held.Count == 1 ? ItemNames.Of(held[0]) : Announcements.TierUnknown(baseName);
        }
    }
}
