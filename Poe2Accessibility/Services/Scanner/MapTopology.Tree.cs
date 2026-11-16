namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     The branch tree of <see cref="MapTopology"/>: the ways that are the only link between their two sides (bridges),
    ///     the blocks they part (places still joined without them), and, per block, the area and goals of its subtree.
    /// </summary>
    public sealed partial class MapTopology
    {
        private int[] nodeBlock = Array.Empty<int>();
        private int[] blockParent = Array.Empty<int>();
        private int[] blockBridge = Array.Empty<int>();
        private int[] blockPiece = Array.Empty<int>();
        private int[] blockFirst = Array.Empty<int>();
        private int[] blockLast = Array.Empty<int>();
        private int[] blockOrder = Array.Empty<int>();
        private int[] subArea = Array.Empty<int>();
        private int[] subBox = Array.Empty<int>();
        private int[] pieceRoot = Array.Empty<int>();

        private MapTopology(Draft draft)
        {
            this.HalfWidthCells = draft.HalfWidth;
            this.RoomDistanceCells = draft.RoomDistance;
            (this.owner, this.left, this.top, this.blocksX, this.blocksY) = (draft.Owner, draft.Left, draft.Top, draft.BlocksX, draft.BlocksY);
            (this.itemArea, this.itemBox, this.paths) = (draft.ItemArea, draft.ItemBox, draft.Paths);
            var count = draft.Kinds.Length;
            var (start, list) = Adjacency(count, draft.EdgeA, draft.EdgeB);
            var bridges = FindBridges(count, draft.EdgeA, draft.EdgeB, start, list);
            this.edges = new TopologyEdge[draft.EdgeA.Length];
            for (var e = 0; e < this.edges.Length; e++)
            {
                this.edges[e] = new TopologyEdge(draft.EdgeA[e], draft.EdgeB[e], draft.Lengths[e], draft.Widths[e], bridges[e]);
            }

            this.GrowTree(count, start, list);
            this.nodes = new TopologyNode[count];
            for (var v = 0; v < count; v++)
            {
                this.nodes[v] = new TopologyNode(v, draft.Kinds[v], draft.Centres[v], draft.Radii[v], this.blockPiece[this.nodeBlock[v]]);
            }

            this.goals = new Goals(this.SumUp(new int[this.blockParent.Length], new int[this.edges.Length]), new int[this.edges.Length]);
            this.ways = new Way[count][];
            for (var v = 0; v < count; v++)
            {
                this.ways[v] = this.WaysOf(v, start, list);
            }

            this.narrows = draft.Narrows.FindAll(this.Worth).ConvertAll(narrow => narrow with { Bridge = this.edges[narrow.Edge].Bridge }).ToArray();
        }

        /// <summary>Which side of a bridge: a block's subtree, or everything of its piece but that subtree.</summary>
        private readonly record struct Side(int Block, bool Complement);

        private static (int[] Start, int[] List) Adjacency(int count, int[] a, int[] b)
        {
            var start = new int[count + 1];
            foreach (var v in a)
            {
                start[v + 1]++;
            }

            foreach (var v in b)
            {
                start[v + 1]++;
            }

            for (var v = 0; v < count; v++)
            {
                start[v + 1] += start[v];
            }

            var list = new int[2 * a.Length];
            var fill = (int[])start.Clone();
            for (var e = 0; e < a.Length; e++)
            {
                list[fill[a[e]]++] = e;
                list[fill[b[e]]++] = e;
            }

            return (start, list);
        }

        /// <summary>Tarjan's bridges, walked with a stack: a way is one when nothing below it reaches back above it.</summary>
        private static bool[] FindBridges(int count, int[] a, int[] b, int[] start, int[] list)
        {
            var bridge = new bool[a.Length];
            var order = new int[count];
            var low = new int[count];
            var via = new int[count];
            var cursor = new int[count];
            var stack = new int[count];
            Array.Fill(order, -1);
            var time = 0;
            for (var root = 0; root < count; root++)
            {
                if (order[root] >= 0)
                {
                    continue;
                }

                var depth = 0;
                stack[depth++] = root;
                (order[root], low[root], via[root], cursor[root]) = (time, time, -1, start[root]);
                time++;
                while (depth > 0)
                {
                    var v = stack[depth - 1];
                    if (cursor[v] < start[v + 1])
                    {
                        var e = list[cursor[v]++];
                        if (e == via[v])
                        {
                            continue;
                        }

                        var u = a[e] == v ? b[e] : a[e];
                        if (order[u] < 0)
                        {
                            (order[u], low[u], via[u], cursor[u]) = (time, time, e, start[u]);
                            time++;
                            stack[depth++] = u;
                        }
                        else
                        {
                            low[v] = Math.Min(low[v], order[u]);
                        }

                        continue;
                    }

                    depth--;
                    if (via[v] >= 0)
                    {
                        var parent = stack[depth - 1];
                        low[parent] = Math.Min(low[parent], low[v]);
                        bridge[via[v]] = low[v] > order[parent];
                    }
                }
            }

            return bridge;
        }

        /// <summary>The blocks (places joined without bridges), the tree the bridges make of them per piece, and its subtree areas and boxes.</summary>
        private void GrowTree(int count, int[] start, int[] list)
        {
            this.nodeBlock = new int[count];
            Array.Fill(this.nodeBlock, -1);
            var stack = new int[Math.Max(count, 1)];
            var blocks = 0;
            for (var root = 0; root < count; root++)
            {
                if (this.nodeBlock[root] >= 0)
                {
                    continue;
                }

                var depth = 0;
                stack[depth++] = root;
                this.nodeBlock[root] = blocks;
                while (depth > 0)
                {
                    var v = stack[--depth];
                    for (var i = start[v]; i < start[v + 1]; i++)
                    {
                        var e = list[i];
                        var u = this.Other(e, v);
                        if (!this.edges[e].Bridge && this.nodeBlock[u] < 0)
                        {
                            this.nodeBlock[u] = blocks;
                            stack[depth++] = u;
                        }
                    }
                }

                blocks++;
            }

            var bridgesOf = new List<int>[blocks];
            for (var k = 0; k < blocks; k++)
            {
                bridgesOf[k] = new List<int>();
            }

            for (var e = 0; e < this.edges.Length; e++)
            {
                if (this.edges[e].Bridge)
                {
                    bridgesOf[this.nodeBlock[this.edges[e].A]].Add(e);
                    bridgesOf[this.nodeBlock[this.edges[e].B]].Add(e);
                }
            }

            this.blockParent = new int[blocks];
            this.blockBridge = new int[blocks];
            this.blockPiece = new int[blocks];
            this.blockFirst = new int[blocks];
            this.blockLast = new int[blocks];
            this.blockOrder = new int[blocks];
            Array.Fill(this.blockPiece, -1);
            var roots = new List<int>();
            var placed = 0;
            var pending = new Stack<int>();
            for (var root = 0; root < blocks; root++)
            {
                if (this.blockPiece[root] >= 0)
                {
                    continue;
                }

                (this.blockParent[root], this.blockBridge[root], this.blockPiece[root]) = (-1, -1, roots.Count);
                roots.Add(root);
                pending.Push(root);
                while (pending.Count > 0)
                {
                    var k = pending.Pop();
                    this.blockFirst[k] = placed;
                    this.blockOrder[placed++] = k;
                    foreach (var e in bridgesOf[k])
                    {
                        var a = this.nodeBlock[this.edges[e].A];
                        var child = a == k ? this.nodeBlock[this.edges[e].B] : a;
                        if (this.blockPiece[child] < 0)
                        {
                            (this.blockParent[child], this.blockBridge[child], this.blockPiece[child]) = (k, e, this.blockPiece[k]);
                            pending.Push(child);
                        }
                    }
                }
            }

            this.pieceRoot = roots.ToArray();
            var size = new int[blocks];
            for (var i = blocks - 1; i >= 0; i--)
            {
                var k = this.blockOrder[i];
                size[k]++;
                this.blockLast[k] = this.blockFirst[k] + size[k] - 1;
                if (this.blockParent[k] >= 0)
                {
                    size[this.blockParent[k]] += size[k];
                }
            }

            this.SumAreas(count);
        }

        private void SumAreas(int count)
        {
            var blocks = this.blockParent.Length;
            var area = new int[blocks];
            var bridgeArea = new int[this.edges.Length];
            this.subBox = new int[4 * blocks];
            for (var k = 0; k < blocks; k++)
            {
                (this.subBox[4 * k], this.subBox[(4 * k) + 1], this.subBox[(4 * k) + 2], this.subBox[(4 * k) + 3]) = (int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);
            }

            for (var item = 0; item < count + this.edges.Length; item++)
            {
                var edge = item - count;
                if (edge >= 0 && this.edges[edge].Bridge)
                {
                    bridgeArea[edge] = this.itemArea[item];
                    continue;
                }

                var k = edge >= 0 ? this.nodeBlock[this.edges[edge].A] : this.nodeBlock[item];
                area[k] += this.itemArea[item];
                Widen(this.subBox, k, this.itemBox, item);
            }

            for (var i = blocks - 1; i >= 0; i--)
            {
                var k = this.blockOrder[i];
                var parent = this.blockParent[k];
                if (parent >= 0)
                {
                    Widen(this.subBox, parent, this.subBox, k);
                    Widen(this.subBox, parent, this.itemBox, count + this.blockBridge[k]);
                }
            }

            this.subArea = this.SumUp(area, bridgeArea);
        }

        /// <summary>Per block, the sum over its subtree of <paramref name="inBlock"/>, plus <paramref name="onBridge"/> of the bridges inside it.</summary>
        private int[] SumUp(int[] inBlock, int[] onBridge)
        {
            var below = (int[])inBlock.Clone();
            for (var i = this.blockOrder.Length - 1; i >= 0; i--)
            {
                var k = this.blockOrder[i];
                var parent = this.blockParent[k];
                if (parent >= 0)
                {
                    below[parent] += below[k] + onBridge[this.blockBridge[k]];
                }
            }

            return below;
        }

        private static void Widen(int[] boxes, int k, int[] from, int item)
        {
            if (from[4 * item] > from[(4 * item) + 2])
            {
                return;
            }

            boxes[4 * k] = Math.Min(boxes[4 * k], from[4 * item]);
            boxes[(4 * k) + 1] = Math.Min(boxes[(4 * k) + 1], from[(4 * item) + 1]);
            boxes[(4 * k) + 2] = Math.Max(boxes[(4 * k) + 2], from[(4 * item) + 2]);
            boxes[(4 * k) + 3] = Math.Max(boxes[(4 * k) + 3], from[(4 * item) + 3]);
        }

        private static int Extent(int[] boxes, int k) =>
            boxes[4 * k] > boxes[(4 * k) + 2] ? 0 : Math.Max(boxes[(4 * k) + 2] - boxes[4 * k], boxes[(4 * k) + 3] - boxes[(4 * k) + 1]) + 1;

        private int Other(int edge, int node) => this.edges[edge].A == node ? this.edges[edge].B : this.edges[edge].A;

        private int BlockOf(int item) => item < this.nodes.Length ? this.nodeBlock[item] : this.nodeBlock[this.edges[item - this.nodes.Length].A];

        private int PieceOf(int item) => this.blockPiece[this.BlockOf(item)];

        /// <summary>Block <paramref name="ancestor"/>'s subtree holds block <paramref name="block"/>.</summary>
        private bool Holds(int ancestor, int block) => this.blockFirst[ancestor] <= this.blockFirst[block] && this.blockFirst[block] <= this.blockLast[ancestor];

        /// <summary>The block of the place walked from: on a bridge, the end nearer along it.</summary>
        private int FromBlock(int item, Vector2 cell)
        {
            var edge = item - this.nodes.Length;
            if (edge < 0 || !this.edges[edge].Bridge)
            {
                return this.BlockOf(item);
            }

            var path = this.paths[edge];
            var nearest = 0;
            for (var i = 1; i < path.Length; i++)
            {
                if (Vector2.DistanceSquared(path[i], cell) < Vector2.DistanceSquared(path[nearest], cell))
                {
                    nearest = i;
                }
            }

            return this.nodeBlock[2 * nearest < path.Length ? this.edges[edge].A : this.edges[edge].B];
        }

        /// <summary>The block of the place walked to: on a bridge, the side away from <paramref name="fromBlock"/>, so the bridge counts as crossed.</summary>
        private int ToBlock(int item, int fromBlock)
        {
            var edge = item - this.nodes.Length;
            if (edge < 0 || !this.edges[edge].Bridge)
            {
                return this.BlockOf(item);
            }

            var a = this.nodeBlock[this.edges[edge].A];
            var b = this.nodeBlock[this.edges[edge].B];
            var child = this.blockBridge[b] == edge ? b : a;
            var parent = child == a ? b : a;
            return this.Holds(child, fromBlock) ? parent : child;
        }

        /// <summary>The side of bridge <paramref name="edge"/> away from <paramref name="node"/>.</summary>
        private Side Beyond(int node, int edge)
        {
            var far = this.nodeBlock[this.Other(edge, node)];
            return this.blockBridge[far] == edge ? new Side(far, false) : new Side(this.nodeBlock[node], true);
        }

        /// <summary>The area of a bridge's side, the bridge's own corridor included.</summary>
        private int AreaOf(Side side, int bridge) => side.Complement
            ? this.subArea[this.pieceRoot[this.blockPiece[side.Block]]] - this.subArea[side.Block]
            : this.subArea[side.Block] + this.itemArea[this.nodes.Length + bridge];

        /// <summary>A bridge's side as a dead end, or null when it holds a goal (the bridge's corridor counts on either side).</summary>
        private DeadEnd? Judge(Side side, int bridge, int nearBlock)
        {
            var goals = this.goals;
            var root = this.pieceRoot[this.blockPiece[side.Block]];
            var held = side.Complement ? goals.Below[root] - goals.Below[side.Block] : goals.Below[side.Block] + goals.OnBridge[bridge];
            if (held > 0)
            {
                return null;
            }

            int extent;
            if (side.Complement)
            {
                extent = Extent(this.subBox, root);
            }
            else
            {
                var box = new int[4];
                Array.Copy(this.subBox, 4 * side.Block, box, 0, 4);
                Widen(box, 0, this.itemBox, this.nodes.Length + bridge);
                extent = Extent(box, 0);
            }

            var edge = this.edges[bridge];
            var near = this.nodeBlock[edge.A] == nearBlock ? edge.A : edge.B;
            var path = this.paths[bridge];
            var entrance = path.Length == 0 ? this.nodes[near].Centre : near == edge.A ? path[0] : path[^1];
            return new DeadEnd(this.AreaOf(side, bridge), extent, entrance);
        }

        private Way[] WaysOf(int v, int[] start, int[] list)
        {
            var found = new Way[start[v + 1] - start[v]];
            var loops = new List<int>();
            for (var i = start[v]; i < start[v + 1]; i++)
            {
                var e = list[i];
                var edge = this.edges[e];
                var forward = edge.A == v;
                if (edge.A == edge.B)
                {
                    forward = !loops.Contains(e);
                    loops.Add(e);
                }

                var far = this.nodes[this.Other(e, v)];
                var path = this.paths[e];
                Vector2 At(int k) => forward ? path[Math.Min(k, path.Length - 1)] : path[path.Length - 1 - Math.Min(k, path.Length - 1)];
                var direction = path.Length > 1 ? At(WayDirectionCells) - At(0) : Vector2.Zero;
                if (direction.LengthSquared() < 1e-6f)
                {
                    direction = far.Centre - this.nodes[v].Centre;
                }

                direction = direction.LengthSquared() < 1e-6f ? Vector2.UnitX : Vector2.Normalize(direction);
                var ahead = path.Length > 0 ? At(WayAheadCells) : far.Centre;
                var nook = edge.Bridge && this.AreaOf(this.Beyond(v, e), e) < RevealDiscCells;
                found[i - start[v]] = new Way(e, far.Id, direction, ahead, nook);
            }

            return found;
        }

        /// <summary>A narrow passage is worth a place unless it is the only way into a nook (a crack in the wall).</summary>
        private bool Worth(Narrow narrow)
        {
            var edge = this.edges[narrow.Edge];
            return !edge.Bridge
                || Math.Min(this.AreaOf(this.Beyond(edge.A, narrow.Edge), narrow.Edge), this.AreaOf(this.Beyond(edge.B, narrow.Edge), narrow.Edge)) >= RevealDiscCells;
        }
    }
}
