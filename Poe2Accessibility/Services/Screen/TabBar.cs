namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     The game's tab control (Options, UI dump 2026-11-10): its first child "buttons_container" holds the tabs, and two
    ///     later children each hold a shoulder-button icon drawn at an end of the bar, the previous tab's first. Pure.
    /// </summary>
    public static class TabBar
    {
        /// <summary>The element holding a tab control's tabs.</summary>
        public const string ButtonsName = "buttons_container";

        /// <summary>What the icon at an end of the bar does, by its order among the two.</summary>
        public static string EndLegend(int order) => order == 0 ? Announcements.PreviousTab : Announcements.NextTab;
    }
}
