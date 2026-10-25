namespace Poe2Accessibility.Services
{
    /// <summary>How much an item on the ground is worth hearing.</summary>
    public enum LootTier
    {
        /// <summary>Normal (white) equipment and flasks.</summary>
        Normal,

        Magic,

        /// <summary>Currency, gold, gems, quest items, rares and uniques; also an item whose worth could not be read.</summary>
        Valuable,
    }

    /// <summary>Which items on the ground sound and are named on approach, as the player chose (D2R audible loot filter).</summary>
    public static class LootFilter
    {
        public const string All = "all";
        public const string Magic = "magic";
        public const string Valuable = "valuable";

        /// <summary>The choices in the settings menu's order, widest first.</summary>
        public static readonly string[] Levels = { All, Magic, Valuable };

        public static bool Allows(string level, LootTier tier) => level switch
        {
            Valuable => tier == LootTier.Valuable,
            Magic => tier != LootTier.Normal,
            _ => true,
        };
    }
}
