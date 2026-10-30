namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Where the player has been in this area, for the scanner's unexplored-areas category (D2R NF2). PoE2 loads
    ///     the whole walkable grid with the area, so "unexplored" is ground never seen: blocks of BlockCells² grid
    ///     cells count as seen once the player comes within <see cref="SeenRadiusCells"/>. The frontier is the
    ///     walkable, reachable, unseen blocks next to seen walkable ones, grouped by connected run and by direction
    ///     from the player; each group is offered at its block nearest the player. Pure: the caller passes the grid
    ///     (two cells per byte, low nibble = even x, 0 = blocked) and positions in grid cells.
    /// </summary>
    public sealed class ExplorationMap
    {
        public const int BlockCells = 8;

        /// <summary>Ground this near the player counts as seen (about half the screen's height).</summary>
        public const float SeenRadiusCells = 60f;

        /// <summary>
        ///     A radar ray is marked unexplored when unseen ground begins within this many cells inside its quarter of the
        ///     screen (LeAccess: twice the reveal radius; ours reveals 60 cells, so frontier lies at least that far).
        /// </summary>
        public const float MarkReachCells = 3 * SeenRadiusCells;

        /// <summary>A block with fewer walkable cells is wall (a corridor three cells wide crossing it still counts).</summary>
        private const int MinWalkableCells = 6;

        /// <summary>Smaller frontier groups are nooks, not places to explore.</summary>
        private const int MinGroupBlocks = 2;

        /// <summary>The frontier is recomputed every two seconds: a place still this near where it was is the same one, moved.</summary>
        public const float SamePlaceCells = 3 * BlockCells;

        /// <summary>The place being sounded is kept until another is less than half as far (no flipping between two).</summary>
        private const float KeepRatio = 2f;

        /// <summary>More blocks than any area holds (a 4000-cell square): a kept map claiming more is not one.</summary>
        private const long MaxBlocks = 500 * 500;

        /// <summary>cos 45°: the edge of a ray's quarter.</summary>
        private static readonly float QuarterCos = MathF.Sqrt(0.5f);

        private bool[] seen = Array.Empty<bool>();
        private int blocksX;
        private int blocksY;

        /// <summary>A new area: nothing seen.</summary>
        public void Reset()
        {
            this.seen = Array.Empty<bool>();
            this.blocksX = this.blocksY = 0;
        }

        /// <summary>The seen blocks as bytes, to be kept past the area and the host (<see cref="AreaMemory"/>): two ints, a bit a block.</summary>
        public byte[] Save()
        {
            var bytes = new byte[8 + ((this.seen.Length + 7) / 8)];
            BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), this.blocksX);
            BitConverter.TryWriteBytes(bytes.AsSpan(4, 4), this.blocksY);
            for (var i = 0; i < this.seen.Length; i++)
            {
                if (this.seen[i])
                {
                    bytes[8 + (i >> 3)] |= (byte)(1 << (i & 7));
                }
            }

            return bytes;
        }

        /// <summary>Blocks kept by <see cref="Save"/>; false, and nothing changed, for bytes that cannot be such.</summary>
        public bool Load(byte[] bytes)
        {
            if (bytes.Length < 8)
            {
                return false;
            }

            var bx = BitConverter.ToInt32(bytes, 0);
            var by = BitConverter.ToInt32(bytes, 4);
            if (bx <= 0 || by <= 0 || (long)bx * by > MaxBlocks || bytes.Length != 8 + (((bx * by) + 7) / 8))
            {
                return false;
            }

            var seen = new bool[bx * by];
            for (var i = 0; i < seen.Length; i++)
            {
                seen[i] = (bytes[8 + (i >> 3)] & (1 << (i & 7))) != 0;
            }

            (this.blocksX, this.blocksY, this.seen) = (bx, by, seen);
            return true;
        }

        /// <summary>Marks the blocks around <paramref name="at"/> (grid cells) as seen.</summary>
        public void Visit(Vector2 at, int widthCells, int heightCells)
        {
            var bx = (widthCells + BlockCells - 1) / BlockCells;
            var by = (heightCells + BlockCells - 1) / BlockCells;
            if (bx <= 0 || by <= 0)
            {
                return;
            }

            if (bx != this.blocksX || by != this.blocksY)
            {
                (this.blocksX, this.blocksY, this.seen) = (bx, by, new bool[bx * by]);
            }

            var radius = (int)MathF.Ceiling(SeenRadiusCells / BlockCells);
            var cx = (int)(at.X / BlockCells);
            var cy = (int)(at.Y / BlockCells);
            for (var y = Math.Max(0, cy - radius); y <= Math.Min(by - 1, cy + radius); y++)
            {
                for (var x = Math.Max(0, cx - radius); x <= Math.Min(bx - 1, cx + radius); x++)
                {
                    var centre = new Vector2((x + 0.5f) * BlockCells, (y + 0.5f) * BlockCells);
                    if (Vector2.Distance(centre, at) <= SeenRadiusCells)
                    {
                        this.seen[(y * bx) + x] = true;
                    }
                }
            }
        }

        /// <summary>The ground at <paramref name="cell"/> (grid cells) has been seen.</summary>
        public bool IsSeen(Vector2 cell)
        {
            var x = (int)(cell.X / BlockCells);
            var y = (int)(cell.Y / BlockCells);
            return x >= 0 && y >= 0 && x < this.blocksX && y < this.blocksY && this.seen[(y * this.blocksX) + x];
        }

        /// <summary>Whole percent of the ground reachable from <paramref name="from"/> that has been seen (D2R explored share); null without a grid.</summary>
        public int? ExploredPercent(byte[] grid, int bytesPerRow, Vector2 from)
        {
            if (this.seen.Length == 0 || bytesPerRow <= 0 || grid.Length / bytesPerRow < 1)
            {
                return null;
            }

            var width = bytesPerRow * 2;
            var height = grid.Length / bytesPerRow;
            if ((width + BlockCells - 1) / BlockCells != this.blocksX || (height + BlockCells - 1) / BlockCells != this.blocksY)
            {
                return null;
            }

            var reachable = this.Reachable(grid, bytesPerRow, width, height, from);
            int total = 0, seenBlocks = 0;
            for (var i = 0; i < reachable.Length; i++)
            {
                total += reachable[i] ? 1 : 0;
                seenBlocks += reachable[i] && this.seen[i] ? 1 : 0;
            }

            return total == 0 ? null : seenBlocks * 100 / total;
        }

        /// <summary>Walkable grid cells to head for, one per unexplored group and direction, nearest first.</summary>
        public IReadOnlyList<Vector2> Frontier(byte[] grid, int bytesPerRow, Vector2 from, int maxEntries)
        {
            if (this.seen.Length == 0 || bytesPerRow <= 0 || grid.Length / bytesPerRow < 1)
            {
                return Array.Empty<Vector2>();
            }

            var width = bytesPerRow * 2;
            var height = grid.Length / bytesPerRow;
            if ((width + BlockCells - 1) / BlockCells != this.blocksX || (height + BlockCells - 1) / BlockCells != this.blocksY)
            {
                return Array.Empty<Vector2>();
            }

            var open = WalkableBlocks(grid, bytesPerRow, width, height);
            var reachable = this.Reachable(grid, bytesPerRow, width, height, from);
            var frontier = new List<int>();
            for (var i = 0; i < open.Length; i++)
            {
                if (reachable[i] && !this.seen[i] && this.Neighbours4(i).Any(n => open[n] && this.seen[n]))
                {
                    frontier.Add(i);
                }
            }

            // Group by connected run (8 neighbours) and by the octant seen from the player.
            var run = new Dictionary<int, int>();
            var runs = 0;
            var inFrontier = new HashSet<int>(frontier);
            foreach (var start in frontier)
            {
                if (run.ContainsKey(start))
                {
                    continue;
                }

                var stack = new Stack<int>();
                stack.Push(start);
                run[start] = runs;
                while (stack.Count > 0)
                {
                    foreach (var n in this.Neighbours8(stack.Pop()))
                    {
                        if (inFrontier.Contains(n) && run.TryAdd(n, runs))
                        {
                            stack.Push(n);
                        }
                    }
                }

                runs++;
            }

            var groups = frontier.GroupBy(block => (run[block], Octant(this.Centre(block) - from)));
            var targets = new List<(Vector2 Cell, float Distance)>();
            foreach (var group in groups)
            {
                if (group.Count() < MinGroupBlocks)
                {
                    continue;
                }

                var nearest = group.OrderBy(block => Vector2.DistanceSquared(this.Centre(block), from)).First();
                if (NearestWalkable(grid, bytesPerRow, width, height, this.Centre(nearest)) is { } cell)
                {
                    targets.Add((cell, Vector2.Distance(cell, from)));
                }
            }

            return targets.OrderBy(target => target.Distance).Take(maxEntries).Select(target => target.Cell).ToList();
        }

        /// <summary>
        ///     The unexplored place to sound next (<see cref="UnexploredLoop"/>): the one sounded before while the
        ///     recomputed frontier still holds it (within <see cref="SamePlaceCells"/>) and nothing is less than half as far;
        ///     else the nearest. Null when nothing is left.
        /// </summary>
        public static Vector2? Follow(Vector2? current, IReadOnlyList<Vector2> frontier, Vector2 from)
        {
            if (frontier.Count == 0)
            {
                return null;
            }

            var nearest = frontier.MinBy(cell => Vector2.DistanceSquared(cell, from));
            if (current is not { } last)
            {
                return nearest;
            }

            var kept = frontier.Where(cell => Vector2.Distance(cell, last) <= SamePlaceCells).OrderBy(cell => Vector2.DistanceSquared(cell, last))
                .Select(cell => (Vector2?)cell).FirstOrDefault();
            return kept is { } same && Vector2.Distance(same, from) <= KeepRatio * Vector2.Distance(nearest, from) ? same : nearest;
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

        private static bool[] WalkableBlocks(byte[] grid, int bytesPerRow, int width, int height)
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

        /// <summary>
        ///     Blocks holding a walkable cell joined to the player's cell through walkable cells (four neighbours, cell
        ///     level: a wall two cells thick inside a block still separates its sides).
        /// </summary>
        private bool[] Reachable(byte[] grid, int bytesPerRow, int width, int height, Vector2 from)
        {
            var reachable = new bool[this.blocksX * this.blocksY];
            if (NearestWalkable(grid, bytesPerRow, width, height, from) is not { } start)
            {
                return reachable;
            }

            var visited = new bool[width * height];
            var queue = new int[width * height];
            var head = 0;
            var tail = 0;
            var first = ((int)start.Y * width) + (int)start.X;
            visited[first] = true;
            queue[tail++] = first;
            while (head < tail)
            {
                var cell = queue[head++];
                var x = cell % width;
                var y = cell / width;
                reachable[((y / BlockCells) * this.blocksX) + (x / BlockCells)] = true;
                Step(x - 1, y);
                Step(x + 1, y);
                Step(x, y - 1);
                Step(x, y + 1);
            }

            return reachable;

            void Step(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                var index = (y * width) + x;
                if (!visited[index] && Walkable(grid, bytesPerRow, x, y))
                {
                    visited[index] = true;
                    queue[tail++] = index;
                }
            }
        }

        private IEnumerable<int> Neighbours4(int block)
        {
            var x = block % this.blocksX;
            var y = block / this.blocksX;
            if (x > 0)
            {
                yield return block - 1;
            }

            if (x < this.blocksX - 1)
            {
                yield return block + 1;
            }

            if (y > 0)
            {
                yield return block - this.blocksX;
            }

            if (y < this.blocksY - 1)
            {
                yield return block + this.blocksX;
            }
        }

        private IEnumerable<int> Neighbours8(int block)
        {
            var x = block % this.blocksX;
            var y = block / this.blocksX;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if ((dx != 0 || dy != 0) && x + dx >= 0 && x + dx < this.blocksX && y + dy >= 0 && y + dy < this.blocksY)
                    {
                        yield return block + (dy * this.blocksX) + dx;
                    }
                }
            }
        }
    }
}
