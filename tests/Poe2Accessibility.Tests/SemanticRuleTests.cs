namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using GameHelper.Utils.CodeReader;
    using Poe2Accessibility.Services;

    /// <summary>The semantic rules: their catalog, and a rule run on a tiny synthetic exe.</summary>
    public static class SemanticRuleTests
    {
        private const string CatalogName = "semantic-fields.txt";

        // A tiny PE32+: four sections of 0x200 bytes each, loaded one page apart.
        private const int TextRva = 0x1000;
        private const int RdataRva = 0x2000;
        private const int DataRva = 0x3000;
        private const int PdataRva = 0x4000;
        private const int SectionBytes = 0x200;
        private const int FirstRaw = 0x400;
        private const int PeAt = 0x40;
        private const int OptionalAt = PeAt + 24;
        private const int OptionalSize = 0xF0;
        private const long ImageBase = 0x1_4000_0000;
        private const int Print = TextRva + 0x100;
        private const int Switch = TextRva + 0x180;
        private const int Table = TextRva + 0x190;
        private const int FallThrough = TextRva + 0x1C0;
        private const int Tracked = TextRva + 0x1D0;
        private const int Chained = TextRva + 0x1F0;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(SemanticRulesParse), SemanticRulesParse),
            (nameof(ALabelYieldsTheFieldsPrintedAfterIt), ALabelYieldsTheFieldsPrintedAfterIt),
            (nameof(DecodingStopsAtDataNotAtGuesses), DecodingStopsAtDataNotAtGuesses),
            (nameof(TheTrackerFollowsCopiesAndCalls), TheTrackerFollowsCopiesAndCalls),
        };

        /// <summary>Dev command: every rule of the catalog on an exe, with what each site read; 0 when every rule reads its value.</summary>
        public static int CheckExe(string exe, string catalog)
        {
            var entries = CodeFieldCatalog.ParseSemantic(File.ReadLines(catalog));
            var watch = Stopwatch.StartNew();
            var file = File.ReadAllBytes(exe);
            var read = watch.ElapsedMilliseconds;
            var image = PeImage.Read(file) ?? throw new InvalidDataException($"{exe} is not a PE32+ file");
            var index = new CodeIndex(image);
            var indexed = watch.ElapsedMilliseconds;
            var rules = new SemanticRules(new CodeAnchors(index));
            var right = 0;
            foreach (var entry in entries)
            {
                var candidates = rules.Candidates(entry.Rule!);
                var agreed = SemanticFieldReader.Agreed(candidates);
                right += agreed == entry.Value ? 1 : 0;
                Console.WriteLine($"{(agreed == entry.Value ? "OK   " : "WRONG")} {entry.Name,-34} want 0x{entry.Value:X} got [{string.Join(", ", candidates.Select(value => $"0x{value:X}"))}]  {entry.Rule!.Kind} {entry.Rule.Name}");
            }

            Console.WriteLine($"{right} of {entries.Count} rules right; file {read} ms, index {indexed - read} ms ({index.FragmentCount} fragments), rules {watch.ElapsedMilliseconds - indexed} ms");
            var whole = Enumerable.Range(0, index.FragmentCount).Count(at => index.FragmentAt(at) is var (start, end) && index.Decode(start, end) != null);
            Console.WriteLine($"{whole} of {index.FragmentCount} .pdata fragments decode ({100.0 * whole / index.FragmentCount:F2}%)");
            return right == entries.Count ? 0 : 1;
        }

        private static void SemanticRulesParse()
        {
            Equal(new SemanticRule(SemanticKind.Label, "Life: ", K: 1), SemanticRule.Parse("label \"Life: \" 1"), "a label keeps its space");
            Equal(new SemanticRule(SemanticKind.Label, "Level: ", With: "Experience: "), SemanticRule.Parse("label \"Level: \" 0 with \"Experience: \""), "with");
            Equal(true, SemanticRule.Parse("label \"Character Name: \" 0 address")!.Address, "address");
            Equal(new SemanticRule(SemanticKind.Geal, "IsAlive", "Life"), SemanticRule.Parse("geal IsAlive Life 0"), "geal");
            Equal(new SemanticRule(SemanticKind.World, "IsLevel"), SemanticRule.Parse("world IsLevel 0"), "world");
            Equal(new SemanticRule(SemanticKind.Callee, "HasBuff", "Buffs"), SemanticRule.Parse("callee HasBuff Buffs"), "callee");
            Equal(new SemanticRule(SemanticKind.Called, "Team: ", Call: 1), SemanticRule.Parse("called \"Team: \" 1 returns 0"), "called, returns");
            Equal(new SemanticRule(SemanticKind.Called, "Buffs: ", K: 1, Call: 1, Argument: 2), SemanticRule.Parse("called \"Buffs: \" 1 arg 2 1"), "called, an argument");
            Equal(new SemanticRule(SemanticKind.Field, "Active Skills: "), SemanticRule.Parse("field \"Active Skills: \" 0"), "field");
            Equal(new SemanticRule(SemanticKind.Getter, SemanticRule.Lookup, "Life"), SemanticRule.Parse("getter Life lookup"), "getter");
            Equal(new SemanticRule(SemanticKind.Object, "GetObjectId"), SemanticRule.Parse("object GetObjectId 0"), "object");
            Equal(new SemanticRule(SemanticKind.Deref, "TypeContains", K: 2, Argument: 3), SemanticRule.Parse("deref TypeContains arg 3 2"), "deref");
            Equal(new SemanticRule(SemanticKind.Slot, "IsWalkable", With: "Tile hash: "), SemanticRule.Parse("slot IsWalkable \"Tile hash: \""), "slot");
            Equal(true, SemanticRule.Parse("slot GetGameObjectById \"Tile hash: \" then")!.Then, "slot, then");
            Equal(true, SemanticRule.Parse("slot GetLocalPlayer \"Tile hash: \" inline")!.Inline, "slot, inline");
            Equal(new SemanticRule(SemanticKind.This, "league", With: "class"), SemanticRule.Parse("this \"league\" 0 with \"class\""), "this");
            foreach (var broken in new[]
            {
                "label \"Life: 0", "label Life", "getter Life heap", "called \"Team: \" 0 returns", "called \"Buffs: \" 1 arg 5", "teleport x", "label \"A\" 0 site",
                "object GetObjectId", "deref TypeContains arg 5 2", "deref TypeContains arg 3 0", "slot IsWalkable", "slot IsWalkable \"T\" then inline", "this \"league\" with \"class\"",
            })
            {
                Equal(null, SemanticRule.Parse(broken), $"'{broken}' does not parse");
            }

            var directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Join(directory, "Poe2Accessibility", "catalog", CatalogName)))
            {
                directory = Path.GetDirectoryName(directory)!;
            }

            var lines = File.ReadLines(Path.Join(directory, "Poe2Accessibility", "catalog", CatalogName)).ToList();
            var shipped = CodeFieldCatalog.ParseSemantic(lines);
            Equal(lines.Count(line => line.Length > 0 && line[0] != '#'), shipped.Count, "every shipped line parses");
            Equal(true, shipped.All(entry => entry.Struct != null || entry.Layout != null), "each moves a struct field or a plugin offset");
        }

        private static void ALabelYieldsTheFieldsPrintedAfterIt()
        {
            var rules = new SemanticRules(new CodeAnchors(new CodeIndex(PeImage.Read(TinyExe())!)));
            Equal("0x1E0", Values(rules.Candidates(SemanticRule.Parse("label \"Life: \" 0")!)), "the first field printed after the label");
            Equal("0x1DC", Values(rules.Candidates(SemanticRule.Parse("label \"Life: \" 1")!)), "the second, past a stack read");
            Equal(string.Empty, Values(rules.Candidates(SemanticRule.Parse("label \"Mana: \" 0")!)), "a label the exe does not carry reads nothing");
            Equal("0x200", Values(rules.Candidates(SemanticRule.Parse("label \"Next: \" 0")!)), "the next label's own field");
        }

        private static void DecodingStopsAtDataNotAtGuesses()
        {
            var index = new CodeIndex(PeImage.Read(TinyExe())!);
            var code = index.Decode(Switch, Table + 0x10)!;
            Equal(Table, (int)code[^1].End, "a switch table indexed ahead ends the code");
            Equal(null, index.Decode(FallThrough, FallThrough + 3), "an unknown encoding after code that falls through voids it");
            Equal(1, index.Decode(Print, Print + 3)!.Count, "after a ret, an unknown encoding is data");
        }

        private static void TheTrackerFollowsCopiesAndCalls()
        {
            var index = new CodeIndex(PeImage.Read(TinyExe())!);
            var accesses = RegisterTracker.Track(index, Tracked, new Dictionary<int, long> { [X64.Rcx] = 0 }, 20)!;
            Equal("0x20, 0x28, 0x30", string.Join(", ", accesses.Select(access => $"0x{access.Field:X}")), "the lea itself, through its rax, then through rbx after the call");

            var chain = RegisterTracker.Track(index.Body(Chained)!, new Dictionary<int, long> { [X64.Rcx] = 0 }, follow: 2);
            Equal("0:0x0, 1:0x8, 2:0x20, 2:0x18", string.Join(", ", chain.Select(access => $"{access.Level}:0x{access.Field:X}")), "two pointers followed, then the fields of the last");
            Equal("0:0x0, 1:0x8", string.Join(", ", RegisterTracker.Track(index.Body(Chained)!, new Dictionary<int, long> { [X64.Rcx] = 0 }, follow: 1)
                .Select(access => $"{access.Level}:0x{access.Field:X}")), "one level: the second load drops the register");
        }

        private static string Values(IReadOnlyList<long> values) => string.Join(", ", values.Select(value => $"0x{value:X}"));

        /// <summary>A PE32+ with a dump function printing two labels, a switch, a bad encoding and a function to track.</summary>
        private static byte[] TinyExe()
        {
            var text = new Assembler(TextRva);
            text.Rip(new byte[] { 0x48, 0x8D, 0x15 }, RdataRva + 2);   // lea rdx,"Life: "
            text.Call(Print);
            text.Bytes(0x8B, 0x83, 0xE0, 0x01, 0x00, 0x00);             // mov eax,[rbx+0x1E0]
            text.Bytes(0x8B, 0x44, 0x24, 0x20);                         // mov eax,[rsp+0x20]
            text.Bytes(0x8B, 0x8B, 0xDC, 0x01, 0x00, 0x00);             // mov ecx,[rbx+0x1DC]
            text.Call(Print);
            text.Rip(new byte[] { 0x48, 0x8D, 0x15 }, RdataRva + 0x12); // lea rdx,"Next: "
            text.Call(Print);
            text.Bytes(0x8B, 0x83, 0x00, 0x02, 0x00, 0x00);             // mov eax,[rbx+0x200]
            text.Bytes(0xC3);
            var dump = text.At;
            text.Pad(Print);
            text.Bytes(0xC3, 0x07, 0x07);                               // ret, then data
            text.Pad(Switch);
            text.Bytes(0x8B, 0x8C, 0x82);                               // mov ecx,[rdx+rax*4+table]
            text.Int32(Table);
            text.Bytes(0xFF, 0xE1);                                     // jmp rcx
            text.Pad(Table);
            text.Bytes(0x07, 0x07, 0x07, 0x07);
            text.Pad(FallThrough);
            text.Bytes(0x90, 0x07, 0xC3);                               // nop, then no instruction
            text.Pad(Tracked);
            text.Bytes(0x48, 0x8B, 0xD9);                               // mov rbx,rcx
            text.Bytes(0x48, 0x8D, 0x43, 0x20);                         // lea rax,[rbx+0x20]
            text.Bytes(0x8B, 0x48, 0x08);                               // mov ecx,[rax+8]
            text.Call(Print);
            text.Bytes(0x8B, 0x43, 0x30);                               // mov eax,[rbx+0x30]
            text.Bytes(0xC3);
            var tracked = text.At;
            text.Pad(Chained);
            text.Bytes(0x48, 0x8B, 0x01);                               // mov rax,[rcx]
            text.Bytes(0x48, 0x8B, 0x40, 0x08);                         // mov rax,[rax+8]
            text.Bytes(0x83, 0x78, 0x20, 0x07);                         // cmp dword [rax+0x20],7
            text.Bytes(0x8B, 0x50, 0x18);                               // mov edx,[rax+0x18]
            text.Bytes(0xC3);
            var chained = text.At;

            var rdata = new byte[SectionBytes];
            System.Text.Encoding.Unicode.GetBytes("Life: ").CopyTo(rdata, 2);
            System.Text.Encoding.Unicode.GetBytes("Next: ").CopyTo(rdata, 0x12);
            const int unwind = 0x100;
            rdata[unwind] = 1;
            var pdata = new List<byte>();
            foreach (var (start, end) in new[] { (TextRva, dump), (Print, Print + 1), (Switch, Table + 0x10), (FallThrough, FallThrough + 3), (Tracked, tracked), (Chained, chained) })
            {
                pdata.AddRange(BitConverter.GetBytes(start).Concat(BitConverter.GetBytes(end)).Concat(BitConverter.GetBytes(RdataRva + unwind)));
            }

            var file = new byte[FirstRaw + (4 * SectionBytes)];
            BitConverter.TryWriteBytes(file.AsSpan(0x3C), PeAt);
            "PE\0\0"u8.CopyTo(file.AsSpan(PeAt));
            BitConverter.TryWriteBytes(file.AsSpan(PeAt + 6), (ushort)4);
            BitConverter.TryWriteBytes(file.AsSpan(PeAt + 20), (ushort)OptionalSize);
            BitConverter.TryWriteBytes(file.AsSpan(OptionalAt), (ushort)0x20B);
            BitConverter.TryWriteBytes(file.AsSpan(OptionalAt + 24), ImageBase);
            BitConverter.TryWriteBytes(file.AsSpan(OptionalAt + 112 + (3 * 8)), PdataRva);
            BitConverter.TryWriteBytes(file.AsSpan(OptionalAt + 112 + (3 * 8) + 4), pdata.Count);
            var sections = new[] { (".text", TextRva, text.Code), (".rdata", RdataRva, rdata), (".data", DataRva, Array.Empty<byte>()), (".pdata", PdataRva, pdata.ToArray()) };
            for (var at = 0; at < sections.Length; at++)
            {
                var (name, rva, bytes) = sections[at];
                var row = file.AsSpan(OptionalAt + OptionalSize + (at * 40), 40);
                System.Text.Encoding.ASCII.GetBytes(name).CopyTo(row);
                BitConverter.TryWriteBytes(row[8..], SectionBytes);
                BitConverter.TryWriteBytes(row[12..], rva);
                BitConverter.TryWriteBytes(row[16..], SectionBytes);
                BitConverter.TryWriteBytes(row[20..], FirstRaw + (at * SectionBytes));
                bytes.CopyTo(file, FirstRaw + (at * SectionBytes));
            }

            return file;
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
            }
        }

        /// <summary>Lays out code at an RVA, resolving call and RIP-relative targets.</summary>
        private sealed class Assembler(int origin)
        {
            private readonly List<byte> code = new();

            public int At => origin + this.code.Count;

            public byte[] Code => this.code.ToArray();

            public void Bytes(params byte[] bytes) => this.code.AddRange(bytes);

            public void Int32(int value) => this.code.AddRange(BitConverter.GetBytes(value));

            public void Call(int target)
            {
                this.Bytes(0xE8);
                this.Int32(target - (this.At + sizeof(int)));
            }

            public void Rip(byte[] prefix, int target)
            {
                this.Bytes(prefix);
                this.Int32(target - (this.At + sizeof(int)));
            }

            public void Pad(int to)
            {
                while (this.At < to)
                {
                    this.code.Add(0xCC);
                }
            }
        }
    }
}
