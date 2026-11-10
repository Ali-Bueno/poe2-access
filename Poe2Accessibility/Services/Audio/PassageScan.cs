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

        /// <summary>
        ///     What makes a doorway, in cells (D2R `Rooms::Doorways`, its tiles scaled as the radar's): a gap at most
        ///     <paramref name="MaxWidth"/> wide, a wall of at least <paramref name="Flank"/> either side, and ground
        ///     <paramref name="Widens"/> wider each side of it within <paramref name="Depth"/> along the ray.
        /// </summary>
        public readonly record struct DoorwayLimits(float MaxWidth, float Flank, float Widens, float Depth);

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
        ///     The first doorway the ray walks through within <paramref name="depthCells"/>, at its middle: where it leaves one
        ///     space for another (D2R's "another chamber"). Null over plain floor and along a corridor; the way must go on a
        ///     doorway's width past it, as touching the border is not leaving through it.
        /// </summary>
        public static Vector2? Doorway(byte[] grid, int bytesPerRow, Vector2 start, Vector2 axis, float depthCells, DoorwayLimits limits)
        {
            if (bytesPerRow <= 0 || grid.Length < bytesPerRow)
            {
                return null;
            }

            var side = new Vector2(-axis.Y, axis.X);
            for (var along = 1f; along <= depthCells; along += 1f)
            {
                var row = start + (axis * along);
                if (!Open(grid, bytesPerRow, row))
                {
                    return null;
                }

                var left = Clear(grid, bytesPerRow, row, -side, limits.MaxWidth + 1);
                var right = Clear(grid, bytesPerRow, row, side, limits.MaxWidth + 1);
                var width = left + right;
                if (width > limits.MaxWidth || along + width > depthCells ||
                    !Walled(grid, bytesPerRow, row - (side * (left + 1)), -side, limits.Flank) ||
                    !Walled(grid, bytesPerRow, row + (side * (right + 1)), side, limits.Flank))
                {
                    continue;
                }

                if (Widens(grid, bytesPerRow, row, axis, side, width, limits) || Widens(grid, bytesPerRow, row, -axis, side, width, limits))
                {
                    return row + (side * ((right - left) / 2f));
                }
            }

            return null;
        }

        /// <summary>The ground goes wider than the gap within the doorway's depth, one way along the ray.</summary>
        private static bool Widens(byte[] grid, int bytesPerRow, Vector2 gap, Vector2 way, Vector2 side, float width, DoorwayLimits limits)
        {
            var wider = width + (2 * limits.Widens);
            for (var depth = 1f; depth <= limits.Depth; depth += 1f)
            {
                var row = gap + (way * depth);
                if (!Open(grid, bytesPerRow, row))
                {
                    return false;
                }

                if (Clear(grid, bytesPerRow, row, side, wider + 1) + Clear(grid, bytesPerRow, row, -side, wider + 1) >= wider)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Open cells from <paramref name="from"/> along <paramref name="way"/>, up to <paramref name="limit"/>.</summary>
        private static float Clear(byte[] grid, int bytesPerRow, Vector2 from, Vector2 way, float limit)
        {
            var clear = 0f;
            while (clear < limit && Open(grid, bytesPerRow, from + (way * (clear + 1))))
            {
                clear += 1f;
            }

            return clear;
        }

        /// <summary>Blocked for <paramref name="length"/> cells: a wall, not a pillar with ground behind it.</summary>
        private static bool Walled(byte[] grid, int bytesPerRow, Vector2 from, Vector2 way, float length)
        {
            for (var step = 0f; step < length; step += 1f)
            {
                if (Open(grid, bytesPerRow, from + (way * step)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Open(byte[] grid, int bytesPerRow, Vector2 cell) =>
            cell.X >= 0 && cell.Y >= 0 && RoutePlanner.IsWalkable(grid, bytesPerRow, (int)cell.X, (int)cell.Y);

        /// <summary>
        ///     An exit belongs to the ray whose quarter holds it, within <paramref name="reachCells"/>, with open ground up to
        ///     <paramref name="slackCells"/> short of it: a door stands in its wall, and a lane missed one on a diagonal.
        /// </summary>
        public static bool ExitToward(byte[] grid, int bytesPerRow, Vector2 start, Vector2 axis, Vector2 exit, float reachCells, float slackCells)
        {
            if (!ExplorationMap.AnyToward(start, axis, new[] { exit }, reachCells))
            {
                return false;
            }

            var offset = exit - start;
            var length = offset.Length();
            return length <= slackCells || BeaconMath.LineWalkable(grid, bytesPerRow, start, exit - (offset / length * slackCells));
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
