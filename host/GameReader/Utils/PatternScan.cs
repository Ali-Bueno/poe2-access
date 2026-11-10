// <copyright file="PatternScan.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameOffsets;
    using GameReader.Utils.CodeReader;

    /// <summary>
    ///     The rules of looking for a byte pattern in the game's image, apart from reading it: every place a pattern
    ///     matches (one that matches twice proves nothing), which parts of the image to look in, and which of two patterns'
    ///     answers to believe.
    /// </summary>
    public static class PatternScan
    {
        /// <summary>Adds to <paramref name="into"/> every place <paramref name="pattern"/> matches in <paramref name="bytes"/>, counted from <paramref name="origin"/>.</summary>
        public static void Hits(ReadOnlySpan<byte> bytes, Pattern pattern, long origin, ISet<long> into)
        {
            // The longest run of fixed bytes is looked for first (a vectorised search); a pattern of wildcards alone says nothing.
            var (runAt, runLength) = LongestFixedRun(pattern.Mask);
            if (runLength == 0)
            {
                return;
            }

            var run = pattern.Data.AsSpan(runAt, runLength);
            var length = pattern.Data.Length;
            for (var from = runAt; from + (length - runAt) <= bytes.Length;)
            {
                var found = bytes[from..].IndexOf(run);
                if (found < 0)
                {
                    return;
                }

                var start = from + found - runAt;
                if (start + length > bytes.Length)
                {
                    return;
                }

                if (Matches(bytes.Slice(start, length), pattern))
                {
                    into.Add(origin + start);
                }

                from += found + 1;
            }
        }

        /// <summary>
        ///     The parts of an image of <paramref name="imageSize"/> bytes to look in: the sections that are
        ///     <paramref name="wanted"/>, or the whole image when its header names none.
        /// </summary>
        public static List<(long Start, long Size)> Ranges(IReadOnlyList<PeSection> sections, long imageSize, Func<PeSection, bool> wanted)
        {
            var ranges = sections.Where(section => wanted(section) && section.Start >= 0 && section.Start < imageSize && section.VirtualSize > 0)
                .Select(section => (section.Start, Math.Min(section.VirtualSize, imageSize - section.Start))).ToList();
            return ranges.Count > 0 ? ranges : new List<(long, long)> { (0, imageSize) };
        }

        /// <summary>
        ///     The one address two patterns for the same static give: the one both name, else the only answer of the first,
        ///     else the only answer of the second; zero when neither settles it.
        /// </summary>
        public static long Pick(IReadOnlyCollection<long> first, IReadOnlyCollection<long> second)
        {
            var both = first.Intersect(second).ToList();
            return both.Count == 1 ? both[0] : first.Count == 1 ? first.First() : second.Count == 1 ? second.First() : 0;
        }

        /// <summary>
        ///     Where the instruction whose 32-bit displacement sits at <paramref name="at"/> points: displacements count
        ///     from the end of their own four bytes. Negative when it points outside an image of <paramref name="imageSize"/> bytes,
        ///     which no static of the game does.
        /// </summary>
        public static long Target(long at, int displacement, long imageSize)
        {
            var target = at + sizeof(int) + displacement;
            return target >= 0 && target < imageSize ? target : -1;
        }

        private static bool Matches(ReadOnlySpan<byte> bytes, Pattern pattern)
        {
            for (var index = 0; index < bytes.Length; index++)
            {
                if (pattern.Mask[index] && bytes[index] != pattern.Data[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static (int At, int Length) LongestFixedRun(bool[] mask)
        {
            var (bestAt, bestLength, at) = (0, 0, 0);
            while (at < mask.Length)
            {
                var end = at;
                while (end < mask.Length && mask[end])
                {
                    end++;
                }

                if (end - at > bestLength)
                {
                    (bestAt, bestLength) = (at, end - at);
                }

                at = end + 1;
            }

            return (bestAt, bestLength);
        }
    }
}
