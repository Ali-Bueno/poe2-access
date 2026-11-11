namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using GameReader.RemoteEnums;
    using static Check;

    /// <summary>The status line names the area only in a game: at the login screen it was the last character's (heard 2026-11-10).</summary>
    public static class StatusLineTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheAreaIsSaidOnlyInAGame), TheAreaIsSaidOnlyInAGame),
            (nameof(TheMenuOnAPlayerSaysItIsAPlayer), TheMenuOnAPlayerSaysItIsAPlayer),
        };

        /// <summary>"Cazadora Niv. 6, Liga Forbidden Rites, LaGuanche. Invitar al grupo" had no word that it was a player (2026-11-10).</summary>
        private static void TheMenuOnAPlayerSaysItIsAPlayer()
        {
            var header = new[] { "Cazadora Niv. 6", "Liga Forbidden Rites", "LaGuanche" };
            Localization.Detect("es");
            try
            {
                Equal("Jugador: Cazadora Niv. 6, Liga Forbidden Rites, LaGuanche", Announcements.ButtonMenuContext(header, name => name == "LaGuanche"), "a player of that name is in the area");
                Equal("Cazadora Niv. 6, Liga Forbidden Rites, LaGuanche", Announcements.ButtonMenuContext(header, _ => false), "no such player: the menu's own texts");
                Equal("Cazadora Niv. 6, Liga Forbidden Rites, LaGuanche", Announcements.ButtonMenuContext(header, null), "nothing to ask");
                Equal(string.Empty, Announcements.ButtonMenuContext(Array.Empty<string>(), _ => true), "a menu with no heading");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void TheAreaIsSaidOnlyInAGame()
        {
            const string Area = "The Ardura Caravan";
            Equal(true, Announcements.Status(true, GameStateTypes.InGameState, Area, "NVDA", string.Empty).Contains(Area), "in the game");
            Equal(true, Announcements.Status(true, GameStateTypes.EscapeState, Area, "NVDA", string.Empty).Contains(Area), "with the game menu open");
            Equal(false, Announcements.Status(true, GameStateTypes.LoginState, Area, "NVDA", string.Empty).Contains(Area), "at the login screen the name is the last character's");
            Equal(false, Announcements.Status(true, GameStateTypes.SelectCharacterState, Area, "NVDA", string.Empty).Contains(Area), "at character selection too");
            Equal(true, Announcements.Status(true, GameStateTypes.LoginState, Area, "NVDA", string.Empty).Contains("NVDA"), "the rest of the line stands");
        }
    }
}
