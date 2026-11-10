namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Whose name a skill is said by: the game's while it is text, the game files' when its read moved (<see cref="SkillNameCheck"/>).</summary>
    public static class SkillNameTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheGamesNameIsSaidWhileItIsText), TheGamesNameIsSaidWhileItIsText),
            (nameof(ARenamedSkillOrTwoAreTheGamesOwnNames), ARenamedSkillOrTwoAreTheGamesOwnNames),
            (nameof(MostSkillsDifferingTwiceIsAMovedOffset), MostSkillsDifferingTwiceIsAMovedOffset),
        };

        private static void TheGamesNameIsSaidWhileItIsText()
        {
            Equal("Saeta básica", SkillNameCheck.Choose("Saeta básica", "Disparo básico", SkillNameSource.Unjudged), "the game's, before anything is judged");
            Equal("Saeta básica", SkillNameCheck.Choose("Saeta básica", "Disparo básico", SkillNameSource.Game), "the game's own display name wins over the row's");
            Equal("Saeta básica", SkillNameCheck.Choose("Saeta básica", null, SkillNameSource.Files), "a skill the files do not know keeps the game's even then");
            Equal("Disparo básico", SkillNameCheck.Choose(string.Empty, "Disparo básico", SkillNameSource.Game), "nothing read: the files stand in");
            Equal("Disparo básico", SkillNameCheck.Choose("\u0001\u0002", "Disparo básico", SkillNameSource.Game), "bytes that are no text: the files stand in");
            Equal("Disparo básico", SkillNameCheck.Choose("...", "Disparo básico", SkillNameSource.Unjudged), "no letter or digit is no name");
            Equal("Disparo básico", SkillNameCheck.Choose("Art/2DArt/x.dds", "Disparo básico", SkillNameSource.Files), "a moved offset reads another column's text: the files' name");
            Equal(string.Empty, SkillNameCheck.Choose(string.Empty, null, SkillNameSource.Game), "neither source: no name, as before");
            Equal(true, SkillNameCheck.IsText("アークメイジ"), "letters of any script");
            Equal(true, SkillNameCheck.IsText("Orden: {0}"), "a name the game fills in is still its name");
            Equal(false, SkillNameCheck.IsText("  "), "spaces");
            Equal(false, SkillNameCheck.IsText("Nova\u0000de"), "a control character inside");
        }

        private static void ARenamedSkillOrTwoAreTheGamesOwnNames()
        {
            var check = new SkillNameCheck();
            Equal(false, check.Observe(2, 2), "two skills judge nothing");
            Equal(SkillNameSource.Unjudged, check.Source, "so nothing is decided");
            Equal(true, check.Observe(8, 1), "eight skills, one shown under another name");
            Equal(SkillNameSource.Game, check.Source, "the game's names are said");
            Equal(false, check.Observe(8, 2), "two of eight: still the game's");
            Equal(false, check.Observe(8, 4), "half is not most");
            Equal(SkillNameSource.Game, check.Source, "unchanged");
        }

        private static void MostSkillsDifferingTwiceIsAMovedOffset()
        {
            var check = new SkillNameCheck();
            Equal(false, check.Observe(8, 7), "one reading that disagrees on most says nothing yet (an area still loading)");
            Equal(SkillNameSource.Unjudged, check.Source, "the game's names until the second");
            Equal(true, check.Observe(8, 7), "the second in a row");
            Equal(SkillNameSource.Files, check.Source, "the files' names are said");
            Equal(false, check.Observe(8, 8), "and stay");
            Equal(false, check.Observe(1, 1), "a reading too small to judge changes nothing");
            Equal(true, check.Observe(8, 0), "the game's read agrees again");
            Equal(SkillNameSource.Game, check.Source, "its names come back at once");
            Equal(false, check.Observe(8, 7), "one bad reading after good ones");
            Equal(SkillNameSource.Game, check.Source, "does not take them away");
            Equal(false, check.Observe(8, 0), "a good one in between");
            Equal(false, check.Observe(8, 7), "starts the count over");
            Equal(SkillNameSource.Game, check.Source, "so two apart are not two in a row");
            check.Observe(8, 7);
            Equal(SkillNameSource.Files, check.Source, "two in a row are");
            check.Reset();
            Equal(SkillNameSource.Unjudged, check.Source, "a new game process judges again");
        }
    }
}
