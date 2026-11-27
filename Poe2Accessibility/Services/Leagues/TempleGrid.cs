namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     The Temple Console's map of Atziri's Temple (Vaal Ruins, docs/research/league-mechanics.md §9): 81 cells named
    ///     "(depth, side)" by the game, depth 0 at the Vestibule (0, 4) and 8 at the far edge, Atziri's Chamber at (9, 4)
    ///     beyond it. The screen draws it as a diamond, depth rising up-right and side down-right, and the pad's cross moves
    ///     along the cells' axes: up adds depth, right adds side (live 2026-11-27: right (1, 4) → (1, 5), down (2, 4) →
    ///     (1, 4), left (3, 4) → (3, 3)). So a cell is a row from the entrance and a column across, and its neighbours are
    ///     named by the pad's direction to them. Pure.
    /// </summary>
    public static class TempleGrid
    {
        /// <summary>The pad's directions to a cell's four neighbours, in the order they are said.</summary>
        public enum Side
        {
            /// <summary>One row deeper (toward Atziri).</summary>
            Up,

            /// <summary>One column right.</summary>
            Right,

            /// <summary>One row back (toward the Vestibule).</summary>
            Down,

            /// <summary>One column left.</summary>
            Left,
        }

        /// <summary>The fewest coordinate-named cells a container must hold to be the map (the 9 by 9 grid; Atziri's sits alone).</summary>
        public const int MinCells = 9;

        /// <summary>The Vestibule's cell, the temple's entrance.</summary>
        public static readonly (int Row, int Column) Vestibule = (0, 4);

        /// <summary>Atziri's Chamber, alone beyond the far edge (dump 2026-11-27: "(9, 4)" in a container of its own).</summary>
        public static readonly (int Row, int Column) AtziriChamber = (9, 4);

        private static readonly Regex CellName = new(@"^\((\d{1,2}), (\d{1,2})\)$", RegexOptions.Compiled);

        /// <summary>The cell an element name gives ("(3, 4)": row 3, column 4, from 0); null for any other name.</summary>
        public static (int Row, int Column)? Parse(string name)
        {
            var match = CellName.Match(name);
            return match.Success
                ? (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture))
                : null;
        }

        /// <summary>The cell one step from another in a pad direction.</summary>
        public static (int Row, int Column) Step((int Row, int Column) cell, Side side) => side switch
        {
            Side.Up => (cell.Row + 1, cell.Column),
            Side.Right => (cell.Row, cell.Column + 1),
            Side.Down => (cell.Row - 1, cell.Column),
            _ => (cell.Row, cell.Column - 1),
        };

        /// <summary>
        ///     The cells reached from the Vestibule: paths link to paths beside them, starting from those on the entrance row,
        ///     and a room is reached when a reached path is beside it. Rooms do not lead on: with Camino held the game offered
        ///     only free cells beside a path or the Vestibule, none beside a room alone (dump 2026-11-27 12:19, 7 cells).
        /// </summary>
        /// <param name="isPath">Every cell with a room or path on it, true for a path (the Vestibule is one).</param>
        public static HashSet<(int Row, int Column)> Reached(IReadOnlyDictionary<(int Row, int Column), bool> isPath)
        {
            var reached = new HashSet<(int Row, int Column)>();
            var queue = new Queue<(int Row, int Column)>();
            // The Vestibule, where the temple is entered (dump 2026-11-27: "(0, 4)"); without a path there, every path of the entrance row.
            foreach (var (cell, path) in isPath)
            {
                if (path && (isPath.GetValueOrDefault(Vestibule) ? cell == Vestibule : cell.Row == 0) && reached.Add(cell))
                {
                    queue.Enqueue(cell);
                }
            }

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (Side side in Enum.GetValues(typeof(Side)))
                {
                    var next = Step(cell, side);
                    if (isPath.TryGetValue(next, out var path) && reached.Add(next) && path)
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return reached;
        }

        /// <summary>
        ///     The room to do next from a cell: the nearest room not done, walking the map's paths (rooms hang off them); Atziri's
        ///     Chamber once every other is done, or when none is left that a path reaches. Null when nothing is left.
        /// </summary>
        /// <param name="isPath">Every cell with a room or path on it, true for a path.</param>
        public static (int Row, int Column)? NextRoom((int Row, int Column) from, IReadOnlyDictionary<(int Row, int Column), bool> isPath, IReadOnlySet<(int Row, int Column)> done)
        {
            var seen = new HashSet<(int Row, int Column)> { from };
            var queue = new Queue<(int Row, int Column)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (Side side in Enum.GetValues(typeof(Side)))
                {
                    var next = Step(cell, side);
                    if (!isPath.TryGetValue(next, out var path) || !seen.Add(next))
                    {
                        continue;
                    }

                    if (path)
                    {
                        queue.Enqueue(next);
                    }
                    else if (next != AtziriChamber && !done.Contains(next))
                    {
                        return next;
                    }
                }
            }

            return isPath.ContainsKey(AtziriChamber) && from != AtziriChamber ? AtziriChamber : null;
        }

        /// <summary>
        ///     What stands beside a cell, by direction: each neighbour with a room or path (the name its tooltip gives),
        ///     empty neighbours and cells off the map left out.
        /// </summary>
        public static IReadOnlyList<(Side Side, string Room)> Neighbours((int Row, int Column) cell, Func<(int Row, int Column), string?> roomAt)
        {
            var found = new List<(Side, string)>();
            foreach (Side side in Enum.GetValues(typeof(Side)))
            {
                if (roomAt(Step(cell, side)) is { Length: > 0 } room)
                {
                    found.Add((side, room));
                }
            }

            return found;
        }
    }
}
