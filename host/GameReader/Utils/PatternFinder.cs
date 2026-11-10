// <copyright file="PatternFinder.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameOffsets;
    using GameReader.Utils.CodeReader;

    /// <summary>Reads the game's image out of its process: its sections, and where the static patterns match in its code.</summary>
    internal static class PatternFinder
    {
        // The section table of a PE sits in its first page.
        private const int HeaderBytes = 0x1000;
        private const int Chunk = 1 << 22;
        private const int Piece = 1 << 16;

        /// <summary>The image's sections as its loaded header lists them; empty when the header cannot be read.</summary>
        internal static IReadOnlyList<PeSection> Sections(SafeMemoryHandle reader, IntPtr module, int size) =>
            PeImage.SectionsOf(reader.ReadMemoryArray<byte>(module, Math.Min(HeaderBytes, size)));

        /// <summary>
        ///     Every place each pattern of <see cref="StaticOffsetsPatterns"/> matches in <paramref name="ranges"/> of the image,
        ///     as the offset of the displacement it marks.
        /// </summary>
        internal static Dictionary<string, List<long>> Find(SafeMemoryHandle reader, IntPtr module, IReadOnlyList<(long Start, long Size)> ranges)
        {
            var patterns = StaticOffsetsPatterns.Patterns;
            var hits = patterns.Select(_ => new SortedSet<long>()).ToArray();

            // A match may lie across two reads: each reads as far into the next as the longest pattern reaches.
            var overlap = patterns.Max(pattern => pattern.Data.Length) - 1;
            foreach (var range in ranges)
            {
                foreach (var (offset, bytes) in Read(reader, module.ToInt64(), range, overlap))
                {
                    for (var index = 0; index < patterns.Length; index++)
                    {
                        PatternScan.Hits(bytes, patterns[index], offset, hits[index]);
                    }
                }
            }

            var found = new Dictionary<string, List<long>>();
            for (var index = 0; index < patterns.Length; index++)
            {
                found[patterns[index].Name] = hits[index].Select(hit => hit + patterns[index].BytesToSkip).ToList();
                if (hits[index].Count != 1)
                {
                    Console.WriteLine($"[PatternFinder] {patterns[index].Name}: {hits[index].Count} matches, one expected. {patterns[index]}");
                }
            }

            return found;
        }

        /// <summary>
        ///     The bytes of <paramref name="range"/>, a few megabytes at a time, each read <paramref name="overlap"/> bytes
        ///     into the next. A read with a page that cannot be read fails whole, so one that fails is taken again in small pieces.
        /// </summary>
        internal static IEnumerable<(long Offset, byte[] Bytes)> Read(SafeMemoryHandle reader, long module, (long Start, long Size) range, int overlap)
        {
            var end = range.Start + range.Size;
            for (var at = range.Start; at < end; at += Chunk)
            {
                var want = (int)Math.Min(Chunk + overlap, end - at);
                var bytes = reader.ReadMemoryArray<byte>(new IntPtr(module + at), want);
                if (bytes.Length > 0)
                {
                    yield return (at, bytes);
                    continue;
                }

                for (var piece = at; piece < at + want; piece += Piece)
                {
                    var part = reader.ReadMemoryArray<byte>(new IntPtr(module + piece), (int)Math.Min(Piece + overlap, end - piece));
                    if (part.Length > 0)
                    {
                        yield return (piece, part);
                    }
                }
            }
        }
    }
}
