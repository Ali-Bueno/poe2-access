namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>The search state of <see cref="RoutePlanner"/>: the per-thread workspaces and their pool, and the jump scans over the padded cell array.</summary>
    public static partial class RoutePlanner
    {
        /// <summary>Most workspaces kept between searches: a workspace holds one and a half bytes a cell (50 MB on a Trial floor), so one per pool thread that ever searched was unbounded.</summary>
        public const int MaxPooledWorkspaces = 2;

        private static readonly Stack<Workspace> Pool = new();

        /// <summary>Workspaces kept for the next search (the tests check the bound).</summary>
        public static int PooledWorkspaces { get { lock (Pool) { return Pool.Count; } } }

        /// <summary>Another area: the workspaces sized for the last one's grid are let go.</summary>
        public static void ReleaseWorkspaces()
        {
            lock (Pool)
            {
                Pool.Clear();
            }
        }

        private static Workspace Rent()
        {
            lock (Pool)
            {
                if (Pool.Count > 0)
                {
                    return Pool.Pop();
                }
            }

            return new Workspace();
        }

        private static void Return(Workspace space)
        {
            lock (Pool)
            {
                if (Pool.Count < MaxPooledWorkspaces)
                {
                    Pool.Push(space);
                }
            }
        }

        /// <summary>Per-thread search state, kept between searches (fresh arrays per search cost more than the search).</summary>
        private sealed class Workspace
        {
            public readonly PriorityQueue<int, double> Open = new();
            public readonly Dictionary<int, (float Cost, int Parent)> Nodes = new();
            public int Width;
            public int Height;

            /// <summary>Cells the failed pocket searches of the current route request flooded, and those of the flood under way.</summary>
            public readonly HashSet<int> Flooded = new();
            public readonly HashSet<int> Flooding = new();
            private readonly Scanner scanner = new();

            /// <summary>True when a failed pocket search of the current route request already flooded the cell.</summary>
            public bool Searched(int index) => this.Flooded.Contains(index);

            /// <summary>One byte per cell with a rock border all round (no bounds checks in the scans); rebuilt only when the grid changed.</summary>
            public byte[] Cells = Array.Empty<byte>();

            public int Stride;
            private byte[] packed = Array.Empty<byte>();
            private int packedRow;

            public void Prepare(byte[] grid, int bytesPerRow)
            {
                var width = bytesPerRow * 2;
                var height = (grid.Length + bytesPerRow - 1) / bytesPerRow;
                (this.Width, this.Height, this.Stride) = (width, height, width + 2);
                this.Flooded.Clear();
                // Cells[(y + 1) * Stride + x + 1] is 1 when the cell is walkable. Only the rows that changed are decoded
                // (a barrier opening touches a few rows; the whole grid is a few milliseconds).
                var same = this.packedRow == bytesPerRow && this.packed.Length == grid.Length && this.Cells.Length == this.Stride * (height + 2);
                var cells = same ? this.Cells : new byte[this.Stride * (height + 2)];
                var copy = same ? this.packed : new byte[grid.Length];
                for (var y = 0; y < height; y++)
                {
                    var length = Math.Min(bytesPerRow, grid.Length - (y * bytesPerRow));
                    var now = grid.AsSpan(y * bytesPerRow, length);
                    if (same && now.SequenceEqual(copy.AsSpan(y * bytesPerRow, length)))
                    {
                        continue;
                    }

                    now.CopyTo(copy.AsSpan(y * bytesPerRow));
                    var at = ((y + 1) * this.Stride) + 1;
                    for (var i = 0; i < length; i++)
                    {
                        var pair = now[i];
                        cells[at++] = (byte)((pair & 0xF) != 0 ? 1 : 0);
                        cells[at++] = (byte)((pair >> 4) != 0 ? 1 : 0);
                    }
                }

                (this.Cells, this.packed, this.packedRow) = (cells, copy, bytesPerRow);
            }

            public Scanner Begin(bool track, (int X, int Y) goal)
            {
                this.Nodes.Clear();
                this.Open.Clear();
                this.scanner.Reset(this, track, goal);
                return this.scanner;
            }
        }

        /// <summary>
        ///     The jump scans of one search: straight and diagonal runs that stop where the way can turn. They walk the
        ///     padded cell array by position (a rock border all round, so no bounds checks), and nodes leave it as
        ///     plain cell indices (y * width + x).
        /// </summary>
        private sealed class Scanner
        {
            private byte[] cells = Array.Empty<byte>();
            private int stride;
            private int width;
            private int goalX;
            private int goalY;
            private int goalPosition;
            private bool track;

            public int Goal;
            public int Anchor;
            public int Closest;
            public int ClosestAnchor;
            public int ClosestVia;
            public float ClosestDistance;
            public int Looked;

            public void Reset(Workspace space, bool trackClosest, (int X, int Y) goal)
            {
                (this.cells, this.stride, this.width, this.track) = (space.Cells, space.Stride, space.Width, trackClosest);
                (this.goalX, this.goalY, this.Goal) = (goal.X, goal.Y, (goal.Y * space.Width) + goal.X);
                this.goalPosition = ((goal.Y + 1) * space.Stride) + goal.X + 1;
                (this.Anchor, this.Closest, this.ClosestAnchor, this.ClosestVia, this.Looked) = (-1, -1, -1, -1, 0);
            }

            /// <summary>
            ///     Whether a move is worth trying from a node reached heading (pdx, pdy): the natural ways on and the
            ///     forced ones beside a wall the parent could not go round (all eight at the start).
            /// </summary>
            public bool Wanted(int x, int y, int dx, int dy, int pdx, int pdy)
            {
                if (pdx == 0 && pdy == 0)
                {
                    return true;
                }

                if (pdx != 0 && pdy != 0)
                {
                    return (dx == pdx || dx == 0) && (dy == pdy || dy == 0); // diagonal arrival: on, or along either side
                }

                if (dx == pdx && dy == pdy)
                {
                    return true;
                }

                // Straight arrival: sideways only where the cell behind that side is rock, and the diagonal that way.
                return pdx != 0
                    ? dy != 0 && (dx == 0 || dx == pdx) && this.Free(x, y + dy) && !this.Free(x - pdx, y + dy)
                    : dx != 0 && (dy == 0 || dy == pdy) && this.Free(x + dx, y) && !this.Free(x + dx, y - pdy);
            }

            /// <summary>The next cell along a diagonal run that can turn (or the goal), -1 at a wall.</summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public int JumpDiagonal(int x, int y, int dx, int dy)
            {
                ref var c = ref MemoryMarshal.GetArrayDataReference(this.cells);
                var (startX, startY) = (x, y);
                var position = ((y + 1) * this.stride) + x + 1;
                var (alongX, alongY) = (dx, dy * this.stride);
                var step = alongX + alongY;
                var steps = 0;
                var found = -1;
                while (true)
                {
                    position += step;
                    if (Unsafe.Add(ref c, position) == 0 || Unsafe.Add(ref c, position - alongX) == 0 || Unsafe.Add(ref c, position - alongY) == 0)
                    {
                        break;
                    }

                    steps++;
                    (x, y) = (x + dx, y + dy);
                    if (position == this.goalPosition ||
                        this.JumpStraight(x, y, dx, 0, (y * this.width) + x) >= 0 || this.JumpStraight(x, y, 0, dy, (y * this.width) + x) >= 0)
                    {
                        found = (y * this.width) + x;
                        break;
                    }
                }

                this.Looked += steps;
                if (this.track && steps > 0)
                {
                    this.Consider(startX, startY, dx, dy, steps, -1);
                }

                return found;
            }

            /// <summary>
            ///     The next cell along a straight run where the way can turn (or the goal), -1 at a wall.
            ///     <paramref name="diagonalCell"/> is the diagonal run's cell this one leaves from, -1 from a node.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public int JumpStraight(int x, int y, int dx, int dy, int diagonalCell)
            {
                ref var c = ref MemoryMarshal.GetArrayDataReference(this.cells);
                var position = ((y + 1) * this.stride) + x + 1;
                var step = dx != 0 ? dx : dy * this.stride;
                var side = dx != 0 ? this.stride : 1;
                var steps = 0;
                var found = -1;
                while (true)
                {
                    position += step;
                    if (Unsafe.Add(ref c, position) == 0)
                    {
                        break;
                    }

                    steps++;

                    // Forced: a free cell beside this one whose neighbour behind it is rock.
                    if (position == this.goalPosition ||
                        (Unsafe.Add(ref c, position + side) != 0 && Unsafe.Add(ref c, position - step + side) == 0) ||
                        (Unsafe.Add(ref c, position - side) != 0 && Unsafe.Add(ref c, position - step - side) == 0))
                    {
                        found = ((((position / this.stride) - 1) * this.width) + (position % this.stride)) - 1;
                        break;
                    }
                }

                this.Looked += steps;
                if (this.track && steps > 0)
                {
                    this.Consider(x, y, dx, dy, steps, diagonalCell);
                }

                return found;
            }

            private bool Free(int x, int y) => this.cells[((y + 1) * this.stride) + x + 1] != 0;

            /// <summary>
            ///     The cell of a run (<paramref name="steps"/> cells from (x, y) along (dx, dy)) nearest the goal. The
            ///     distance along a straight line only bends where one axis reaches the goal's, so the ends and those
            ///     two points are all there is to try.
            /// </summary>
            private void Consider(int x, int y, int dx, int dy, int steps, int via)
            {
                Try(1);
                Try(steps);
                if (dx != 0)
                {
                    Try((this.goalX - x) / dx);
                }

                if (dy != 0)
                {
                    Try((this.goalY - y) / dy);
                }

                void Try(int t)
                {
                    t = Math.Clamp(t, 1, steps);
                    var (cx, cy) = (x + (dx * t), y + (dy * t));
                    var left = Heuristic(cx, cy, this.goalX, this.goalY);
                    if (left < this.ClosestDistance)
                    {
                        (this.Closest, this.ClosestDistance, this.ClosestAnchor, this.ClosestVia) = ((cy * this.width) + cx, left, this.Anchor, via);
                    }
                }
            }
        }
    }
}
