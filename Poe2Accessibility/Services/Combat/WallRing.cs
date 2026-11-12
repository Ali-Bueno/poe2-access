namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>Where the player stands to the walls a boss raised (<see cref="WallRing"/>).</summary>
    public enum WallWay
    {
        /// <summary>Beyond the walls already.</summary>
        Out,

        /// <summary>Walled in, with a way out on foot.</summary>
        Open,

        /// <summary>Walled in on every side: a wall must be broken.</summary>
        Closed,
    }

    /// <summary>The way out of a boss's walls: the place to walk to (the opening, or the nearest wall when there is none), in grid cells.</summary>
    public readonly record struct WallExit(WallWay Way, Vector2 At);

    /// <summary>
    ///     The way out of the walls a boss raises around its arena (Kabala's bone cage, Asinia's; docs/research/boss-mechanics.md
    ///     §13): the game's files give the ring the walls stand on, never which of them are missing, so the opening is
    ///     looked for on the ground itself, walking from the player over the cells no wall covers until the ring is behind. Pure.
    /// </summary>
    public static class WallRing
    {
        /// <summary>Fewer walls than this are a barrier beside the fight, not an enclosure.</summary>
        public const int MinWalls = 3;

        /// <summary>Cells beyond the walls' ring that count as out of it (the walls' own depth and a step more).</summary>
        private const float BeyondCells = 6f;

        /// <summary>
        ///     The share of the walls' usual spacing each one closes around itself: neighbours a spacing and a half apart
        ///     still shut the way between them, a missing wall (two spacings) leaves it open.
        /// </summary>
        private const float ClosedShare = 0.75f;

        /// <summary>A wall's half width (cells) at least and at most, whatever the spacing reads as (BoneWall.ot: objects of size 4).</summary>
        private const float MinHalfWidth = 1.5f;

        private const float MaxHalfWidth = 4f;

        /// <summary>Walls covering less of their circle than this (degrees), or lying this far off it (share of its radius), do not give its centre.</summary>
        private const float MinArcDegrees = 100f;

        private const float MaxOffRing = 0.2f;

        /// <summary>The largest side (cells) of the ground searched: twice Kabala's ring (radius 50 cells) and its margin.</summary>
        private const int MaxSide = 360;

        private static readonly (int Dx, int Dy)[] Steps = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

        /// <summary>
        ///     The way out for a player at <paramref name="player"/> among <paramref name="walls"/> (all in grid cells) over the
        ///     walkable <paramref name="grid"/>; null when the walls are too few or too far apart to search.
        /// </summary>
        public static WallExit? Read(byte[] grid, int bytesPerRow, Vector2 player, IReadOnlyList<Vector2> walls)
        {
            if (walls.Count < MinWalls)
            {
                return null;
            }

            var ring = Fit(walls);
            var reach = walls.Max(wall => Vector2.Distance(wall, player)) + BeyondCells;
            bool Beyond(Vector2 cell) => ring is { } circle
                ? Vector2.Distance(cell, circle.Centre) >= circle.Radius + BeyondCells
                : Vector2.Distance(cell, player) >= reach;

            if (Beyond(player))
            {
                return new WallExit(WallWay.Out, player);
            }

            var nearest = walls.OrderBy(wall => Vector2.DistanceSquared(wall, player)).First();
            var span = ring is { } fitted ? fitted.Radius + BeyondCells + 2f : reach + 2f;
            var middle = ring?.Centre ?? player;
            var left = (int)MathF.Floor(middle.X - span);
            var top = (int)MathF.Floor(middle.Y - span);
            var side = (int)MathF.Ceiling(span * 2f) + 1;
            if (side > MaxSide)
            {
                return null;
            }

            var shut = Shut(walls, left, top, side);
            var seen = new bool[side * side];
            var queue = new Queue<(int X, int Y)>();
            var start = ((int)MathF.Floor(player.X), (int)MathF.Floor(player.Y));
            queue.Enqueue(start);
            Mark(seen, start.Item1 - left, start.Item2 - top, side);
            while (queue.Count > 0)
            {
                var (x, y) = queue.Dequeue();
                var cell = new Vector2(x + 0.5f, y + 0.5f);
                if (Beyond(cell))
                {
                    return new WallExit(WallWay.Open, cell);
                }

                foreach (var (dx, dy) in Steps)
                {
                    var (nx, ny) = (x + dx, y + dy);
                    var (lx, ly) = (nx - left, ny - top);
                    if (lx < 0 || ly < 0 || lx >= side || ly >= side || seen[(ly * side) + lx] || shut[(ly * side) + lx] ||
                        !RoutePlanner.IsWalkable(grid, bytesPerRow, nx, ny))
                    {
                        continue;
                    }

                    seen[(ly * side) + lx] = true;
                    queue.Enqueue((nx, ny));
                }
            }

            return new WallExit(WallWay.Closed, nearest);
        }

        /// <summary>
        ///     The circle the walls stand on (least squares), or null when they do not give one: fewer than three, all to one
        ///     side, or not on a circle.
        /// </summary>
        public static (Vector2 Centre, float Radius)? Fit(IReadOnlyList<Vector2> walls)
        {
            if (walls.Count < 3)
            {
                return null;
            }

            // x² + y² + Dx + Ey + F = 0, about the walls' mean so the sums stay small.
            var mean = new Vector2(walls.Average(wall => wall.X), walls.Average(wall => wall.Y));
            double sxx = 0, syy = 0, sxy = 0, sxz = 0, syz = 0, sz = 0;
            foreach (var wall in walls)
            {
                double x = wall.X - mean.X, y = wall.Y - mean.Y, z = (x * x) + (y * y);
                sxx += x * x;
                syy += y * y;
                sxy += x * y;
                sxz += x * z;
                syz += y * z;
                sz += z;
            }

            var det = (sxx * syy) - (sxy * sxy);
            if (Math.Abs(det) < 1e-6)
            {
                return null;
            }

            var cx = ((sxz * syy) - (syz * sxy)) / (2 * det);
            var cy = ((syz * sxx) - (sxz * sxy)) / (2 * det);
            var radius = (float)Math.Sqrt((cx * cx) + (cy * cy) + (sz / walls.Count));
            var centre = mean + new Vector2((float)cx, (float)cy);
            if (radius <= 0 || walls.Any(wall => MathF.Abs(Vector2.Distance(wall, centre) - radius) > radius * MaxOffRing))
            {
                return null;
            }

            var angles = walls.Select(wall => MathF.Atan2(wall.Y - centre.Y, wall.X - centre.X) * 180f / MathF.PI).OrderBy(angle => angle).ToList();
            var widest = 360f - (angles[^1] - angles[0]);
            for (var i = 1; i < angles.Count; i++)
            {
                widest = MathF.Max(widest, angles[i] - angles[i - 1]);
            }

            return 360f - widest >= MinArcDegrees ? (centre, radius) : null;
        }

        /// <summary>The cells the walls close, over the searched square.</summary>
        private static bool[] Shut(IReadOnlyList<Vector2> walls, int left, int top, int side)
        {
            var spacings = walls.Select(wall => walls.Where(other => other != wall).Min(other => Vector2.Distance(other, wall))).OrderBy(gap => gap).ToList();
            var half = Math.Clamp(spacings[spacings.Count / 2] * ClosedShare, MinHalfWidth, MaxHalfWidth);
            var shut = new bool[side * side];
            foreach (var wall in walls)
            {
                for (var y = (int)MathF.Floor(wall.Y - half); y <= (int)MathF.Ceiling(wall.Y + half); y++)
                {
                    for (var x = (int)MathF.Floor(wall.X - half); x <= (int)MathF.Ceiling(wall.X + half); x++)
                    {
                        if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), wall) <= half)
                        {
                            Mark(shut, x - left, y - top, side);
                        }
                    }
                }
            }

            return shut;
        }

        private static void Mark(bool[] cells, int x, int y, int side)
        {
            if (x >= 0 && y >= 0 && x < side && y < side)
            {
                cells[(y * side) + x] = true;
            }
        }
    }
}
