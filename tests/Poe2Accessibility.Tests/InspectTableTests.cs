namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     Inspect Mode's comparison table: an item's tooltip lines and the worn item's lined up by stat, walked one line of
    ///     one item at a time (docs/research/ui-layers.md, "Item Inspect Mode").
    /// </summary>
    public static class InspectTableTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(PropertyLinesMatchByLabel), PropertyLinesMatchByLabel),
            (nameof(ModifierLinesMatchWithoutTheirNumbers), ModifierLinesMatchWithoutTheirNumbers),
            (nameof(AbsentCellsGetTheRowsWords), AbsentCellsGetTheRowsWords),
            (nameof(TheRobesAlignByStat), TheRobesAlignByStat),
            (nameof(ARepeatedLinePairsInOrder), ARepeatedLinePairsInOrder),
            (nameof(ARingAgainstBothRings), ARingAgainstBothRings),
            (nameof(FlicksWalkTheRobesOneLineAtATime), FlicksWalkTheRobesOneLineAtATime),
            (nameof(EdgesSayTheCellAgain), EdgesSayTheCellAgain),
            (nameof(TheFirstFlickSidewaysEntersTheFirstRow), TheFirstFlickSidewaysEntersTheFirstRow),
            (nameof(OneTooltipIsOneColumn), OneTooltipIsOneColumn),
            (nameof(ARebuildSaysOnlyAChangedCell), ARebuildSaysOnlyAChangedCell),
            (nameof(AnItemWithoutSocketsSaysSo), AnItemWithoutSocketsSaysSo),
            (nameof(ARebuildWithoutTheRowMovesToTheNearest), ARebuildWithoutTheRowMovesToTheNearest),
            (nameof(TheOpeningWaitsForTheTooltipsToSettle), TheOpeningWaitsForTheTooltipsToSettle),
            (nameof(ClosingInspectModeEndsTheTable), ClosingInspectModeEndsTheTable),
            (nameof(WithTheTableOffOnlyTheCrossingsAreSaid), WithTheTableOffOnlyTheCrossingsAreSaid),
            (nameof(TwoWornRingsAreToldApartBySlot), TwoWornRingsAreToldApartBySlot),
            (nameof(ASingleTooltipOpensWithItsName), ASingleTooltipOpensWithItsName),
            (nameof(ALineFillingInIsNotSaid), ALineFillingInIsNotSaid),
            (nameof(TheRobesReadAsTheirTooltips), TheRobesReadAsTheirTooltips),
            (nameof(AdvancedInfoOffReadsThePlainLines), AdvancedInfoOffReadsThePlainLines),
            (nameof(ARaresTitleHoldsItsBase), ARaresTitleHoldsItsBase),
            (nameof(AUniquesGroupsAndStoryAreRows), AUniquesGroupsAndStoryAreRows),
            (nameof(AGrantedSkillAndUnidentifiedAreRows), AGrantedSkillAndUnidentifiedAreRows),
            (nameof(AWornWeaponsEquippedLineIsNoRow), AWornWeaponsEquippedLineIsNoRow),
            (nameof(AnUnmetRequirementIsSaidOnItsRow), AnUnmetRequirementIsSaidOnItsRow),
            (nameof(ANodeWithoutLayoutIsNoTooltip), ANodeWithoutLayoutIsNoTooltip),
            (nameof(WornTooltipsTakeSlotsByTitleInOrder), WornTooltipsTakeSlotsByTitleInOrder),
        };

        /// <summary>The magic robe in the bag the user compared on 2026-11-24, advanced info on (Fixtures/inspect-layers.txt, "robes").</summary>
        internal static InspectItem Robe => new("Hale Tattered Robe of the Pupil", new[]
        {
            new InspectLine("Hale Tattered Robe of the Pupil, magic", "Hale Tattered Robe of the Pupil"),
            new InspectLine("Body Armour: Item Level 7", "Body Armour: Item Level 7"),
            new InspectLine("Energy Shield: 28", "Energy Shield: 28"),
            new InspectLine("+19(10-19) to maximum Life, T13, Prefix", "+19(10-19) to maximum Life"),
            new InspectLine("+6(5-8) to Intelligence, T8, Suffix", "+6(5-8) to Intelligence"),
        }, false, null);

        /// <summary>The magic robe the user wore against it.</summary>
        internal static InspectItem WornRobe => new("Protective Feathered Robe of the Cloud", new[]
        {
            new InspectLine("Protective Feathered Robe of the Cloud, magic", "Protective Feathered Robe of the Cloud"),
            new InspectLine("Body Armour: Item Level 8", "Body Armour: Item Level 8"),
            new InspectLine("Energy Shield: 43", "Energy Shield: 43"),
            new InspectLine("Requires: Level 5, 11 Int", "Requires: Level 5, 11 Int"),
            new InspectLine("23(15-26)% increased Energy Shield, T8, Prefix", "23(15-26)% increased Energy Shield"),
            new InspectLine("+9(6-10)% to Lightning Resistance, T8, Suffix", "+9(6-10)% to Lightning Resistance"),
        }, true, null);

        /// <summary>Lines said as they are matched.</summary>
        internal static IReadOnlyList<InspectLine> Lines(params string[] texts) => texts.Select(text => new InspectLine(text, text)).ToList();

        /// <summary>A row's cells: I for the inspected item's, W for a worn item's, - where the item lacks the row.</summary>
        private static string Cells(InspectRow row) =>
            string.Concat(row.Cells.Select((cell, index) => cell == null ? '-' : index == 0 ? 'I' : 'W'));

        private static void PropertyLinesMatchByLabel()
        {
            Expect("Energy Shield:", InspectRows.Key("Energy Shield: 43"), "a property line");
            Expect(InspectRows.Key("Energy Shield: 43"), InspectRows.Key("Energy Shield: 28"), "same label, other value");
            Expect("Requires:", InspectRows.Key("Requires：Level 5"), "a full-width colon is a colon");
            Equal(true, InspectRows.IsLabel("Requires:"), "a label key");
        }

        private static void ModifierLinesMatchWithoutTheirNumbers()
        {
            Expect("+# to maximum Life", InspectRows.Key("+19(10-19) to maximum Life"), "range and value");
            Expect("#% increased Energy Shield", InspectRows.Key("23(15-26)% increased Energy Shield"), "percent");
            Expect(InspectRows.Key("Adds 1 to 10 Lightning Damage"), InspectRows.Key("Adds 3 to 7 Lightning Damage"), "two numbers");
            Equal(false, InspectRows.IsLabel("+# to maximum Life"), "not a label key");
        }

        private static void AbsentCellsGetTheRowsWords()
        {
            Expect("to maximum Life", InspectRows.Words("+19(10-19) to maximum Life"), "a modifier");
            Expect("increased Energy Shield", InspectRows.Words("23(15-26)% increased Energy Shield"), "a percent");
            Expect("Requires", InspectRows.Words("Requires: Level 5, 11 Int"), "a property keeps its label");
        }

        private static void TheRobesAlignByStat()
        {
            var rows = InspectRows.Align(new[] { Robe, WornRobe });
            Expect("title|Body Armour:|Energy Shield:|Requires:|+# to maximum Life|+# to Intelligence|#% increased Energy Shield|+#% to Lightning Resistance",
                string.Join("|", rows.Select(row => row.Key)), "row order");
            Expect("-W", Cells(rows[3]), "only the worn robe has requirements");
            Expect("I-", Cells(rows[4]), "only this robe has life");
            Expect("IW", Cells(rows[2]), "both have energy shield");
        }

        private static void ARepeatedLinePairsInOrder()
        {
            var a = new InspectItem("A", Lines("A", "+10% to Fire Resistance", "+20% to Fire Resistance"), false, null);
            var b = new InspectItem("B", Lines("B", "+15% to Fire Resistance"), true, null);
            var rows = InspectRows.Align(new[] { a, b });
            Expect("title:1|+#% to Fire Resistance:1|+#% to Fire Resistance:2", string.Join("|", rows.Select(row => $"{row.Key}:{row.Occurrence}")), "occurrences");
            Expect("IW", Cells(rows[1]), "first with first");
            Expect("I-", Cells(rows[2]), "the second has no partner");
        }

        private static void ARingAgainstBothRings()
        {
            var ring = new InspectItem("R", Lines("R", "+20 to maximum Life"), false, null);
            var left = new InspectItem("L", Lines("L", "+10 to maximum Life", "+5 to maximum Mana"), true, "left ring");
            var right = new InspectItem("Q", Lines("Q", "+8 to maximum Mana"), true, "right ring");
            var rows = InspectRows.Align(new[] { ring, left, right });
            Expect("title|+# to maximum Life|+# to maximum Mana", string.Join("|", rows.Select(row => row.Key)), "rows");
            Expect("IW-", Cells(rows[1]), "life");
            Expect("-WW", Cells(rows[2]), "mana");
        }

        // The robes' rows: 0 title, 1 item level, 2 energy shield, 3 requirements, 4 life, 5 intelligence,
        // 6 increased energy shield, 7 lightning resistance.
        private static void FlicksWalkTheRobesOneLineAtATime()
        {
            var table = new InspectTable(new[] { "This item", "Worn" }, InspectRows.Align(new[] { Robe, WornRobe }));
            Expect("Hale Tattered Robe of the Pupil, magic", table.Move(PadGesture.FlickDown), "the first flick down reads the first row");
            Expect("Body Armour: Item Level 7", table.Move(PadGesture.FlickDown), "row 2");
            Expect("Energy Shield: 28", table.Move(PadGesture.FlickDown), "row 3");
            Expect("Worn. Energy Shield: 43", table.Move(PadGesture.FlickRight), "switching items says the label first");
            Expect("Requires: Level 5, 11 Int", table.Move(PadGesture.FlickDown), "up and down don't repeat the label");
            Expect("This item. Requires, none", table.Move(PadGesture.FlickLeft), "an absent cell names the row");
            Expect("+19(10-19) to maximum Life, T13, Prefix", table.Move(PadGesture.FlickDown), "this robe's life");
            Expect("Worn. to maximum Life, none", table.Move(PadGesture.FlickRight), "the worn robe has none");
            Expect("Worn. to maximum Life, none", table.Move(PadGesture.FlickRight), "past the last item the cell is said again");
        }

        private static void EdgesSayTheCellAgain()
        {
            var table = new InspectTable(new[] { "This item", "Worn" }, InspectRows.Align(new[] { Robe, WornRobe }));
            table.Move(PadGesture.FlickDown);
            Expect("Hale Tattered Robe of the Pupil, magic", table.Move(PadGesture.FlickUp), "above the first row");
            for (var i = 0; i < 10; i++)
            {
                table.Move(PadGesture.FlickDown);
            }

            Expect("to Lightning Resistance, none", table.Move(PadGesture.FlickDown), "below the last row (lightning resistance, which this robe lacks)");
        }

        private static void TheFirstFlickSidewaysEntersTheFirstRow()
        {
            var table = new InspectTable(new[] { "This item", "Worn" }, InspectRows.Align(new[] { Robe, WornRobe }));
            Expect("Worn. Protective Feathered Robe of the Cloud, magic", table.Move(PadGesture.FlickRight), "right from the start");
        }

        private static void OneTooltipIsOneColumn()
        {
            var table = new InspectTable(new[] { "This item" }, InspectRows.Align(new[] { Robe }));
            table.Move(PadGesture.FlickDown);
            Expect("This item. Hale Tattered Robe of the Pupil, magic", table.Move(PadGesture.FlickRight), "no other item to go to");
        }

        private static void ARebuildSaysOnlyAChangedCell()
        {
            var labels = new[] { "This item", "Worn" };
            var table = new InspectTable(labels, InspectRows.Align(new[] { Robe, WornRobe }));
            for (var i = 0; i < 5; i++)
            {
                table.Move(PadGesture.FlickDown); // five flicks down end on this robe's life row (row 4)
            }

            Expect(null, table.Rebuild(labels, InspectRows.Align(new[] { Robe, WornRobe })), "same text, nothing said");
            var plain = Robe with { Lines = Robe.Lines.Select(line => line.Match.Contains("Life", StringComparison.Ordinal) ? new InspectLine("+19 to maximum Life", "+19 to maximum Life") : line).ToList() };
            Expect("+19 to maximum Life", table.Rebuild(labels, InspectRows.Align(new[] { plain, WornRobe })), "advanced info off: the row's new text");
        }

        private static void AnItemWithoutSocketsSaysSo()
        {
            var socketed = new InspectItem("A", new[] { new InspectLine("A", "A"), new InspectLine("2 sockets: Lesser Iron Rune, empty", InspectRows.SocketsMatch) }, false, null);
            var plain = new InspectItem("B", Lines("B"), true, null);
            var table = new InspectTable(new[] { "This item", "Worn" }, InspectRows.Align(new[] { socketed, plain }));
            table.Move(PadGesture.FlickDown);
            Expect("2 sockets: Lesser Iron Rune, empty", table.Move(PadGesture.FlickDown), "the sockets row");
            Expect("Worn. no sockets", table.Move(PadGesture.FlickRight), "the other item has none");
        }

        private static void ARebuildWithoutTheRowMovesToTheNearest()
        {
            var labels = new[] { "This item", "Worn" };
            var table = new InspectTable(labels, InspectRows.Align(new[] { Robe, WornRobe }));
            for (var i = 0; i < 4; i++)
            {
                table.Move(PadGesture.FlickDown); // four flicks end on the requirements row, "Requires, none" on this robe
            }

            var noRequirements = WornRobe with { Lines = WornRobe.Lines.Where(line => !line.Match.StartsWith("Requires", StringComparison.Ordinal)).ToList() };
            Expect("+19(10-19) to maximum Life, T13, Prefix", table.Rebuild(labels, InspectRows.Align(new[] { Robe, noRequirements })), "the row went, the next one is said");
        }

        private static void TheOpeningWaitsForTheTooltipsToSettle()
        {
            var watch = new InspectTableWatch();
            Expect(null, watch.Update(new[] { Robe }, "Inspect Mode", true, 0), "the worn tooltip isn't drawn yet");
            Equal(true, watch.Active, "flicks go to the table from the start");
            Expect(null, watch.Update(new[] { Robe, WornRobe }, "Inspect Mode", true, 50), "it arrives: the wait starts again");
            Expect(null, watch.Tick(150), "still settling");
            Expect("Inspect Mode. Hale Tattered Robe of the Pupil, against worn Protective Feathered Robe of the Cloud", watch.Tick(210), "settled");
            Expect(null, watch.Tick(400), "said once");
            Expect("Hale Tattered Robe of the Pupil, magic", watch.Flick(PadGesture.FlickDown), "the table");
        }

        private static void ClosingInspectModeEndsTheTable()
        {
            var watch = new InspectTableWatch();
            watch.Update(new[] { Robe, WornRobe }, "Inspect Mode", true, 0);
            Expect(null, watch.Update(null, "Inspect Mode", true, 300), "closed");
            Equal(false, watch.Active, "no flicks routed");
            Expect(string.Empty, watch.Flick(PadGesture.FlickDown), "nothing to say");
        }

        private static void WithTheTableOffOnlyTheCrossingsAreSaid()
        {
            var watch = new InspectTableWatch();
            Expect(null, watch.Update(new[] { Robe, WornRobe }, "Inspect Mode", false, 0), "no opening");
            Expect(null, watch.Tick(300), "still none");
            Equal(false, watch.Active, "no table");
            Expect("Worn", watch.CrossedTo(1), "the focus starts in the worn robe's tooltip");
            Expect(null, watch.CrossedTo(1), "moving within it");
            Expect("This item", watch.CrossedTo(0), "crossing to the other robe");
        }

        private static void TwoWornRingsAreToldApartBySlot()
        {
            var watch = new InspectTableWatch();
            var ring = new InspectItem("R", Lines("R"), false, null);
            var left = new InspectItem("L", Lines("L"), true, "left ring");
            var right = new InspectItem("Q", Lines("Q"), true, "right ring");
            watch.Update(new[] { ring, left, right }, "Inspect Mode", true, 0);
            Expect("Worn, right ring", watch.CrossedTo(2), "the second ring");
        }

        private static void ASingleTooltipOpensWithItsName()
        {
            var watch = new InspectTableWatch();
            watch.Update(new[] { Robe }, "Inspect Mode", true, 0);
            Expect("Inspect Mode. Hale Tattered Robe of the Pupil", watch.Tick(200), "nothing worn: the item alone");
        }

        private static void ALineFillingInIsNotSaid()
        {
            var watch = new InspectTableWatch();
            var partial = WornRobe with { Lines = WornRobe.Lines.Take(3).ToList() };
            watch.Update(new[] { Robe, partial }, "Inspect Mode", true, 0);
            Expect(null, watch.Update(new[] { Robe, WornRobe }, "Inspect Mode", true, 20), "more lines before any flick");
        }

        /// <summary>
        ///     A section of Fixtures/inspect-layers.txt as one node a tooltip: a line is an element, two spaces a depth, then
        ///     its name, plain text and tagged text separated by tabs; "\n" in a text is a line break.
        /// </summary>
        private static IReadOnlyList<InspectNode> Tooltips(string section)
        {
            var path = RoomTests.Find(System.IO.Path.Join("tests", "Poe2Accessibility.Tests", "Fixtures", "inspect-layers.txt"))
                       ?? throw new System.IO.FileNotFoundException("inspect-layers.txt");
            var lines = System.IO.File.ReadAllLines(path);
            var start = Array.FindIndex(lines, line => line.StartsWith($"== {section}:", StringComparison.Ordinal));
            if (start < 0)
            {
                throw new InvalidOperationException($"no section {section}");
            }

            var roots = new List<FixtureNode>();
            var open = new List<FixtureNode>();
            foreach (var line in lines.Skip(start + 1).TakeWhile(line => line.Length > 0 && !line.StartsWith("==", StringComparison.Ordinal)))
            {
                var indent = line.TakeWhile(character => character == ' ').Count();
                var parts = line[indent..].Split('\t');
                var node = new FixtureNode(parts[0], parts[1].Replace("\\n", "\n"), parts[2].Replace("\\n", "\n"));
                var depth = indent / 2;
                if (depth == 0)
                {
                    roots.Add(node);
                }
                else
                {
                    open[depth - 1].Children.Add(node);
                }

                open.RemoveRange(depth, open.Count - depth);
                open.Add(node);
            }

            return roots.Select(root => root.Build()).ToList();
        }

        private static string Spoken((string Title, IReadOnlyList<InspectLine> Lines)? read) =>
            read is { } tooltip ? string.Join(" | ", tooltip.Lines.Select(line => line.Spoken)) : "no tooltip";

        private static void TheRobesReadAsTheirTooltips()
        {
            var robes = Tooltips("robes");
            var bag = InspectLayout.Read(robes[0], false);
            Expect("Hale Tattered Robe of the Pupil", bag?.Title, "the title");
            Expect("Hale Tattered Robe of the Pupil | Body Armour: Item Level 7 | Energy Shield: 28 | +19(10-19) to maximum Life, T13, Prefix | +6(5-8) to Intelligence, T8, Suffix",
                Spoken(bag), "the bag robe's lines, no price nor Equip");
            Expect("+19(10-19) to maximum Life", bag?.Lines[3].Match, "a modifier is matched by its text alone");
            Expect("Protective Feathered Robe of the Cloud | Body Armour: Item Level 8 | Energy Shield: 43 | Requires: Level 5, 11 Int | 23(15-26)% increased Energy Shield, T8, Prefix | +9(6-10)% to Lightning Resistance, T8, Suffix",
                Spoken(InspectLayout.Read(robes[1], true)), "the worn robe's lines, no Equipped");
        }

        private static void AdvancedInfoOffReadsThePlainLines() =>
            Expect("Hale Tattered Robe of the Pupil | Body Armour | Energy Shield: 28 | +19 to maximum Life | +6 to Intelligence",
                Spoken(InspectLayout.Read(Tooltips("robes-advanced-off")[0], false)), "one line a modifier, no tier or label");

        private static void ARaresTitleHoldsItsBase()
        {
            var rare = InspectLayout.Read(Tooltips("identified-rare")[0], false);
            Expect("Eagle Hold, Firm Bracers", rare?.Title, "name and base");
            Expect("Eagle Hold, Firm Bracers", rare?.Lines[0].Spoken, "the title row");
            Equal(3, rare?.Lines.Count(line => line.Spoken.EndsWith(", Prefix", StringComparison.Ordinal)), "each prefix line carries the group's label");
            Equal(true, rare?.Lines.Any(line => line.Spoken == "+16(10-19) to maximum Life, T9, Prefix"), "a prefix line");
        }

        private static void AUniquesGroupsAndStoryAreRows()
        {
            var said = Spoken(InspectLayout.Read(Tooltips("worn-unique-amulet")[0], true));
            Equal(true, said.Contains("+14(10-15) to Strength, I, Implicit", StringComparison.Ordinal), "the implicit");
            Equal(true, said.Contains("+57(40-60) to maximum Life, U, Unique", StringComparison.Ordinal), "a unique modifier");
            Equal(true, said.EndsWith("The sacred sap flows slowly, but surely.", StringComparison.Ordinal), "the story line, last once Equipped is gone");
            Equal(false, said.Contains("Equipped", StringComparison.Ordinal) || said.Contains("Unequip", StringComparison.Ordinal) || said.Contains("1,215", StringComparison.Ordinal),
                "no Equipped, button or sell value");
        }

        private static void AGrantedSkillAndUnidentifiedAreRows()
        {
            var said = Spoken(InspectLayout.Read(Tooltips("stash-staff")[0], false));
            Expect("Ashen Staff | Staff: Item Level 8 | Grants Skill: Level 3 Firebolt | Unidentified", said, "the staff's rows, no buttons");
        }

        private static void AWornWeaponsEquippedLineIsNoRow() =>
            Expect("+1.35(1.01-1.5)% to Critical Hit Chance, T6, Suffix", InspectLayout.Read(Tooltips("merchant-shortbow")[1], true)?.Lines[^1].Spoken,
                "the last suffix, not Equipped: Weapon Set");

        private static void AnUnmetRequirementIsSaidOnItsRow()
        {
            var requirement = new InspectNode(string.Empty, "Requires: Level 30, 40 Dex", "Requires: <valuedefault>{Level <unmet>{30}, <unmet>{40} Dex}", Array.Empty<InspectNode>());
            var tooltip = new InspectNode(string.Empty, string.Empty, string.Empty, new[]
            {
                new InspectNode(string.Empty, string.Empty, string.Empty, new[] { new InspectNode(string.Empty, "Doom Chant", "Doom Chant", Array.Empty<InspectNode>()) }),
                new InspectNode("layout", string.Empty, string.Empty, new[] { requirement }),
            });
            var read = InspectLayout.Read(tooltip, false);
            Expect("Requires: Level 30, 40 Dex. Cannot use: Level 30, 40 Dex", read?.Lines[1].Spoken, "the game paints them red; the mod names them");
            Expect("Requires: Level 30, 40 Dex", read?.Lines[1].Match, "matched by the game's line");
        }

        private static void ANodeWithoutLayoutIsNoTooltip() =>
            Expect(null, InspectLayout.Read(new InspectNode(string.Empty, "Inspect Mode", "Inspect Mode", Array.Empty<InspectNode>()), false), "the layer's label");

        private static void WornTooltipsTakeSlotsByTitleInOrder()
        {
            Expect("left ring|right ring", string.Join("|", InspectLayout.MatchSlots(new[] { "Iron Ring", "Iron Ring" }, new[] { ("left ring", "Iron Ring"), ("right ring", "Iron Ring") })),
                "two alike take the slots in order");
            Expect("right ring", string.Join("|", InspectLayout.MatchSlots(new[] { "Gold Ring" }, new[] { ("left ring", "Iron Ring"), ("right ring", "Gold Ring") })), "by title");
            Expect(string.Empty, string.Join("|", InspectLayout.MatchSlots(new[] { "Topaz Ring" }, new[] { ("left ring", "Iron Ring") })), "no slot has it");
        }

        /// <summary>A fixture element while its children are read.</summary>
        private sealed class FixtureNode
        {
            public FixtureNode(string name, string text, string tagged) => (this.Name, this.Text, this.Tagged) = (name, text, tagged);

            public string Name { get; }

            public string Text { get; }

            public string Tagged { get; }

            public List<FixtureNode> Children { get; } = new();

            public InspectNode Build() => new(this.Name, this.Text, this.Tagged, this.Children.Select(child => child.Build()).ToList());
        }
    }
}
