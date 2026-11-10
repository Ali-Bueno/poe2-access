namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    /// <summary>
    ///     Shortest walkable route on the area's grid (A*, 8 neighbours, no corner cutting). The grid is the
    ///     game's walkability map as GameHelper reads it: one 4-bit cell per grid unit, two per byte, 0 = blocked
    ///     (same decoding as GameHelper's Radar). Pure: the caller passes the grid.
    /// </summary>
    public static class RoutePlanner
    {
        /// <summary>Expansion budget: a whole act-1 area is a few hundred thousand cells.</summary>
        private const int MaxExpansions = 600_000;

        /// <summary>How far to look for a walkable cell when the player stands on a blocked one.</summary>
        private const int StartSnapRadius = 12;

        /// <summary>A start region smaller than this (under 8x8 cells) is a pocket beside a rock, not the ground the player walks on.</summary>
        private const int PocketCells = 64;

        /// <summary>How far from a blocked own cell other start cells are tried (a pocket sits right beside the rock).</summary>
        private const int PocketSnapRadius = 4;

        /// <summary>Pockets tried before giving up on finding the player's real ground.</summary>
        private const int MaxStartTries = 6;

        /// <summary>Scans overlap, so the cells they count can be this many times the region they cover.</summary>
        private const int LookedPerCell = 8;

        /// <summary>A landmark's tile centre can sit inside a building (Clearfell's gate: blocked centre).</summary>
        private const int GoalSnapRadius = 60;

        private const float Diagonal = 1.41421356f;

        /// <summary><see cref="Keep"/>: the player within this many cells of the earlier route is still on it.</summary>
        private const float KeepNearCells = 6f;

        /// <summary><see cref="Keep"/>: two routes ending this close (cells) lead to the same place.</summary>
        private const float KeepEndCells = 3f;

        /// <summary><see cref="Keep"/>: the earlier way is kept unless the new one is shorter by this share of it...</summary>
        private const float KeepSlack = 0.1f;

        /// <summary>...plus this many cells (a step or two of walking: noise near the start).</summary>
        private const float KeepCells = 8f;

        /// <summary>
        ///     The distance left counts slightly over 1 in the priority, so of equal-cost ways the search follows the one
        ///     nearest the goal (far fewer cells expanded); a route is at most this much longer than the optimum.
        /// </summary>
        private const double TieBreak = 1.0001;

        /// <summary>Priority differences below this are float noise, not a better entry.</summary>
        private const double StaleSlack = 1e-3;

        [ThreadStatic]
        private static Workspace? workspace;

        private static readonly (int Dx, int Dy, float Cost)[] Neighbours =
        {
            (1, 0, 1f), (-1, 0, 1f), (0, 1, 1f), (0, -1, 1f),
            (1, 1, Diagonal), (1, -1, Diagonal), (-1, 1, Diagonal), (-1, -1, Diagonal),
        };

        public static bool IsWalkable(byte[] grid, int bytesPerRow, int x, int y)
        {
            if (x < 0 || y < 0 || x >= bytesPerRow * 2)
            {
                return false;
            }

            var index = (y * bytesPerRow) + (x / 2);
            return (uint)index < (uint)grid.Length && ((grid[index] >> ((x & 1) * 4)) & 0xF) != 0;
        }

        /// <summary>Grid cells from start to goal, or null when no route exists within the budget.</summary>
        public static List<Vector2>? FindRoute(byte[] grid, int bytesPerRow, Vector2 start, Vector2 goal) =>
            FindRoute(grid, bytesPerRow, start, goal, false, out _);

        /// <summary>The walkable cell a route to <paramref name="goal"/> would end on; null when none is near it.</summary>
        public static Vector2? Snap(byte[] grid, int bytesPerRow, Vector2 goal) =>
            TrySnap(grid, bytesPerRow, (int)MathF.Round(goal.X), (int)MathF.Round(goal.Y), GoalSnapRadius, out var cell) ? new Vector2(cell.X, cell.Y) : null;

        /// <summary>
        ///     Like <see cref="FindRoute(byte[], int, Vector2, Vector2)"/>, but when the goal cannot be reached (a gate
        ///     still closed) and <paramref name="orNearest"/> is set, the route to the reachable cell closest to it:
        ///     the player is led as far as the way goes, to the gate the quest will open. <paramref name="reached"/>
        ///     says which one it is.
        /// </summary>
        public static List<Vector2>? FindRoute(byte[] grid, int bytesPerRow, Vector2 start, Vector2 goal, bool orNearest, out bool reached)
        {
            reached = false;
            if (grid.Length == 0 || bytesPerRow <= 0 ||
                !TrySnap(grid, bytesPerRow, (int)MathF.Round(goal.X), (int)MathF.Round(goal.Y), GoalSnapRadius, out var to))
            {
                return null;
            }

            // The player's own cell can read blocked beside a rock, and the nearest walkable cell can be a pocket of a
            // few cells cut off from the ground around it: a search that ends in a pocket tries the next nearest cell
            // outside what was already searched (the route flipped between "no route" and the long way each step).
            // Only among cells within PocketSnapRadius of the player: ground farther away is not where they stand.
            var origin = ((int)MathF.Round(start.X), (int)MathF.Round(start.Y));
            var space = workspace ??= new Workspace();
            space.Prepare(grid, bytesPerRow);
            List<Vector2>? first = null;
            var firstReached = false;
            var tries = 0;
            foreach (var from in StartCandidates(grid, bytesPerRow, origin.Item1, origin.Item2, StartSnapRadius))
            {
                if (from.X < 0 || from.Y < 0 || from.X >= space.Width || from.Y >= space.Height || space.Searched((from.Y * space.Width) + from.X))
                {
                    continue;
                }

                if (tries > 0 && Math.Max(Math.Abs(from.X - origin.Item1), Math.Abs(from.Y - origin.Item2)) > PocketSnapRadius)
                {
                    break;
                }

                var route = Search(space, from, to, orNearest, out var region, out var found);
                if (!found && region < PocketCells * LookedPerCell)
                {
                    region = Flood(space, grid, bytesPerRow, from); // the scans overlap and skip cells: count the whole pocket
                }

                if (tries++ == 0)
                {
                    (first, firstReached) = (route, found);
                }

                if (found || region >= PocketCells || tries >= MaxStartTries)
                {
                    reached = found;
                    return route;
                }
            }

            reached = firstReached;
            return first;
        }

        /// <summary>
        ///     Jump point search from one start cell (A* that only keeps the cells where the way can turn, so open ground
        ///     costs a handful of steps instead of every cell; same optimal routes, same no-corner-cutting rule).
        ///     <paramref name="region"/> counts the cells it looked at (a pocket stays small).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static List<Vector2>? Search(Workspace space, (int X, int Y) from, (int X, int Y) to, bool orNearest,
                                             out int region, out bool reached)
        {
            reached = false;
            region = 0;
            var width = space.Width;
            if (from.X < 0 || from.Y < 0 || from.X >= width || from.Y >= space.Height || to.X < 0 || to.Y < 0 || to.X >= width || to.Y >= space.Height)
            {
                return null;
            }

            var scan = space.Begin(orNearest, to);
            var nodes = space.Nodes;
            var open = space.Open;
            var startIndex = (from.Y * width) + from.X;
            var startLeft = Heuristic(from.X, from.Y, to.X, to.Y);
            nodes[startIndex] = (0f, -1);
            open.Enqueue(startIndex, startLeft * TieBreak);
            scan.Closest = startIndex;
            scan.ClosestDistance = startLeft;
            scan.ClosestAnchor = startIndex;
            var expansions = 0;
            try
            {
                while (open.TryDequeue(out var current, out var priority))
                {
                    var (cx, cy) = (current % width, current / width);
                    var here = nodes[current].Cost;
                    if (priority > (here + (Heuristic(cx, cy, to.X, to.Y) * TieBreak)) + StaleSlack)
                    {
                        continue; // a better entry for this cell was already expanded
                    }

                    if (expansions++ >= MaxExpansions)
                    {
                        break;
                    }

                    if (current == scan.Goal)
                    {
                        reached = true;
                        return Rebuild(nodes, current, width);
                    }

                    var parent = nodes[current].Parent;
                    var (pdx, pdy) = parent < 0 ? (0, 0) : (Math.Sign(cx - (parent % width)), Math.Sign(cy - (parent / width)));
                    foreach (var (dx, dy, step) in Neighbours)
                    {
                        if (!scan.Wanted(cx, cy, dx, dy, pdx, pdy))
                        {
                            continue;
                        }

                        scan.Anchor = current;
                        var jump = dx != 0 && dy != 0 ? scan.JumpDiagonal(cx, cy, dx, dy) : scan.JumpStraight(cx, cy, dx, dy, -1);
                        if (jump < 0)
                        {
                            continue;
                        }

                        var steps = Math.Max(Math.Abs((jump % width) - cx), Math.Abs((jump / width) - cy));
                        var candidate = here + (steps * step);
                        if (nodes.TryGetValue(jump, out var known) && known.Cost <= candidate)
                        {
                            continue;
                        }

                        nodes[jump] = (candidate, current);
                        open.Enqueue(jump, candidate + (Heuristic(jump % width, jump / width, to.X, to.Y) * TieBreak));
                    }
                }

                if (orNearest && scan.Closest != startIndex)
                {
                    // The reachable cell nearest the goal: the way to the node it was seen from, then straight on.
                    var route = Rebuild(nodes, scan.ClosestAnchor, width);
                    if (scan.ClosestVia >= 0)
                    {
                        Extend(route, scan.ClosestVia, width);
                    }

                    Extend(route, scan.Closest, width);
                    return route;
                }

                return null;
            }
            finally
            {
                region = scan.Looked;
            }
        }

        /// <summary>
        ///     The way to follow when a route is searched again on the way to the same goal: the earlier route from the player's
        ///     place on it, unless the new one is clearly shorter. Of two ways of nearly equal length round a block the shorter
        ///     swaps as the player walks, and each fresh search sent them back toward the other side: Clearfell, 2026-11-08,
        ///     the guide's ping turned the player round four times in a corridor between two log piles (318 and 347 cells round
        ///     each side). The earlier way is kept while the player stands within <see cref="KeepNearCells"/> of it, it is still
        ///     walkable and ends where the new one does; a way that opens (a gate) and is shorter by more than
        ///     <see cref="KeepSlack"/> plus <see cref="KeepCells"/> replaces it.
        /// </summary>
        public static List<Vector2> Keep(IReadOnlyList<Vector2>? previous, List<Vector2> fresh, Vector2 player, byte[] grid, int bytesPerRow)
        {
            if (previous is not { Count: >= 2 } || fresh.Count < 2 || Vector2.Distance(previous[^1], fresh[^1]) > KeepEndCells)
            {
                return fresh;
            }

            var nearest = 0;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < previous.Count; i++)
            {
                var distance = Vector2.DistanceSquared(previous[i], player);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            nearestDistance = MathF.Sqrt(nearestDistance);
            if (nearestDistance > KeepNearCells)
            {
                return fresh; // the player left that way: the new search is where they are
            }

            var rest = new List<Vector2>(previous.Count - nearest);
            for (var i = nearest; i < previous.Count; i++)
            {
                if (!IsWalkable(grid, bytesPerRow, (int)MathF.Round(previous[i].X), (int)MathF.Round(previous[i].Y)))
                {
                    return fresh; // closed since (a barrier): only the new way is real
                }

                rest.Add(previous[i]);
            }

            var kept = nearestDistance + Length(rest);
            return kept <= (Length(fresh) * (1f + KeepSlack)) + KeepCells ? rest : fresh;
        }

        /// <summary>Length of a route in grid units.</summary>
        public static float Length(IReadOnlyList<Vector2> route)
        {
            var length = 0f;
            for (var i = 1; i < route.Count; i++)
            {
                length += Vector2.Distance(route[i - 1], route[i]);
            }

            return length;
        }

        /// <summary>
        ///     The route's real turns (Ramer–Douglas–Peucker): the staircase an 8-neighbour grid draws for a slanted
        ///     line stays within <paramref name="tolerance"/> grid units of it and falls away.
        /// </summary>
        public static List<Vector2> Corners(IReadOnlyList<Vector2> route, float tolerance)
        {
            if (route.Count < 3)
            {
                return new List<Vector2>(route);
            }

            var keep = new bool[route.Count];
            keep[0] = keep[^1] = true;
            var spans = new Stack<(int First, int Last)>();
            spans.Push((0, route.Count - 1));
            while (spans.Count > 0)
            {
                var (first, last) = spans.Pop();
                var farthest = -1;
                var worst = tolerance;
                for (var i = first + 1; i < last; i++)
                {
                    var off = DistanceToSegment(route[i], route[first], route[last]);
                    if (off > worst)
                    {
                        worst = off;
                        farthest = i;
                    }
                }

                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    spans.Push((first, farthest));
                    spans.Push((farthest, last));
                }
            }

            var corners = new List<Vector2>();
            for (var i = 0; i < route.Count; i++)
            {
                if (keep[i])
                {
                    corners.Add(route[i]);
                }
            }

            return corners;
        }

        /// <summary>
        ///     The route as legs to walk: one stick direction each (<paramref name="directionOf"/> bins a grid
        ///     vector) and its length in grid units. Legs shorter than <paramref name="minLeg"/> (a sidestep round a
        ///     rock; the game slides the player along walls anyway) are dropped, then neighbours that point the same
        ///     way are one leg.
        /// </summary>
        public static List<(int Direction, float Length)> Legs(IReadOnlyList<Vector2> corners, Func<Vector2, int> directionOf, float minLeg)
        {
            var segments = new List<(int Direction, float Length)>();
            for (var i = 1; i < corners.Count; i++)
            {
                var delta = corners[i] - corners[i - 1];
                if (delta.Length() > 0f)
                {
                    segments.Add((directionOf(delta), delta.Length()));
                }
            }

            var kept = segments.FindAll(segment => segment.Length >= minLeg);
            if (kept.Count == 0 && segments.Count > 0)
            {
                kept.Add(segments.MaxBy(segment => segment.Length)); // a short route: its main way
            }

            var legs = new List<(int Direction, float Length)>();
            foreach (var segment in kept)
            {
                if (legs.Count > 0 && legs[^1].Direction == segment.Direction)
                {
                    legs[^1] = (segment.Direction, legs[^1].Length + segment.Length);
                }
                else
                {
                    legs.Add(segment);
                }
            }

            return legs;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var along = to - from;
            var lengthSquared = along.LengthSquared();
            if (lengthSquared == 0f)
            {
                return Vector2.Distance(point, from);
            }

            var t = Math.Clamp(Vector2.Dot(point - from, along) / lengthSquared, 0f, 1f);
            return Vector2.Distance(point, from + (along * t));
        }

        private static float Heuristic(int ax, int ay, int bx, int by)
        {
            var dx = Math.Abs(ax - bx);
            var dy = Math.Abs(ay - by);
            return Math.Max(dx, dy) + ((Diagonal - 1f) * Math.Min(dx, dy));
        }

        /// <summary>Cells of the walkable region around a start, marked as searched; stops at <see cref="PocketCells"/>.</summary>
        private static int Flood(Workspace space, byte[] grid, int bytesPerRow, (int X, int Y) from)
        {
            var width = space.Width;
            var queue = new Queue<int>();
            var start = (from.Y * width) + from.X;
            space.Seen[start] = space.Search;
            queue.Enqueue(start);
            var count = 0;
            while (queue.Count > 0 && count < PocketCells)
            {
                var current = queue.Dequeue();
                count++;
                var (cx, cy) = (current % width, current / width);
                foreach (var (dx, dy, _) in Neighbours.Take(4))
                {
                    var next = ((cy + dy) * width) + cx + dx;
                    if (IsWalkable(grid, bytesPerRow, cx + dx, cy + dy) && space.Seen[next] != space.Search)
                    {
                        space.Seen[next] = space.Search;
                        queue.Enqueue(next);
                    }
                }
            }

            return count + queue.Count;
        }

        /// <summary>The route through the jump points, the straight runs between them filled in cell by cell.</summary>
        private static List<Vector2> Rebuild(Dictionary<int, (float Cost, int Parent)> nodes, int end, int width)
        {
            var points = new List<int>();
            for (var current = end; current >= 0; current = nodes[current].Parent)
            {
                points.Add(current);
            }

            var route = new List<Vector2> { new(points[^1] % width, points[^1] / width) };
            for (var i = points.Count - 2; i >= 0; i--)
            {
                Extend(route, points[i], width);
            }

            return route;
        }

        /// <summary>Adds the straight (cardinal or diagonal) run from the route's last cell to <paramref name="to"/>.</summary>
        private static void Extend(List<Vector2> route, int to, int width)
        {
            var last = route[^1];
            var (tx, ty) = (to % width, to / width);
            var (x, y) = ((int)last.X, (int)last.Y);
            var (sx, sy) = (Math.Sign(tx - x), Math.Sign(ty - y));
            while (x != tx || y != ty)
            {
                x += sx;
                y += sy;
                route.Add(new Vector2(x, y));
            }
        }

        /// <summary>
        ///     Per-thread search state, kept between searches (fresh arrays per search cost more than the search): a
        ///     cell's mark says in which search it was looked at, so nothing is cleared.
        /// </summary>
        private sealed class Workspace
        {
            public readonly PriorityQueue<int, double> Open = new();
            public readonly Dictionary<int, (float Cost, int Parent)> Nodes = new();
            public int Width;
            public int Height;
            public int[] Seen = Array.Empty<int>();
            public int Search;
            public int CallFirst;
            private readonly Scanner scanner = new();

            /// <summary>True when a failed pocket search of the current route request already flooded the cell.</summary>
            public bool Searched(int index) => this.Seen[index] >= this.CallFirst;

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
                if (this.Seen.Length < width * height)
                {
                    (this.Seen, this.Search) = (new int[width * height], 0);
                }

                this.CallFirst = this.Search + 1;
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
                this.Search++;
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

        /// <summary>Walkable cells around a point, nearest ring first.</summary>
        private static IEnumerable<(int X, int Y)> StartCandidates(byte[] grid, int bytesPerRow, int x, int y, int maxRadius)
        {
            for (var radius = 0; radius <= maxRadius; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if ((Math.Abs(dx) == radius || Math.Abs(dy) == radius) && IsWalkable(grid, bytesPerRow, x + dx, y + dy))
                        {
                            yield return (x + dx, y + dy);
                        }
                    }
                }
            }
        }

        private static bool TrySnap(byte[] grid, int bytesPerRow, int x, int y, int maxRadius, out (int X, int Y) cell)
        {
            for (var radius = 0; radius <= maxRadius; radius++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        if ((Math.Abs(dx) == radius || Math.Abs(dy) == radius) && IsWalkable(grid, bytesPerRow, x + dx, y + dy))
                        {
                            cell = (x + dx, y + dy);
                            return true;
                        }
                    }
                }
            }

            cell = default;
            return false;
        }
    }
}
