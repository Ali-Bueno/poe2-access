namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading;

    /// <summary>
    ///     The raster half of <see cref="MapTopology.Build"/>: the walkable grid cropped to its ground, pillars filled, specks
    ///     dropped, each cell's distance to the nearest wall (Felzenszwalb), and the skeleton (Zhang-Suen, then pruned).
    /// </summary>
    public sealed partial class MapTopology
    {
        private sealed partial class Builder
        {
            private const byte Open = 1;
            private const byte Seen = 2;
            private const byte Kept = 3;

            /// <summary>Blocked ground known to be no pillar: too large, or open to the crop's rim.</summary>
            private const byte Wide = 4;

            /// <summary>A distance standing for "no wall on this line yet", far above any squared distance in a grid.</summary>
            private const double Far = 1e20;

            private static readonly bool[] FirstPass = ThinningTable(first: true);
            private static readonly bool[] SecondPass = ThinningTable(first: false);
            private static readonly bool[] Redundant = RedundantTable();

            private readonly CancellationToken cancel;

            /// <summary>The grid cell of the crop's corner: the crop keeps one blocked cell around the ground, so no walk leaves it.</summary>
            private readonly int left;
            private readonly int top;
            private readonly int width;
            private readonly int height;

            /// <summary>The eight neighbours' offsets, clockwise from north (Zhang-Suen's P2 to P9).</summary>
            private readonly int[] around;
            private readonly byte[] walk;
            private readonly byte[] skeleton;
            private readonly float[] distance;
            private readonly int[] labels;

            /// <summary>The walks' queue: a pillar's cells at first, then as many as there are walkable cells.</summary>
            private int[] queue = new int[PillarCells];
            private List<int> skeletonCells = new();

            private Builder(ReadOnlySpan<byte> grid, int bytesPerRow, int rows, (int Left, int Top, int Right, int Bottom) box, CancellationToken cancel)
            {
                this.cancel = cancel;
                (this.left, this.top) = (box.Left - 1, box.Top - 1);
                (this.width, this.height) = (box.Right - box.Left + 3, box.Bottom - box.Top + 3);
                var w = this.width;
                this.around = new[] { -w, -w + 1, 1, w + 1, w, w - 1, -1, -w - 1 };
                var cells = w * this.height;
                this.walk = new byte[cells];
                this.skeleton = new byte[cells];
                this.distance = new float[cells];
                this.labels = new int[cells];
                for (var y = box.Top; y <= box.Bottom; y++)
                {
                    var row = grid.Slice(y * bytesPerRow, bytesPerRow);
                    var at = ((y - this.top) * w) - this.left;
                    for (var x = box.Left; x <= box.Right; x++)
                    {
                        var pair = row[x >> 1];
                        if (((x & 1) == 0 ? pair & 15 : pair >> 4) != 0)
                        {
                            this.walk[at + x] = Open;
                        }
                    }
                }
            }

            public static Draft Run(ReadOnlySpan<byte> grid, int bytesPerRow, int rows, CancellationToken cancel)
            {
                var box = Bounds(grid, bytesPerRow, rows);
                if (box.Right < box.Left)
                {
                    return new Draft();
                }

                var builder = new Builder(grid, bytesPerRow, rows, box, cancel);
                builder.FillPillars();
                builder.DropSpecks();
                builder.MeasureDistances();
                builder.Thin();
                builder.Prune();
                return builder.Assemble();
            }

            /// <summary>The smallest box (inclusive cells) holding every walkable cell; right below left when there is none.</summary>
            private static (int Left, int Top, int Right, int Bottom) Bounds(ReadOnlySpan<byte> grid, int bytesPerRow, int rows)
            {
                var (minX, minY, maxX, maxY) = (int.MaxValue, int.MaxValue, -1, -1);
                for (var y = 0; y < rows; y++)
                {
                    var row = grid.Slice(y * bytesPerRow, bytesPerRow);
                    var first = row.IndexOfAnyExcept((byte)0);
                    if (first < 0)
                    {
                        continue;
                    }

                    var last = row.LastIndexOfAnyExcept((byte)0);
                    minX = Math.Min(minX, (row[first] & 15) != 0 ? 2 * first : (2 * first) + 1);
                    maxX = Math.Max(maxX, (row[last] >> 4) != 0 ? (2 * last) + 1 : 2 * last);
                    minY = Math.Min(minY, y);
                    maxY = y;
                }

                return (minX, minY, maxX, maxY);
            }

            /// <summary>
            ///     Fills enclosed obstacles smaller than <see cref="PillarCells"/>: they change no way, only split corridors around
            ///     them. A walk stops at that size or at known wide ground, so it never queues more than a pillar.
            /// </summary>
            private void FillPillars()
            {
                var (w, h) = (this.width, this.height);
                for (var y = 0; y < h; y++)
                {
                    this.cancel.ThrowIfCancellationRequested();
                    for (var x = 0; x < w; x++)
                    {
                        var start = (y * w) + x;
                        if (this.walk[start] != 0)
                        {
                            continue;
                        }

                        this.walk[start] = Seen;
                        this.queue[0] = start;
                        var count = 1;
                        var wide = false;
                        for (var head = 0; head < count && !wide; head++)
                        {
                            var p = this.queue[head];
                            var (px, py) = (p % w, p / w);
                            wide = px == 0 || py == 0 || px == w - 1 || py == h - 1
                                || this.PushBlocked(p - 1, ref count) || this.PushBlocked(p + 1, ref count)
                                || this.PushBlocked(p - w, ref count) || this.PushBlocked(p + w, ref count);
                        }

                        for (var i = 0; i < count; i++)
                        {
                            this.walk[this.queue[i]] = wide ? Wide : Open;
                        }
                    }
                }

                var walkable = 0;
                for (var p = 0; p < this.walk.Length; p++)
                {
                    this.walk[p] = this.walk[p] == Open ? Open : (byte)0;
                    walkable += this.walk[p];
                }

                this.queue = new int[Math.Max(walkable, PillarCells)];
            }

            /// <summary>Queues a blocked neighbour; true when the obstacle proves wide (it meets wide ground, or outgrows a pillar).</summary>
            private bool PushBlocked(int q, ref int count)
            {
                if (this.walk[q] == Wide)
                {
                    return true;
                }

                if (this.walk[q] != 0)
                {
                    return false;
                }

                if (count == PillarCells)
                {
                    return true;
                }

                this.walk[q] = Seen;
                this.queue[count++] = q;
                return false;
            }

            /// <summary>Drops pieces of ground smaller than <see cref="MinPieceCells"/> (the crop's blocked rim keeps every walk inside it).</summary>
            private void DropSpecks()
            {
                for (var start = 0; start < this.walk.Length; start++)
                {
                    if (this.walk[start] != Open)
                    {
                        continue;
                    }

                    this.walk[start] = Seen;
                    this.queue[0] = start;
                    var count = 1;
                    for (var head = 0; head < count; head++)
                    {
                        var p = this.queue[head];
                        foreach (var offset in this.around)
                        {
                            if (this.walk[p + offset] == Open)
                            {
                                this.walk[p + offset] = Seen;
                                this.queue[count++] = p + offset;
                            }
                        }
                    }

                    var mark = count >= MinPieceCells ? Kept : (byte)0;
                    for (var i = 0; i < count; i++)
                    {
                        this.walk[this.queue[i]] = mark;
                    }
                }

                for (var p = 0; p < this.walk.Length; p++)
                {
                    this.walk[p] = this.walk[p] == Kept ? Open : (byte)0;
                }
            }

            /// <summary>Each cell's Euclidean distance to the nearest blocked cell: Felzenszwalb's lower envelope, columns then rows.</summary>
            private void MeasureDistances()
            {
                var (w, h) = (this.width, this.height);
                var size = Math.Max(w, h);
                var f = new double[size];
                var d = new double[size];
                var v = new int[size];
                var z = new double[size + 1];
                for (var x = 0; x < w; x++)
                {
                    for (var y = 0; y < h; y++)
                    {
                        f[y] = this.walk[(y * w) + x] != 0 ? Far : 0;
                    }

                    Envelope(f, d, v, z, h);
                    for (var y = 0; y < h; y++)
                    {
                        this.distance[(y * w) + x] = (float)d[y];
                    }
                }

                this.cancel.ThrowIfCancellationRequested();
                for (var y = 0; y < h; y++)
                {
                    var row = y * w;
                    for (var x = 0; x < w; x++)
                    {
                        f[x] = this.distance[row + x];
                    }

                    Envelope(f, d, v, z, w);
                    for (var x = 0; x < w; x++)
                    {
                        this.distance[row + x] = MathF.Sqrt((float)d[x]);
                    }
                }
            }

            private static void Envelope(double[] f, double[] d, int[] v, double[] z, int n)
            {
                var k = 0;
                (v[0], z[0], z[1]) = (0, double.NegativeInfinity, double.PositiveInfinity);
                for (var q = 1; q < n; q++)
                {
                    var s = (f[q] + ((double)q * q) - f[v[k]] - ((double)v[k] * v[k])) / ((2.0 * q) - (2.0 * v[k]));
                    while (s <= z[k])
                    {
                        k--;
                        s = (f[q] + ((double)q * q) - f[v[k]] - ((double)v[k] * v[k])) / ((2.0 * q) - (2.0 * v[k]));
                    }

                    k++;
                    (v[k], z[k], z[k + 1]) = (q, s, double.PositiveInfinity);
                }

                k = 0;
                for (var q = 0; q < n; q++)
                {
                    while (z[k + 1] < q)
                    {
                        k++;
                    }

                    d[q] = ((double)(q - v[k]) * (q - v[k])) + f[v[k]];
                }
            }

            /// <summary>Zhang-Suen thinning, its two passes walking only the cells on the edge of what is left.</summary>
            private void Thin()
            {
                Array.Copy(this.walk, this.skeleton, this.walk.Length);
                var candidates = new List<int>();
                for (var p = 0; p < this.walk.Length; p++)
                {
                    if (this.skeleton[p] != 0 && this.Code(p) != 255)
                    {
                        candidates.Add(p);
                    }
                }

                var removed = new List<int>();
                var next = new List<int>();
                var thinning = true;
                while (thinning)
                {
                    this.cancel.ThrowIfCancellationRequested();
                    thinning = false;
                    foreach (var table in new[] { FirstPass, SecondPass })
                    {
                        removed.Clear();
                        foreach (var c in candidates)
                        {
                            if (table[this.Code(c)])
                            {
                                removed.Add(c);
                            }
                        }

                        foreach (var r in removed)
                        {
                            this.skeleton[r] = 0;
                        }

                        next.Clear();
                        foreach (var c in candidates)
                        {
                            this.Queue(c, next);
                        }

                        foreach (var r in removed)
                        {
                            foreach (var offset in this.around)
                            {
                                this.Queue(r + offset, next);
                            }
                        }

                        foreach (var c in next)
                        {
                            this.skeleton[c] = Open;
                        }

                        (candidates, next) = (next, candidates);
                        thinning |= removed.Count > 0;
                    }
                }

                this.skeletonCells = candidates;
            }

            /// <summary>Adds a skeleton cell to the next candidates once (marked <see cref="Seen"/> until the list is done).</summary>
            private void Queue(int p, List<int> next)
            {
                if (this.skeleton[p] == Open)
                {
                    this.skeleton[p] = Seen;
                    next.Add(p);
                }
            }

            /// <summary>Cuts end branches too short to be ways (study §1), round after round, keeping the skeleton one cell thin.</summary>
            private void Prune()
            {
                var path = new List<int>();
                for (var round = 0; round < MaxPruneRounds; round++)
                {
                    this.cancel.ThrowIfCancellationRequested();
                    this.Slim();
                    var forks = this.LabelJunctions(1);
                    var segments = this.TraceSegments(path);
                    var cut = false;
                    foreach (var segment in segments)
                    {
                        var fork = segment.A > 0 && segment.B == Free ? segment.A : segment.B > 0 && segment.A == Free ? segment.B : 0;
                        if (fork > 0 && segment.Count < (SpurWidthFactor * forks[fork - 1].Reach) + SpurFloorCells)
                        {
                            for (var i = 0; i < segment.Count; i++)
                            {
                                this.skeleton[path[segment.Start + i]] = 0;
                            }

                            cut = true;
                        }
                    }

                    this.ClearLabels();
                    if (!cut)
                    {
                        break;
                    }
                }

                this.Slim();
            }

            /// <summary>Removes skeleton cells whose removal changes no connection (staircase corners), so three neighbours mean a fork.</summary>
            private void Slim()
            {
                bool changed;
                do
                {
                    changed = false;
                    foreach (var p in this.skeletonCells)
                    {
                        if (this.skeleton[p] != 0 && Redundant[this.Code(p)])
                        {
                            this.skeleton[p] = 0;
                            changed = true;
                        }
                    }
                }
                while (changed);
                this.skeletonCells.RemoveAll(p => this.skeleton[p] == 0);
            }

            /// <summary>The eight neighbours as bits, clockwise from north (bit 0).</summary>
            private int Code(int p)
            {
                var s = this.skeleton;
                var w = this.width;
                return (s[p - w] != 0 ? 1 : 0) | (s[p - w + 1] != 0 ? 2 : 0) | (s[p + 1] != 0 ? 4 : 0) | (s[p + w + 1] != 0 ? 8 : 0)
                    | (s[p + w] != 0 ? 16 : 0) | (s[p + w - 1] != 0 ? 32 : 0) | (s[p - 1] != 0 ? 64 : 0) | (s[p - w - 1] != 0 ? 128 : 0);
            }

            private int Neighbours(int p) => BitOperations.PopCount((uint)this.Code(p));

            /// <summary>Zhang-Suen's deletion rule for one pass, per neighbour code.</summary>
            private static bool[] ThinningTable(bool first)
            {
                var table = new bool[256];
                for (var code = 0; code < 256; code++)
                {
                    var on = new bool[8];
                    for (var k = 0; k < 8; k++)
                    {
                        on[k] = ((code >> k) & 1) != 0;
                    }

                    var count = BitOperations.PopCount((uint)code);
                    var rises = 0;
                    for (var k = 0; k < 8; k++)
                    {
                        rises += !on[k] && on[(k + 1) % 8] ? 1 : 0;
                    }

                    var blocked = first
                        ? (on[0] && on[2] && on[4]) || (on[2] && on[4] && on[6])
                        : (on[0] && on[2] && on[6]) || (on[0] && on[4] && on[6]);
                    table[code] = count >= 2 && count <= 6 && rises == 1 && !blocked;
                }

                return table;
            }

            /// <summary>A cell is redundant when it joins nothing (Yokoi's 8-connectivity number is 1) and does not end a line.</summary>
            private static bool[] RedundantTable()
            {
                // Yokoi's x1..x8 run counter-clockwise from east; the codes run clockwise from north.
                var bits = new[] { 2, 1, 0, 7, 6, 5, 4, 3 };
                var table = new bool[256];
                for (var code = 0; code < 256; code++)
                {
                    int Off(int k) => ((code >> bits[k % 8]) & 1) == 0 ? 1 : 0;
                    var number = 0;
                    for (var k = 0; k < 8; k += 2)
                    {
                        number += Off(k) - (Off(k) * Off(k + 1) * Off(k + 2));
                    }

                    table[code] = number == 1 && BitOperations.PopCount((uint)code) >= 2;
                }

                return table;
            }
        }
    }
}
