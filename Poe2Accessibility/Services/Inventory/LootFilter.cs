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

        /// <summary>The section of the game's config file that holds both keys.</summary>
        public const string OptionSection = "UI";

        /// <summary>The game's config key that names the item filter chosen.</summary>
        public const string ChosenFilterKey = "item_filter";

        /// <summary>What the game writes under <see cref="ChosenFilterKey"/> with no filter chosen (the owner's file, 2026-11-28).</summary>
        public const string NoFilter = "None";

        /// <summary>The code catalog's names for the verdict's fields: all of them placed by the game's code, or nothing is hidden.</summary>
        public static readonly string[] Fields = { "WorldItem.FilterResult", "WorldItem.FilterReady", "WorldItem.NeverHidden", "WorldItem.ItemNeverHidden" };

        /// <summary>The compatibility report's name for it.</summary>
        public const string Part = "item filter";

        /// <summary>The verdict's "computed" byte when it is: the game sets 1 and tests for it (0x17E2FAF, 0x17E2EC2).</summary>
        private const byte Computed = 1;

        /// <summary>
        ///     Whether the player has the game hide what a filter hides: the option on and a filter chosen. Only the game's own
        ///     word for "none" says none is: an option not read hides nothing, a filter's name not read may still be one.
        /// </summary>
        public static bool Wanted(bool? hideOption, string? chosenFilter) =>
            hideOption == true && !string.Equals(chosenFilter, NoFilter, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     Whether the component's bytes say the filter hides the item: the verdict is computed, its Hide bit is set, and the item
        ///     is not one the filter never hides. The item entity's own keep byte is checked apart (read only when this is true).
        ///     A "computed" byte that is not the game's 1 is another field read in its place: this mod hides loot from a player who
        ///     cannot see it, so anything that does not read as the game writes it hides nothing.
        /// </summary>
        public static bool VerdictHides(byte ready, byte result, byte neverHidden) =>
            ready == Computed && (result & GameLayout.WorldItemComponent.HideBit) != 0 && neverHidden == 0;
    }
}
