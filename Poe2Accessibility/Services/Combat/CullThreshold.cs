namespace Poe2Accessibility.Services
{
    using System;
    using GameReader.RemoteEnums;

    /// <summary>
    ///     Culling Strike, as the game defines it (KeywordPopups "CullingStrike", read 2026-10-26): it kills a Normal
    ///     enemy at 35% life or below, Magic at 20%, Rare at 10% and Unique at 5%, checked before the hit's damage. The
    ///     player's increases raise it: stats "culling_strike_threshold_+%" and, against rare and unique monsters,
    ///     "culling_strike_threshold_+%_vs_rare_or_unique_monsters" (35% increased by 20% is 42%). Pure.
    /// </summary>
    public static class CullThreshold
    {
        /// <summary>When the culling cue plays (setting CullCue): while a skill of the character's culls, always, never.</summary>
        public const string WithSkill = "with_skill";
        public const string Always = "always";
        public const string Never = "never";

        public static readonly string[] Modes = { WithSkill, Always, Never };

        public static int Percent(Rarity rarity) => rarity switch
        {
            Rarity.Unique => 5,
            Rarity.Rare => 10,
            Rarity.Magic => 20,
            _ => 35,
        };

        /// <summary>The threshold in percent of life with the player's increases, never below none.</summary>
        public static double Percent(Rarity rarity, int increase, int increaseVsRareOrUnique) =>
            Percent(rarity) * Math.Max(0, 100 + increase + (rarity is Rarity.Rare or Rarity.Unique ? increaseVsRareOrUnique : 0)) / 100.0;

        /// <summary>Whether a culling strike now kills an enemy with this much life left.</summary>
        public static bool Reached(Rarity rarity, int current, int total, int increase = 0, int increaseVsRareOrUnique = 0) =>
            total > 0 && current > 0 && current * 100.0 <= total * Percent(rarity, increase, increaseVsRareOrUnique);
    }
}
