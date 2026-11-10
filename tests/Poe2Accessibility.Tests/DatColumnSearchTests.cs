namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Poe2Accessibility.Services;

    /// <summary>
    ///     Surviving a content patch (docs/compatibility.md): dat columns found again by what they hold, a skill graph file of
    ///     another version read structurally, the stick rule's self-check and its constants read from code bytes.
    /// </summary>
    public static class DatColumnSearchTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ColumnsAreFoundWhereMeasured), ColumnsAreFoundWhereMeasured),
            (nameof(MovedColumnsAreFoundNearbyAndSaid), MovedColumnsAreFoundNearbyAndSaid),
            (nameof(ColumnsOfOneTypeKeepTheirOrder), ColumnsOfOneTypeKeepTheirOrder),
            (nameof(AKnownRowsValueTellsColumnsApart), AKnownRowsValueTellsColumnsApart),
            (nameof(AColumnNotFoundFailsTheStep), AColumnNotFoundFailsTheStep),
            (nameof(ASmallTableJudgesTheSame), ASmallTableJudgesTheSame),
            (nameof(AnIdIsADistinctIdentifierOnEveryRow), AnIdIsADistinctIdentifierOnEveryRow),
            (nameof(AGraphOfAnotherVersionIsReadWhenItHoldsTogether), AGraphOfAnotherVersionIsReadWhenItHoldsTogether),
            (nameof(TheStickRuleIsLostWhenHalfMissAndHoldsAgain), TheStickRuleIsLostWhenHalfMissAndHoldsAgain),
            (nameof(StickConstantsAreReadFromTheCodesLoads), StickConstantsAreReadFromTheCodesLoads),
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        /// <summary>A .datc64 in memory: fixed rows, then the 0xBB marker and a string pool.</summary>
        internal sealed class DatTableBuilder
        {
            private const long NullKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

            /// <summary>String and list offsets count from the marker's start, so the pool's first byte is at 8.</summary>
            private const int MarkerBytes = 8;

            private readonly int rowSize;
            private readonly List<byte[]> rows = new();
            private readonly MemoryStream pool = new();

            public DatTableBuilder(int rowSize)
            {
                this.rowSize = rowSize;
            }

            public int Count => this.rows.Count;

            public DatTableBuilder Row(Action<RowWriter> write)
            {
                var row = new byte[this.rowSize];
                write(new RowWriter(row, this.pool));
                this.rows.Add(row);
                return this;
            }

            public DatTable Build()
            {
                using var stream = new MemoryStream();
                stream.Write(BitConverter.GetBytes(this.rows.Count));
                foreach (var row in this.rows)
                {
                    stream.Write(row);
                }

                stream.Write(Enumerable.Repeat((byte)0xBB, 8).ToArray());
                stream.Write(this.pool.ToArray());
                return DatTable.FromBytes(stream.ToArray());
            }

            public sealed class RowWriter
            {
                private readonly byte[] row;
                private readonly MemoryStream pool;

                public RowWriter(byte[] row, MemoryStream pool) => (this.row, this.pool) = (row, pool);

                public RowWriter Text(int offset, string text)
                {
                    BitConverter.GetBytes(MarkerBytes + this.pool.Position).CopyTo(this.row, offset);
                    this.pool.Write(Encoding.Unicode.GetBytes(text));
                    this.pool.Write(new byte[4]); // the NUL pair, and one more so the next string starts after two zero bytes
                    return this;
                }

                public RowWriter Key(int offset, long key)
                {
                    BitConverter.GetBytes(key < 0 ? NullKey : key).CopyTo(this.row, offset);
                    return this;
                }

                public RowWriter Int(int offset, int value)
                {
                    BitConverter.GetBytes(value).CopyTo(this.row, offset);
                    return this;
                }

                public RowWriter Byte(int offset, byte value)
                {
                    this.row[offset] = value;
                    return this;
                }

                public RowWriter KeyList(int offset, params long[] keys)
                {
                    BitConverter.GetBytes((long)keys.Length).CopyTo(this.row, offset);
                    BitConverter.GetBytes(MarkerBytes + this.pool.Position).CopyTo(this.row, offset + 8);
                    foreach (var key in keys)
                    {
                        this.pool.Write(BitConverter.GetBytes(key));
                        this.pool.Write(new byte[8]);
                    }

                    return this;
                }
            }
        }

        /// <summary>Objectives as the game lays them out: Id, text, completion text, art, icon key; <paramref name="shift"/> moves every column after the Id.</summary>
        private static DatTable Objectives(int shift, int rows = 4)
        {
            var builder = new DatTableBuilder(0x30 + shift);
            for (var row = 0; row < rows; row++)
            {
                var index = row;
                builder.Row(write => write.Text(0x0, index == 0 ? "Breach" : $"Objective{index}")
                    .Text(0x8 + shift, index == 0 ? "Open all [ContainsBreach|Unstable Breaches]" : $"Do thing {index} well")
                    .Text(0x10 + shift, $"Thing {index} done")
                    .Text(0x18 + shift, $"Art/2DArt/Icon{index}.dds")
                    .Key(0x20 + shift, index == 1 ? -1 : index));
            }

            return builder.Build();
        }

        private static DatTable Icons() => new DatTableBuilder(0x10).Row(w => w.Text(0, "Breach")).Row(w => w.Text(0, "Ritual")).Row(w => w.Text(0, "Shrine")).Row(w => w.Text(0, "Essence")).Build();

        private static (int Text, int Done, int Art, int Icon, DatColumnSearch Search) Find(DatTable objectives, DatTable icons)
        {
            var search = new DatColumnSearch();
            var known = DatColumnSearch.RowOf(objectives, "Breach");
            var text = search.Text(objectives, "Objectives.Text", 0x8, t => !t.StartsWith("Art/", StringComparison.Ordinal), known: (known, t => t.Contains("ContainsBreach", StringComparison.Ordinal)));
            var done = search.Text(objectives, "Objectives.Done", 0x10, t => !t.StartsWith("Art/", StringComparison.Ordinal), text, (known, t => t.Contains(' ', StringComparison.Ordinal)));
            var art = search.Text(objectives, "Objectives.Art", 0x18, t => t.StartsWith("Art/", StringComparison.Ordinal), done);
            var icon = search.Key(objectives, "Objectives.Icon", 0x20, icons, art, (known, key => key == 0));
            return (text, done, art, icon, search);
        }

        private static void ColumnsAreFoundWhereMeasured()
        {
            var (text, done, art, icon, search) = Find(Objectives(0), Icons());
            Expect("8 10 18 20", $"{text:X} {done:X} {art:X} {icon:X}", "the measured offsets");
            Expect(true, search.AllFound && search.Moved.Count == 0, "nothing moved, nothing missing");
        }

        private static void MovedColumnsAreFoundNearbyAndSaid()
        {
            var (text, done, art, icon, search) = Find(Objectives(8), Icons());
            Expect("10 18 20 28", $"{text:X} {done:X} {art:X} {icon:X}", "a column inserted after the Id moves them all");
            Expect(4, search.Moved.Count, "each move said");
            Expect("Objectives.Text moved from +0x8 to +0x10", search.Moved[0], "how a move is said");
            Expect(true, search.AllFound, "all found");
        }

        private static void ColumnsOfOneTypeKeepTheirOrder()
        {
            // Done is nearest to +0x10 where Text now is: the order constraint keeps it past Text.
            var (text, done, _, _, _) = Find(Objectives(8), Icons());
            Expect(true, done > text, "the second text column lies after the first");
        }

        private static void AKnownRowsValueTellsColumnsApart()
        {
            // Two key columns into the icons: the known row's icon names the right one.
            var table = new DatTableBuilder(0x20).Row(w => w.Text(0, "Breach").Key(0x8, 2).Key(0x10, 0)).Row(w => w.Text(0, "Other").Key(0x8, 1).Key(0x10, 3)).Build();
            var search = new DatColumnSearch();
            var known = DatColumnSearch.RowOf(table, "Breach");
            Expect(0x10, search.Key(table, "T.Icon", 0x8, Icons(), known: (known, key => key == 0)), "the column whose Breach row says Breach");
            Expect(0x8, new DatColumnSearch().Key(table, "T.Icon", 0x8, Icons(), known: (-1, key => key == 0)), "no known row: the nearest key column");
        }

        private static void AColumnNotFoundFailsTheStep()
        {
            var search = new DatColumnSearch();
            var table = Objectives(0);
            Expect(-1, search.Key(table, "Objectives.Missing", 0x28, Icons(), known: (0, key => key == 3)), "no column holds it");
            Expect("Objectives.Missing near +0x28", search.Missing[0], "what is said");
            Expect(false, search.AllFound, "the step fails");
            Expect(false, search.Report("test"), "the report says so");
        }

        private static void ASmallTableJudgesTheSame()
        {
            // Two rows, as a 1.0 table might shrink to: shares, not counts, decide.
            var (text, done, art, icon, search) = Find(Objectives(8, rows: 2), Icons());
            Expect("10 18 20 28", $"{text:X} {done:X} {art:X} {icon:X}", "found on two rows");
            Expect(true, search.AllFound, "all found");
            var keyLists = new DatTableBuilder(0x20).Row(w => w.KeyList(0x8, 0, 1)).Row(w => w.KeyList(0x8)).Build();
            Expect(0x8, new DatColumnSearch().KeyList(keyLists, "T.List", 0x8, Icons()), "a list column on two rows, one empty");
        }

        private static void AnIdIsADistinctIdentifierOnEveryRow()
        {
            // Characters: the Id is the class's metadata path, then its translated name; a copy of the Id would not be distinct.
            var classes = new DatTableBuilder(0x18)
                .Row(w => w.Text(0, "Metadata/Characters/Str/StrFour").Text(8, "Guerrero").Text(0x10, "Art/A"))
                .Row(w => w.Text(0, "Metadata/Characters/Dex/DexFour").Text(8, "Cazadora").Text(0x10, "Art/A"))
                .Build();
            bool Path(string id) => id.StartsWith("Metadata/Characters/", StringComparison.Ordinal);
            Expect(true, DatColumnSearch.IsIdColumn(classes, 0, Path), "the paths at +0");
            Expect(false, DatColumnSearch.IsIdColumn(classes, 8, Path), "a translated name does not fit the path");
            Expect(false, DatColumnSearch.IsIdColumn(classes, 0x10), "the same text on two rows is not an Id");
            Expect(false, DatColumnSearch.IsIdColumn(classes, 4), "inside a pointer: no string");
            var named = new DatTableBuilder(0x10).Row(w => w.Text(0, "Gran Golpe").Text(8, "GreatStrike")).Row(w => w.Text(0, "Parada").Text(8, "ParryPlayer")).Build();
            Expect(false, DatColumnSearch.IsIdColumn(named, 0), "a name with a blank is not an Id");
            Expect(true, DatColumnSearch.IsIdColumn(named, 8), "identifiers, one per row");
        }

        private static void AGraphOfAnotherVersionIsReadWhenItHoldsTogether()
        {
            var bytes = Graph((0, 0, new[] { (10, new[] { 20 }), (11, Array.Empty<int>()) }), (100, -50, new[] { (20, Array.Empty<int>()) }));
            var known = SkillGraphFile.Parse(bytes) ?? throw new Exception("not parsed");
            Expect(true, known.VersionKnown && known.IsConsistent, "today's version");
            bytes[0] = SkillGraphFile.KnownVersion + 1;
            var other = SkillGraphFile.Parse(bytes) ?? throw new Exception("another version with the same layout is read");
            Expect(false, other.VersionKnown, "flagged as unknown");
            Expect(true, other.IsConsistent, "its links all point at nodes");
            var dangling = Graph((0, 0, new[] { (10, new[] { 99 }) }));
            dangling[0] = SkillGraphFile.KnownVersion + 1;
            Expect(false, SkillGraphFile.Parse(dangling)!.IsConsistent, "a link to no node: not accepted");
            Expect(true, SkillGraphFile.Parse(bytes.Concat(new byte[] { 0 }).ToArray()) == null, "a byte left over: not read");
        }

        private static void TheStickRuleIsLostWhenHalfMissAndHoldsAgain()
        {
            var check = new StickCheck();
            foreach (var hit in new[] { false, true, true, false, true, false, true, true, false, true })
            {
                Expect(StickVerdict.Same, check.Record(hit), "four misses in ten keep the rule (popups, slanted pushes)");
            }

            Expect(StickVerdict.Same, check.Record(false), "the oldest miss has left the window: still four");
            Expect(StickVerdict.Lost, check.Record(false), "the fifth miss loses it, once");
            Expect(true, check.Lost, "lost");
            Expect(StickVerdict.Same, check.Record(false), "said once only");
            for (var push = 0; push < 7; push++)
            {
                check.Record(true);
            }

            Expect(true, check.Lost, "still three misses in the window");
            Expect(StickVerdict.Regained, check.Record(true), "the misses fell to two: it holds again");
            Expect(false, check.Lost, "trusted");
            check.Reset();
            Expect(false, check.Lost, "a new game process starts over");
        }

        private static void StickConstantsAreReadFromTheCodesLoads()
        {
            // Code at RVA 0x1000: the linked sequence twice would be ambiguous unless both load the same floats.
            var signature = StickRuleCode.Parse("AA BB ^ ?? ?? ?? ?? CC ^ ?? ?? ?? ??");
            var floats = new Dictionary<long, float> { [0x2000] = 0.94f, [0x2004] = 8.333333f };
            float? FloatAt(long rva) => floats.TryGetValue(rva, out var value) ? value : null;
            var code = new List<byte> { 0x90, 0xAA, 0xBB };
            code.AddRange(BitConverter.GetBytes(0x2000 - (0x1000 + 7))); // displacement from the next instruction's start
            code.Add(0xCC);
            code.AddRange(BitConverter.GetBytes(0x2004 - (0x1000 + 12)));
            code.Add(0x90);
            Expect("0,94 8,333333", string.Join(" ", (StickRuleCode.Floats(code.ToArray(), 0x1000, signature, FloatAt) ?? Array.Empty<float>()).Select(f => f.ToString("0.######", new System.Globalization.CultureInfo("es-ES")))), "the floats the displacements reach");
            var twice = code.Concat(code).ToArray();
            Expect(true, StickRuleCode.Floats(twice, 0x1000, signature, FloatAt) == null, "a second hit reading other floats (its displacements land elsewhere) is ambiguous");
            Expect(true, StickRuleCode.Floats(new byte[] { 0x90, 0x90 }, 0x1000, signature, FloatAt) == null, "no hit");
        }

        /// <summary>A version-3 graph (as EndgameTests builds one): kind, orbit sizes, roots, then groups of nodes with their links.</summary>
        private static byte[] Graph(params (float X, float Y, (int Id, int[] Links)[] Nodes)[] groups)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)SkillGraphFile.KnownVersion);
            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write(new byte[] { 1, 6 });
            writer.Write(1);
            writer.Write(groups[0].Nodes[0].Id);
            writer.Write(0);
            writer.Write(groups.Length);
            foreach (var (x, y, nodes) in groups)
            {
                writer.Write(x);
                writer.Write(y);
                writer.Write(0);
                writer.Write(0);
                writer.Write((byte)0);
                writer.Write(nodes.Length);
                foreach (var (id, links) in nodes)
                {
                    writer.Write(id);
                    writer.Write(0);
                    writer.Write(0);
                    writer.Write(links.Length);
                    foreach (var link in links)
                    {
                        writer.Write(link);
                        writer.Write(-3);
                    }
                }
            }

            writer.Flush();
            return stream.ToArray();
        }
    }
}
