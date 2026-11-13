namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where an entity's Positioned component keeps its grid cell, found from entities whose place is known another way
    ///     (their Render component): the one pair of integers that is every sample's cell. Nothing is assumed of the layout,
    ///     so a patch that moves the field is followed (docs/compatibility.md). Pure.
    /// </summary>
    public static class PositionedGridSearch
    {
        /// <summary>
        ///     How much of the component is searched (bytes): on 0.5.5f the cell lies at +0x444 and the world place, as two
        ///     floats, at +0x490 (six of the Ardura caravan's people, dev command <c>positioned</c>, 2026-11-13).
        /// </summary>
        public const int SearchBytes = 0x800;

        /// <summary>Samples in at least this many different cells: two entities can share a number by chance, three in a row do not.</summary>
        public const int MinCells = 3;

        /// <summary>A body drawn between two cells is at most this far (cells) from the cell it is counted in.</summary>
        public const int ToleranceCells = 1;

        /// <summary>The offset of the cell's X (its Y follows), or null when the samples are too few or no pair fits them all.</summary>
        public static int? Find(IReadOnlyList<(byte[] Bytes, int X, int Y)> samples)
        {
            if (samples.Select(sample => (sample.X, sample.Y)).Distinct().Count() < MinCells)
            {
                return null;
            }

            var length = samples.Min(sample => sample.Bytes.Length);
            for (var offset = 0; offset + (2 * sizeof(int)) <= length; offset += sizeof(int))
            {
                if (samples.All(sample => Near(BitConverter.ToInt32(sample.Bytes, offset), sample.X) && Near(BitConverter.ToInt32(sample.Bytes, offset + sizeof(int)), sample.Y)))
                {
                    return offset;
                }
            }

            return null;
        }

        private static bool Near(int read, int expected) => Math.Abs((long)read - expected) <= ToleranceCells;
    }
}
