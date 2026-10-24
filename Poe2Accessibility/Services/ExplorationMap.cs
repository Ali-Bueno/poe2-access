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

        /// <summary>A block with fewer walkable cells is wall (a corridor three cells wide crossing it still counts).</summary>
        private const int MinWalkableCells = 6;

        /// <summary>Smaller frontier groups are nooks, not places to explore.</summary>
        private const int MinGroupBlocks = 2;

        private bool[] seen = Array.Empty<bool>();
        private int blocksX;
        private int blocksY;

        /// <summary>A new area: nothing seen.</summary>
        public void Reset()
        {
            this.seen = Array.Empty<bool>();
            this.blocksX = this.blocksY = 0;
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
