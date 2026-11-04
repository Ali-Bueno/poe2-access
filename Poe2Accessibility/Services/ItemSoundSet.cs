namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The loot sounds the player chose (D2R item sound sets): the game's own, or the Diablo IV set in
    ///     <c>assets/diablo4/</c>, which covers items, chests, bodies and shrines; the rest keeps the game's sounds. That
    ///     set is made of loops, as in D2R: each thing heard keeps one voice going, never a cue that repeats.
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

        /// <summary>
        ///     A pile's loops together are this many times one near item at most (D2R's 0.5 over a near gain of 0.4). In power:
        ///     they start at unrelated places, so they add that way, and D2R's plain sum left four items quieter than one.
        /// </summary>
        private const float PileLimit = 1.25f;

        /// <summary>The choices in the settings menu's order, the default first.</summary>
        public static readonly string[] Sets = { Game, DiabloIV };

        /// <param name="power">The gains of the loops heard together, each squared, summed.</param>
        /// <param name="near">One item's gain when the player stands by it.</param>
        /// <returns>What each loop's gain is scaled by, so a pile does not drown everything else.</returns>
        public static float PileScale(float power, float near)
        {
            var most = PileLimit * near;
            return power > most * most ? most / MathF.Sqrt(power) : 1f;
        }

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
