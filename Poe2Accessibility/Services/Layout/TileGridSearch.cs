namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The layout of the area's tile grid (GameLayout.TileGrid), found again by what its records hold when a game
    ///     update moves it (docs/compatibility.md): one record per sub-tile, row by row, so the record size is the grid's
    ///     bytes over its sub-tiles; one pointer field leads to a tile file whose path ends in ".tdt" (on half the
    ///     sub-tiles or more); three bytes give the sub-tile's x and y within its tile and the rotation selector, so two
    ///     neighbours in a row from the same file differ by one step in x or y (693 of 766 pairs in the Act 1 town) and
    ///     the selector stays within 0..7. Pure.
    /// </summary>
    public static class TileGridSearch
    {
        /// <summary>Sub-tiles sampled for the file pointer and path (the town has 990, large areas thousands).</summary>
        public const int Samples = 256;

        /// <summary>A record is never this large: no layout is judged from bytes that would make it so.</summary>
        public const int MaxStride = 0x200;

        /// <summary>A tile file's path within reach of where it was measured.</summary>
        public const int PathRadius = 0x40;

        public const int MaxRotation = 7;

        /// <summary>Neighbour pairs needed to judge the ids, and the share of them one step apart.</summary>
        public const int MinPairs = 10;
        public const double StepShare = 0.7;

        public const double NamedShare = 0.5;
        public const double RotationShare = 0.9;

        /// <summary>Where a record's fields sit: its size, the tile file pointer, the file's path, the first of the three bytes.</summary>
        public sealed record Layout(int Stride, int FileOffset, int PathOffset, int IdsOffset)
        {
            public override string ToString() => $"record 0x{this.Stride:X}, file +0x{this.FileOffset:X}, path +0x{this.PathOffset:X}, ids +0x{this.IdsOffset:X}";
        }

        /// <summary>
        ///     The layout, nearest to the measured one, that the grid's bytes fit; null when none does or the grid is too
        ///     small to judge. <paramref name="namesTile"/> says whether a pointer leads to a file naming a tile with its
        ///     path at an offset (it is asked once per distinct pointer and offset).
        /// </summary>
        public static Layout? Find(byte[] grid, int subTiles, int perRow, Layout measured, Func<long, int, bool> namesTile)
        {
            if (subTiles <= 0 || perRow <= 0 || grid.Length == 0 || grid.Length % subTiles != 0)
            {
                return null;
            }

            var stride = grid.Length / subTiles;
            if (stride % 8 != 0 || stride > MaxStride)
            {
                return null;
            }

            var known = new Dictionary<(long, int), bool>();
            bool Names(long pointer, int path)
            {
                if (!known.TryGetValue((pointer, path), out var names))
                {
                    names = pointer != 0 && namesTile(pointer, path);
                    known[(pointer, path)] = names;
                }

                return names;
            }

            var sampled = Enumerable.Range(0, Math.Min(Samples, subTiles)).Select(i => (int)((long)i * subTiles / Math.Min(Samples, subTiles))).ToList();
            long Pointer(int tile, int offset) => BitConverter.ToInt64(grid, (tile * stride) + offset);

            foreach (var file in OffsetSearch.Outward(Math.Min(measured.FileOffset, stride - 8), stride, 8).Where(at => at + 8 <= stride))
            {
                foreach (var path in OffsetSearch.Outward(measured.PathOffset, PathRadius, 8))
                {
                    if (sampled.Count(tile => Names(Pointer(tile, file), path)) < NamedShare * sampled.Count)
                    {
                        continue;
                    }

                    var named = Enumerable.Range(0, subTiles).Where(tile => Names(Pointer(tile, file), path)).ToList();
                    if (Ids(grid, stride, perRow, named, tile => Pointer(tile, file), measured.IdsOffset) is { } ids)
                    {
                        return new Layout(stride, file, path, ids);
                    }
                }
            }

            return null;
        }

        /// <summary>The first field that differs between two layouts (record size, file pointer, path, the three bytes), for the report.</summary>
        public static (int From, int To) FirstMoved(Layout measured, Layout found) =>
            new[] { (measured.Stride, found.Stride), (measured.FileOffset, found.FileOffset), (measured.PathOffset, found.PathOffset), (measured.IdsOffset, found.IdsOffset) }
                .FirstOrDefault(pair => pair.Item1 != pair.Item2);

        /// <summary>The first of the three bytes, nearest to where measured: in-range selectors and one-step neighbours.</summary>
        private static int? Ids(byte[] grid, int stride, int perRow, List<int> named, Func<int, long> file, int measured)
        {
            var isNamed = named.ToHashSet();
            var neighbours = named.Where(tile => tile % perRow != perRow - 1 && isNamed.Contains(tile + 1) && file(tile) == file(tile + 1)).ToList();
            if (neighbours.Count < MinPairs)
            {
                return null;
            }

            foreach (var at in OffsetSearch.Outward(Math.Min(measured, stride - 3), stride, 1).Where(at => at + 3 <= stride))
            {
                var turned = named.Count(tile => grid[(tile * stride) + at + 2] <= MaxRotation);
                if (turned < RotationShare * named.Count)
                {
                    continue;
                }

                var steps = neighbours.Count(tile =>
                {
                    var (here, next) = ((tile * stride) + at, ((tile + 1) * stride) + at);
                    return Math.Abs(grid[next] - grid[here]) + Math.Abs(grid[next + 1] - grid[here + 1]) == 1;
                });
                if (steps >= StepShare * neighbours.Count)
                {
                    return at;
                }
            }

            return null;
        }
    }
}
