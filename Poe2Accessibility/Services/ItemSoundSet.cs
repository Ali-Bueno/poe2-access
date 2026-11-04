namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The loot sounds the player chose (D2R item sound sets): the game's own, or the Diablo IV set in
    ///     <c>assets/diablo4/</c>, which covers items, chests, bodies and shrines; the rest keeps the game's sounds.
    /// </summary>
    public static class ItemSoundSet
    {
        public const string Game = "poe2";
        public const string DiabloIV = "diablo4";

        /// <summary>The Diablo IV set's folder under the plugin's <c>assets/</c>.</summary>
        public const string Folder = "diablo4";

        public const string MagicSample = "magic equipment";
        public const string LegendarySample = "legendary equipment";
        public const string ScrollSample = "scroll";

        private const string GameScrollDrop = "Scroll_Drop";

        /// <summary>The choices in the settings menu's order, the default first.</summary>
        public static readonly string[] Sets = { Game, DiabloIV };

        /// <param name="kind">The entry's scanner kind (<see cref="ScanEntry.Kind"/>).</param>
        /// <param name="baseId">Its base type id, empty when unread.</param>
        /// <param name="tier">Its worth, which tells normal from magic items.</param>
        /// <param name="gameDrop">The game's drop sample for it, by which scrolls are told from other currency; null when it has none.</param>
        /// <returns>The Diablo IV sample of a scanner entry, null when the set has none and the game's own plays.</returns>
        public static string? DiabloIVSample(string kind, string baseId, LootTier tier, string? gameDrop)
        {
            switch (kind)
            {
                case "gold":
                    return "gold";
                case "quest":
                    return "quest item";
                case "chest" or "corpse" or "shrine":
                    return kind;
                case "unique":
                    return LegendarySample;
                case "rare":
                    return "rare equipment";
                case "currency" or "item":
                    break;
                default:
                    return null;
            }

            if (gameDrop?.StartsWith(GameScrollDrop, StringComparison.Ordinal) == true)
            {
                return ScrollSample;
            }

            return ItemSoundGroup.Of(kind, baseId) switch
            {
                "currency" => "rune",
                ItemSoundGroup.Flask => "potion",
                ItemSoundGroup.Gem => "gem",
                _ => tier == LootTier.Magic ? MagicSample : "normal equipment",
            };
        }
    }
}
