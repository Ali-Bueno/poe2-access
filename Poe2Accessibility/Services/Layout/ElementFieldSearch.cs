namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where a UI element keeps its name, its label texts and its image texture, looked for over the whole object once
    ///     nothing fits near where they were measured (<see cref="OffsetSearch.Only"/>): the proof asked far away is two facts
    ///     that agree, at a single place. Pure: the blocks and the decoders are given, so the tests run it on synthetic memory.
    /// </summary>
    public static class ElementFieldSearch
    {
        /// <summary>Far from where it was measured, a name field is one that names two elements with two different known names.</summary>
        public const int WideNames = 2;

        /// <summary>How many different names of <paramref name="known"/> the <paramref name="nodes"/> read at the offset.</summary>
        public static int KnownNames(IReadOnlyList<byte[]> nodes, int offset, Func<byte[], int, string?> nameAt, IReadOnlyCollection<string> known) =>
            nodes.Select(node => nameAt(node, offset)).Where(name => name != null && known.Contains(name)).Distinct(StringComparer.Ordinal).Count();

        /// <summary>
        ///     The control name's offset: nearest <paramref name="measured"/> where one element carries a known name; else the
        ///     one place of the elements where two of them carry two different known names.
        /// </summary>
        public static int? Name(IReadOnlyList<byte[]> nodes, Func<byte[], int, string?> nameAt, IReadOnlyCollection<string> known, int measured, int radius, int step) =>
            OffsetSearch.NearThenWide(
                measured, radius, step, at => KnownNames(nodes, at, nameAt, known) >= 1,
                step, Limit(nodes), at => KnownNames(nodes, at, nameAt, known) >= WideNames);

        /// <summary>
        ///     How far the label text fields moved together: the shift nearest 0 under which <paramref name="minHits"/> labels
        ///     read (<paramref name="textAt"/>: the tagged text and two equal copies); else the one shift of
        ///     <paramref name="min"/>..<paramref name="limit"/> under which they do.
        /// </summary>
        public static int? TextShift(IReadOnlyList<byte[]> leaves, Func<byte[], int, bool> textAt, int radius, int step, int min, int limit, int minHits)
        {
            bool Fits(int shift) => leaves.Count(leaf => textAt(leaf, shift)) >= minHits;
            return OffsetSearch.First(0, radius, step, Fits, Math.Max(min, -radius)) ?? OffsetSearch.Only(0, min, limit, step, Fits);
        }

        /// <summary>
        ///     The image texture's offset: <paramref name="minHits"/> images lead through the pointer there to an art key
        ///     (<paramref name="leadsToArt"/>) and far fewer through the next slot (the smaller class's pointer sits one step
        ///     before the larger's); nearest <paramref name="measured"/>, else the one such place of the elements.
        /// </summary>
        public static int? Texture(IReadOnlyList<byte[]> leaves, Func<long, bool> leadsToArt, int measured, int radius, int step, int minHits)
        {
            var hits = new Dictionary<int, int>();
            int Hits(int offset)
            {
                if (!hits.TryGetValue(offset, out var count))
                {
                    count = offset < 0 ? 0 : leaves.Count(leaf => offset + sizeof(long) <= leaf.Length && BitConverter.ToInt64(leaf, offset) is var pointer && pointer != 0 && leadsToArt(pointer));
                    hits[offset] = count;
                }

                return count;
            }

            bool Fits(int offset) => Hits(offset) is var count && count >= minHits && Hits(offset + step) * 2 < count;
            return OffsetSearch.NearThenWide(measured, radius, step, Fits, step, Limit(leaves), Fits);
        }

        private static int Limit(IReadOnlyList<byte[]> blocks) => blocks.Count == 0 ? 0 : blocks.Max(block => block.Length);
    }
}
