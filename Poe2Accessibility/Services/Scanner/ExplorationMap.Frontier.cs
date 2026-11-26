namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>The frontier of <see cref="ExplorationMap"/>: the explored share, the unexplored places and how much ground lies behind each, from the survey of reachable blocks.</summary>
    public sealed partial class ExplorationMap
    {
        /// <summary>
        ///     Whole percent of the ground reachable from <paramref name="from"/> that has been seen (D2R explored share); null
        ///     without a grid. With <paramref name="closedMask"/>, the ground past the barriers it holds closed counts too: a
        ///     player penned in by a gate has not explored the map (Hunting Grounds, 2026-11-26).
        /// </summary>
        public int? ExploredPercent(byte[] grid, int bytesPerRow, Vector2 from, byte[]? closedMask = null)
        {
            if (!this.Fits(grid, bytesPerRow, out var width, out var height))
            {
                return null;
            }

            var reachable = this.Reachable(grid, bytesPerRow, width, height, from, closedMask?.Length == grid.Length ? closedMask : null);
            int total = 0, seenBlocks = 0;
            for (var i = 0; i < reachable.Length; i++)
            {
                total += reachable[i] ? 1 : 0;
                seenBlocks += reachable[i] && this.seen[i] ? 1 : 0;
            }

            return total == 0 ? null : seenBlocks * 100 / total;
        }

        /// <summary>
        ///     Places to head for, one per unexplored group and direction, each with the size of the unseen ground behind it:
        ///     ground that goes on first, then what soon ends, then pockets, nearest first. A place the walk to one of
        ///     <paramref name="goals"/> (grid cells) passes carries its index, and is kept past <paramref name="maxEntries"/>.
        ///     With <paramref name="closedMask"/> (the cells the barrier memory holds closed), then one place per barrier with
        ///     unseen ground past it, at its near side, also kept past <paramref name="maxEntries"/>. Unseen ground on the walk to
        ///     one of <paramref name="exits"/> (grid cells the area's tiles place exits at) is no pocket, and is not named.
        /// </summary>
        public IReadOnlyList<FrontierPlace> Frontier(byte[] grid, int bytesPerRow, Vector2 from, int maxEntries, IReadOnlyList<Vector2>? goals = null, byte[]? closedMask = null, IReadOnlyList<Vector2>? exits = null)
        {
            if (!this.Fits(grid, bytesPerRow, out var width, out var height))
            {
                return Array.Empty<FrontierPlace>();
            }

            var open = WalkableBlocks(grid, bytesPerRow, width, height);
            var (reachable, unseenGround, walk) = this.Survey(grid, bytesPerRow, width, height, from);
            var frontier = new List<int>();
            for (var i = 0; i < open.Length; i++)
            {
                if (reachable[i] && !this.seen[i] && this.BesideSeen(i, open))
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
            var leads = this.BlocksLeadingTo(grid, bytesPerRow, from, goals ?? Array.Empty<Vector2>(), inFrontier);
            // The unseen ground on the walk to an exit the area's tiles place goes somewhere, however small: no nook, and
            // not named (Hunting Grounds, 2026-11-26: Freythorn's road ended at the map's edge and was passed by as a nook).
            var toExits = this.BlocksLeadingTo(grid, bytesPerRow, from, exits ?? Array.Empty<Vector2>(), inFrontier).Select(lead => lead.Block).ToHashSet();
            var targets = new List<(FrontierPlace Place, float Distance)>();
            foreach (var group in groups)
            {
                var nearest = group.OrderBy(block => Vector2.DistanceSquared(this.Centre(block), from)).First();
                var leadsTo = leads.Where(lead => group.Contains(lead.Block)).Select(lead => lead.Goal).ToList();
                var toExit = group.Any(toExits.Contains);
                if (leadsTo.Count == 0 && !toExit && group.Count() < MinGroupBlocks && unseenGround[nearest] < PocketBlocks)
                {
                    continue; // a narrow gap counts when ground goes on behind it
                }

                var size = SizeOf(unseenGround[nearest]);
                if (NearestWalkable(grid, bytesPerRow, width, height, this.Centre(nearest)) is { } cell)
                {
                    targets.Add((new FrontierPlace(cell, toExit && size == UnseenSize.Pocket ? UnseenSize.Small : size, walk[nearest], leadsTo), Vector2.Distance(cell, from)));
                }
            }

            var best = targets.Count == 0 ? UnseenSize.Pocket : targets.Max(target => target.Place.Size);
            this.ground = (reachable, unseenGround, best == UnseenSize.Pocket ? 0 : PocketBlocks);
            var places = targets.OrderBy(target => RankOf(target.Place.Size)).ThenBy(target => target.Distance).Select(target => target.Place)
                .Where((place, index) => index < maxEntries || place.LeadsTo.Count > 0).ToList();
            if (closedMask?.Length != grid.Length)
            {
                this.behind = NoBehind;
                return places;
            }

            var behind = new bool[reachable.Length];
            var behindGround = new int[reachable.Length];
            places.AddRange(this.BarrierPlaces(grid, bytesPerRow, width, height, from, closedMask, behind, behindGround));
            this.behind = (behind, behindGround);
            return places;
        }

        /// <summary>Per goal, the frontier block where the walk to it leaves the seen ground (<see cref="WayOn"/>), or the frontier block beside it.</summary>
        private List<(int Goal, int Block)> BlocksLeadingTo(byte[] grid, int bytesPerRow, Vector2 from, IReadOnlyList<Vector2> goals, HashSet<int> frontier)
        {
            var leads = new List<(int Goal, int Block)>();
            for (var goal = 0; goal < goals.Count; goal++)
            {
                if (this.WayOn(grid, bytesPerRow, from, goals[goal]) is not { } way)
                {
                    continue;
                }

                var block = ((int)way.Y / BlockCells * this.blocksX) + ((int)way.X / BlockCells);
                foreach (var near in this.Neighbours8(block).Prepend(block))
                {
                    if (frontier.Contains(near))
                    {
                        leads.Add((goal, near));
                        break;
                    }
                }
            }

            return leads;
        }

        /// <summary>
        ///     Whether a place is on the ground the player can walk to, by the last <see cref="Frontier"/>: its block or one
        ///     beside it holds reached ground. Null while that survey is of other ground: before the first, or just after the
        ///     player was carried to ground apart, as between the rooms of a Trial's floor.
        /// </summary>
        public bool? OnPlayersGround(Vector2 cell, Vector2 player)
        {
            var reachable = this.ground.Reachable;
            if (reachable.Length == 0 || reachable.Length != this.seen.Length || !Reached(player))
            {
                return null;
            }

            return Reached(cell);

            bool Reached(Vector2 at)
            {
                var x = (int)(at.X / BlockCells);
                var y = (int)(at.Y / BlockCells);
                if (x < 0 || y < 0 || x >= this.blocksX || y >= this.blocksY)
                {
                    return false;
                }

                var block = (y * this.blocksX) + x;
                return reachable[block] || this.Neighbours8(block).Any(beside => reachable[beside]);
            }
        }

        private static UnseenSize SizeOf(int blocks) =>
            blocks < PocketBlocks ? UnseenSize.Pocket : blocks < SmallBlocks ? UnseenSize.Small : blocks < LargeBlocks ? UnseenSize.Medium : UnseenSize.Large;

        /// <summary>Ground that goes on (medium or large) together, then what soon ends, then nooks.</summary>
        private static int RankOf(UnseenSize size) => size >= UnseenSize.Medium ? 0 : size == UnseenSize.Small ? 1 : 2;

        /// <summary>
        ///     Blocks holding a walkable cell joined to the player's cell through walkable cells (four neighbours, cell
        ///     level: a wall two cells thick inside a block still separates its sides).
        /// </summary>
        private bool[] Reachable(byte[] grid, int bytesPerRow, int width, int height, Vector2 from, byte[]? open = null)
        {
            var reachable = new bool[this.blocksX * this.blocksY];
            if (NearestWalkable(grid, bytesPerRow, width, height, from) is not { } start)
            {
                return reachable;
            }

            lock (this.walks)
            {
                Walk(grid, bytesPerRow, width, height, start, this.walks, false, open);
                var marks = this.walks.Marks;
                for (var cell = 0; cell < width * height; cell++)
                {
                    if (marks[cell] != 0)
                    {
                        reachable[this.BlockOf(cell, width)] = true;
                    }
                }
            }

            return reachable;
        }

        /// <summary>
        ///     The blocks the player can reach and, per block, how many blocks its unseen ground holds: the unseen blocks whose
        ///     walkable cells join without crossing seen ground (four neighbours, cell by cell, as the walk). Blocks side by
        ///     side were taken for one ground until 2026-11-12, so a dead end beside a wall with unseen ground behind it was
        ///     "ground that goes on" and the walk to it ended at that wall (user). A block two grounds share takes the larger.
        ///     Also each block's shortest walk from the player.
        /// </summary>
        private (bool[] Reachable, int[] UnseenGround, int[] Walk) Survey(byte[] grid, int bytesPerRow, int width, int height, Vector2 from)
        {
            var reachable = new bool[this.blocksX * this.blocksY];
            var sizes = new int[reachable.Length];
            var nearest = new int[reachable.Length];
            Array.Fill(nearest, int.MaxValue);
            if (NearestWalkable(grid, bytesPerRow, width, height, from) is not { } start)
            {
                return (reachable, sizes, nearest);
            }

            var cells = width * height;
            lock (this.walks)
            {
                Walk(grid, bytesPerRow, width, height, start, this.walks, true);
                var (walk, queue, steps) = (this.walks.Marks, this.walks.Queue, this.walks.Steps);
                for (var cell = 0; cell < cells; cell++)
                {
                    if (walk[cell] != 0)
                    {
                        var block = this.BlockOf(cell, width);
                        reachable[block] = true;
                        nearest[block] = Math.Min(nearest[block], steps[cell]);
                    }
                }

                // A cell taken into a ground is marked so in the walk's marks, so none is taken twice.
                var lastGround = new int[reachable.Length];
                var touched = new List<int>();
                var number = 0;
                for (var first = 0; first < cells; first++)
                {
                    if (walk[first] != Reached || this.seen[this.BlockOf(first, width)])
                    {
                        continue;
                    }

                    number++;
                    touched.Clear();
                    walk[first] = Taken;
                    queue.Enqueue(first);
                    while (queue.TryDequeue(out var cell))
                    {
                        var block = this.BlockOf(cell, width);
                        if (lastGround[block] != number)
                        {
                            lastGround[block] = number;
                            touched.Add(block);
                        }

                        var x = cell % width;
                        if (x > 0)
                        {
                            Take(cell - 1);
                        }

                        if (x < width - 1)
                        {
                            Take(cell + 1);
                        }

                        if (cell >= width)
                        {
                            Take(cell - width);
                        }

                        if (cell < cells - width)
                        {
                            Take(cell + width);
                        }
                    }

                    foreach (var block in touched)
                    {
                        sizes[block] = Math.Max(sizes[block], touched.Count);
                    }

                    void Take(int cell)
                    {
                        if (walk[cell] == Reached && !this.seen[this.BlockOf(cell, width)])
                        {
                            walk[cell] = Taken;
                            queue.Enqueue(cell);
                        }
                    }
                }
            }

            return (reachable, sizes, nearest);
        }

        private int BlockOf(int cell, int width) => ((cell / width / BlockCells) * this.blocksX) + (cell % width / BlockCells);

        /// <summary>The grid has this map's size and something has been seen on it.</summary>
        private bool Fits(byte[] grid, int bytesPerRow, out int width, out int height)
        {
            width = bytesPerRow * 2;
            height = bytesPerRow > 0 ? grid.Length / bytesPerRow : 0;
            return this.seen.Length > 0 && height >= 1 &&
                   (width + BlockCells - 1) / BlockCells == this.blocksX && (height + BlockCells - 1) / BlockCells == this.blocksY;
        }

        /// <summary>A block beside this one (four neighbours) is seen, and walkable when <paramref name="open"/> is given; asked of every block, so nothing is allocated.</summary>
        private bool BesideSeen(int block, bool[]? open)
        {
            var x = block % this.blocksX;
            var y = block / this.blocksX;
            return (x > 0 && this.SeenOpen(block - 1, open)) || (x < this.blocksX - 1 && this.SeenOpen(block + 1, open)) ||
                   (y > 0 && this.SeenOpen(block - this.blocksX, open)) || (y < this.blocksY - 1 && this.SeenOpen(block + this.blocksX, open));
        }

        private bool SeenOpen(int block, bool[]? open) => this.seen[block] && (open == null || open[block]);

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
