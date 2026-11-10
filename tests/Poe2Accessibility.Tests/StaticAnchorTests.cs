namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using GameOffsets;
    using GameReader.Utils;
    using GameReader.Utils.CodeReader;
    using static Check;

    /// <summary>How the statics everything hangs from are found in the game's image: the byte patterns and what is believed of them.</summary>
    public static class StaticAnchorTests
    {
        /// <summary>Dev command: the static patterns on an exe on disk.</summary>
        public const string CheckCommand = "patterns";

        /// <summary>Dev command: everything that can be judged of a game build from its exe on disk (tools/Check-GameExe.ps1).</summary>
        public const string GateCommand = "exe";

        private const uint Code = 0x60000020;
        private const uint Data = 0xC0000040;
        private const uint Constants = 0x40000040;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(EveryMatchOfAPatternIsFound), EveryMatchOfAPatternIsFound),
            (nameof(WildcardsMatchAnyByte), WildcardsMatchAnyByte),
            (nameof(APatternOfWildcardsSaysNothing), APatternOfWildcardsSaysNothing),
            (nameof(EveryShippedPatternHasBytesToLookFor), EveryShippedPatternHasBytesToLookFor),
            (nameof(CodeAndDataAreToldByTheirFlags), CodeAndDataAreToldByTheirFlags),
            (nameof(AnImageWithNoSectionsIsSearchedWhole), AnImageWithNoSectionsIsSearchedWhole),
            (nameof(TwoPatternsSettleOneAddress), TwoPatternsSettleOneAddress),
            (nameof(ADisplacementPointsInsideTheImageOrNowhere), ADisplacementPointsInsideTheImageOrNowhere),
            (nameof(TheGameIsToldFromTheFirstOne), TheGameIsToldFromTheFirstOne),
        };

        /// <summary>
        ///     Dev command: where each static pattern matches in an exe on disk and what it points at; 0 when each matches once
        ///     and points into writable data. The game need not run, so a patch's exe is judged before the first start.
        /// </summary>
        public static int CheckExe(string exe)
        {
            var file = System.IO.File.ReadAllBytes(exe);
            var sections = PeImage.SectionsOf(file);
            var size = sections.Count == 0 ? 0 : sections.Max(section => section.End);
            var right = 0;
            foreach (var pattern in StaticOffsetsPatterns.Patterns)
            {
                var hits = new SortedSet<long>();
                foreach (var section in sections.Where(section => section.IsExecutable))
                {
                    PatternScan.Hits(file.AsSpan((int)section.RawStart, (int)Math.Min(section.Size, file.Length - section.RawStart)), pattern, section.Start, hits);
                }

                var targets = hits.Select(hit => hit + pattern.BytesToSkip).Select(at =>
                {
                    var code = sections.First(section => section.Holds(at));
                    return PatternScan.Target(at, BitConverter.ToInt32(file, (int)(code.RawStart + at - code.Start)), size);
                }).Distinct().ToList();
                var into = targets.Count == 1 ? sections.FirstOrDefault(section => section.Holds(targets[0])) : default;
                var good = hits.Count == 1 && into.IsWritable;
                right += good ? 1 : 0;
                Console.WriteLine(
                    $"{(good ? "OK   " : "WRONG")} {pattern.Name,-20} {hits.Count} match(es) at [{string.Join(", ", hits.Select(hit => $"0x{hit:X}"))}] -> " +
                    $"[{string.Join(", ", targets.Select(target => $"0x{target:X}"))}] {into.Name}{(into.IsWritable ? " (writable)" : string.Empty)}");
            }

            Console.WriteLine($"{right} of {StaticOffsetsPatterns.Patterns.Length} patterns right");
            return right == StaticOffsetsPatterns.Patterns.Length ? 0 : 1;
        }

        /// <summary>
        ///     Dev command: the static patterns, the code-field catalog and the semantic rules on an exe on disk, with the
        ///     catalogs of <paramref name="catalogs"/>. 0 when every pattern is right and every field reads where it was measured;
        ///     a field that moved is followed by the mod at the start, one that no longer reads is the one to look at.
        /// </summary>
        public static int CheckBuild(string exe, string catalogs)
        {
            Console.WriteLine("== Static patterns");
            var patterns = CheckExe(exe);

            Console.WriteLine("== Code fields");
            var file = System.IO.File.ReadAllBytes(exe);
            var (start, length) = CodeFieldSearch.CodeOf(file);
            var entries = Services.CodeFieldCatalog.Parse(System.IO.File.ReadLines(System.IO.Path.Combine(catalogs, "code-fields.txt")));
            var (moved, unread) = (0, 0);
            foreach (var (entry, found) in Services.CodeFieldCatalog.Read(file.AsSpan(start, length), entries))
            {
                moved += found != null && found != entry.Measured ? 1 : 0;
                unread += found == null ? 1 : 0;
                if (found != entry.Measured)
                {
                    Console.WriteLine($"{(found == null ? "UNREAD" : "MOVED ")} {entry.Name,-40} 0x{entry.Measured:X} -> {(found == null ? "?" : $"0x{found:X}")}");
                }
            }

            Console.WriteLine($"{entries.Count - moved - unread} of {entries.Count} fields where measured, {moved} moved, {unread} unread");

            Console.WriteLine("== Semantic rules");
            var semantic = SemanticRuleTests.CheckExe(exe, System.IO.Path.Combine(catalogs, "semantic-fields.txt"));
            return patterns == 0 && moved == 0 && unread == 0 && semantic == 0 ? 0 : 1;
        }

        private static void EveryMatchOfAPatternIsFound()
        {
            var pattern = new Pattern("test", "48 8B 05 ^ ?? ?? ?? ?? 48 85 C0");
            var bytes = new byte[0x100];
            foreach (var at in new[] { 0, 0x40, 0x100 - 10 })
            {
                new byte[] { 0x48, 0x8B, 0x05, 1, 2, 3, 4, 0x48, 0x85, 0xC0 }.CopyTo(bytes, at);
            }

            // Its first bytes alone, and its last ones cut by the end of the read: no match.
            new byte[] { 0x48, 0x8B, 0x05, 1, 2, 3, 4, 0x48, 0x85, 0xC1 }.CopyTo(bytes, 0x80);
            var hits = new SortedSet<long>();
            PatternScan.Hits(bytes, pattern, 0x1000, hits);
            Equal("1000,1040,10F6", string.Join(",", hits.Select(hit => hit.ToString("X"))), "every place, the first and the last byte included");
            Equal(3, pattern.BytesToSkip, "the marker is where the displacement starts");

            hits.Clear();
            PatternScan.Hits(bytes.AsSpan(0, 0x100 - 1), pattern, 0, hits);
            Equal(2, hits.Count, "a match the read cuts short is not one");

            // The same place seen by two reads that overlap is one place.
            PatternScan.Hits(bytes.AsSpan(0x40), pattern, 0x40, hits);
            Equal("0,40,F6", string.Join(",", hits.Select(hit => hit.ToString("X"))), "reads that overlap count a place once");
        }

        private static void WildcardsMatchAnyByte()
        {
            var pattern = new Pattern("test", "?? AA ^ ?? BB BB BB ?? CC");
            var hits = new SortedSet<long>();
            PatternScan.Hits(new byte[] { 0, 0x11, 0xAA, 0x22, 0xBB, 0xBB, 0xBB, 0x33, 0xCC, 0 }, pattern, 0, hits);
            Equal("1", string.Join(",", hits), "found from its longest run of fixed bytes, wildcards before and after");
            hits.Clear();
            PatternScan.Hits(new byte[] { 0x11, 0xAB, 0x22, 0xBB, 0xBB, 0xBB, 0x33, 0xCC }, pattern, 0, hits);
            Equal(0, hits.Count, "a fixed byte outside the run still has to match");
            PatternScan.Hits(new byte[] { 0xAA, 0x22, 0xBB, 0xBB, 0xBB, 0x33, 0xCC }, pattern, 0, hits);
            Equal(0, hits.Count, "a wildcard still takes a byte");
        }

        private static void APatternOfWildcardsSaysNothing()
        {
            var hits = new SortedSet<long>();
            PatternScan.Hits(new byte[16], new Pattern("test", "?? ^ ?? ?? ??"), 0, hits);
            Equal(0, hits.Count, "wildcards alone match everywhere, so nowhere");
        }

        private static void EveryShippedPatternHasBytesToLookFor()
        {
            foreach (var pattern in StaticOffsetsPatterns.Patterns)
            {
                Equal(true, pattern.Mask.Count(isFixed => isFixed) >= 5, $"{pattern.Name}: too few fixed bytes to mean anything");
                Equal(true, pattern.BytesToSkip + sizeof(int) <= pattern.Data.Length, $"{pattern.Name}: the displacement lies inside the pattern");
                Equal(false, pattern.Mask.Skip(pattern.BytesToSkip).Take(sizeof(int)).Any(isFixed => isFixed), $"{pattern.Name}: the displacement is not fixed bytes");
            }

            Equal(
                true,
                new[] { StaticOffsetsPatterns.GameStates, StaticOffsetsPatterns.Cull, StaticOffsetsPatterns.CullAlt }.OrderBy(name => name)
                    .SequenceEqual(StaticOffsetsPatterns.Patterns.Select(pattern => pattern.Name).OrderBy(name => name)),
                "a pattern for each static the mod reads and no other");
        }

        private static void CodeAndDataAreToldByTheirFlags()
        {
            // A packer's names: what a section is for is in its flags.
            var sections = PeImage.SectionsOf(Header((".a", 0x1000, 0x5000, Code), (".b", 0x6000, 0x2000, Constants), (".c", 0x8000, 0x3000, Data), (".d", 0xB000, 0x9000, Data)));
            Equal(4, sections.Count, "the sections of the header");
            Equal("1000+5000", Text(PatternScan.Ranges(sections, 0x10000, section => section.IsExecutable)), "the code");
            Equal("8000+3000,B000+5000", Text(PatternScan.Ranges(sections, 0x10000, section => section.IsWritable)), "the writable data, cut where the image ends");
        }

        private static void AnImageWithNoSectionsIsSearchedWhole()
        {
            Equal("0+10000", Text(PatternScan.Ranges(PeImage.SectionsOf(new byte[0x400]), 0x10000, section => section.IsExecutable)), "no header");
            var sections = PeImage.SectionsOf(Header((".rdata", 0x1000, 0x1000, Constants)));
            Equal("0+10000", Text(PatternScan.Ranges(sections, 0x10000, section => section.IsWritable)), "no section of the kind wanted");
        }

        private static void TwoPatternsSettleOneAddress()
        {
            Equal(0x30L, PatternScan.Pick(new long[] { 0x30 }, new long[] { 0x30 }), "both name it");
            Equal(0x30L, PatternScan.Pick(new long[] { 0x30 }, Array.Empty<long>()), "the first alone");
            Equal(0x40L, PatternScan.Pick(Array.Empty<long>(), new long[] { 0x40 }), "the second alone");
            Equal(0x40L, PatternScan.Pick(new long[] { 0x30, 0x40 }, new long[] { 0x40 }), "the first names two places: the one the second agrees with");
            Equal(0x30L, PatternScan.Pick(new long[] { 0x30 }, new long[] { 0x40, 0x50 }), "the second names two places and neither is the first's");
            Equal(0L, PatternScan.Pick(new long[] { 0x30, 0x40 }, new long[] { 0x50, 0x60 }), "neither settles it");
            Equal(0L, PatternScan.Pick(Array.Empty<long>(), Array.Empty<long>()), "nothing found");
        }

        private static void ADisplacementPointsInsideTheImageOrNowhere()
        {
            Equal(0x2004L, PatternScan.Target(0x1000, 0x1000, 0x10000), "counted from the end of the displacement");
            Equal(0xF04L, PatternScan.Target(0x1000, -0x100, 0x10000), "backwards");
            Equal(-1L, PatternScan.Target(0x1000, 0x7FFF0000, 0x10000), "past the image");
            Equal(-1L, PatternScan.Target(0x1000, -0x2000, 0x10000), "before the image");
        }

        private static void TheGameIsToldFromTheFirstOne()
        {
            const string Steam = @"D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe";
            Equal(true, GameProcessDetails.IsGame("PathOfExileSteam", "Path of Exile 2", Steam), "the game");
            Equal(true, GameProcessDetails.IsGame("pathofexilesteam", "path of exile 2", null), "by its title alone, whatever the case");
            Equal(true, GameProcessDetails.IsGame("PathOfExile", "Path of Exile 2: Early Access", null), "a title a patch added words to");
            Equal(true, GameProcessDetails.IsGame("PathOfExileSteam", "PoE2", Steam), "a title reworded: the folder it runs from says it");
            Equal(false, GameProcessDetails.IsGame("PathOfExileSteam", "Path of Exile", @"D:\games\steam\steamapps\common\Path of Exile\PathOfExileSteam.exe"), "the first game");
            Equal(false, GameProcessDetails.IsGame("PathOfExileSteam", string.Empty, Steam), "no window yet: still starting");
            Equal(false, GameProcessDetails.IsGame("notepad", "Path of Exile 2", Steam), "another program showing the name");
        }

        private static string Text(IEnumerable<(long Start, long Size)> ranges) => string.Join(",", ranges.Select(range => $"{range.Start:X}+{range.Size:X}"));

        /// <summary>The first page of a PE32+ file with these sections (name, RVA, size once loaded, flags).</summary>
        private static byte[] Header(params (string Name, uint Start, uint Size, uint Flags)[] sections)
        {
            const int At = 0x80;
            const int OptionalSize = 0xF0;
            var file = new byte[0x400];
            BitConverter.TryWriteBytes(file.AsSpan(0x3C), At);
            BitConverter.TryWriteBytes(file.AsSpan(At + 6), (ushort)sections.Length);
            BitConverter.TryWriteBytes(file.AsSpan(At + 20), (ushort)OptionalSize);
            for (var index = 0; index < sections.Length; index++)
            {
                var row = file.AsSpan(At + 24 + OptionalSize + (index * 40), 40);
                Encoding.ASCII.GetBytes(sections[index].Name).CopyTo(row);
                BitConverter.TryWriteBytes(row[8..], sections[index].Size);
                BitConverter.TryWriteBytes(row[12..], sections[index].Start);
                BitConverter.TryWriteBytes(row[36..], sections[index].Flags);
            }

            return file;
        }
    }
}
