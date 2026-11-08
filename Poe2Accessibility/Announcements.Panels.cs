namespace Poe2Accessibility
{
    /// <summary>Phrases of the panels not reached in play yet: inline pictures, art-only controls (docs/research/ui-unseen-panels.md).</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     The word an inline picture means, by its art's file stem (lower case; `InlineIcons`): the game's own icons
        ///     for gold, spirit, tribute, map portals and revives, a tick, a check box, a deferred favour. Null for any other.
        /// </summary>
        public static string? InlineIconWord(string artStem) => artStem switch
        {
            "marketicongold" => Gold,
            "killpointicon" => T("tribute", "tributo"),
            "spiriticon" => T("spirit", "espíritu"),
            "partyportalicon" => T("portals", "portales"),
            "partyrevivesicon" => T("revives", "resurrecciones"),
            "stashtickicon" => T("completed", "completado"),
            "checkboxchecked" => Checked,
            "checkboxunchecked" => Unchecked,
            "savedshopitemicon" => T("deferred", "reservado"),
            _ => null,
        };

        /// <summary>A table cell under its column heading, both in the game's words ("Jugadores: 3"; `TableHeadings`).</summary>
        public static string TableCell(string heading, string value) => T($"{heading}: {value}", $"{heading}: {value}");
    }
}
