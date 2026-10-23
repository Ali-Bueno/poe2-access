namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

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

        /// <summary>A landmark's tile centre can sit inside a building (Clearfell's gate: blocked centre).</summary>
        private const int GoalSnapRadius = 60;

        private const float Diagonal = 1.41421356f;

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
        public static List<Vector2>? FindRoute(byte[] grid, int bytesPerRow, Vector2 start, Vector2 goal)
        {
            if (grid.Length == 0 || bytesPerRow <= 0 ||
                !TrySnap(grid, bytesPerRow, (int)MathF.Round(start.X), (int)MathF.Round(start.Y), StartSnapRadius, out var from) ||
                !TrySnap(grid, bytesPerRow, (int)MathF.Round(goal.X), (int)MathF.Round(goal.Y), GoalSnapRadius, out var to))
            {
                return null;
            }

            var cameFrom = new Dictionary<(int, int), (int, int)>();
            var cost = new Dictionary<(int, int), float> { [from] = 0f };
            var open = new PriorityQueue<(int X, int Y), float>();
            open.Enqueue(from, Heuristic(from, to));
            var expansions = 0;
            while (open.TryDequeue(out var current, out _) && expansions++ < MaxExpansions)
            {
                if (current == to)
                {
                    return Rebuild(cameFrom, current);
                }

                var here = cost[current];
                foreach (var (dx, dy, step) in Neighbours)
                {
                    var next = (current.X + dx, current.Y + dy);
                    if (!IsWalkable(grid, bytesPerRow, next.Item1, next.Item2) ||
                        (dx != 0 && dy != 0 && (!IsWalkable(grid, bytesPerRow, current.X + dx, current.Y) || !IsWalkable(grid, bytesPerRow, current.X, current.Y + dy))))
                    {
                        continue;
                    }

                    var candidate = here + step;
                    if (cost.TryGetValue(next, out var known) && known <= candidate)
                    {
                        continue;
                    }

                    cost[next] = candidate;
                    cameFrom[next] = current;
                    open.Enqueue(next, candidate + Heuristic(next, to));
                }
            }

            return null;
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

        private static float Heuristic((int X, int Y) a, (int X, int Y) b)
        {
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            return Math.Max(dx, dy) + ((Diagonal - 1f) * Math.Min(dx, dy));
        }

        private static List<Vector2> Rebuild(Dictionary<(int, int), (int, int)> cameFrom, (int X, int Y) end)
        {
            var route = new List<Vector2> { new(end.X, end.Y) };
            var current = end;
            while (cameFrom.TryGetValue(current, out var previous))
            {
                current = previous;
                route.Add(new Vector2(current.X, current.Y));
            }

            route.Reverse();
            return route;
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
