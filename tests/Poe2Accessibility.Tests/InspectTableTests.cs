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
    }
}
