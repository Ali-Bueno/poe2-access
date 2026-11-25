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
    }
}
