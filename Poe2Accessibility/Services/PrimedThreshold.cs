namespace Poe2Accessibility.Services
{
    using GameHelper.RemoteEnums;

    /// <summary>
    ///     Primed for Stun (and Freeze, Electrocution, Pin), as the game defines it (KeywordPopups "PrimedStun", read
    ///     2026-10-26): at least 40% of the buildup for a Normal enemy, 50% Magic, 60% Rare, 70% Unique. Pure.
    /// </summary>
    public static class PrimedThreshold
    {
        public static int Percent(Rarity rarity) => rarity switch
        {
            Rarity.Unique => 70,
            Rarity.Rare => 60,
            Rarity.Magic => 50,
            _ => 40,
        };

        /// <summary>Whether this buildup primes the enemy; never for a reading that cannot be one (no threshold, or far past it).</summary>
        public static bool Reached(Rarity rarity, int buildup, int threshold) =>
            threshold > 0 && buildup >= 0 && buildup <= (long)threshold * 4 && buildup * 100L >= (long)threshold * Percent(rarity);
    }
}
