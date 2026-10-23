namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     How far the walkable ground runs along one radar ray (D2R radar_passage.cpp): a breadth-first walk over
    ///     walkable cells that never steps backwards along the ray and stays within a narrow lane around it, so the
    ///     depth is the connected way ahead, not the first blocked cell on a line.
    /// </summary>
    public static class PassageScan
    {
        /// <summary>Depth in cells along the ray; Unknown when the grid ends before the opening depth.</summary>
        public readonly record struct Result(float Depth, bool Unknown);

        /// <summary>How far past the open depth the obstacle test looks for the blocked cell that stopped the ray.</summary>
        private const float ObstacleSearchCells = 3f;

        private static readonly (int X, int Y)[] Neighbours =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        };

        /// <param name="grid">Walkable grid, two cells per byte (low nibble first), zero = blocked.</param>
        /// <param name="start">Player position in cells.</param>
        /// <param name="axis">Unit ray direction in cells.</param>
        public static Result Scan(byte[] grid, int bytesPerRow, Vector2 start, Vector2 axis, float laneCells, float reachCells, float openCells)
        {
            if (bytesPerRow <= 0 || grid.Length < bytesPerRow)
            {
                return new Result(0, true);
            }

            var columns = bytesPerRow * 2;
            var rows = grid.Length / bytesPerRow;
            var side = new Vector2(-axis.Y, axis.X);
            var first = ((int)start.X, (int)start.Y);
            var visited = new HashSet<int> { (first.Item2 * columns) + first.Item1 };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(first);
            var depth = 0f;
            var unknown = false;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                var cellProjection = Vector2.Dot(Centre(cell) - start, axis);
                foreach (var (dx, dy) in Neighbours)
                {
                    var next = (X: cell.X + dx, Y: cell.Y + dy);
                    var offset = Centre(next) - start;
                    var projection = Vector2.Dot(offset, axis);
                    var step = projection - cellProjection;
                    if (step < 0 || (cell == first && step <= 0) || MathF.Abs(Vector2.Dot(offset, side)) > laneCells || projection > reachCells)
                    {
                        continue;
                    }

                    if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows)
                    {
                        unknown |= projection <= openCells; // the grid ends: never a wall, only the end of what is known
                        continue;
                    }

                    var index = (next.Y * columns) + next.X;
                    if (!RoutePlanner.IsWalkable(grid, bytesPerRow, next.X, next.Y) || !visited.Add(index))
                    {
                        continue;
                    }

                    depth = MathF.Max(depth, projection);
                    queue.Enqueue(next);
                }
            }

            return new Result(depth, unknown);
        }

        /// <summary>
        ///     Whether the blocked cells that stop a ray form a small obstacle the player can step round (D2R
        ///     small-obstacle timbre): the first blocked cell within a few cells past <paramref name="depthCells"/>, its
        ///     8-connected blocked group no wider or taller than <paramref name="maxSide"/> cells and clear of the map's edge.
        /// </summary>
        public static bool IsSmallObstacle(byte[] grid, int bytesPerRow, Vector2 start, Vector2 axis, float depthCells, int maxSide)
        {
            if (bytesPerRow <= 0 || grid.Length < bytesPerRow)
            {
                return false;
            }

            var columns = bytesPerRow * 2;
            var rows = grid.Length / bytesPerRow;
            (int X, int Y)? first = null;
            for (var step = 0f; step <= ObstacleSearchCells && first == null; step += 0.5f)
            {
                var point = start + (axis * (depthCells + step));
                var cell = ((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));
                if (cell.Item1 < 0 || cell.Item2 < 0 || cell.Item1 >= columns || cell.Item2 >= rows)
                {
                    return false; // the map's edge stops it: a wall
                }

                if (!RoutePlanner.IsWalkable(grid, bytesPerRow, cell.Item1, cell.Item2))
                {
                    first = cell;
                }
            }

            if (first is not { } seed)
            {
                return false;
            }

            var seen = new HashSet<(int, int)> { seed };
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue(seed);
            int minX = seed.X, maxX = seed.X, minY = seed.Y, maxY = seed.Y;
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var (dx, dy) in Neighbours)
                {
                    var next = (X: cell.X + dx, Y: cell.Y + dy);
                    if (next.X < 0 || next.Y < 0 || next.X >= columns || next.Y >= rows)
                    {
                        return false; // joined to the map's edge
                    }

                    if (RoutePlanner.IsWalkable(grid, bytesPerRow, next.X, next.Y) || !seen.Add(next))
                    {
                        continue;
                    }

                    (minX, maxX, minY, maxY) = (Math.Min(minX, next.X), Math.Max(maxX, next.X), Math.Min(minY, next.Y), Math.Max(maxY, next.Y));
                    if (maxX - minX >= maxSide || maxY - minY >= maxSide)
                    {
                        return false; // grows past the size of a rock or a pillar: a wall
                    }

                    queue.Enqueue(next);
                }
            }

            return true;
        }

        private static Vector2 Centre((int X, int Y) cell) => new(cell.X + 0.5f, cell.Y + 0.5f);
    }
}
