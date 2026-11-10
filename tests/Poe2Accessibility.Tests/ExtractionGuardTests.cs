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
