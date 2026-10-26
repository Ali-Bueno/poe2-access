namespace Poe2Accessibility.Services
{
    using GameHelper.RemoteEnums;

    /// <summary>
    ///     Culling Strike, as the game defines it (KeywordPopups "CullingStrike", read 2026-10-26): it kills a Normal
    ///     enemy at 35% life or below, Magic at 20%, Rare at 10% and Unique at 5%, checked before the hit's damage. A
    ///     raised threshold (Enhanced Culling) is the player's and not read. Pure.
    /// </summary>
    public static class CullThreshold
    {
        public static int Percent(Rarity rarity) => rarity switch
        {
            Rarity.Unique => 5,
            Rarity.Rare => 10,
            Rarity.Magic => 20,
            _ => 35,
        };

        /// <summary>Whether a culling strike now kills an enemy with this much life left.</summary>
        public static bool Reached(Rarity rarity, int current, int total) =>
            total > 0 && current > 0 && current * 100L <= (long)total * Percent(rarity);
    }
}
