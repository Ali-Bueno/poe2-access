namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where a passive node record keeps the edges that end at it and those that start at it
    ///     (<see cref="GameLayout.PassiveTree.RecordInEdgesOffset"/>), judged against the graph file's own edge list, which
    ///     says how many of each every record has: a vector there must hold that many edges, the first of them naming the
    ///     record at the right end. Two sources that do not depend on each other. Pure: the readings are given.
    /// </summary>
    public static class RecordEdgesSearch
    {
        /// <summary>Records with edges of a kind that must be looked at before that kind is judged.</summary>
        public const int MinRecords = 8;

        /// <summary>An MSVC vector: first, last, end.</summary>
        public const int VectorBytes = 3 * sizeof(long);

        /// <summary>The graph's list counts one edge in some thousands another way than the records do (5385 of 5386, 2026-10-24).</summary>
        private const double HoldShare = 0.9;

        private const int MaxEdgesPerRecord = 64;

        /// <summary>One record: its address, its first bytes, and how many edges of the graph's list end and start at it.</summary>
        public readonly record struct Sample(long Record, byte[] Block, int In, int Out);

        /// <summary>
        ///     Whether the vector at <paramref name="at"/> holds the edges that end at each record (<paramref name="ending"/>) or
        ///     start at it; null with too few records that have such edges. <paramref name="pointers"/> reads pointers of the game.
        /// </summary>
        public static bool? Holds(IReadOnlyList<Sample> samples, int at, bool ending, Func<long, int, long[]> pointers)
        {
            var judged = samples.Where(sample => (ending ? sample.In : sample.Out) > 0).ToList();
            if (judged.Count < MinRecords)
            {
                return null;
            }

            return judged.Count(sample => HoldsOn(sample, at, ending, pointers)) >= judged.Count * HoldShare;
        }

        /// <summary>
        ///     Both vectors, each nearest where it was measured within the records' bytes; null when either is judged and found
        ///     nowhere, or when there are too few records to judge.
        /// </summary>
        public static (int In, int Out)? Find(IReadOnlyList<Sample> samples, (int In, int Out) measured, int step, Func<long, int, long[]> pointers)
        {
            var limit = samples.Count == 0 ? 0 : samples.Min(sample => sample.Block.Length) - VectorBytes;
            int? Place(int near, bool ending) =>
                Holds(samples, near, ending, pointers) == null ? null : OffsetSearch.First(near, limit, step, at => at <= limit && Holds(samples, at, ending, pointers) == true);
            return Place(measured.In, true) is { } into && Place(measured.Out, false) is { } from && into != from ? (into, from) : null;
        }

        private static bool HoldsOn(Sample sample, int at, bool ending, Func<long, int, long[]> pointers)
        {
            if (at < 0 || at + VectorBytes > sample.Block.Length)
            {
                return false;
            }

            var (first, last) = (BitConverter.ToInt64(sample.Block, at), BitConverter.ToInt64(sample.Block, at + sizeof(long)));
            var count = ending ? sample.In : sample.Out;
            if (first == 0 || last - first != (long)count * sizeof(long) || count > MaxEdgesPerRecord)
            {
                return false;
            }

            // An edge is (from record, to record): the one this vector lists first must name the record at its end.
            return pointers(first, 1) is { Length: 1 } edge && pointers(edge[0], 2) is { Length: 2 } ends && ends[ending ? 1 : 0] == sample.Record;
        }
    }
}
