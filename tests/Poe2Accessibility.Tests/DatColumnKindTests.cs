namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Check;
    using DatTableBuilder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>
    ///     Self-describing tables (docs/compatibility.md): the kind of column at each offset told from the bytes alone, and
    ///     the second opinion that fails a step whose column changed kind.
    /// </summary>
    public static class DatColumnKindTests
    {
        // One row layout for every test: Id, name, a key, a list of texts, a list of keys, a flag, a number, bytes never set.
        private const int Id = 0x0, Name = 0x8, Key = 0x10, Texts = 0x20, Keys = 0x30, Flag = 0x40, Number = 0x41, Blank = 0x45, RowSize = 0x4D;
        private const int Rows = 12;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EachKindIsToldFromTheBytes), EachKindIsToldFromTheBytes),
            (nameof(TheRowIsLaidOutColumnByColumn), TheRowIsLaidOutColumnByColumn),
            (nameof(AColumnReadAsItsKindAgrees), AColumnReadAsItsKindAgrees),
            (nameof(AColumnReadAsAnotherKindDiffers), AColumnReadAsAnotherKindDiffers),
            (nameof(ABlankColumnOrATinyTableCannotBeTold), ABlankColumnOrATinyTableCannotBeTold),
            (nameof(TheAuditNamesTheTableTheOffsetAndBothKinds), TheAuditNamesTheTableTheOffsetAndBothKinds),
            (nameof(ASearchsTrialsAreNotReads), ASearchsTrialsAreNotReads),
            (nameof(AnIdColumnThatReadsAsNamesIsSaid), AnIdColumnThatReadsAsNamesIsSaid),
            (nameof(KnownLinesMustStillReadAsTheyDid), KnownLinesMustStillReadAsTheyDid),
            (nameof(ClientStringsAreFoundByAKnownRow), ClientStringsAreFoundByAKnownRow),
            (nameof(ACompiledOffsetNothingJudgedIsReported), ACompiledOffsetNothingJudgedIsReported),
        };

        private static DatTable Table(int rows = Rows, bool namesFirst = false)
        {
            var builder = new DatTableBuilder(RowSize);
            for (var row = 0; row < rows; row++)
            {
                var index = row;
                builder.Row(write => write.Text(Id, namesFirst ? $"The Name of Row {index}" : $"Metadata/Row{index}")
                    .Text(Name, $"Name of row {index}")
                    .WideKey(Key, index % 3 == 0 ? -1 : index)
                    .TextList(Texts, index % 2 == 0 ? new[] { $"a{index}.ao", $"b{index}.ao" } : Array.Empty<string>())
                    .KeyList(Keys, index % 4 == 0 ? Array.Empty<long>() : new long[] { index, index + 1 })
                    .Byte(Flag, (byte)(index % 2))
                    .Int(Number, 0x01010101 * (index + 2)));
            }

            return builder.Build();
        }

        private static void EachKindIsToldFromTheBytes()
        {
            var table = Table();
            Expect(DatColumnKind.Text, DatColumnKinds.At(table, Id), "the Id");
            Expect(DatColumnKind.Text, DatColumnKinds.At(table, Name), "a name");
            Expect(DatColumnKind.Key, DatColumnKinds.At(table, Key), "a key with nulls");
            Expect(DatColumnKind.TextList, DatColumnKinds.At(table, Texts), "a list of texts");
            Expect(DatColumnKind.KeyList, DatColumnKinds.At(table, Keys), "a list of keys");
            Expect(DatColumnKind.Flag, DatColumnKinds.At(table, Flag), "a flag");
            Expect(DatColumnKind.Number, DatColumnKinds.At(table, Number), "a number");
            Expect(DatColumnKind.Blank, DatColumnKinds.At(table, Blank), "bytes never set");
        }

        private static void TheRowIsLaidOutColumnByColumn()
        {
            var layout = DatColumnKinds.Layout(Table()).ToList();
            Expect("0:Text:8 8:Text:8 10:Key:16 20:TextList:16 30:KeyList:16 40:Flag:1 41:Number:4 45:Blank:8",
                string.Join(' ', layout.Select(column => $"{column.Offset:X}:{column.Kind}:{column.Width}")), "the layout");
        }

        private static void AColumnReadAsItsKindAgrees()
        {
            var table = Table();
            foreach (var (offset, read) in new[] { (Id, DatRead.Text), (Name, DatRead.Text), (Key, DatRead.Key), (Texts, DatRead.TextList), (Keys, DatRead.KeyList), (Flag, DatRead.Flag), (Number, DatRead.Number) })
            {
                Expect(DatKindVerdict.Agrees, DatColumnKinds.Judge(table, offset, read), $"+0x{offset:X} read as {read}");
            }
        }

        private static void AColumnReadAsAnotherKindDiffers()
        {
            var table = Table();
            foreach (var (offset, read) in new[]
                     {
                         (Key, DatRead.Text), (Texts, DatRead.Text), (Number, DatRead.Text), // a key, a list and a number are no string offsets
                         (Name, DatRead.Key), (Texts + 4, DatRead.Key), // a text column is no key; neither are bytes across two fields
                         (Name, DatRead.TextList), (Key, DatRead.KeyList), (Keys, DatRead.TextList), (Texts, DatRead.KeyList), // a list of the other kind
                         (Number, DatRead.Flag), // a byte above 1
                         (Name, DatRead.Number), (Key, DatRead.Number), (Texts, DatRead.Number), // text, a null key's 0xFE bytes and a list are no number
                     })
            {
                Expect(DatKindVerdict.Differs, DatColumnKinds.Judge(table, offset, read), $"+0x{offset:X} read as {read}");
            }
        }

        private static void ABlankColumnOrATinyTableCannotBeTold()
        {
            var table = Table();
            Expect(DatKindVerdict.CannotTell, DatColumnKinds.Judge(table, Blank, DatRead.Number), "constant bytes read as a number");
            Expect(DatKindVerdict.CannotTell, DatColumnKinds.Judge(table, Blank, DatRead.Flag), "constant bytes read as a flag");
            Expect(DatKindVerdict.CannotTell, DatColumnKinds.Judge(table, Blank, DatRead.Key), "constant zero read as a key");

            // Three rows prove nothing about a number that happens to land on string starts: the read is not contradicted.
            Expect(false, DatColumnKinds.Judge(Table(rows: 3), Name, DatRead.Number) == DatKindVerdict.Differs, "a tiny table's text read as a number");
        }

        private static void TheAuditNamesTheTableTheOffsetAndBothKinds()
        {
            var table = Table();
            var audit = new DatColumnAudit();
            audit.Add(table);
            for (var row = 0; row < table.RowCount; row++)
            {
                table.String(row, Id);
                table.StringList(row, Texts);
                table.Key(row, Key);
            }

            var sound = audit.Judge();
            Expect(true, sound.Holds, "every column read as its kind");
            Expect((3, 3), (sound.Columns, sound.Agree), "three columns read, three agree");

            // The patch that turned the name into a key: the extractor still reads text at its old place.
            table.StringOrNull(0, Key);
            var changed = audit.Judge();
            Expect(false, changed.Holds, "a column read as text that holds a key");
            Expect(true, changed.Changed.Single().Contains($"{table.Name} +0x{Key:X} is read as text, but its bytes hold a key", StringComparison.Ordinal), changed.Changed.Single());
        }

        private static void ASearchsTrialsAreNotReads()
        {
            var table = Table();
            var audit = new DatColumnAudit();
            audit.Add(table);
            using (DatTable.Probe())
            {
                for (var offset = 0; offset < table.RowSize; offset++)
                {
                    table.StringOrNull(0, offset);
                    table.Key(0, offset);
                }
            }

            Expect(0, audit.Judge().Columns, "trials inside a probe");
            Expect(0x8, new DatColumnSearch().Text(table, "T.Name", Name), "a search finds the column");
            Expect(0, audit.Judge().Columns, "the search's own trials");
            table.StringOrNull(0, Name);
            Expect(1, audit.Judge().Columns, "the read that follows it");
        }

        private static void AnIdColumnThatReadsAsNamesIsSaid()
        {
            Expect(true, DatColumnKinds.StartsWithIds(Table()), "paths at the start");
            Expect(false, DatColumnKinds.StartsWithIds(Table(namesFirst: true)), "names at the start");
            Expect(null, DatColumnKinds.StartsWithIds(Table(rows: 3, namesFirst: true)), "too few rows to say");

            var table = Table(namesFirst: true);
            var audit = new DatColumnAudit();
            audit.Add(table);
            table.String(0, Id);
            Expect(true, audit.Judge().Changed.Single().Contains("is read as the Id, but its bytes hold display text", StringComparison.Ordinal), "the audit says it");
        }

        private static void KnownLinesMustStillReadAsTheyDid()
        {
            var lines = new[] { "Slam|geometry|shape=rectangle|Metadata/Effects/slam.ao|", "Marker|spawn|||Metadata/Objects/Marker" };
            var known = new[] { ("Slam", new[] { "|geometry|", "slam.ao" }), ("Marker", new[] { "|spawn|", "Objects/Marker" }), ("Gone", new[] { "|effect|" }) };
            Expect(0, DatAnchors.Unmet(lines, '|', known).Count, "the known lines read as they did; a skill the game dropped judges nothing");

            var moved = new[] { "Slam|geometry|shape=rectangle|Metadata/Effects/other.ao|", lines[1] };
            Expect(true, DatAnchors.Unmet(moved, '|', known).Single().StartsWith("Slam no longer reads with", StringComparison.Ordinal), "a list column that now leads elsewhere");
        }

        private static void ACompiledOffsetNothingJudgedIsReported()
        {
            var folder = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests");
            var log = new Poe2Accessibility.Services.PluginLog(folder, false);
            var file = System.IO.Path.Join(folder, "dat-columns-" + Guid.NewGuid().ToString("N") + ".json");
            IReadOnlyList<string> With(string? json)
            {
                if (json != null)
                {
                    System.IO.File.WriteAllText(file, json);
                }

                Poe2Accessibility.Services.DatColumns.Apply(file, log);
                return Poe2Accessibility.Services.DatColumns.Unjudged;
            }

            try
            {
                var none = With(null);
                Expect(true, none.Count > 20 && none.All(column => column.Contains("no dat-columns.json", StringComparison.Ordinal)), "without the file every compiled offset is unjudged");

                // An older launcher's file: no column of Mods, but the table's row size as it was measured.
                var sameSize = With("{\"Mods.RowSize\": 693}");
                Expect(false, sameSize.Any(column => column.StartsWith("Mods.Name", StringComparison.Ordinal)), "a table of the measured size keeps its compiled offset");
                Expect(true, sameSize.Any(column => column.StartsWith("Characters.Name", StringComparison.Ordinal)), "a table the file says nothing of does not");

                var grown = With("{\"Mods.RowSize\": 701}");
                Expect(true, grown.Single(column => column.StartsWith("Mods.Name", StringComparison.Ordinal)).Contains("0x2BD bytes, 0x2B5 when it was measured", StringComparison.Ordinal), "a table that grew");
                Expect(null, Poe2Accessibility.Services.DatColumns.Doubt("Mods", new Dictionary<string, int> { ["Mods.RowSize"] = 693 }), "no doubt at the measured size");

                // A skill's stats at its gem's level were read at compiled offsets no file could move until 2026-11-12.
                var moved = With("{\"GrantedEffectStatSetsPerLevel.AdditionalStats\": 137, \"GrantedEffectStatSetsPerLevel.AdditionalStatsValues\": 52, \"GrantedEffectStatSetsPerLevel.RowSize\": 193}");
                Expect(137, GameLayout.StatSetLevelRow.StatKeysOffset, "the stat keys follow the file");
                Expect(52, GameLayout.StatSetLevelRow.ValuesOffset, "and their values");
                Expect(false, moved.Any(column => column.StartsWith("GrantedEffectStatSetsPerLevel.", StringComparison.Ordinal)), "a column the file gives is judged");
                With("{\"GrantedEffectStatSetsPerLevel.RowSize\": 185}");
                Expect(0x81, GameLayout.StatSetLevelRow.StatKeysOffset, "a file without them leaves the compiled offset");

                // Said when the catalogs are loaded, before any key is read, once per game process.
                var report = new Poe2Accessibility.Services.CompatibilityReport(log);
                Poe2Accessibility.Services.DatColumns.Report(report);
                Expect(true, report.IsMissing(Poe2Accessibility.Services.DatColumns.UnjudgedPart), "the unjudged columns are a part that may read wrong");
                report.Found(Poe2Accessibility.Services.DatColumns.UnjudgedPart);
                Poe2Accessibility.Services.DatColumns.Report(report);
                Expect(false, report.IsMissing(Poe2Accessibility.Services.DatColumns.UnjudgedPart), "not said twice in one game process");
                report.Reset();
                Poe2Accessibility.Services.DatColumns.Report(report);
                Expect(true, report.IsMissing(Poe2Accessibility.Services.DatColumns.UnjudgedPart), "a new game process is told again");
            }
            finally
            {
                System.IO.File.Delete(file);
                Poe2Accessibility.Services.DatColumns.Apply(file, log); // back to the compiled offsets for the tests after this one
            }
        }

        private static void ClientStringsAreFoundByAKnownRow()
        {
            static DatTable Strings(int shift)
            {
                var builder = new DatTableBuilder(0x20 + shift);
                foreach (var (key, text, other) in new[] { ("GameName", "Path of Exile 2", "x"), (ClientStringColumns.KnownKey, ClientStringColumns.KnownText, "Pueblo"), ("Options", "Options Menu", "y") })
                {
                    builder.Row(write => write.Text(0x0 + shift, key).Text(0x8 + shift, text).Text(0x10 + shift, other));
                }

                return builder.Build();
            }

            Expect((0x0, 0x8), ClientStringColumns.Find(Strings(0), new DatColumnSearch()), "where measured");
            var search = new DatColumnSearch();
            Expect((0x8, 0x10), ClientStringColumns.Find(Strings(8), search), "after a column was added before them");
            Expect(2, search.Moved.Count, "both moves are said");

            var reworded = new DatTableBuilder(0x18).Row(w => w.Text(0, "GameName").Text(8, "Path of Exile 2")).Row(w => w.Text(0, "Options").Text(8, "Options Menu")).Build();
            var missing = new DatColumnSearch();
            Expect(null, ClientStringColumns.Find(reworded, missing), "a table without the known key");
            Expect(false, missing.AllFound, "the step fails");
        }
    }
}
