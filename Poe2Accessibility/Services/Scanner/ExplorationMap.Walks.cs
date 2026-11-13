namespace Poe2Accessibility.Services
{
    using System;
    using System.Buffers;
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

            var toPlayer = Walk(grid, bytesPerRow, width, height, start);
            var toGoal = Walk(grid, bytesPerRow, width, height, end);
            try
            {
                return this.WhereTheWalksLeaveTheSeen(toPlayer, toGoal, width, height, end);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(toPlayer);
                ArrayPool<int>.Shared.Return(toGoal);
            }
        }

        /// <summary>The unseen cell next to seen ground with the shortest walk through it, from the player's steps and the goal's.</summary>
        private Vector2? WhereTheWalksLeaveTheSeen(int[] toPlayer, int[] toGoal, int width, int height, Vector2 end)
        {
            Vector2? best = null;
            var bestWalk = int.MaxValue;
            var bestLeft = float.MaxValue;
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
                        if (toPlayer[index] < 0 || toGoal[index] < 0)
                        {
                            continue;
                        }

                        // Four-neighbour walks tie across open ground: the straight line to the goal settles it.
                        var walk = toPlayer[index] + toGoal[index];
                        var cell = new Vector2(x + 0.5f, y + 0.5f);
                        var remaining = Vector2.DistanceSquared(cell, end);
                        if (walk < bestWalk || (walk == bestWalk && remaining < bestLeft))
                        {
                            (best, bestWalk, bestLeft) = (cell, walk, remaining);
                        }
                    }
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

        internal static bool Walkable(byte[] grid, int bytesPerRow, int x, int y)
        {
            var value = grid[(y * bytesPerRow) + (x / 2)];
            return ((x & 1) == 0 ? value & 0xF : value >> 4) != 0;
        }

        /// <summary>
        ///     Steps from <paramref name="start"/> to every walkable cell joined to it (four neighbours), -1 for the rest, in
        ///     the first width × height entries of an array of the shared pool, which the caller gives back: two arrays of
        ///     megabytes per call were most of what the collector had to clear in town (profile, 2026-11-10).
        /// </summary>
        private static int[] Walk(byte[] grid, int bytesPerRow, int width, int height, Vector2 start)
        {
            var cells = width * height;
            var steps = ArrayPool<int>.Shared.Rent(cells);
            Array.Fill(steps, -1, 0, cells);
            var queue = ArrayPool<int>.Shared.Rent(cells);
            var head = 0;
            var tail = 0;
            var first = ((int)start.Y * width) + (int)start.X;
            steps[first] = 0;
            queue[tail++] = first;
            while (head < tail)
            {
                var cell = queue[head++];
                var x = cell % width;
                var y = cell / width;
                Step(x - 1, y, steps[cell] + 1);
                Step(x + 1, y, steps[cell] + 1);
                Step(x, y - 1, steps[cell] + 1);
                Step(x, y + 1, steps[cell] + 1);
            }

            ArrayPool<int>.Shared.Return(queue);
            return steps;

            void Step(int x, int y, int count)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                var index = (y * width) + x;
                if (steps[index] < 0 && Walkable(grid, bytesPerRow, x, y))
                {
                    steps[index] = count;
                    queue[tail++] = index;
                }
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
