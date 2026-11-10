namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using GameOffsets.Natives;
    using GameReader.Utils;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The reader's reads without an array each: the strings decode as they always did, and a pooled block is never shared.</summary>
    public static class ReaderBufferTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AnInlineStringReadsAsBefore), AnInlineStringReadsAsBefore),
            (nameof(AnInlineWideStringReadsAsBefore), AnInlineWideStringReadsAsBefore),
            (nameof(AStringOfUnknownLengthEndsAtItsZero), AStringOfUnknownLengthEndsAtItsZero),
            (nameof(AWideStringOfUnknownLengthReadsAsBefore), AWideStringOfUnknownLengthReadsAsBefore),
            (nameof(ABlockIsHandedOutAgainOnlyOnceGivenBack), ABlockIsHandedOutAgainOnlyOnceGivenBack),
            (nameof(ABlockOfAnotherSizeIsNeverHandedOut), ABlockOfAnotherSizeIsNeverHandedOut),
        };

        private static void AnInlineStringReadsAsBefore()
        {
            var random = new Random(1);
            for (var run = 0; run < 2000; run++)
            {
                var value = new StdString { Buffer = (IntPtr)random.NextInt64(), ReservedBytes = (IntPtr)random.NextInt64(), Length = random.Next(1, 20), Capacity = 15 };
                Expect(OldInlineAscii(value), NativeText.InlineAscii(value), $"inline text of length {value.Length}");
            }

            var hello = new StdString { Buffer = (IntPtr)BitConverter.ToInt64(Encoding.ASCII.GetBytes("Metadata")), ReservedBytes = (IntPtr)BitConverter.ToInt64(Encoding.ASCII.GetBytes("/Chest\0\0")), Length = 14 };
            Expect("Metadata/Chest", NativeText.InlineAscii(hello), "a text across both words");
            Expect(string.Empty, NativeText.InlineAscii(hello with { Length = 16 }), "a length that does not fit inside the struct");
        }

        private static void AnInlineWideStringReadsAsBefore()
        {
            var random = new Random(2);
            var bytes = new byte[16];
            for (var run = 0; run < 4000; run++)
            {
                random.NextBytes(bytes);
                if (run % 2 == 0)
                {
                    // Surrogates everywhere, and so a pair across the two words too.
                    for (var at = 1; at < bytes.Length; at += 2)
                    {
                        bytes[at] = (byte)(0xD8 + random.Next(8));
                    }
                }

                var value = new StdWString { Buffer = (IntPtr)BitConverter.ToInt64(bytes, 0), ReservedBytes = (IntPtr)BitConverter.ToInt64(bytes, 8), Length = random.Next(1, 10), Capacity = 7 };
                Expect(OldInlineWide(value), NativeText.InlineWide(value), $"inline wide text of length {value.Length}, run {run}");
            }
        }

        private static void AStringOfUnknownLengthEndsAtItsZero()
        {
            var buffer = new byte[128];
            Encoding.ASCII.GetBytes("Render").CopyTo(buffer, 0);
            Expect("Render", NativeText.AsciiZ(buffer), "a component's name");
            Expect(string.Empty, NativeText.AsciiZ(new byte[128]), "a zero first");
            Array.Fill(buffer, (byte)'a');
            Expect(string.Empty, NativeText.AsciiZ(buffer), "no zero in the bytes read");
            Expect(string.Empty, NativeText.AsciiZ(ReadOnlySpan<byte>.Empty), "a failed read");
        }

        private static void AWideStringOfUnknownLengthReadsAsBefore()
        {
            var random = new Random(3);
            for (var run = 0; run < 3000; run++)
            {
                var buffer = new byte[256];
                random.NextBytes(buffer);
                var zeros = random.Next(0, 60);
                for (var zero = 0; zero < zeros; zero++)
                {
                    buffer[random.Next(buffer.Length)] = 0;
                }

                var at = random.Next(buffer.Length - 4);
                if (run % 3 != 0)
                {
                    buffer[at] = buffer[at + 1] = buffer[at + 2] = 0;
                }

                Expect(OldWideZ(buffer), NativeText.WideZ(buffer), $"run {run}");
            }

            var name = new byte[256];
            Encoding.Unicode.GetBytes("Fuego").CopyTo(name, 0);
            Expect("Fuego", NativeText.WideZ(name), "a buff's name");
            Expect(string.Empty, NativeText.WideZ(new byte[256]), "an empty name");
            Expect(string.Empty, NativeText.WideZ(ReadOnlySpan<byte>.Empty), "a failed read");
        }

        private static void ABlockIsHandedOutAgainOnlyOnceGivenBack()
        {
            var pool = new BlockPool(2);
            var first = pool.Rent(64);
            var second = pool.Rent(64);
            Expect(64, first.Length, "the size asked for");
            Expect(false, ReferenceEquals(first, second), "two blocks out at once are two arrays");
            pool.Return(first);
            Expect(true, ReferenceEquals(first, pool.Rent(64)), "the block given back is the next one out");
            Expect(false, ReferenceEquals(first, pool.Rent(64)), "and only once");
            pool.Return(null);
            Expect(0, pool.Kept, "nothing to give back after a failed read");
            pool.Return(first);
            pool.Return(second);
            pool.Return(new byte[64]);
            Expect(2, pool.Kept, "no more kept than its limit");
        }

        private static void ABlockOfAnotherSizeIsNeverHandedOut()
        {
            var pool = new BlockPool(8);
            pool.Return(new byte[64]);
            pool.Return(new byte[64]);
            var bigger = pool.Rent(96);
            Expect(96, bigger.Length, "a block of the new size");
            Expect(0, pool.Kept, "the blocks of the old size are dropped");
            pool.Return(bigger);
            Expect(64, pool.Rent(64).Length, "and never one of another size");
        }

        /// <summary>The inline std::string as SafeMemoryHandle.ReadStdString decoded it before 2026-11-10.</summary>
        private static string OldInlineAscii(StdString value)
        {
            var buffer = BitConverter.GetBytes(value.Buffer.ToInt64());
            var ret = Encoding.ASCII.GetString(buffer);
            buffer = BitConverter.GetBytes(value.ReservedBytes.ToInt64());
            ret += Encoding.ASCII.GetString(buffer);
            return value.Length < ret.Length ? ret[..value.Length] : string.Empty;
        }

        /// <summary>The inline std::wstring as SafeMemoryHandle.ReadStdWString decoded it before 2026-11-10.</summary>
        private static string OldInlineWide(StdWString value)
        {
            var buffer = BitConverter.GetBytes(value.Buffer.ToInt64());
            var ret = Encoding.Unicode.GetString(buffer);
            buffer = BitConverter.GetBytes(value.ReservedBytes.ToInt64());
            ret += Encoding.Unicode.GetString(buffer);
            return value.Length < ret.Length ? ret[..value.Length] : string.Empty;
        }

        /// <summary>The string of unknown length as SafeMemoryHandle.ReadUnicodeString decoded it before 2026-11-10.</summary>
        private static string OldWideZ(byte[] buffer)
        {
            var count = 0x00;
            for (var i = 0; i < buffer.Length - 2; i++)
            {
                if (buffer[i] == 0x00 && buffer[i + 1] == 0x00 && buffer[i + 2] == 0x00)
                {
                    count = i % 2 == 0 ? i : i + 1;
                    break;
                }
            }

            return count == 0 ? string.Empty : Encoding.Unicode.GetString(buffer, 0, count);
        }
    }
}
