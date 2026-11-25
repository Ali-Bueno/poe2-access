namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using System.Threading;

    /// <summary>
    ///     An area's walkable ground drawn as a person would: forks, rooms, the ways between them, narrow passages, and which
    ///     ways lead only to ground with no way on (docs/research/map-topology.md). Pure: <see cref="Build"/> reads the walkable
    ///     grid (two cells per byte, low nibble = even x, 0 = blocked) off the frame thread; positions are grid cells. Built at
    ///     full resolution, so a gap one cell wide still joins the ground on both sides.
    /// </summary>
    public sealed partial class MapTopology
    {
        /// <summary>What one look shows (π · 60², <see cref="ExplorationMap.LookCells"/>): a branch smaller than this is a nook.</summary>
        public const int RevealDiscCells = (int)(Math.PI * ExplorationMap.LookCells * ExplorationMap.LookCells);

        /// <summary>
        ///     A passage at most this many cells wide (about 90 world units, a quarter of the Manor's usual corridor) is narrow:
        ///     easy to walk past without seeing it (the owner, 2026-11-16). 4 cells found none in the four study dumps; 8 finds
        ///     0-4 an area, all joining parts of the map.
        /// </summary>
        public const float NarrowWidthCells = 8f;

        /// <summary>Fewer ways than this tell nothing of the layout.</summary>
        private const int MinLayoutWays = 3;

        /// <summary>Diagonal once this share of the ways near runs diagonal on screen; straight again at or under <see cref="StraightShare"/>.</summary>
        private const float DiagonalShare = 0.85f;

        private const float StraightShare = 0.6f;

        /// <summary>
        ///     Which way the ways around the player run on screen, from how many run diagonal and how many straight: true
        ///     nearly all diagonal, false a fair share straight, null unclear (the rays stay as they are).
        /// </summary>
        public static bool? RunDiagonal(int diagonal, int straight)
        {
            var ways = diagonal + straight;
            return ways < MinLayoutWays ? null
                : diagonal >= DiagonalShare * ways ? true
                : diagonal <= StraightShare * ways ? false
                : null;
        }

        /// <summary>Pieces of ground under half a look (the explorer's pocket) have no ways to tell apart: left out.</summary>
        private const int MinPieceCells = RevealDiscCells / 2;

        /// <summary>Enclosed obstacles under this are pillars and furniture: filled, or each splits a corridor into a false pair of forks (study §1).</summary>
        private const int PillarCells = 2_500;

        /// <summary>An end branch shorter than this many times its fork's half-width, plus <see cref="SpurFloorCells"/>, is a bump of the wall (study §1).</summary>
        private const float SpurWidthFactor = 1.5f;

        private const float SpurFloorCells = 12f;

        /// <summary>Cutting a bump may leave another; the study's grids settle in 2-3 rounds.</summary>
        private const int MaxPruneRounds = 15;

        /// <summary>Ground this far from any wall is a room's core: the study's threshold, which drew the Manor's six rooms.</summary>
        private const float RoomMinDistanceCells = 24f;

        /// <summary>
        ///     Wider maps start their rooms at this many times their median half-width: 24 / 17.8, the Manor's median measured
        ///     here (the study's 12 on its halved grid); the study's caves measure 33-45, so their rooms start at 44-61 cells.
        /// </summary>
        private const float RoomWidthFactor = 1.35f;

        /// <summary>A core smaller than this (at the 24-cell distance, scaled by its square with it) is a wide corridor, not a room.</summary>
        private const float RoomMinCoreCells = 1_200f;

        /// <summary>A room's widest point is at least this far from walls (at the 24-cell distance, scaled with it).</summary>
        private const float RoomMinPeakCells = 40f;

        /// <summary>Two places joined by a way shorter than its width plus this are one place (a wide junction drawn as two).</summary>
        private const float MergeFloorCells = 6f;

        /// <summary>A way's direction is read over its first this-many cells: past the fork's own bend, before the corridor turns.</summary>
        private const int WayDirectionCells = 24;

        /// <summary>A way's ahead cell lies this far along it: out of the fork, where looking down the way shows it.</summary>
        private const int WayAheadCells = 40;

        /// <summary>Narrow stretches closer than this along a way are one passage (a wall's bumps).</summary>
        private const int NarrowJoinCells = 8;

        /// <summary>A narrow passage's direction is read this many cells either side of its narrowest point.</summary>
        private const int NarrowDirectionCells = 6;

        /// <summary>A position this near walkable ground counts on it: feet on a wall's edge.</summary>
        private const int LookupSlackCells = 8;

        /// <summary>A goal this near walkable ground counts there: an exit or a quest object often stands on blocked cells.</summary>
        private const int GoalSlackCells = 24;

        /// <summary>The cell owner map is kept in blocks of this many cells a side: lookups need no finer, and it is a quarter of the memory.</summary>
        private const int OwnerBlock = 2;

        private readonly TopologyNode[] nodes;
        private readonly TopologyEdge[] edges;
        private readonly Vector2[][] paths;
        private readonly Way[][] ways;
        private readonly Narrow[] narrows;

        /// <summary>The node or edge (an item: node id, or node count + edge id) owning each block, plus one; 0 for none.</summary>
        private readonly ushort[] owner;
        private readonly int left;
        private readonly int top;
        private readonly int blocksX;
        private readonly int blocksY;
        private readonly int[] itemArea;
        private readonly int[] itemBox;

        private Goals goals;

        /// <summary>The places: forks, rooms and the ends of ways.</summary>
        public IReadOnlyList<TopologyNode> Nodes => this.nodes;

        /// <summary>The ways between places.</summary>
        public IReadOnlyList<TopologyEdge> Edges => this.edges;

        /// <summary>The narrow passages between parts worth a place of their own.</summary>
        public IReadOnlyList<Narrow> Narrows => this.narrows;

        /// <summary>The map's median corridor half-width (cells), from which its rooms are told.</summary>
        public float HalfWidthCells { get; }

        /// <summary>How far from walls a room's core begins on this map (cells).</summary>
        public float RoomDistanceCells { get; }

        /// <summary>The topology of a walkable grid; throws <see cref="OperationCanceledException"/> when cancelled.</summary>
        public static MapTopology Build(ReadOnlySpan<byte> grid, int bytesPerRow, int rows, CancellationToken cancel)
        {
            if (bytesPerRow <= 0 || rows <= 0 || grid.Length < (long)bytesPerRow * rows)
            {
                throw new ArgumentException("The grid is smaller than its rows.", nameof(grid));
            }

            return new MapTopology(Builder.Run(grid, bytesPerRow, rows, cancel));
        }

        /// <summary>The fork or room <paramref name="cell"/> stands in (within its radius plus <paramref name="slack"/>); null elsewhere.</summary>
        public int? NodeNear(Vector2 cell, float slack)
        {
            var item = this.ItemAt(cell, LookupSlackCells);
            if (item >= 0 && item < this.nodes.Length && this.nodes[item].Kind == TopologyNodeKind.Room)
            {
                return item;
            }

            var piece = item >= 0 ? this.PieceOf(item) : -1;
            int? best = null;
            var bestGap = slack;
            foreach (var node in this.nodes)
            {
                if (node.Kind == TopologyNodeKind.End || (piece >= 0 && node.Piece != piece))
                {
                    continue;
                }

                var gap = Vector2.Distance(cell, node.Centre) - node.Radius;
                if (gap <= bestGap)
                {
                    (best, bestGap) = (node.Id, gap);
                }
            }

            return best;
        }

        /// <summary>The ways leaving <paramref name="node"/>; a loop back to it is two ways.</summary>
        public IReadOnlyList<Way> Ways(int node) => node >= 0 && node < this.ways.Length ? this.ways[node] : Array.Empty<Way>();

        /// <summary>The piece of ground (joined by walking, not by teleports) <paramref name="cell"/> is on; null off the analysed ground.</summary>
        public int? PieceAt(Vector2 cell)
        {
            var item = this.ItemAt(cell, LookupSlackCells);
            return item >= 0 ? this.PieceOf(item) : null;
        }

        /// <summary>
        ///     Walking from <paramref name="from"/> to <paramref name="to"/>: the ground past the first branch on the way whose far
        ///     side holds no goal (<see cref="SetGoals"/>), <paramref name="to"/> lying in it; null when every branch on the way
        ///     leads on to a goal, when none parts them (a loop), or when they are on different pieces. The far sides shrink
        ///     along the way, so this is the largest ground with no way on around <paramref name="to"/>.
        /// </summary>
        public DeadEnd? DeadEndToward(Vector2 from, Vector2 to)
        {
            var fromItem = this.ItemAt(from, LookupSlackCells);
            var toItem = this.ItemAt(to, LookupSlackCells);
            if (fromItem < 0 || toItem < 0)
            {
                return null;
            }

            var fromBlock = this.FromBlock(fromItem, from);
            var toBlock = this.ToBlock(toItem, fromBlock);
            if (fromBlock == toBlock || this.blockPiece[fromBlock] != this.blockPiece[toBlock])
            {
                return null;
            }

            for (var block = fromBlock; !this.Holds(block, toBlock); block = this.blockParent[block])
            {
                if (this.Judge(new Side(block, true), this.blockBridge[block], block) is { } up)
                {
                    return up;
                }
            }

            var down = new List<int>();
            for (var block = toBlock; !this.Holds(block, fromBlock); block = this.blockParent[block])
            {
                down.Add(block);
            }

            for (var i = down.Count - 1; i >= 0; i--)
            {
                if (this.Judge(new Side(down[i], false), this.blockBridge[down[i]], this.blockParent[down[i]]) is { } dead)
                {
                    return dead;
                }
            }

            return null;
        }

        /// <summary>The ground beyond <paramref name="way"/> from <paramref name="node"/> holds no goal and has no other way out.</summary>
        public bool IsDeadEndWay(int node, Way way, out DeadEnd info)
        {
            info = default;
            if (node < 0 || node >= this.nodes.Length || way.Edge < 0 || way.Edge >= this.edges.Length || !this.edges[way.Edge].Bridge)
            {
                return false;
            }

            var dead = this.Judge(this.Beyond(node, way.Edge), way.Edge, this.nodeBlock[node]);
            info = dead ?? default;
            return dead != null;
        }

        /// <summary>The way out of <paramref name="node"/> the shortest walk to <paramref name="goal"/> takes; null when the goal stands in it or on other ground.</summary>
        public Way? WayToward(int node, Vector2 goal)
        {
            var item = this.ItemAt(goal, GoalSlackCells);
            if (item < 0 || item == node || node < 0 || node >= this.nodes.Length || this.PieceOf(item) != this.nodes[node].Piece)
            {
                return null;
            }

            // Walks to the goal from every place, outward from it; a goal on a way counts half of it from either end.
            var walks = new Dictionary<int, float>();
            var queue = new PriorityQueue<int, float>();
            var goalEdge = item - this.nodes.Length;
            if (goalEdge < 0)
            {
                queue.Enqueue(item, 0f);
            }
            else
            {
                queue.Enqueue(this.edges[goalEdge].A, this.edges[goalEdge].Length / 2);
                queue.Enqueue(this.edges[goalEdge].B, this.edges[goalEdge].Length / 2);
            }

            while (queue.TryDequeue(out var at, out var walk))
            {
                if (at == node || !walks.TryAdd(at, walk))
                {
                    continue; // a walk back through the place itself is none of its ways' own
                }

                foreach (var way in this.ways[at])
                {
                    queue.Enqueue(way.To, walk + this.edges[way.Edge].Length);
                }
            }

            Way? best = null;
            var shortest = float.MaxValue;
            foreach (var way in this.ways[node])
            {
                var length = this.edges[way.Edge].Length;
                var walk = way.Edge == goalEdge ? length / 2 : walks.TryGetValue(way.To, out var rest) ? length + rest : float.MaxValue;
                if (walk < shortest)
                {
                    (best, shortest) = (way, walk);
                }
            }

            return best;
        }

        /// <summary>The places worth reaching (exits, quest targets, unfound boss rooms): ground holding one is never a dead end.</summary>
        public void SetGoals(IEnumerable<Vector2> cells)
        {
            var inBlock = new int[this.blockParent.Length];
            var onBridge = new int[this.edges.Length];
            foreach (var cell in cells)
            {
                var item = this.ItemAt(cell, GoalSlackCells);
                if (item < 0)
                {
                    continue;
                }

                if (item >= this.nodes.Length && this.edges[item - this.nodes.Length].Bridge)
                {
                    onBridge[item - this.nodes.Length]++;
                }
                else
                {
                    inBlock[this.BlockOf(item)]++;
                }
            }

            this.goals = new Goals(this.SumUp(inBlock, onBridge), onBridge);
        }

        /// <summary>The item owning the walkable ground at or within <paramref name="slack"/> cells of <paramref name="cell"/>; -1 for none.</summary>
        private int ItemAt(Vector2 cell, int slack)
        {
            if (this.owner.Length == 0 || !float.IsFinite(cell.X) || !float.IsFinite(cell.Y))
            {
                return -1;
            }

            var bx = (int)MathF.Floor((cell.X - this.left) / OwnerBlock);
            var by = (int)MathF.Floor((cell.Y - this.top) / OwnerBlock);
            var reach = (slack + OwnerBlock - 1) / OwnerBlock;
            for (var ring = 0; ring <= reach; ring++)
            {
                var best = -1;
                var bestDistance = int.MaxValue;
                for (var y = by - ring; y <= by + ring; y++)
                {
                    for (var x = bx - ring; x <= bx + ring; x++)
                    {
                        if ((Math.Abs(x - bx) != ring && Math.Abs(y - by) != ring) || x < 0 || y < 0 || x >= this.blocksX || y >= this.blocksY)
                        {
                            continue;
                        }

                        var item = this.owner[(y * this.blocksX) + x] - 1;
                        var distance = ((x - bx) * (x - bx)) + ((y - by) * (y - by));
                        if (item >= 0 && distance < bestDistance)
                        {
                            (best, bestDistance) = (item, distance);
                        }
                    }
                }

                if (best >= 0)
                {
                    return best;
                }
            }

            return -1;
        }

        /// <summary>The goals counted up the branch tree: per block, those in its subtree, the bridges into its children included.</summary>
        private sealed record Goals(int[] Below, int[] OnBridge);
    }

    /// <summary>What a place is: a fork of corridors, a room (wide ground, its ways collapsed into one place), or the end of a way.</summary>
    public enum TopologyNodeKind
    {
        Junction,
        Room,
        End,
    }

    /// <summary>A place: its centre and radius (a room's reach, a fork's half-width) in cells, and the piece of ground it is on.</summary>
    public readonly record struct TopologyNode(int Id, TopologyNodeKind Kind, Vector2 Centre, float Radius, int Piece);

    /// <summary>A way between two places: its length and mean width in cells, and whether it is the only way between its two sides.</summary>
    public readonly record struct TopologyEdge(int A, int B, float Length, float Width, bool Bridge);

    /// <summary>
    ///     A way out of a place: its edge and far place, its unit direction (grid space) over its first cells, a cell a little way
    ///     along it (for a "seen" check), and whether the ground beyond is a nook: a branch under one look (<see cref="MapTopology.RevealDiscCells"/>).
    /// </summary>
    public readonly record struct Way(int Edge, int To, Vector2 Direction, Vector2 Ahead, bool Nook);

    /// <summary>Ground with no way on: its area and largest extent in cells, and the cell where the branch into it begins.</summary>
    public readonly record struct DeadEnd(int AreaCells, int ExtentCells, Vector2 Entrance);

    /// <summary>A narrow passage: its narrowest cell, width in cells, unit direction along it, and whether it is the only way between its sides.</summary>
    public readonly record struct Narrow(Vector2 Cell, float WidthCells, Vector2 Direction, bool Bridge, int Edge);
}
