namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Small rules that sat inside host-bound classes and had no test of their own (audit 2026-11-10, "Tests that do not test").</summary>
    public static class ReachableRuleTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(OnlyATapOnAFocusTheGameMovedIsTheGames), OnlyATapOnAFocusTheGameMovedIsTheGames),
            (nameof(ThePanelBesideIsNamedOnlyWhenItAddsSomething), ThePanelBesideIsNamedOnlyWhenItAddsSomething),
            (nameof(ALongTextSplitsIntoItsLinesWithWords), ALongTextSplitsIntoItsLinesWithWords),
            (nameof(AScrollLandsOnTheParagraphHoldingTheOffset), AScrollLandsOnTheParagraphHoldingTheOffset),
        };

        /// <summary>The press that moved the focus (a stash's search field) is the game's: neither a hold nor a missing focus is.</summary>
        private static void OnlyATapOnAFocusTheGameMovedIsTheGames()
        {
            Equal(true, L3Gestures.GamesPress(PadGesture.Tap, true, 1000, 1040), "a tap whose focus began after the press");
            Equal(true, L3Gestures.GamesPress(PadGesture.DoubleTap, true, 1000, 1000), "a double tap, focus at the press");
            Equal(false, L3Gestures.GamesPress(PadGesture.Tap, true, 1000, 700), "the focus was already there: the tap is the mod's");
            Equal(false, L3Gestures.GamesPress(PadGesture.Tap, false, 1000, 1040), "no focus: gameplay, not a menu");
            Equal(false, L3Gestures.GamesPress(PadGesture.Tap, true, null, 1040), "no press time known");
            Equal(false, L3Gestures.GamesPress(PadGesture.FlickUp, true, 1000, 1040), "a flick is the scanner's");
            Equal(false, L3Gestures.GamesPress(PadGesture.Hold, true, 1000, 1040), "so is a hold");
        }

        /// <summary>The third condition: the panel beside is named before the tab unless it is the focus's own part or the tab already holds it.</summary>
        private static void ThePanelBesideIsNamedOnlyWhenItAddsSomething()
        {
            var title = Announcements.TabTitle("1");
            Equal($"Alijo, {title}", FocusDetails.PageBeside("Inventario", "1", "Alijo"), "a panel that adds a name");
            Equal(title, FocusDetails.PageBeside("Inventario", "1", string.Empty), "no panel beside");
            Equal(title, FocusDetails.PageBeside("Alijo", "1", "Alijo"), "the panel is where the focus is: not said twice");
            Equal(title, FocusDetails.PageBeside("Inventario", "1", $"Alijo, {title}"), "the panel's title already holds the tab");
        }

        private static void ALongTextSplitsIntoItsLinesWithWords()
        {
            Equal("uno|dos|tres", string.Join("|", LongTextParagraphs.Split("uno\n\n  dos \r\n\ntres\n")), "blank lines and padding go");
            Equal(0, LongTextParagraphs.Split(" \n\n ").Length, "a text of blanks has no paragraph");
            Equal(0, LongTextParagraphs.Split(string.Empty).Length, "nor an empty one");
        }

        private static void AScrollLandsOnTheParagraphHoldingTheOffset()
        {
            var starts = new[] { 0, 10, 25, 40 };
            Equal(0, LongTextParagraphs.At(starts, 0), "the top");
            Equal(0, LongTextParagraphs.At(starts, 9), "the last character of the first");
            Equal(1, LongTextParagraphs.At(starts, 10), "the first character of the second");
            Equal(2, LongTextParagraphs.At(starts, 39), "inside the third");
            Equal(3, LongTextParagraphs.At(starts, 5000), "past the end lands on the last");
            Equal(0, LongTextParagraphs.At(Array.Empty<int>(), 7), "no paragraph, index 0");
        }
    }
}
