namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The MinimapIcons Ids the scanner's rules match by their text (<see cref="MinimapMarks"/>); the data step "game
    ///     names" checks each is still an Id of the table (<see cref="GameNames"/>). Pure.
    /// </summary>
    public static class MinimapIconIds
    {
        public const string StoryGlyph = "StoryGlyph";

        public const string Ritual = "RitualRune";

        public const string RitualDone = "RitualRuneFinished";

        public const string Essence = "Essence";

        /// <summary>
        ///     Icons the game draws on what the quest wants used or picked up (rows 5, 6, 15), the Death Crystal's, and those
        ///     of what moves a Trial on: the Relic Altar, its map and the floor doors, then each reward's own
        ///     (docs/research/minimap.md, "The Trial's rewards").
        /// </summary>
        public static readonly string[] Quest =
        {
            "QuestObject", "QuestItem", "MissionTarget", "SanctumFountain", "MarakethSanctumRoomSelector",
            "MarakethSanctumFountain", "MarakethSanctumKey", "MarakethSanctumKeyConverterActive", "SanctumPact",
        };

        public static IEnumerable<string> All => Quest.Concat(new[] { StoryGlyph, Ritual, RitualDone, Essence });
    }
}
