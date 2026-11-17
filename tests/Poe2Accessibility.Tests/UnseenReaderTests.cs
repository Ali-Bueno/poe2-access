namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The generic reader's widening for windows nobody has seen (docs/research/ui-unseen-panels.md): art words, unnamed logs, read-screen, price changes.</summary>
    public static class UnseenReaderTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AFileNameGivesItsWordsWithoutDecoration), AFileNameGivesItsWordsWithoutDecoration),
            (nameof(TheStemInFewestSiblingsNamesTheControl), TheStemInFewestSiblingsNamesTheControl),
            (nameof(AnUnnamedFocusIsOneWithNothingToSay), AnUnnamedFocusIsOneWithNothingToSay),
            (nameof(AnUnknownPictureBesideANumberIsItsUnit), AnUnknownPictureBesideANumberIsItsUnit),
            (nameof(ASliderOutOfItsMaximum), ASliderOutOfItsMaximum),
            (nameof(ARequestedScreenKeepsNumbersBesideWords), ARequestedScreenKeepsNumbersBesideWords),
            (nameof(OnlyAPriceThePlayerChangedIsSaid), OnlyAPriceThePlayerChangedIsSaid),
            (nameof(ATickBecomesANumbersChange), ATickBecomesANumbersChange),
        };

        private static List<ScreenText> Read(ScreenText[] texts) => ScreenReadout.Texts(texts, text => text.Button.Length > 0);

        private static ScreenText Text(string text, bool control = false, string button = "") => new((IntPtr)1, text, "t", control, true, button);

        private static void AFileNameGivesItsWordsWithoutDecoration()
        {
            Expect("Bag", ArtWords.Words("Art/2DArt/UIImages/InGame/Shop/BagIcon.dds|0,0,0,0|0"), "icon is decoration");
            Expect("orb of fusing", ArtWords.Words("Art/x/orb_of_fusing_01"), "snake case and a number piece");
            Expect(string.Empty, ArtWords.Words("Art/x/ButtonFrameHover"), "a frame");
            Expect(string.Empty, ArtWords.Words("Art/x/bg"), "fewer than three letters");
            Expect(string.Empty, ArtWords.Words("Art/2DArt/UIImages/Gamepad/DualSense/Button4.dds"), "a controller glyph");
            Expect(string.Empty, ArtWords.Words("Controller/Button4"), "a glyph token's art");
            Expect("bagicon", ArtWords.Stem("Art/x/BagIcon.dds|0,0|0"), "stem");
        }

        private static void TheStemInFewestSiblingsNamesTheControl()
        {
            var skin = "Art/x/SlotSkin.dds";
            var own = new[] { skin, "Art/x/Chaos.dds" };
            var siblings = new IReadOnlyList<string>[] { new[] { skin }, new[] { skin, "Art/x/Divine.dds" } };
            Expect("Chaos", ArtWords.Pick(own, siblings)?.Words ?? string.Empty, "the skin all share is left out");
            Expect("Slot Skin", ArtWords.Pick(new[] { skin }, Array.Empty<IReadOnlyList<string>>())?.Words ?? string.Empty, "alone, nothing is shared");
            Expect(true, ArtWords.Pick(new[] { skin }, siblings) == null, "only the skin");
            Expect("Chaos", ArtWords.Pick(new[] { "Art/x/Divine.dds", "Art/x/Chaos.dds" }, new IReadOnlyList<string>[] { new[] { "Art/x/Divine.dds" }, new[] { "Art/x/Other.dds" } })?.Words ?? string.Empty, "Chaos is in no sibling, Divine in one");
        }

        private static void AnUnnamedFocusIsOneWithNothingToSay()
        {
            Expect(true, UnnamedFocus.Is("", "", 0, false), "a bare button");
            Expect(false, UnnamedFocus.Is("Buy", "", 0, false), "named");
            Expect(false, UnnamedFocus.Is("", "", 0, true), "an empty cell is a cell");
            Expect("role=Button ancestors=[a < b] art=[x] children=3 layer=\"L\"", UnnamedFocus.Details("Button", new[] { "a", "b" }, new[] { "x" }, 3, "L"), "the line");
        }

        private static void AnUnknownPictureBesideANumberIsItsUnit()
        {
            Localization.Set("en", new());
            InlineIcons.Set(InlineIcons.Parse(new[]
            {
                "bag_icon\tArt/2DArt/UIImages/InGame/Shop/BagIcon",
                "frame_bit\tArt/2DArt/UIImages/Common/PanelFrameCorner",
                "xbox_button_a\tController/Button4",
            }));
            Expect("3/5 bag", ControllerGlyphs.Plain("<<bag_icon>>3/5"), "unit after a number it precedes");
            Expect("3 bag", ControllerGlyphs.Plain("3 <<bag_icon>>"), "after the number");
            Expect("Open", ControllerGlyphs.Plain("<<bag_icon>> Open"), "beside a word it stays unsaid");
            Expect("5", ControllerGlyphs.Plain("<<frame_bit>>5"), "decoration says nothing");
            Expect(true, InlineIcons.HasNamed("<<bag_icon>>3/5"), "the text is read whole");
            Expect(false, InlineIcons.HasNamed("<<bag_icon>> Open"), "and not here");
        }

        private static void ASliderOutOfItsMaximum()
        {
            Localization.Set("en", new());
            Expect("30 percent", Announcements.SliderValue(30, 0, 100), "the 0-100 range");
            Expect("3 of 10", Announcements.SliderValue(3, 0, 10), "another range");
            Expect("4", Announcements.SliderValue(4, 0, 0), "no maximum known");
        }

        private static void ARequestedScreenKeepsNumbersBesideWords()
        {
            var read = Read(new[] { Text("Level"), Text("12"), Text("7"), Text("Dex"), Text("DEX"), Text("11"), Text("25%"), Text("RT", false, "RT") });
            Expect("Level: 12|Dex: 7|DEX: 11", string.Join("|", read.Select(text => text.Text)), "a number beside a label");
            Expect("Cost: 1,000", Read(new[] { Text("Cost:"), Text("1,000") })[0].Text, "a colon is not doubled");
            Expect(0, Read(new[] { Text("5"), Text("-") }).Count, "numbers alone are still dropped");
        }

        private static void OnlyAPriceThePlayerChangedIsSaid()
        {
            Expect("Reroll: 300", NumberChanges.Said("Reroll: 250", "Reroll: 300", 400, 5000) ?? string.Empty, "price after a press");
            Expect("300 gold", NumberChanges.Said("250 gold", "300 gold", 400, 5000) ?? string.Empty, "a unit price");
            Expect(true, NumberChanges.Said("Reroll: 250", "Reroll: 300", 60000, 5000) == null, "no recent input");
            Expect(true, NumberChanges.Said("Time 0:09", "Time 0:08", 400, 5000) == null, "a clock");
            Expect(true, NumberChanges.Said("Left 9s", "Left 8s", 400, 5000) == null, "seconds counted");
            Expect(true, NumberChanges.Said("Progress 40%", "Progress 41%", 400, 5000) == null, "a percentage");
            Expect(true, NumberChanges.Said("Reroll: 250", "Reroll: 300", 400, 800) == null, "too soon after the last line");
            Expect(true, NumberChanges.Said("250", "300", 400, 5000) == null, "a bare number");
            Expect(true, NumberChanges.Said("Reroll: 250", "Reroll again", 400, 5000) == null, "more than digits changed");
        }

        private static void ATickBecomesANumbersChange()
        {
            var differ = new ScreenDiffer();
            ScreenSnapshot Frame(string price) => new(IntPtr.Zero, new[] { new ScreenLayer((IntPtr)7, 1, new[] { new ScreenText((IntPtr)70, price, "price", false, true) }, true, false) });
            differ.Apply(Frame("Reroll 250"));
            differ.Apply(Frame("Reroll 250"));
            differ.Apply(Frame("Reroll 300"));
            var changes = differ.Apply(Frame("Reroll 300")).ToList();
            Expect(0, changes.Count, "Apply reports no tick as news");
            changes = changes.Concat(differ.Ticks).ToList();
            var change = changes.FirstOrDefault(each => each.Kind == ScreenChangeKind.NumbersChanged);
            Expect("Reroll 250", change?.Before[0] ?? string.Empty, "the text as it was");
            Expect("Reroll 300", change?.Texts[0].Text ?? string.Empty, "the text as it is");
            Expect(0, changes.Count(each => each.Kind == ScreenChangeKind.TextsAppeared), "still no news as text");
        }
    }
}
