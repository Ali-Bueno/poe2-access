namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>
    ///     Widgets of the panels not reached in play yet (docs/research/ui-unseen-panels.md): inline pictures named from the
    ///     game's markup table, price units drawn as pictures, icon-only controls named by the item they show. The texts are
    ///     the game's own (ClientStrings 0.5.5f) with their placeholders filled.
    /// </summary>
    public static class PanelWidgetTests
    {
        /// <summary>The game's MarkupImages entries these tests use (uisettings.xml, 0.5.5f).</summary>
        private static readonly Dictionary<string, string> Table = InlineIcons.Parse(new[]
        {
            "xbox_button_a\tController/Button4",
            "xbox_button_future\tController/Button2",
            "village_gold\tArt/2DArt/UIImages/InGame/Market/MarketIconGold",
            "market_ratio\tArt/2DArt/UIImages/InGame/Market/MarketIconRatioMarket",
            "spirit_resource_icon\tArt/2DArt/UIImages/InGame/SpiritIcon",
            "TickIcon\tArt/2DArt/UIImages/InGame/StashTickIcon",
            "DiamondIcon\tArt/2DArt/UIImages/Common/IconDiamondSmall",
            "checkbox_on\tArt/2DArt/UIImages/Common/CheckBoxChecked",
            "checkbox_off\tArt/2DArt/UIImages/Common/CheckBoxUnchecked",
            "map_portal_icon\tArt/2DArt/UIImages/InGame/PartyMenu/PartyPortalIcon",
            "weapon_set_2\tArt/2DArt/UIImages/InGame/SkillPanel/InventoryPanelWeaponSwap2Hover",
            "RitualTribute\tArt/2DArt/UIImages/InGame/Ritual/killpointicon",
            "malformed line without a tab",
        });

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(GoldCostIsSaidAfterItsAmount), GoldCostIsSaidAfterItsAmount),
            (nameof(SpiritAfterItsNumberAndNeverTwice), SpiritAfterItsNumberAndNeverTwice),
            (nameof(TickAndCheckBoxesAreTheLinesState), TickAndCheckBoxesAreTheLinesState),
            (nameof(UnnamedPicturesStayDropped), UnnamedPicturesStayDropped),
            (nameof(PortalCountAndWeaponSetAreRead), PortalCountAndWeaponSetAreRead),
            (nameof(GlyphTokensComeFromTheGamesTable), GlyphTokensComeFromTheGamesTable),
            (nameof(OnlyMoneyPicturesArePriceUnits), OnlyMoneyPicturesArePriceUnits),
            (nameof(NamedPicturesAreTold), NamedPicturesAreTold),
            (nameof(IconOnlyControlIsTheItemItShows), IconOnlyControlIsTheItemItShows),
            (nameof(ShippedInlineIconTableNamesTheKnownPictures), ShippedInlineIconTableNamesTheKnownPictures),
            (nameof(InstanceManagerRowSaysWhatEachNumberCounts), InstanceManagerRowSaysWhatEachNumberCounts),
            (nameof(LadderRowUnderATitleKeepsItsColumns), LadderRowUnderATitleKeepsItsColumns),
            (nameof(LabelsThatAreNoHeadingRowAreLeftAlone), LabelsThatAreNoHeadingRowAreLeftAlone),
        };

        private static TableHeadings.Box Box(string text, float x, float width, float y = 0, float height = 20) => new(text, x, y, width, height);

        /// <summary>
        ///     The Instance Manager's columns (ClientStrings InstanceChooser*ColumnHeading / *Subheading), headings left aligned
        ///     over centred numbers.
        /// </summary>
        private static void InstanceManagerRowSaysWhatEachNumberCounts()
        {
            English();
            var headings = new[]
            {
                Box("Players", 100, 120), Box("in this instance", 90, 140, 22, 16),
                Box("Time Left", 300, 110), Box("before instance closes", 280, 150, 22, 16),
                Box("Created", 500, 90), Box("Realm", 680, 70),
            };
            var row = new[] { Box("3", 150, 12, 60), Box("9 minutes", 320, 80, 60), Box("2 minutes ago", 495, 110, 60), Box("Europe", 680, 60, 60) };
            Expect("Players: 3|Time Left: 9 minutes|Created: 2 minutes ago|Realm: Europe", string.Join("|", TableHeadings.Pair(headings, row) ?? new()), "instance row");
        }

        /// <summary>The ladder (LadderColumnRank / Name / Class / Level) under the panel's title, which spans every column.</summary>
        private static void LadderRowUnderATitleKeepsItsColumns()
        {
            English();
            var headings = new[] { Box("Ladder", 0, 800, -40, 30), Box("Rank", 0, 60), Box("Name", 100, 200), Box("Class", 350, 100), Box("Level", 500, 60) };
            var row = new[] { Box("1", 10, 15, 40), Box("Yarkidius", 100, 120, 40), Box("Witch", 350, 70, 40), Box("95", 505, 25, 40) };
            Expect("Rank: 1|Name: Yarkidius|Class: Witch|Level: 95", string.Join("|", TableHeadings.Pair(headings, row) ?? new()), "ladder row");
            var withHeadingWord = new[] { Box("1", 10, 15, 40), Box("Yarkidius", 100, 120, 40), Box("Class", 350, 70, 40), Box("Level 95", 505, 80, 40) };
            Expect("Rank: 1|Name: Yarkidius|Class|Level 95", string.Join("|", TableHeadings.Pair(headings, withHeadingWord) ?? new()), "a cell that says its heading");
            var wide = headings.Concat(new[] { Box("Points", 600, 60), Box("Deaths", 700, 60), Box("Kills", 800, 60) }).ToArray();
            var wideRow = row.Concat(new[] { Box("1200", 605, 40, 40), Box("0", 720, 10, 40), Box("31", 810, 20, 40) }).ToArray();
            Expect("Rank: 1|Name: Yarkidius|Class: Witch|Level: 95|Points: 1200|Deaths: 0|Kills: 31", string.Join("|", TableHeadings.Pair(wide, wideRow) ?? new()), "seven columns");
        }

        private static void LabelsThatAreNoHeadingRowAreLeftAlone()
        {
            English();
            var twoLabels = new[] { Box("Guild", 0, 100), Box("Members", 200, 100) };
            var fiveCells = new[] { Box("a", 0, 10, 40), Box("b", 50, 10, 40), Box("c", 100, 10, 40), Box("d", 150, 10, 40), Box("e", 200, 10, 40) };
            Expect(true, TableHeadings.Pair(twoLabels, fiveCells) == null, "two labels over five cells");
            var stacked = new[] { Box("You are not in a guild", 0, 300), Box("Create Guild", 0, 300, 80) };
            Expect(true, TableHeadings.Pair(stacked, new[] { Box("x", 0, 10, 120), Box("y", 100, 10, 120) }) == null, "lines one under another are no heading row");
            var oneColumn = new[] { Box("Players", 100, 120), Box("Realm", 680, 70) };
            Expect(true, TableHeadings.Pair(oneColumn, new[] { Box("3", 150, 12, 60), Box("4", 160, 12, 60) }) == null, "every cell under one heading");
        }

        private static void Expect(string expected, string actual, string what)
        {
            if (expected != actual)
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        private static void English()
        {
            Localization.Set("en", new());
            InlineIcons.Set(Table);
        }

        /// <summary>CurrencyExchangeGoldCost "&lt;&lt;village_gold&gt;&gt; &lt;goldvalue&gt;{{{0}}}" and AscendancySelectorWindowInsufficientGoldPopup.</summary>
        private static void GoldCostIsSaidAfterItsAmount()
        {
            English();
            Expect("500 gold", ControllerGlyphs.Plain("<<village_gold>> <goldvalue>{500}"), "exchange order cost");
            Expect("1,000 gold", ControllerGlyphs.Plain("<<village_gold>> <red>{1,000}"), "ascendancy cost not afforded");
            Expect("Market Ratio", ControllerGlyphs.Plain("<<market_ratio>> Market Ratio "), "a picture that means nothing stays unsaid");
        }

        /// <summary>GamepadSpiritManagementTabAssignedSpiritValueLabel "{0} &lt;&lt;spirit_resource_icon&gt;&gt;" and the total "{0} Spirit &lt;&lt;…&gt;&gt;".</summary>
        private static void SpiritAfterItsNumberAndNeverTwice()
        {
            English();
            Expect("30 spirit", ControllerGlyphs.Plain("30 <<spirit_resource_icon>>"), "assigned spirit");
            Expect("100 Spirit", ControllerGlyphs.Plain("100 Spirit <<spirit_resource_icon>>"), "the text already says spirit");
            Localization.Set("es", new());
            Expect("100 de espíritu", ControllerGlyphs.Plain("100 de espíritu <<spirit_resource_icon>>"), "Spanish says it too");
            Expect("30 espíritu", ControllerGlyphs.Plain("30 <<spirit_resource_icon>>"), "Spanish unit");
        }

        /// <summary>SplinterInfoComplete "&lt;&lt;TickIcon&gt;&gt; &lt;rgb(214, 186, 139)&gt;{{{0}}}"; check boxes drawn inline.</summary>
        private static void TickAndCheckBoxesAreTheLinesState()
        {
            English();
            Expect("Breach Splinter, completed", ControllerGlyphs.Plain("<<TickIcon>> <rgb(214, 186, 139)>{Breach Splinter}"), "ticked entry");
            Expect("Show Offline, checked\nPC only, unchecked", ControllerGlyphs.Plain("<<checkbox_on>> Show Offline\n<<checkbox_off>> PC only"), "one state per line");
            Expect("checked", ControllerGlyphs.Plain("<<checkbox_on>>"), "a box alone");
        }

        /// <summary>SplinterInfoIncomplete's diamond is a bullet: dropped as the game's stripped copy drops it.</summary>
        private static void UnnamedPicturesStayDropped()
        {
            English();
            Expect("Breach Splinter", ControllerGlyphs.Plain("<<DiamondIcon>> <rgb(127, 127, 127)>{Breach Splinter}"), "bullet");
            Expect(false, InlineIcons.HasNamed("<<DiamondIcon>> <<xbox_button_a>> to use"), "neither a bullet nor a glyph is a named picture");
            Expect(true, InlineIcons.HasNamed("<<village_gold>> 5"), "gold is");
        }

        private static void PortalCountAndWeaponSetAreRead()
        {
            English();
            Expect("6 portals", ControllerGlyphs.Plain("<<map_portal_icon>> 6"), "map portals left");
            Expect("weapon set II: 30 spirit", ControllerGlyphs.Plain("<<weapon_set_2>>: 30 <<spirit_resource_icon>>"), "a set's numeral where it stands");
        }

        /// <summary>A pad token the mod's own list lacks is named from the game's table ("Controller/Button2" = Y).</summary>
        private static void GlyphTokensComeFromTheGamesTable()
        {
            English();
            Expect(ControllerGlyphs.Render("Press <<xbox_button_y>> to confirm"), ControllerGlyphs.Render("Press <<xbox_button_future>> to confirm"), "new glyph token");
            Expect(ControllerGlyphs.FromMarkup("<<xbox_button_y>>"), ControllerGlyphs.FromMarkup("<<xbox_button_future>>"), "legend button");
            Expect("Pay 250 tribute", ControllerGlyphs.Render("Pay <<RitualTribute>> 250"), "a tooltip line rendered with its glyphs keeps the unit");
        }

        private static void OnlyMoneyPicturesArePriceUnits()
        {
            English();
            Expect("tribute", InlineIcons.PriceUnit("Art/2DArt/UIImages/InGame/Ritual/killpointicon|0,0,0,0|0") ?? string.Empty, "tribute icon beside a favour's cost");
            Expect("gold", InlineIcons.PriceUnit("Art/2DArt/UIImages/InGame/Market/MarketIconGold") ?? string.Empty, "market gold");
            Expect(string.Empty, InlineIcons.PriceUnit("Art/2DArt/UIImages/InGame/SpiritIcon") ?? string.Empty, "spirit is no price");
            Expect(string.Empty, InlineIcons.PriceUnit("Art/2DArt/SkillIcons/GamepadActions/DodgeRoll.dds|0,0,0,0|0") ?? string.Empty, "any other picture");
        }

        private static void NamedPicturesAreTold()
        {
            English();
            Expect(true, InlineIcons.MeaningOfToken("TickIcon") is { Kind: InlineIcons.Kind.State }, "tick is a state");
            Expect(true, InlineIcons.MeaningOfToken("village_gold") is { Kind: InlineIcons.Kind.Price }, "gold is a price");
            Expect(false, InlineIcons.MeaningOfToken("market_ratio").HasValue, "ratio icon means nothing to say");
            Expect(false, InlineIcons.MeaningOfToken("unknown_token").HasValue, "a token the table lacks");
            Expect("Button4", InlineIcons.GlyphFile("xbox_button_a") ?? string.Empty, "glyph file");
            Expect(string.Empty, InlineIcons.GlyphFile("village_gold") ?? string.Empty, "not a glyph");
        }

        /// <summary>The Currency Exchange's picker once a currency is chosen shows only its art (currency-art.json keys).</summary>
        private static void IconOnlyControlIsTheItemItShows()
        {
            English();
            CurrencyIcons.Set(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["art/2ditems/currency/currencyaddmodtorare.dds"] = "Exalted Orb" },
            });
            Expect("Exalted Orb", ArtCaptions.Pick(new[]
            {
                "Art/2DArt/UIImages/InGame/Market/ExchangeSlotFrame",
                "Art/2DItems/Currency/CurrencyAddModToRare.dds|0,0,0,0|0",
            }), "picker with a frame and the currency");
            Expect(string.Empty, ArtCaptions.Pick(new[] { "Art/2DArt/UIImages/InGame/Market/ExchangeSlotFrame" }), "an empty picker shows no item");
            CurrencyIcons.Set(new Dictionary<string, Dictionary<string, string>>());
        }

        /// <summary>The shipped table, made from this install's uisettings.xml, still holds the pictures the mod names.</summary>
        private static void ShippedInlineIconTableNamesTheKnownPictures()
        {
            var file = ShippedCatalog("inline-icons.txt");
            if (file == null)
            {
                return; // run away from the repository: nothing to check
            }

            Localization.Set("en", new());
            InlineIcons.Set(InlineIcons.Parse(System.IO.File.ReadAllLines(file)));
            foreach (var token in new[] { "village_gold", "spirit_resource_icon", "TickIcon", "checkbox_on", "map_portal_icon", "party_revives_icon", "RitualTribute", "ritual_suspended" })
            {
                Expect(true, InlineIcons.MeaningOfToken(token).HasValue, $"{token} named");
            }

            Expect("Button4", InlineIcons.GlyphFile("xbox_button_a") ?? string.Empty, "glyphs listed");
            InlineIcons.Set(Table);
        }

        /// <summary>The repository's shipped catalog file, found from the test's folder upwards; null away from the repository.</summary>
        private static string? ShippedCatalog(string name)
        {
            for (var folder = new System.IO.DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = System.IO.Path.Combine(folder.FullName, "Poe2Accessibility", "catalog", name);
                if (System.IO.File.Exists(file))
                {
                    return file;
                }
            }

            return Environment.GetEnvironmentVariable("POE2ACCESS_REPO") is { Length: > 0 } repo &&
                   System.IO.File.Exists(System.IO.Path.Combine(repo, "Poe2Accessibility", "catalog", name))
                ? System.IO.Path.Combine(repo, "Poe2Accessibility", "catalog", name)
                : null;
        }

        private static void Expect(bool expected, bool actual, string what)
        {
            if (expected != actual)
            {
                throw new Exception($"{what}: expected {expected}, got {actual}");
            }
        }
    }
}
