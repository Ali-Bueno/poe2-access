namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The game's "before > after" spoken with a word, and only where it is a change.</summary>
    public static class ChangeSignTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AChangeIsSpokenWithAWord), AChangeIsSpokenWithAWord),
            (nameof(MarkupPathsAndBareSignsStay), MarkupPathsAndBareSignsStay),
        };

        private static void AChangeIsSpokenWithAWord()
        {
            try
            {
                Localization.Detect("es");
                Equal("Nivel 4 a nivel 5", ChangeSign.Spoken("Nivel 4 > nivel 5"), "level up");
                Equal("10 a 12 Fue", ChangeSign.Spoken("10 > 12 Fue"), "numbers");
                Equal("14 a 16 de maná, 3 a 4 Int", ChangeSign.Spoken("14 > 16 de maná, 3 > 4 Int"), "two in a line");
                Localization.Detect("en");
                Equal("Level 4 to level 5", ChangeSign.Spoken("Level 4 > level 5"), "English");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void MarkupPathsAndBareSignsStay()
        {
            Localization.Detect("en");
            Equal("<a href=x > y>", ChangeSign.Spoken("<a href=x > y>"), "inside a tag");
            Equal("Metadata/Foo > Metadata/Bar", ChangeSign.Spoken("Metadata/Foo > Metadata/Bar"), "paths");
            Equal("Life >5", ChangeSign.Spoken("Life >5"), "no spaces");
            Equal("a > ", ChangeSign.Spoken("a > "), "nothing after");
            Equal("> 5", ChangeSign.Spoken("> 5"), "nothing before");
            Equal("No change", ChangeSign.Spoken("No change"), "plain text");
        }
    }
}
