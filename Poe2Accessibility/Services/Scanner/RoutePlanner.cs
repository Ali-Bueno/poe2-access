namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Shortest walkable route on the area's grid (A*, 8 neighbours, no corner cutting). The grid is the
    ///     game's walkability map as GameHelper reads it: one 4-bit cell per grid unit, two per byte, 0 = blocked
    ///     (same decoding as GameHelper's Radar). Pure: the caller passes the grid.
    /// </summary>
    public static partial class RoutePlanner
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

        /// <summary>Nodes the last start cell's search expanded: the work done, which tests count instead of a clock that load distorts.</summary>
        public static int LastExpansions { get; private set; }

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
        public static List<Vector2>? FindRoute(byte[] grid, int bytesPerRow, Vector2 start, Vector2 goal, bool orNearest, out bool reached) =>
            FindRoute(grid, bytesPerRow, start, goal, orNearest, out reached, out _);

        /// <summary>
        ///     As above; <paramref name="spent"/> says the search ran out of its <paramref name="budget"/> of expansions
        ///     before it could tell whether the goal is reachable, so a route that falls short is only as far as the
        ///     search got, not a way that is truly closed (a later search from further on may go on).
        /// </summary>
        public static List<Vector2>? FindRoute(byte[] grid, int bytesPerRow, Vector2 start, Vector2 goal, bool orNearest, out bool reached,
                                               out bool spent, int budget = MaxExpansions)
        {
            reached = false;
            spent = false;
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
            var space = Rent();
            try
            {
                return FindWith(space, grid, bytesPerRow, origin, to, orNearest, budget, out reached, out spent);
            }
            finally
            {
                Return(space);
            }
        }

        private static List<Vector2>? FindWith(Workspace space, byte[] grid, int bytesPerRow, (int, int) origin, (int X, int Y) to, bool orNearest,
                                               int budget, out bool reached, out bool spent)
        {
            reached = false;
            spent = false;
            space.Prepare(grid, bytesPerRow);
            List<Vector2>? first = null;
            var firstReached = false;
            var firstSpent = false;
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

                var route = Search(space, from, to, orNearest, budget, out var region, out var found, out var ranOut);
                if (!found && region < PocketCells * LookedPerCell)
                {
                    region = Flood(space, grid, bytesPerRow, from); // the scans overlap and skip cells: count the whole pocket
                }

                if (tries++ == 0)
                {
                    (first, firstReached, firstSpent) = (route, found, ranOut);
                }

                if (found || region >= PocketCells || tries >= MaxStartTries)
                {
                    reached = found;
                    spent = ranOut && !found;
                    return route;
                }
            }

            reached = firstReached;
            spent = firstSpent && !firstReached;
            return first;
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
