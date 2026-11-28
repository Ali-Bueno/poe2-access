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

    /// <summary>
    ///     The game's own item filter (the one chosen in Options > Game > Item Filter), as the game applies it to a ground item: the
    ///     verdict its WorldItem component keeps (<see cref="GameLayout.WorldItemComponent"/>). A verdict not computed yet, or not read,
    ///     hides nothing, so the item stays listed and audible.
    /// </summary>
    public static class GameItemFilter
    {
        /// <summary>The game's config key that makes the filter's Hide blocks hide items on the ground (the game reads it under [UI]).</summary>
        public const string HideOptionKey = "hide_all_filtered_ground_items";

        /// <summary>
        ///     Whether the component's bytes say the filter hides the item: the verdict is computed, its Hide bit is set, and the item
        ///     is not one the filter never hides. The item entity's own keep byte is checked apart (read only when this is true).
        /// </summary>
        public static bool VerdictHides(byte ready, byte result, byte neverHidden) =>
            ready != 0 && (result & GameLayout.WorldItemComponent.HideBit) != 0 && neverHidden == 0;
    }
}
