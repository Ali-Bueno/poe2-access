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
    ///     When the image is searched for the static that holds the state object, and what a search settles, apart from
    ///     doing it. The place a pattern gave is looked at every <see cref="LookSeconds"/> for as long as the game runs: a
    ///     game that makes its state object late (it downloads a patch in its own window) is still read when it does.
    /// </summary>
    public static class StateSearchRules
    {
        /// <summary>How often the place the pattern gave is looked at; the first searches are this far apart too.</summary>
        public const double LookSeconds = 3;

        /// <summary>A place that may yet hold the state object is waited on this long: the game takes up to half a minute to build its states.</summary>
        public const double PatienceSeconds = 30;

        /// <summary>The first searches read the image's writable data; the later ones, this far apart, the whole image.</summary>
        public const int QuickSearches = 5;
        public const double SlowSeconds = 15;

        /// <summary>After this long the searches are <see cref="SeldomSeconds"/> apart: each reads the whole image, and may find nothing for hours.</summary>
        public const double KeenSeconds = 600;
        public const double SeldomSeconds = 60;

        /// <summary>
        ///     Whether the image is searched now: not while the pattern's place may yet hold the object (<paramref name="mayYetHold"/>:
        ///     empty, or an object still being built) and <see cref="PatienceSeconds"/> have not passed; then a few times in a
        ///     row, then less and less often, never given up.
        /// </summary>
        public static bool SearchDue(bool mayYetHold, double waited, double sinceSearch, int searches) =>
            !(mayYetHold && waited < PatienceSeconds) &&
            sinceSearch >= (searches < QuickSearches ? LookSeconds : waited < KeenSeconds ? SlowSeconds : SeldomSeconds);

        /// <summary>Whether search number <paramref name="searches"/> (from zero) reads the writable data only, not the whole image.</summary>
        public static bool DataOnly(int searches) => searches < QuickSearches;

        /// <summary>The one object the statics found hold; zero when they hold none, or several: two that both pass settle nothing.</summary>
        public static long OneObject(IEnumerable<long> objects)
        {
            var distinct = objects.Distinct().Take(2).ToList();
            return distinct.Count == 1 ? distinct[0] : 0;
        }

        /// <summary>
        ///     The state object's static the patterns give at attach: the first place that holds the object; else the only
        ///     place, unproven (the game builds its states a while after it starts); else none (zero).
        /// </summary>
        public static (long Address, bool Proven) Given(IReadOnlyList<long> places, Func<long, bool> holds)
        {
            var proven = places.FirstOrDefault(holds);
            return proven != 0 ? (proven, true) : (places.Count == 1 ? places[0] : 0, false);
        }

        /// <summary>
        ///     One look while the state object is not proven: the pattern's place now holds it (taken, or read again when it is
        ///     already the one in use); a place found by search stands, the pattern's still watched when it gave one; else the
        ///     image is searched when <see cref="SearchDue"/> says so.
        /// </summary>
        public static StateLook Look(bool givenHolds, bool givenInUse, bool gaveOne, bool mayYetHold, double waited, double sinceSearch, int searches) =>
            givenHolds ? (givenInUse ? StateLook.Rejudge : StateLook.TakeGiven)
            : !givenInUse ? (gaveOne ? StateLook.Watch : StateLook.Stop)
            : SearchDue(mayYetHold, waited, sinceSearch, searches) ? StateLook.Search : StateLook.Watch;

        /// <summary>What a search settles: the static of the one object found, unless it is the pattern's own place (the next look proves that); zero for nothing.</summary>
        public static long Settled(IReadOnlyList<(long Static, long Object)> found, long given) =>
            OneObject(found.Select(place => place.Object)) == 0 || found.Any(place => place.Static == given) ? 0 : found[0].Static;
    }

    /// <summary>What a look at the state object's static does.</summary>
    public enum StateLook
    {
        /// <summary>The pattern's place holds it and another is in use: it is taken.</summary>
        TakeGiven,

        /// <summary>The pattern's place, in use since the attach when it held nothing, holds it now: what it holds is read.</summary>
        Rejudge,

        /// <summary>A place found by search is in use and the pattern gave none to watch: the looks end.</summary>
        Stop,

        /// <summary>Nothing to do on this look.</summary>
        Watch,

        /// <summary>The image is searched.</summary>
        Search,
    }

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
