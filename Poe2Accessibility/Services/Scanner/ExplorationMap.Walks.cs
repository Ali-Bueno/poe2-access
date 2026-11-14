namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>The walks of <see cref="ExplorationMap"/>: where a look or a route leaves the seen ground, and the grid walking behind it.</summary>
    public sealed partial class ExplorationMap
    {
        /// <summary>
        ///     The straight line from <paramref name="from"/> along <paramref name="axis"/> leaves the seen ground within
        ///     <paramref name="reach"/> cells, onto ground the player can reach (as of the last <see cref="Frontier"/>):
        ///     ground that goes on while any is left, then what soon ends, pockets last. The line alone, not what lies beside it: ground to a side of the way
        ///     the player came is not ahead on the way back (docs/radar.md).
        /// </summary>
        public bool LeavesTheSeen(Vector2 from, Vector2 axis, float reach) => this.UnseenAlong(from, axis, reach) != null;

        /// <summary>
        ///     What that line leaves the seen ground onto, as walking past it shows a sighted player on the map: ground that goes
        ///     on (the radar's two pips), or ground that soon ends (one pip: a room, a short branch, and a nook once nothing
        ///     else is left); null when it stays on seen ground. The largest the line meets within reach.
        /// </summary>
        public UnseenSize? UnseenAlong(Vector2 from, Vector2 axis, float reach)
        {
            if (this.ground.Reachable.Length != this.seen.Length || axis.LengthSquared() <= 0)
            {
                return null;
            }

            UnseenSize? met = null;

            var along = Vector2.Normalize(axis);
            const float step = BlockCells / 2f; // no block the line crosses is stepped over
            for (var distance = step; distance <= reach; distance += step)
            {
                var cell = from + (along * distance);
                var x = (int)(cell.X / BlockCells);
                var y = (int)(cell.Y / BlockCells);
                if (cell.X < 0 || cell.Y < 0 || x >= this.blocksX || y >= this.blocksY)
                {
                    return met;
                }

                var block = (y * this.blocksX) + x;
                if (this.seen[block] || !this.ground.Reachable[block])
                {
                    continue;
                }

                var size = this.ground.UnseenGround[block];
                if (size >= SmallBlocks)
                {
                    return SizeOf(size);
                }

                met = size >= this.ground.MarkFloor ? UnseenSize.Small : met;
            }

            return met;
        }

        /// <summary>
        ///     Where the shortest walk to <paramref name="goal"/> leaves the seen ground: what a look at the map suggests,
        ///     without saying where the goal lies. Null when no unseen ground leads there, or the goal's ground is seen.
        /// </summary>
        public Vector2? WayOn(byte[] grid, int bytesPerRow, Vector2 from, Vector2 goal)
        {
            if (!this.Fits(grid, bytesPerRow, out var width, out var height) || this.IsSeen(goal) ||
                NearestWalkable(grid, bytesPerRow, width, height, from) is not { } start ||
                RoutePlanner.Snap(grid, bytesPerRow, goal) is not { } end)
            {
                return null;
            }

            // One steps array for both walks: the player's steps are kept only for the cells where the seen ground ends.
            lock (this.walks)
            {
                Walk(grid, bytesPerRow, width, height, start, this.walks, true);
                var toPlayer = this.EdgeSteps(width, height);
                Walk(grid, bytesPerRow, width, height, end, this.walks, true);
                return this.WhereTheWalksLeaveTheSeen(toPlayer, width, end);
            }
        }

        /// <summary>The reached cells of unseen blocks next to seen ones, block by block, with their steps from the last walk.</summary>
        private List<(int Cell, int Steps)> EdgeSteps(int width, int height)
        {
            var (marks, steps) = (this.walks.Marks, this.walks.Steps);
            var edge = new List<(int, int)>();
            for (var block = 0; block < this.seen.Length; block++)
            {
                if (this.seen[block] || !this.Neighbours4(block).Any(n => this.seen[n]))
                {
                    continue;
                }

                var left = (block % this.blocksX) * BlockCells;
                var top = (block / this.blocksX) * BlockCells;
                for (var y = top; y < Math.Min(top + BlockCells, height); y++)
                {
                    for (var x = left; x < Math.Min(left + BlockCells, width); x++)
                    {
                        var index = (y * width) + x;
                        if (marks[index] != 0)
                        {
                            edge.Add((index, steps[index]));
                        }
                    }
                }
            }

            return edge;
        }

        /// <summary>The unseen cell next to seen ground with the shortest walk through it, from the player's steps and the last walk's (the goal's).</summary>
        private Vector2? WhereTheWalksLeaveTheSeen(List<(int Cell, int Steps)> toPlayer, int width, Vector2 end)
        {
            var (marks, toGoal) = (this.walks.Marks, this.walks.Steps);
            Vector2? best = null;
            var bestWalk = int.MaxValue;
            var bestLeft = float.MaxValue;
            foreach (var (index, steps) in toPlayer)
            {
                if (marks[index] == 0)
                {
                    continue;
                }

                // Four-neighbour walks tie across open ground: the straight line to the goal settles it.
                var walk = steps + toGoal[index];
                var cell = new Vector2((index % width) + 0.5f, (index / width) + 0.5f);
                var remaining = Vector2.DistanceSquared(cell, end);
                if (walk < bestWalk || (walk == bestWalk && remaining < bestLeft))
                {
                    (best, bestWalk, bestLeft) = (cell, walk, remaining);
                }
            }

            return best;
        }

        /// <summary>Any of <paramref name="cells"/> lies within <paramref name="reach"/> of <paramref name="from"/>, within 45° of <paramref name="axis"/>.</summary>
        public static bool AnyToward(Vector2 from, Vector2 axis, IEnumerable<Vector2> cells, float reach)
        {
            var along = axis.LengthSquared() > 0 ? Vector2.Normalize(axis) : Vector2.Zero;
            foreach (var cell in cells)
            {
                var offset = cell - from;
                var distance = offset.Length();
                if (distance > 0 && distance <= reach && Vector2.Dot(offset / distance, along) >= QuarterCos)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A walk's mark on a cell it reached; a survey's on one a ground already took.</summary>
        private const byte Reached = 1;
        private const byte Taken = 2;

        internal static bool Walkable(byte[] grid, int bytesPerRow, int x, int y)
        {
            var value = grid[(y * bytesPerRow) + (x / 2)];
            return ((x & 1) == 0 ? value & 0xF : value >> 4) != 0;
        }

        /// <summary>
        ///     Marks every walkable cell joined to <paramref name="start"/> (four neighbours) <see cref="Reached"/> in the
        ///     buffers' marks and, when <paramref name="counted"/>, its steps from there (read only where marked).
        /// </summary>
        private static void Walk(byte[] grid, int bytesPerRow, int width, int height, Vector2 start, GridWalks walks, bool counted)
        {
            var marks = walks.Fit(width * height, counted);
            var steps = walks.Steps;
            var queue = walks.Queue;
            queue.Clear();
            var first = ((int)start.Y * width) + (int)start.X;
            marks[first] = Reached;
            if (counted)
            {
                steps[first] = 0;
            }

            queue.Enqueue(first);
            while (queue.TryDequeue(out var cell))
            {
                var x = cell % width;
                var y = cell / width;
                var next = counted ? steps[cell] + 1 : 0;
                Step(x - 1, y, next);
                Step(x + 1, y, next);
                Step(x, y - 1, next);
                Step(x, y + 1, next);
            }

            void Step(int x, int y, int count)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                var index = (y * width) + x;
                if (marks[index] == 0 && Walkable(grid, bytesPerRow, x, y))
                {
                    marks[index] = Reached;
                    if (counted)
                    {
                        steps[index] = count;
                    }

                    queue.Enqueue(index);
                }
            }
        }

        /// <summary>
        ///     A walk's buffers, kept at the area's size and let go with it: a Trial floor holds 34 million cells, and the
        ///     shared pool kept one such array per thread that ever walked (docs/research/performance.md).
        /// </summary>
        private sealed class GridWalks
        {
            public byte[] Marks = Array.Empty<byte>();
            public int[] Steps = Array.Empty<int>();

            /// <summary>The cells to walk from next: as long as the walk's widest front, not the grid.</summary>
            public readonly Queue<int> Queue = new();

            /// <summary>Marks for <paramref name="cells"/> cleared, and steps for as many when <paramref name="counted"/>.</summary>
            public byte[] Fit(int cells, bool counted)
            {
                if (this.Marks.Length == cells)
                {
                    Array.Clear(this.Marks);
                }
                else
                {
                    this.Marks = new byte[cells];
                }

                if (counted && this.Steps.Length != cells)
                {
                    this.Steps = new int[cells];
                }

                return this.Marks;
            }
        }

        internal static bool[] WalkableBlocks(byte[] grid, int bytesPerRow, int width, int height)
        {
            var bx = (width + BlockCells - 1) / BlockCells;
            var by = (height + BlockCells - 1) / BlockCells;
            var counts = new int[bx * by];
            for (var y = 0; y < height; y++)
            {
                var rowBlock = (y / BlockCells) * bx;
                for (var x = 0; x < width; x++)
                {
                    if (Walkable(grid, bytesPerRow, x, y))
                    {
                        counts[rowBlock + (x / BlockCells)]++;
                    }
                }
            }

            return counts.Select(count => count >= MinWalkableCells).ToArray();
        }

        private static int Octant(Vector2 offset) =>
            (int)((((Math.Round(Math.Atan2(offset.Y, offset.X) / (Math.PI / 4)) % 8) + 8) % 8));

        private static Vector2? NearestWalkable(byte[] grid, int bytesPerRow, int width, int height, Vector2 centre)
        {
            Vector2? best = null;
            var bestDistance = float.MaxValue;
            var half = BlockCells / 2;
            for (var y = (int)centre.Y - half; y < (int)centre.Y + half; y++)
            {
                for (var x = (int)centre.X - half; x < (int)centre.X + half; x++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height || !Walkable(grid, bytesPerRow, x, y))
                    {
                        continue;
                    }

                    var cell = new Vector2(x + 0.5f, y + 0.5f);
                    var distance = Vector2.DistanceSquared(cell, centre);
                    if (distance < bestDistance)
                    {
                        (best, bestDistance) = (cell, distance);
                    }
                }
            }

            return best;
        }

        private Vector2 Centre(int block) => new(((block % this.blocksX) + 0.5f) * BlockCells, ((block / this.blocksX) + 0.5f) * BlockCells);
    }
}
