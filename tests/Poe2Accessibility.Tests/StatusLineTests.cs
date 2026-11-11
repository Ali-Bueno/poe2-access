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
        };

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
