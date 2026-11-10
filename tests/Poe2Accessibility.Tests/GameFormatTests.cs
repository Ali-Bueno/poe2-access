namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using static Check;

    /// <summary>Each file format the extractor reads tells "still the format I know" from "this changed" (docs/patch-day.md).</summary>
    public static class GameFormatTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ATextFileIsKnownByHowItStarts), ATextFileIsKnownByHowItStarts),
            (nameof(AFormatMostFilesNoLongerStartAsFailsTheStep), AFormatMostFilesNoLongerStartAsFailsTheStep),
            (nameof(TheBundleIndexHeaderMustAddUp), TheBundleIndexHeaderMustAddUp),
            (nameof(ABankMustBeTheContainerKnown), ABankMustBeTheContainerKnown),
        };

        private static void ATextFileIsKnownByHowItStarts()
        {
            Expect("version 3", GameFormats.StartOf("version 3\r\nextends \"Metadata/Parent\"\r\n"), "an .ao");
            Expect("animated_object", GameFormats.StartOf("animated_object \"Metadata/Monsters/A.ao\"\r\n{"), "an .act");
            Expect("(empty)", GameFormats.StartOf("  \r\n"), "nothing");
            Expect("(not text)", GameFormats.StartOf(((char)1).ToString() + ((char)2) + "binary"), "a binary file");
            Expect(true, GameFormats.Known[".ao"].Knows("version 2") && GameFormats.Known[".ao"].Knows("version 3"), "the versions read alike");
            Expect(false, GameFormats.Known[".ao"].Knows("version 4"), "a version nobody has looked at");
            Expect(true, GameFormats.Known[".act"].Knows("parent"), "an .act that only names its parent");
        }

        private static void AFormatMostFilesNoLongerStartAsFailsTheStep()
        {
            var formats = new GameFormats();
            for (var file = 0; file < 20; file++)
            {
                formats.Note($"metadata/a{file}.ao", "version 3\nextends \"x\"");
                formats.Note($"metadata/a{file}.xyz", "anything"); // a format with no rule is never judged
            }

            formats.Note("metadata/odd.ao", "version 9\n");
            Expect(0, formats.Problems().Count, "one odd file among twenty");

            var changed = new GameFormats();
            for (var file = 0; file < 20; file++)
            {
                changed.Note($"metadata/a{file}.ao", file < 3 ? "version 3\n" : "version 4\n");
            }

            Expect(true, changed.Problems().Single().StartsWith("the .ao format changed: 3 of 20 files", StringComparison.Ordinal), changed.Problems().Single());

            var few = new GameFormats();
            few.Note("a.ao", "version 4\n");
            Expect(0, few.Problems().Count, "one file judges nothing");
        }

        private static byte[] IndexHeader(uint uncompressed, uint payload, uint? blocks = null, uint granularity = 262144)
        {
            var count = blocks ?? ((uncompressed + granularity - 1) / granularity);
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(uncompressed);
            writer.Write(payload);
            writer.Write(48 + (4 * count));
            writer.Write(12u); // the compressor
            writer.Write(1u);
            writer.Write((ulong)uncompressed);
            writer.Write((ulong)payload);
            writer.Write(count);
            writer.Write(granularity);
            writer.Write(new byte[16]);
            return stream.ToArray();
        }

        private static void TheBundleIndexHeaderMustAddUp()
        {
            // The real index of 2026-11-10: 151,240,657 bytes in 577 blocks, 116,424,635 compressed.
            const uint Uncompressed = 151240657, Payload = 116424635;
            const long Length = 12 + 48 + (4 * 577) + Payload;
            Expect(null, BundleIndexCheck.HeaderProblem(IndexHeader(Uncompressed, Payload), Length), "the game's own header");
            Expect(true, BundleIndexCheck.HeaderProblem(IndexHeader(Uncompressed, Payload), Length + 8) is { }, "bytes the header does not account for");
            Expect(true, BundleIndexCheck.HeaderProblem(IndexHeader(Uncompressed, Payload, granularity: 65536), Length) is { }, "another block size");
            Expect(true, BundleIndexCheck.HeaderProblem(IndexHeader(Uncompressed, Payload, blocks: 10), Length) is { }, "a block count that does not cover the data");
            var shifted = new byte[4].Concat(IndexHeader(Uncompressed, Payload)).Take(BundleIndexCheck.HeaderBytes).ToArray();
            Expect(true, BundleIndexCheck.HeaderProblem(shifted, Length) is { }, "a header with a new field before the sizes");
            Expect(true, BundleIndexCheck.HeaderProblem(new byte[10], 10) is { }, "a file cut short");
        }

        private static byte[] Bank(uint version, uint samples, int dataBytes, string riffKind = "FEV ")
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(0u);
            writer.Write(Encoding.ASCII.GetBytes(riffKind));
            writer.Write(new byte[32]); // the chunks before the samples
            writer.Write(Encoding.ASCII.GetBytes("FSB5"));
            writer.Write(version);
            writer.Write(samples);
            writer.Write(16u); // sample headers
            writer.Write(8u); // name table
            writer.Write(64u); // data
            writer.Write(new byte[(version == 1 ? 0x3C : 0x40) - 24]);
            writer.Write(new byte[16 + 8 + dataBytes]);
            return stream.ToArray();
        }

        private static void ABankMustBeTheContainerKnown()
        {
            Expect(null, Fsb5.Problem(Bank(1, 3, 64)), "a version 1 archive whose sizes fit");
            Expect(null, Fsb5.Problem(Bank(0, 3, 64)), "a version 0 archive");
            Expect(true, Fsb5.Problem(Bank(2, 3, 64))!.Contains("version 2", StringComparison.Ordinal), "a header version nobody has read");
            Expect(true, Fsb5.Problem(Bank(1, 3, 10)) is { }, "sizes the bank does not hold");
            Expect(true, Fsb5.Problem(Bank(1, 3, 64, "WAVE")) is { }, "another RIFF file");
            Expect(true, Fsb5.Problem(new byte[100]) is { }, "no bank at all");
        }
    }
}
