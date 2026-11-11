namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     The game's tab control (Options, UI dump 2026-11-10): its first child "buttons_container" holds the tabs, and two
    ///     later children each hold a shoulder-button icon drawn at an end of the bar, the previous tab's first. Pure.
    /// </summary>
    public static class TabBar
    {
        /// <summary>The element holding a tab control's tabs, its first child, and the one holding its pages, the second.</summary>
        public const string ButtonsName = "buttons_container";
        public const string PagesName = "pages_container";

        /// <summary>Whether a child by this name and place is a tab control's bar, or its pages after the bar was met.</summary>
        public static bool IsPart(string name, int childIndex, bool barMet) =>
            (name == ButtonsName && childIndex == 0 && !barMet) || (name == PagesName && childIndex == 1 && barMet);

        /// <summary>A tab control is an element both parts were met under: a row of buttons alone (a dialog's) is not one.</summary>
        public static bool IsControl(int partsMet) => partsMet == 2;

        /// <summary>What the icon at an end of the bar does, by its order among the two.</summary>
        public static string EndLegend(int order) => order == 0 ? Announcements.PreviousTab : Announcements.NextTab;
    }
}
