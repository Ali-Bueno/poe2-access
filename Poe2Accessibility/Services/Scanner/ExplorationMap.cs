namespace Poe2Accessibility.Services
{
    using System;
    using System.Buffers;
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

        /// <summary>A scanner entry's kind for a pocket (<see cref="FrontierPlace"/>).</summary>
        public const string PocketKind = "pocket";


        /// <summary>Ground this near the player counts as seen (about half the screen's height).</summary>
        public const float SeenRadiusCells = 60f;

        /// <summary>
        ///     A radar ray is marked unexplored when it leaves the seen ground within this many cells (LeAccess: twice the
        ///     reveal radius). Farther, the edge of the strip a walk revealed is met by the ray pointing back along it.
        /// </summary>
        public const float MarkReachCells = 2 * SeenRadiusCells;

        /// <summary>A block with fewer walkable cells is wall (a corridor three cells wide crossing it still counts).</summary>
        private const int MinWalkableCells = 6;

        /// <summary>Smaller frontier groups before a pocket are not worth a place of their own.</summary>
        private const int MinGroupBlocks = 2;

        /// <summary>Unseen ground smaller than this is a pocket: half the reveal disc, what one look from its edge shows whole.</summary>
        private const int PocketBlocks = (int)(Math.PI * SeenRadiusCells * SeenRadiusCells / (2 * BlockCells * BlockCells));

        /// <summary>More blocks than any area holds (a 4000-cell square): a kept map claiming more is not one.</summary>
        private const long MaxBlocks = 500 * 500;

        /// <summary>cos 45°: the edge of a ray's quarter.</summary>
        private static readonly float QuarterCos = MathF.Sqrt(0.5f);

        private bool[] seen = Array.Empty<bool>();
        private int blocksX;
        private int blocksY;

        /// <summary>What the last <see cref="Frontier"/> found: the blocks the player can reach, and the size of each one's unseen ground.</summary>
        private (bool[] Reachable, int[] UnseenGround, bool GoesOn) ground = NoGround;

        private static readonly (bool[], int[], bool) NoGround = (Array.Empty<bool>(), Array.Empty<int>(), false);

        /// <summary>How many blocks have been seen: it grows while the player finds new ground.</summary>
        public int SeenBlocks { get; private set; }

        /// <summary>A new area: nothing seen.</summary>
        public void Reset()
        {
            this.seen = Array.Empty<bool>();
            this.blocksX = this.blocksY = 0;
            this.ground = NoGround;
            this.SeenBlocks = 0;
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
            this.ground = NoGround;
            this.SeenBlocks = seen.Count(block => block);
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
                this.ground = NoGround;
                this.SeenBlocks = 0;
            }

            var radius = (int)MathF.Ceiling(SeenRadiusCells / BlockCells);
            var cx = (int)(at.X / BlockCells);
            var cy = (int)(at.Y / BlockCells);
            for (var y = Math.Max(0, cy - radius); y <= Math.Min(by - 1, cy + radius); y++)
            {
                for (var x = Math.Max(0, cx - radius); x <= Math.Min(bx - 1, cx + radius); x++)
                {
                    var centre = new Vector2((x + 0.5f) * BlockCells, (y + 0.5f) * BlockCells);
                    if (Vector2.Distance(centre, at) <= SeenRadiusCells && !this.seen[(y * bx) + x])
                    {
                        this.seen[(y * bx) + x] = true;
                        this.SeenBlocks++;
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
            if (!this.Fits(grid, bytesPerRow, out var width, out var height))
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

        /// <summary>Places to head for, one per unexplored group and direction: ground that goes on first, then pockets, nearest first.</summary>
        public IReadOnlyList<FrontierPlace> Frontier(byte[] grid, int bytesPerRow, Vector2 from, int maxEntries)
        {
            if (!this.Fits(grid, bytesPerRow, out var width, out var height))
            {
                return Array.Empty<FrontierPlace>();
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
            var unseenGround = this.UnseenGround(reachable);
            var targets = new List<(FrontierPlace Place, float Distance)>();
            foreach (var group in groups)
            {
                var nearest = group.OrderBy(block => Vector2.DistanceSquared(this.Centre(block), from)).First();
                if (group.Count() < MinGroupBlocks && unseenGround[nearest] < PocketBlocks)
                {
                    continue; // a narrow gap counts when ground goes on behind it
                }

                if (NearestWalkable(grid, bytesPerRow, width, height, this.Centre(nearest)) is { } cell)
                {
                    targets.Add((new FrontierPlace(cell, unseenGround[nearest] < PocketBlocks), Vector2.Distance(cell, from)));
                }
            }

            this.ground = (reachable, unseenGround, targets.Any(target => !target.Place.Pocket));
            return targets.OrderBy(target => target.Place.Pocket).ThenBy(target => target.Distance).Take(maxEntries).Select(target => target.Place).ToList();
        }

        /// <summary>
        ///     The straight line from <paramref name="from"/> along <paramref name="axis"/> leaves the seen ground within
        ///     <paramref name="reach"/> cells, onto ground the player can reach (as of the last <see cref="Frontier"/>);
        ///     pockets only once nothing else is left. The line alone, not what lies beside it: ground to a side of the way
        ///     the player came is not ahead on the way back (docs/radar.md).
        /// </summary>
        public bool LeavesTheSeen(Vector2 from, Vector2 axis, float reach)
        {
            if (this.ground.Reachable.Length != this.seen.Length || axis.LengthSquared() <= 0)
            {
                return false;
            }

            var along = Vector2.Normalize(axis);
            const float step = BlockCells / 2f; // no block the line crosses is stepped over
            for (var distance = step; distance <= reach; distance += step)
            {
                var cell = from + (along * distance);
                var x = (int)(cell.X / BlockCells);
                var y = (int)(cell.Y / BlockCells);
                if (cell.X < 0 || cell.Y < 0 || x >= this.blocksX || y >= this.blocksY)
                {
                    return false;
                }

                var block = (y * this.blocksX) + x;
                if (!this.seen[block] && this.ground.Reachable[block] && (!this.ground.GoesOn || this.ground.UnseenGround[block] >= PocketBlocks))
                {
                    return true;
                }
            }

            return false;
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

            var walk = Walk(grid, bytesPerRow, width, height, start);
            for (var cell = 0; cell < width * height; cell++)
            {
                if (walk[cell] >= 0)
                {
                    reachable[((cell / width / BlockCells) * this.blocksX) + (cell % width / BlockCells)] = true;
                }
            }

            ArrayPool<int>.Shared.Return(walk);
            return reachable;
        }

        /// <summary>Per block, how many blocks its connected unseen ground holds (8 neighbours: ground that goes on is never called a pocket).</summary>
        private int[] UnseenGround(bool[] reachable)
        {
            var sizes = new int[reachable.Length];
            var done = new bool[reachable.Length];
            var members = new List<int>();
            var stack = new Stack<int>();
            for (var first = 0; first < reachable.Length; first++)
            {
                if (done[first] || !reachable[first] || this.seen[first])
                {
                    continue;
                }

                members.Clear();
                done[first] = true;
                stack.Push(first);
                while (stack.Count > 0)
                {
                    var block = stack.Pop();
                    members.Add(block);
                    foreach (var n in this.Neighbours8(block))
                    {
                        if (!done[n] && reachable[n] && !this.seen[n])
                        {
                            done[n] = true;
                            stack.Push(n);
                        }
                    }
                }

                foreach (var block in members)
                {
                    sizes[block] = members.Count;
                }
            }

            return sizes;
        }

        /// <summary>The grid has this map's size and something has been seen on it.</summary>
        private bool Fits(byte[] grid, int bytesPerRow, out int width, out int height)
        {
            width = bytesPerRow * 2;
            height = bytesPerRow > 0 ? grid.Length / bytesPerRow : 0;
            return this.seen.Length > 0 && height >= 1 &&
                   (width + BlockCells - 1) / BlockCells == this.blocksX && (height + BlockCells - 1) / BlockCells == this.blocksY;
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

    /// <summary>An unexplored place: a pocket is unseen ground one look from its edge would show whole, a nook with no way on.</summary>
    public readonly record struct FrontierPlace(Vector2 Cell, bool Pocket);
}
