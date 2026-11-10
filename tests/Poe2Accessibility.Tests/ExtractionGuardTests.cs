namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Poe2Accessibility.Tests.DatColumnSearchTests;
    using static Check;

    /// <summary>
    ///     The rules that make a game patch followed or reported, never silently wrong (docs/patch-day.md): name columns told
    ///     by a known row, known-row checks that cannot run said, tables that do not read, counts that collapse.
    /// </summary>
    public static class ExtractionGuardTests
    {
        private const string AnchorId = "G1_1";
        private const string AnchorName = "The Riverbank";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ANameColumnIsToldByTheAnchorsName), ANameColumnIsToldByTheAnchorsName),
            (nameof(ANameColumnNoRowHoldsFailsTheStep), ANameColumnNoRowHoldsFailsTheStep),
            (nameof(AMissingAnchorRowIsSaidAndFallsBackOnTheTranslation), AMissingAnchorRowIsSaidAndFallsBackOnTheTranslation),
            (nameof(TablesThatDifferInSizeAreNotAName), TablesThatDifferInSizeAreNotAName),
            (nameof(ASkippedKnownRowCheckIsSaid), ASkippedKnownRowCheckIsSaid),
            (nameof(ABundleIndexThatDoesNotLookLikeTheGamesIsOneNamedFailure), ABundleIndexThatDoesNotLookLikeTheGamesIsOneNamedFailure),
            (nameof(ATableThatIsNotWholeRowsFailsAndNamesItself),ATableThatIsNotWholeRowsFailsAndNamesItself),
            (nameof(ATableWithoutRowsOrTooShortFailsToo), ATableWithoutRowsOrTooShortFailsToo),
            (nameof(OverrideColumnsAreFoundWhereMeasuredAndAfterAShift),OverrideColumnsAreFoundWhereMeasuredAndAfterAShift),
            (nameof(AnOverrideTableWithoutTheKnownRowIsJudgedByShape), AnOverrideTableWithoutTheKnownRowIsJudgedByShape),
            (nameof(AKnownOverrideThatReadsOtherwiseFailsTheStep), AKnownOverrideThatReadsOtherwiseFailsTheStep),
            (nameof(OverrideColumnsThatNameNoClassAreNotFound), OverrideColumnsThatNameNoClassAreNotFound),
            (nameof(TheTriggerAnimationKeyMustLeadToAnimationFiles), TheTriggerAnimationKeyMustLeadToAnimationFiles),
        };

        private static readonly NameAnchor Anchor = new("worldareas", "WorldAreas.Name", 0x10, AnchorId, AnchorName);

        /// <summary>
        ///     Areas: Id, a note column that a translation changes too (the old heuristic's first pick), then the name at
        ///     <paramref name="nameAt"/>; <paramref name="language"/> translates both.
        /// </summary>
        private static DatTable Areas(bool translated, int nameAt = 0x10, bool withAnchorRow = true, int rowSize = 0x20)
        {
            var builder = new DatTableBuilder(rowSize);
            for (var row = 0; row < 8; row++)
            {
                var index = row;
                var isAnchor = withAnchorRow && row == 0;
                builder.Row(write => write.Text(0, isAnchor ? AnchorId : $"Area{index}")
                    .Text(0x8, translated ? $"nota {index}" : $"note {index}")
                    .Text(nameAt, translated ? $"Nombre {index}" : isAnchor ? AnchorName : $"Name {index}"));
            }

            return builder.Build();
        }

        private static void ANameColumnIsToldByTheAnchorsName()
        {
            var search = new DatColumnSearch();
            Expect(0x10, NameColumns.Find(Areas(false), Areas(true), Anchor, search), "the column holding the anchor's name, not the first one a translation changes");
            Expect(true, search.AllFound && search.Unverified.Count == 0, "nothing missing, nothing unchecked");
            Expect(0x8, (int)(NameColumns.FirstTranslated(Areas(false), Areas(true)) ?? -1), "the old heuristic would have taken the note column");
        }

        private static void ANameColumnNoRowHoldsFailsTheStep()
        {
            var search = new DatColumnSearch();
            Expect(-1, NameColumns.Find(Areas(false, nameAt: 0x10), Areas(true, nameAt: 0x10), Anchor with { Name = "Somewhere Else" }, search), "no column holds that name");
            Expect(false, search.AllFound, "the step fails");
            Expect(true, search.Missing[0].StartsWith("WorldAreas.Name", StringComparison.Ordinal), "and names the column");
        }

        private static void AMissingAnchorRowIsSaidAndFallsBackOnTheTranslation()
        {
            var search = new DatColumnSearch();
            var column = NameColumns.Find(Areas(false, withAnchorRow: false), Areas(true, withAnchorRow: false), Anchor, search);
            Expect(0x8, column, "without the anchor row only the translation can tell");
            Expect(1, search.Unverified.Count, "the skipped check is said");
            Expect(true, search.Unverified[0].Contains(AnchorId, StringComparison.Ordinal), "naming the row");
        }

        private static void TablesThatDifferInSizeAreNotAName()
        {
            var search = new DatColumnSearch();
            Expect(-1, NameColumns.Find(Areas(false), Areas(true, rowSize: 0x28), Anchor, search), "a translated copy of another row size");
            Expect(false, search.AllFound, "the step fails");
        }

        private const int OverrideRows = 12;
        private const int KnownReplacedRow = 20;
        private const int KnownShownRow = 21;

        private static DatTable Classes() => new DatTableBuilder(0x20)
            .Row(w => w.Text(0, PassiveOverrideColumns.KnownCharacter)).Row(w => w.Text(0, "Metadata/Characters/Dex/DexFour")).Build();

        private static DatTable Nodes()
        {
            var builder = new DatTableBuilder(0x10);
            for (var row = 0; row < 40; row++)
            {
                var id = row == KnownReplacedRow ? PassiveOverrideColumns.KnownReplaced : row == KnownShownRow ? PassiveOverrideColumns.KnownShown : $"node{row}";
                builder.Row(w => w.Text(0, id));
            }

            return builder.Build();
        }

        /// <summary>Overrides of the Witch (class 0), each node replaced once; the first is the known one unless <paramref name="known"/> is off.</summary>
        private static DatTable Overrides(int shift, bool known = true, int shownOfKnown = KnownShownRow)
        {
            var builder = new DatTableBuilder(0x30 + shift);
            for (var row = 0; row < OverrideRows; row++)
            {
                var index = row;
                var replaced = index == 0 && known ? KnownReplacedRow : index;
                var shown = index == 0 ? (known ? shownOfKnown : 22) : 22 + index;
                builder.Row(w => w.Key(0, 0x7777777777777777).Key(PassiveOverrideColumns.CharacterOffset + shift, 0).Key(PassiveOverrideColumns.ReplacedOffset + shift, replaced).Key(PassiveOverrideColumns.ShownOffset + shift, shown));
            }

            return builder.Build();
        }

        private static void OverrideColumnsAreFoundWhereMeasuredAndAfterAShift()
        {
            var found = PassiveOverrideColumns.Find(Overrides(0), Classes(), Nodes()) ?? throw new Exception("not found");
            Expect("0 10 20", $"{found.Character:X} {found.Replaced:X} {found.Shown:X}", "the measured offsets");
            var moved = PassiveOverrideColumns.Find(Overrides(8), Classes(), Nodes()) ?? throw new Exception("not found after a shift");
            Expect("8 18 28", $"{moved.Character:X} {moved.Replaced:X} {moved.Shown:X}", "a column inserted before them moves them all");
        }

        private static void AnOverrideTableWithoutTheKnownRowIsJudgedByShape()
        {
            // Without the Witch's known override the columns stand on their shape (the log says the check did not run).
            Expect(true, PassiveOverrideColumns.Find(Overrides(0, known: false), Classes(), Nodes()) != null, "found by shape");
        }

        private static void AKnownOverrideThatReadsOtherwiseFailsTheStep()
        {
            Expect(true, PassiveOverrideColumns.Find(Overrides(0, shownOfKnown: 25), Classes(), Nodes()) == null, "the Witch's spells18 shows another node: the columns are wrong");
        }

        private static void OverrideColumnsThatNameNoClassAreNotFound()
        {
            var oneClass = new DatTableBuilder(0x20).Row(w => w.Text(0, "Not a character")).Row(w => w.Text(0, "Metadata/Characters/Dex/DexFour")).Build();
            Expect(true, PassiveOverrideColumns.Find(Overrides(0), oneClass, Nodes()) == null, "a class Id is a player's metadata path");
        }

        private static void TheTriggerAnimationKeyMustLeadToAnimationFiles()
        {
            static DatTable Animations(string suffix) => new DatTableBuilder(0x10)
                .Row(w => w.Text(0, "a").Text(8, "Effects/one" + suffix)).Row(w => w.Text(0, "b").Text(8, "Effects/two" + suffix)).Row(w => w.Text(0, "c").Text(8, "Effects/three" + suffix)).Build();
            static DatTable Triggers(int shift) => new DatTableBuilder(0x40 + shift)
                .Row(w => w.Key(TelegraphColumns.TriggerAnimationOffset + shift, 0)).Row(w => w.Key(TelegraphColumns.TriggerAnimationOffset + shift, -1))
                .Row(w => w.Key(TelegraphColumns.TriggerAnimationOffset + shift, 2)).Build();
            Expect(0x14, TelegraphColumns.FindTriggerAnimation(Triggers(0), Animations(".ao"), 8, new DatColumnSearch()), "found where measured");
            Expect(0x1C, TelegraphColumns.FindTriggerAnimation(Triggers(8), Animations(".ao"), 8, new DatColumnSearch()), "found after a shift");
            var search = new DatColumnSearch();
            Expect(-1, TelegraphColumns.FindTriggerAnimation(Triggers(0), Animations(".txt"), 8, search), "keys that lead to other files");
            Expect(false, search.AllFound, "the step fails");
        }

        /// <summary>A file of <paramref name="rows"/> rows of <paramref name="rowSize"/> bytes, <paramref name="extra"/> stray bytes before the marker.</summary>
        private static byte[] DatFile(int rows, int rowSize, int extra = 0, bool marker = true)
        {
            var bytes = new List<byte>(BitConverter.GetBytes(rows));
            bytes.AddRange(new byte[Math.Max(0, rows * rowSize) + extra]);
            if (marker)
            {
                bytes.AddRange(Enumerable.Repeat((byte)0xBB, 8));
            }

            bytes.AddRange(new byte[16]);
            return bytes.ToArray();
        }

        private static string Unreadable(byte[] file, string name)
        {
            try
            {
                DatTable.FromBytes(file, name);
                return string.Empty;
            }
            catch (System.IO.InvalidDataException error)
            {
                return error.Message;
            }
        }

        private static void ATableThatIsNotWholeRowsFailsAndNamesItself()
        {
            Expect(0x20, DatTable.FromBytes(DatFile(5, 0x20), "data/balance/mods.datc64").RowSize, "whole rows read");
            var message = Unreadable(DatFile(5, 0x20, extra: 3), "data/balance/mods.datc64");
            Expect(true, message.StartsWith("data/balance/mods.datc64:", StringComparison.Ordinal) && message.Contains("whole number of rows", StringComparison.Ordinal), "stray bytes: the table is named and the cause said");
            Expect(true, Unreadable(DatFile(5, 0x20, marker: false), "stats").Contains("marker", StringComparison.Ordinal), "no marker");
        }

        private static void ATableWithoutRowsOrTooShortFailsToo()
        {
            Expect(true, Unreadable(DatFile(0, 0x20), "stats").Contains("0 rows", StringComparison.Ordinal), "the header says no rows");
            Expect(true, Unreadable(new byte[6], "stats").Contains("too short", StringComparison.Ordinal), "a file without a header");
            Expect(true, Unreadable(DatFile(-4, 0x20), "stats").Contains("-4 rows", StringComparison.Ordinal), "a nonsense row count");
        }

        private static void ABundleIndexThatDoesNotLookLikeTheGamesIsOneNamedFailure()
        {
            Expect(null, BundleIndexCheck.Problem(80_000, 600, statsTableFound: true, unparsedPaths: 5), "today's shape");
            Expect(true, BundleIndexCheck.Problem(0, 600, true) is { }, "no files");
            Expect(true, BundleIndexCheck.Problem(80_000, 0, true) is { }, "no bundles");
            Expect(true, BundleIndexCheck.Problem(80_000, 600, statsTableFound: false)!.Contains("stats.datc64", StringComparison.Ordinal), "the table every step needs is not listed");
            Expect(true, BundleIndexCheck.Problem(80_000, 600, true, unparsedPaths: 70_000) is { }, "most paths did not parse: a format change");
            Expect("bundle index", BundleIndexCheck.Name, "the name game-data-failed.txt lists");
        }

        private static void ASkippedKnownRowCheckIsSaid()
        {
            var table = Areas(false);
            var search = new DatColumnSearch();
            Expect(0x8, search.Text(table, "T.Note", 0x8, known: (-1, _ => false)), "the check cannot run, so the shape alone decides");
            Expect(1, search.Unverified.Count, "and the log says so");
            Expect(true, search.Report("test"), "it is not a failure");
        }
    }
}
