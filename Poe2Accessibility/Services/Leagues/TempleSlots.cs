namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Atziri's Temple on the ground: each room or path of the console's map is one square slot of terrain, so the slots the
    ///     walkable ground fills, matched to the map, say which cell a point stands in (docs/research/league-mechanics.md §9). Pure.
    /// </summary>
    public static class TempleSlots
    {
        /// <summary>A slot's side in grid cells: 11 sub-tiles of 23 (`k 11 11` in every room and path `.arm`).</summary>
        public const int SlotCells = 11 * 23;

        /// <summary>The map's cells a match may get wrong and still be taken (a room no path reaches may be left unbuilt).</summary>
        public const int MaxMismatches = 2;

        /// <summary>The share of the fullest slot's ground a slot needs to hold a room or path: the fills between are solid.</summary>
        private const float OccupiedShare = 0.05f;

        /// <summary>How near the least crossing a boundary line's cost must be to lie in the same wall (share of the cost range).</summary>
        private const float WallTolerance = 0.05f;

        /// <summary>The map's side: rows and columns 0 to 8 (81 cells named "(row, column)"); Atziri's Chamber sits beyond, at (9, 4).</summary>
        private const int MapSide = 9;

        private static readonly (int RowX, int ColumnX, int RowY, int ColumnY)[] Turns =
        {
            (1, 0, 0, 1), (1, 0, 0, -1), (-1, 0, 0, 1), (-1, 0, 0, -1),
            (0, 1, 1, 0), (0, 1, -1, 0), (0, -1, 1, 0), (0, -1, -1, 0),
        };

        /// <summary>The terrain cut into slots: where the slot lines run, and which slots hold ground.</summary>
        /// <param name="BoundaryX">The first grid column of a slot (0 to <see cref="SlotCells"/> − 1); slot 0 is the part before it.</param>
        /// <param name="Occupied">[slot x, slot y]: the slot holds a room or a path.</param>
        public sealed record Ground(int BoundaryX, int BoundaryY, bool[,] Occupied)
        {
            /// <summary>The slot a grid point lies in.</summary>
            public (int X, int Y) SlotOf(float gridX, float gridY) =>
                ((int)Math.Floor((gridX - this.BoundaryX + SlotCells) / SlotCells), (int)Math.Floor((gridY - this.BoundaryY + SlotCells) / SlotCells));

            /// <summary>Whether a slot holds ground; false off the terrain.</summary>
            public bool Holds(int x, int y) =>
                x >= 0 && y >= 0 && x < this.Occupied.GetLength(0) && y < this.Occupied.GetLength(1) && this.Occupied[x, y];

            /// <summary>The slots, row by row ('#' ground, '.' none), for the log.</summary>
            public string Picture() => string.Join("/", Enumerable.Range(0, this.Occupied.GetLength(1))
                .Select(y => new string(Enumerable.Range(0, this.Occupied.GetLength(0)).Select(x => this.Occupied[x, y] ? '#' : '.').ToArray())));
        }

        /// <summary>How the console's map lies on the ground: slot = offset + row × (RowX, RowY) + column × (ColumnX, ColumnY).</summary>
        public sealed record Frame(Ground Ground, int RowX, int ColumnX, int RowY, int ColumnY, int OffsetX, int OffsetY)
        {
            /// <summary>The map cell a grid point stands in (it may lie off the map: the entrance's hall, Atziri's way).</summary>
            public (int Row, int Column) CellOf(float gridX, float gridY)
            {
                var (x, y) = this.Ground.SlotOf(gridX, gridY);
                var (dx, dy) = (x - this.OffsetX, y - this.OffsetY);
                return ((this.RowX * dx) + (this.RowY * dy), (this.ColumnX * dx) + (this.ColumnY * dy));
            }

            /// <summary>The middle of a cell's slot, in grid cells (each room's minimap icon stands there: garrison `.arm` "126 126").</summary>
            public (float X, float Y) CentreOf((int Row, int Column) cell)
            {
                var (x, y) = this.SlotAt(cell);
                return (this.Ground.BoundaryX + ((x - 1) * SlotCells) + (SlotCells / 2f), this.Ground.BoundaryY + ((y - 1) * SlotCells) + (SlotCells / 2f));
            }

            /// <summary>The slot a map cell lies on.</summary>
            public (int X, int Y) SlotAt((int Row, int Column) cell) =>
                (this.OffsetX + (this.RowX * cell.Row) + (this.ColumnX * cell.Column), this.OffsetY + (this.RowY * cell.Row) + (this.ColumnY * cell.Column));
        }

        /// <summary>
        ///     Cuts the walkable grid into slots: the slot lines run where the fewest walkable cells cross them (each slot is walled
        ///     at its edges, the doors aside), and a slot holds ground when it has a share of the fullest one's.
        /// </summary>
        public static Ground Measure(byte[] grid, int bytesPerRow)
        {
            var width = bytesPerRow * 2;
            var height = bytesPerRow > 0 ? grid.Length / bytesPerRow : 0;
            var columns = new long[width];
            var rows = new long[height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (IsWalkable(grid, bytesPerRow, x, y))
                    {
                        columns[x]++;
                        rows[y]++;
                    }
                }
            }

            var (boundaryX, boundaryY) = (Boundary(columns), Boundary(rows));
            var counts = new long[(width / SlotCells) + 2, (height / SlotCells) + 2];
            for (var y = 0; y < height; y++)
            {
                var slotY = (y - boundaryY + SlotCells) / SlotCells;
                for (var x = 0; x < width; x++)
                {
                    if (IsWalkable(grid, bytesPerRow, x, y))
                    {
                        counts[(x - boundaryX + SlotCells) / SlotCells, slotY]++;
                    }
                }
            }

            var fullest = counts.Cast<long>().DefaultIfEmpty(0).Max();
            var occupied = new bool[counts.GetLength(0), counts.GetLength(1)];
            for (var x = 0; x < counts.GetLength(0); x++)
            {
                for (var y = 0; y < counts.GetLength(1); y++)
                {
                    occupied[x, y] = fullest > 0 && counts[x, y] >= fullest * OccupiedShare;
                }
            }

            return new Ground(boundaryX, boundaryY, occupied);
        }

        /// <summary>
        ///     Where slot lines run along one axis (0 to <see cref="SlotCells"/> − 1): of the phases whose lines the fewest walkable
        ///     cells cross, the middle of the run that holds the least, since each line runs between two walls as thick.
        /// </summary>
        /// <param name="sums">Walkable cells per grid line across the axis.</param>
        public static int Boundary(IReadOnlyList<long> sums)
        {
            var cost = new long[SlotCells];
            for (var phase = 0; phase < SlotCells; phase++)
            {
                for (var at = phase; at < sums.Count; at += SlotCells)
                {
                    cost[phase] += sums[at] + (at > 0 ? sums[at - 1] : 0);
                }
            }

            var (least, most) = (cost.Min(), cost.Max());
            if (least == most)
            {
                return 0;
            }

            var low = least + (long)((most - least) * WallTolerance);
            var lowest = Array.IndexOf(cost, least);
            var start = lowest;
            while (cost[(start - 1 + SlotCells) % SlotCells] <= low && (start - 1 + SlotCells) % SlotCells != lowest)
            {
                start = (start - 1 + SlotCells) % SlotCells;
            }

            var length = 1;
            while (length < SlotCells && cost[(start + length) % SlotCells] <= low)
            {
                length++;
            }

            return (start + (length / 2)) % SlotCells;
        }

        /// <summary>
        ///     The one way the console's map lies on the ground: of the eight turns and every shift, those whose built cells (the
        ///     map's 81 and Atziri's Chamber) hold ground and whose empty cells do not, at most <see cref="MaxMismatches"/> wrong;
        ///     null when none fits or two fit as well and the spawn does not tell them apart.
        /// </summary>
        /// <param name="built">Every map cell with a room or path on it (the Vestibule and Atziri's Chamber among them).</param>
        /// <param name="spawn">The slot the player arrived in, when seen: the Vestibule's hall, at or beside the Vestibule's cell.</param>
        public static Frame? Match(Ground ground, IReadOnlySet<(int Row, int Column)> built, (int X, int Y)? spawn, out int mismatches)
        {
            var cells = Enumerable.Range(0, MapSide).SelectMany(row => Enumerable.Range(0, MapSide).Select(column => (row, column)))
                .Append(TempleGrid.AtziriChamber).ToList();
            var (width, height) = (ground.Occupied.GetLength(0), ground.Occupied.GetLength(1));
            var best = new List<Frame>();
            mismatches = int.MaxValue;
            foreach (var (rowX, columnX, rowY, columnY) in Turns)
            {
                for (var offsetX = -MapSide - 1; offsetX <= width + MapSide; offsetX++)
                {
                    for (var offsetY = -MapSide - 1; offsetY <= height + MapSide; offsetY++)
                    {
                        var frame = new Frame(ground, rowX, columnX, rowY, columnY, offsetX, offsetY);
                        var wrong = 0;
                        foreach (var cell in cells)
                        {
                            var (x, y) = frame.SlotAt(cell);
                            if (ground.Holds(x, y) != built.Contains(cell) && ++wrong > Math.Min(mismatches, MaxMismatches))
                            {
                                break;
                            }
                        }

                        if (wrong > MaxMismatches || wrong > mismatches)
                        {
                            continue;
                        }

                        if (wrong < mismatches)
                        {
                            best.Clear();
                            mismatches = wrong;
                        }

                        best.Add(frame);
                    }
                }
            }

            if (best.Count > 1 && spawn is { } arrived)
            {
                best = best.Where(frame => frame.CellOf(Middle(ground, arrived).X, Middle(ground, arrived).Y) is var cell &&
                                           Math.Abs(cell.Row - TempleGrid.Vestibule.Row) + Math.Abs(cell.Column - TempleGrid.Vestibule.Column) <= 1).ToList();
            }

            return best.Count == 1 ? best[0] : null;
        }

        private static (float X, float Y) Middle(Ground ground, (int X, int Y) slot) =>
            (ground.BoundaryX + ((slot.X - 1) * SlotCells) + (SlotCells / 2f), ground.BoundaryY + ((slot.Y - 1) * SlotCells) + (SlotCells / 2f));

        private static bool IsWalkable(byte[] grid, int bytesPerRow, int x, int y) => RoutePlanner.IsWalkable(grid, bytesPerRow, x, y);
    }
}
