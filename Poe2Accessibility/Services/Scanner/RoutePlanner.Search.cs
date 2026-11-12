namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Runtime.CompilerServices;

    /// <summary>The jump point search of <see cref="RoutePlanner"/>: expansion, cost and priority rules, pocket flooding and rebuilding the route.</summary>
    public static partial class RoutePlanner
    {
        /// <summary>
        ///     The distance left counts slightly over 1 in the priority, so of equal-cost ways the search follows the one
        ///     nearest the goal (far fewer cells expanded); a route is at most this much longer than the optimum.
        /// </summary>
        private const double TieBreak = 1.0001;

        /// <summary>Priority differences below this are float noise, not a better entry.</summary>
        private const double StaleSlack = 1e-3;

        /// <summary>
        ///     Jump point search from one start cell (A* that only keeps the cells where the way can turn, so open ground
        ///     costs a handful of steps instead of every cell; same optimal routes, same no-corner-cutting rule).
        ///     <paramref name="region"/> counts the cells it looked at (a pocket stays small).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static List<Vector2>? Search(Workspace space, (int X, int Y) from, (int X, int Y) to, bool orNearest, int budget,
                                             out int region, out bool reached, out bool spent)
        {
            reached = false;
            spent = false;
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

                    if (expansions++ >= budget)
                    {
                        spent = true;
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
                LastExpansions = expansions;
                region = scan.Looked;
            }
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
    }
}
