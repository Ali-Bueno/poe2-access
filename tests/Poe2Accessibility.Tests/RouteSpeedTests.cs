namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;

    /// <summary>The route search on realistic cave and room grids: valid, never longer than the optimum, and cheap on the frame thread.</summary>
    public static class RouteSpeedTests
    {
        /// <summary>Side of a large area's grid in cells (act areas are 1000 to 2000 cells wide).</summary>
        private const int LargeSide = 1600;

        private const int SmallSide = 120;

        /// <summary>The frame budget wanted (60 Hz = 16 ms; the target is a third of it).</summary>
        private const double TargetMs = 5.0;

        /// <summary>Generous bound the test enforces, so a slow machine does not flake.</summary>
        private const double BoundMs = 40.0;

        /// <summary>A route may be this much longer than the optimum (the search breaks ties by distance left).</summary>
        private const float LengthSlack = 1.001f;

        private const int Seeds = 6;

        private const int Waypoints = 6;

        private const int CorridorHalf = 2;

        private const float Diagonal = 1.41421356f;

        private const int RandomSide = 24;

        private const int RandomRounds = 400;

        private const int RandomSeed = 7;

        /// <summary>A start region smaller than this is a pocket (the planner's own threshold).</summary>
        private const int PocketCells = 64;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(RoutesAreValidAndNeverLongerThanTheOptimum), RoutesAreValidAndNeverLongerThanTheOptimum),
            (nameof(RandomGridsGiveTheOptimalRoute), RandomGridsGiveTheOptimalRoute),
            (nameof(AWalledGoalLeadsToTheNearestReachableCell), AWalledGoalLeadsToTheNearestReachableCell),
            (nameof(ALongRouteStaysWithinTheFrameBound), ALongRouteStaysWithinTheFrameBound),
            (nameof(AnUnreachableGoalStaysWithinTheFrameBound), AnUnreachableGoalStaysWithinTheFrameBound),
        };

        /// <summary>Cave-like grid: scattered rock blobs and walls with gaps; row = side / 2 bytes (two cells per byte).</summary>
        internal static (byte[] Grid, int Row) Cave(int side, int seed)
        {
            var random = new Random(seed);
            var cells = new bool[side, side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    cells[x, y] = true;
                }
            }

            var blobs = side * side / 600;
            for (var i = 0; i < blobs; i++)
            {
                var cx = random.Next(side);
                var cy = random.Next(side);
                var radius = random.Next(2, 9);
                for (var y = Math.Max(0, cy - radius); y < Math.Min(side, cy + radius); y++)
                {
                    for (var x = Math.Max(0, cx - radius); x < Math.Min(side, cx + radius); x++)
                    {
                        cells[x, y] = false;
                    }
                }
            }

            // Long walls with a gap, as rooms and corridors make.
            for (var i = 0; i < side / 20; i++)
            {
                var vertical = random.Next(2) == 0;
                var at = random.Next(side);
                var gap = random.Next(side - 20);
                for (var t = 0; t < side; t++)
                {
                    if (t >= gap && t < gap + 12)
                    {
                        continue;
                    }

                    var (x, y) = vertical ? (at, t) : (t, at);
                    cells[x, y] = false;
                }
            }

            // A winding corridor through random waypoints keeps the corners connected, whatever the walls did.
            var (px, py) = (0, 0);
            for (var i = 0; i <= Waypoints; i++)
            {
                var (nx, ny) = i == Waypoints ? (side - 1, side - 1) : (random.Next(side), random.Next(side));
                for (var t = 0; t <= side; t++)
                {
                    var cx = px + ((nx - px) * t / side);
                    var cy = py + ((ny - py) * t / side);
                    for (var y = Math.Max(0, cy - CorridorHalf); y <= Math.Min(side - 1, cy + CorridorHalf); y++)
                    {
                        for (var x = Math.Max(0, cx - CorridorHalf); x <= Math.Min(side - 1, cx + CorridorHalf); x++)
                        {
                            cells[x, y] = true;
                        }
                    }
                }

                (px, py) = (nx, ny);
            }

            // Both ends open so a route exists between the corners.
            for (var y = 0; y < 10; y++)
            {
                for (var x = 0; x < 10; x++)
                {
                    cells[x, y] = true;
                    cells[side - 1 - x, side - 1 - y] = true;
                }
            }

            var row = side / 2;
            var grid = new byte[row * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    if (cells[x, y])
                    {
                        grid[(y * row) + (x / 2)] |= (byte)(1 << ((x & 1) * 4));
                    }
                }
            }

            return (grid, row);
        }

        /// <summary>Plain Dijkstra over the same moves: the optimum to compare with (small grids only).</summary>
        private static float Optimum(byte[] grid, int row, Vector2 start, Vector2 goal)
        {
            var dist = new Dictionary<(int, int), float> { [((int)start.X, (int)start.Y)] = 0f };
            var open = new PriorityQueue<(int X, int Y), float>();
            open.Enqueue(((int)start.X, (int)start.Y), 0f);
            var done = new HashSet<(int, int)>();
            while (open.TryDequeue(out var cur, out _))
            {
                if (!done.Add(cur))
                {
                    continue;
                }

                if (cur == ((int)goal.X, (int)goal.Y))
                {
                    return dist[cur];
                }

                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var diagonal = dx != 0 && dy != 0;
                        if ((dx == 0 && dy == 0) || !RoutePlanner.IsWalkable(grid, row, cur.X + dx, cur.Y + dy) ||
                            (diagonal && (!RoutePlanner.IsWalkable(grid, row, cur.X + dx, cur.Y) || !RoutePlanner.IsWalkable(grid, row, cur.X, cur.Y + dy))))
                        {
                            continue;
                        }

                        var next = (cur.X + dx, cur.Y + dy);
                        var cost = dist[cur] + (diagonal ? Diagonal : 1f);
                        if (!dist.TryGetValue(next, out var known) || cost < known)
                        {
                            dist[next] = cost;
                            open.Enqueue(next, cost);
                        }
                    }
                }
            }

            return float.PositiveInfinity;
        }

        /// <summary>Random grid of the given rock density (percent), packed two cells to a byte.</summary>
        private static (byte[] Grid, int Row) Scatter(int side, int rockPercent, Random random)
        {
            var row = side / 2;
            var grid = new byte[row * side];
            for (var y = 0; y < side; y++)
            {
                for (var x = 0; x < side; x++)
                {
                    if (random.Next(100) >= rockPercent)
                    {
                        grid[(y * row) + (x / 2)] |= (byte)(1 << ((x & 1) * 4));
                    }
                }
            }

            return (grid, row);
        }

        private static void RandomGridsGiveTheOptimalRoute()
        {
            var random = new Random(RandomSeed);
            for (var round = 0; round < RandomRounds; round++)
            {
                var (grid, row) = Scatter(RandomSide, random.Next(5, 45), random);
                var start = new Vector2(random.Next(RandomSide), random.Next(RandomSide));
                var goal = new Vector2(random.Next(RandomSide), random.Next(RandomSide));
                if (!RoutePlanner.IsWalkable(grid, row, (int)start.X, (int)start.Y) || !RoutePlanner.IsWalkable(grid, row, (int)goal.X, (int)goal.Y))
                {
                    continue;
                }

                if (Reach(grid, row, start).Count < PocketCells)
                {
                    continue; // a pocket: the planner starts from the ground beside it, by design
                }

                var best = Optimum(grid, row, start, goal);
                var route = RoutePlanner.FindRoute(grid, row, start, goal);
                if (float.IsPositiveInfinity(best) != (route == null))
                {
                    Fail($"round {round}: route {(route == null ? "missing" : "found")} but the optimum is {best}");
                }

                if (route != null)
                {
                    if (route[0] != start || route[^1] != goal || RoutePlanner.Length(route) > best * LengthSlack)
                    {
                        Fail($"round {round}: route {RoutePlanner.Length(route):F2} against the optimum {best:F2}");
                    }

                    for (var i = 1; i < route.Count; i++)
                    {
                        var (dx, dy) = ((int)(route[i].X - route[i - 1].X), (int)(route[i].Y - route[i - 1].Y));
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1 || !RoutePlanner.IsWalkable(grid, row, (int)route[i].X, (int)route[i].Y) ||
                            (dx != 0 && dy != 0 && (!RoutePlanner.IsWalkable(grid, row, (int)route[i - 1].X + dx, (int)route[i - 1].Y) || !RoutePlanner.IsWalkable(grid, row, (int)route[i - 1].X, (int)route[i - 1].Y + dy))))
                        {
                            Fail($"round {round}: step {i} is not a legal move");
                        }
                    }
                }
            }
        }

        private static void AWalledGoalLeadsToTheNearestReachableCell()
        {
            var random = new Random(RandomSeed + 1);
            for (var round = 0; round < RandomRounds; round++)
            {
                var (grid, row) = Scatter(RandomSide, random.Next(20, 60), random);
                var start = new Vector2(random.Next(RandomSide), random.Next(RandomSide));
                var goal = new Vector2(random.Next(RandomSide), random.Next(RandomSide));
                if (!RoutePlanner.IsWalkable(grid, row, (int)start.X, (int)start.Y) || !RoutePlanner.IsWalkable(grid, row, (int)goal.X, (int)goal.Y))
                {
                    continue;
                }

                var reach = Reach(grid, row, start);
                if (reach.Count < PocketCells || reach.Contains(((int)goal.X, (int)goal.Y)))
                {
                    continue;
                }

                var route = RoutePlanner.FindRoute(grid, row, start, goal, true, out var reached);
                if (reached)
                {
                    Fail($"round {round}: reached an unreachable goal");
                }

                var end = route == null ? start : route[^1];
                var nearest = reach.Min(cell => Octile(cell.X, cell.Y, goal));

                if (Octile(end.X, end.Y, goal) > nearest + LengthSlack - 1f)
                {
                    Fail($"round {round}: the way ends {Octile(end.X, end.Y, goal):F2} from the goal, a cell as near as {nearest:F2} is reachable");
                }
            }
        }

        /// <summary>The cells connected to a start (four-way is enough: a legal diagonal needs both sides open).</summary>
        private static HashSet<(int X, int Y)> Reach(byte[] grid, int row, Vector2 start)
        {
            var seen = new HashSet<(int X, int Y)> { ((int)start.X, (int)start.Y) };
            var queue = new Queue<(int X, int Y)>(seen);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                {
                    if (RoutePlanner.IsWalkable(grid, row, next.Item1, next.Item2) && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return seen;
        }

        private static float Octile(float x, float y, Vector2 goal)
        {
            var (dx, dy) = (Math.Abs(x - goal.X), Math.Abs(y - goal.Y));
            return Math.Max(dx, dy) + ((Diagonal - 1f) * Math.Min(dx, dy));
        }

        private static void Fail(string message) => throw new Exception(message);

        private static void RoutesAreValidAndNeverLongerThanTheOptimum()
        {
            for (var seed = 1; seed <= Seeds; seed++)
            {
                var (grid, row) = Cave(SmallSide, seed);
                var start = new Vector2(2, 2);
                var goal = new Vector2(SmallSide - 3, SmallSide - 3);
                var best = Optimum(grid, row, start, goal);
                var route = RoutePlanner.FindRoute(grid, row, start, goal);
                if (float.IsPositiveInfinity(best))
                {
                    if (route != null)
                    {
                        Fail($"seed {seed}: a route where there is none");
                    }

                    continue;
                }

                if (route == null || route[0] != start || route[^1] != goal)
                {
                    Fail($"seed {seed}: route missing or not from start to goal");
                }

                for (var i = 0; i < route!.Count; i++)
                {
                    if (!RoutePlanner.IsWalkable(grid, row, (int)route[i].X, (int)route[i].Y) ||
                        (i > 0 && Math.Max(Math.Abs(route[i].X - route[i - 1].X), Math.Abs(route[i].Y - route[i - 1].Y)) != 1f))
                    {
                        Fail($"seed {seed}: step {i} is blocked or not adjacent");
                    }
                }

                var length = RoutePlanner.Length(route);
                if (length > best * LengthSlack)
                {
                    Fail($"seed {seed}: route {length:F2} longer than the optimum {best:F2}");
                }
            }
        }

        /// <summary>Median of several searches in ms, after one warm-up.</summary>
        private static double Median(Action search)
        {
            search();
            var times = new List<double>();
            for (var i = 0; i < Seeds; i++)
            {
                var watch = Stopwatch.StartNew();
                search();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            return times[times.Count / 2];
        }

        private static void ALongRouteStaysWithinTheFrameBound()
        {
            var worst = 0d;
            for (var seed = 1; seed <= Seeds; seed++)
            {
                var (grid, row) = Cave(LargeSide, seed);
                var start = new Vector2(2, 2);
                var goal = new Vector2(LargeSide - 3, LargeSide - 3);
                List<Vector2>? route = null;
                var ms = Median(() => route = RoutePlanner.FindRoute(grid, row, start, goal));
                Console.WriteLine($"    long route seed {seed}: {(route == null ? "none" : route.Count + " cells")} in {ms:F1} ms (target {TargetMs} ms)");
                worst = Math.Max(worst, ms);
            }

            if (worst > BoundMs)
            {
                Fail($"a long route took {worst:F1} ms (bound {BoundMs} ms)");
            }
        }

        private static void AnUnreachableGoalStaysWithinTheFrameBound()
        {
            var (grid, row) = Cave(LargeSide, 1);
            // Walls off the goal corner completely.
            for (var i = 0; i < 40; i++)
            {
                foreach (var (x, y) in new[] { (LargeSide - 40, LargeSide - 1 - i), (LargeSide - 1 - i, LargeSide - 40) })
                {
                    grid[(y * row) + (x / 2)] &= (byte)(0xF0 >> ((x & 1) * 4));
                }
            }

            var start = new Vector2(2, 2);
            var goal = new Vector2(LargeSide - 3, LargeSide - 3);
            var reached = true;
            List<Vector2>? route = null;
            var ms = Median(() => route = RoutePlanner.FindRoute(grid, row, start, goal, true, out reached));
            Console.WriteLine($"    walled goal: {(route == null ? "none" : route.Count + " cells")} reached={reached} in {ms:F1} ms");
            if (ms > BoundMs * 2)
            {
                Fail($"a walled-off goal took {ms:F1} ms (bound {BoundMs * 2} ms)");
            }
        }
    }
}
