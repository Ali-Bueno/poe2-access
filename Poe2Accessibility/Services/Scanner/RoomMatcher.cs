namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>A named room found on the area's tile grid: its label key and its box in grid cells.</summary>
    public readonly record struct NamedRoom(string Label, int X, int Y, int Width, int Height)
    {
        public bool Contains(Vector2 grid) => grid.X >= this.X && grid.X < this.X + this.Width && grid.Y >= this.Y && grid.Y < this.Y + this.Height;

        /// <summary>Grid cells from the box's edge to a point; 0 inside.</summary>
        public float DistanceTo(Vector2 grid) =>
            Vector2.Distance(grid, Vector2.Clamp(grid, new Vector2(this.X, this.Y), new Vector2(this.X + this.Width, this.Y + this.Height)));
    }

    /// <summary>
    ///     Finds the catalog's named rooms on the placed tiles: each room's cell classes under the eight turns and mirrors,
    ///     anchored on its rarest telling class, with no cell wrong (a twentieth for a room of a hundred cells or more). Pure;
    ///     docs/research/rooms.md.
    /// </summary>
    public static class RoomMatcher
    {
        /// <summary>A room this large may miss a twentieth of its cells (a tile the generator swapped).</summary>
        private const int LenientCells = 100;
        private const int LenientShare = 20;

        /// <summary>More separate places than this for one room is a shape that fits anywhere, not the room.</summary>
        private const int MaxPlaces = 3;

        /// <summary>A family with this share of the area's known tiles (1 in 10) is what the area is built from.</summary>
        private const int FamilyShare = 10;

        /// <summary>A room covering half of the area's known tiles is the area itself (a town map): its name says nothing.</summary>
        private const int WholeAreaShare = 2;

        private static readonly HashSet<string> PlainClasses = new(StringComparer.Ordinal) { "wall", "floor", "excavation", "stairs" };

        /// <summary>The named rooms the placed tiles hold, at most one per place and <see cref="MaxPlaces"/> per label.</summary>
        public static List<NamedRoom> Match(RoomCatalog catalog, IReadOnlyList<TilePlacement> placements, int subTileCells)
        {
            var live = new Dictionary<(int X, int Y), int>();
            var families = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var placement in placements)
            {
                if (catalog.Tiles.TryGetValue(placement.Path, out var cls))
                {
                    live[((int)MathF.Floor(placement.Grid.X / subTileCells), (int)MathF.Floor(placement.Grid.Y / subTileCells))] = cls;
                    if (RoomCatalog.FamilyOf(placement.Path) is { } family)
                    {
                        families[family] = families.GetValueOrDefault(family) + 1;
                    }
                }
            }

            if (live.Count == 0)
            {
                return new List<NamedRoom>();
            }

            var present = catalog.Families.Select(family => families.GetValueOrDefault(family) * FamilyShare >= live.Count).ToArray();
            var byClass = live.GroupBy(cell => cell.Value).ToDictionary(group => group.Key, group => group.Select(cell => cell.Key).ToList());
            var plain = catalog.Classes.Select(PlainClasses.Contains).ToArray();
            var found = new List<(NamedRoom Room, int Score)>();
            foreach (var room in catalog.Rooms)
            {
                // A room without cells is told by its objects alone (RoomObjectMatcher).
                if (room.Cells.Count == 0 || (room.Families.Count > 0 && !room.Families.Any(family => present[family])) || room.Cells.Count * WholeAreaShare >= live.Count)
                {
                    continue;
                }

                found.AddRange(Places(room, catalog, live, byClass, plain, subTileCells));
            }

            var kept = new List<NamedRoom>();
            foreach (var (room, _) in found.OrderByDescending(hit => hit.Score))
            {
                if (!kept.Any(other => Overlaps(other, room)) && kept.Count(other => other.Label == room.Label) < MaxPlaces)
                {
                    kept.Add(room);
                }
            }

            return kept;
        }

        /// <summary>The named room at a grid cell (the smallest box holding it), or null.</summary>
        public static NamedRoom? At(IReadOnlyList<NamedRoom> rooms, Vector2 grid) =>
            rooms.Where(room => room.Contains(grid)).OrderBy(room => room.Width * room.Height).Cast<NamedRoom?>().FirstOrDefault();

        /// <summary>Where one room fits, its separate places scored by the cells that agree; none when it fits too many.</summary>
        private static IEnumerable<(NamedRoom Room, int Score)> Places(
            RoomShape room, RoomCatalog catalog, Dictionary<(int X, int Y), int> live, Dictionary<int, List<(int X, int Y)>> byClass, bool[] plain, int subTileCells)
        {
            int Seen(int cls) => byClass.TryGetValue(cls, out var cells) ? cells.Count : 0;
            var classes = room.Cells.Select(cell => cell.Class).Distinct().ToList();
            var telling = classes.Where(cls => !plain[cls] && !catalog.Soft[cls]).ToList();
            var anchor = (telling.Count > 0 ? telling : classes.Where(cls => !catalog.Soft[cls])).OrderBy(Seen).Cast<int?>().FirstOrDefault();
            if (anchor is not { } anchorClass || Seen(anchorClass) == 0)
            {
                return Array.Empty<(NamedRoom, int)>();
            }

            var allowed = room.Cells.Count >= LenientCells ? room.Cells.Count / LenientShare : 0;
            var hits = new List<(NamedRoom Room, int Score)>();
            for (var turn = 0; turn < 8; turn++)
            {
                // Rarest classes first: a wrong place fails on its first cells.
                var cells = room.Cells.Select(cell => Turned(cell, turn, room.Width, room.Height)).OrderBy(cell => Seen(cell.Class)).ToArray();
                var first = Array.FindIndex(cells, cell => cell.Class == anchorClass);
                var (minX, minY, maxX, maxY) = (cells.Min(cell => cell.X), cells.Min(cell => cell.Y), cells.Max(cell => cell.X), cells.Max(cell => cell.Y));
                foreach (var (x, y) in byClass[anchorClass])
                {
                    var (dx, dy) = (x - cells[first].X, y - cells[first].Y);
                    var wrong = 0;
                    foreach (var cell in cells)
                    {
                        if (!Takes(catalog, cell.Class, live.TryGetValue((cell.X + dx, cell.Y + dy), out var there) ? there : -1) && ++wrong > allowed)
                        {
                            break;
                        }
                    }

                    if (wrong <= allowed)
                    {
                        var box = new NamedRoom(room.Label, (minX + dx) * subTileCells, (minY + dy) * subTileCells, (maxX - minX + 1) * subTileCells, (maxY - minY + 1) * subTileCells);
                        var same = hits.FindIndex(hit => Overlaps(hit.Room, box));
                        if (same < 0)
                        {
                            hits.Add((box, cells.Length - wrong));
                        }
                        else if (hits[same].Score < cells.Length - wrong)
                        {
                            hits[same] = (box, cells.Length - wrong);
                        }
                    }
                }
            }

            return hits.Count <= MaxPlaces ? hits : Array.Empty<(NamedRoom, int)>();
        }

        /// <summary>Whether a placed tile of class <paramref name="there"/> (-1: none known) can stand where the room asks for <paramref name="cls"/>.</summary>
        private static bool Takes(RoomCatalog catalog, int cls, int there) =>
            there == cls || (there >= 0 && catalog.Soft[cls] && catalog.SoftAccepts[there]);

        /// <summary>A cell turned and mirrored: bit 2 swaps the axes, bit 0 mirrors across, bit 1 mirrors down (within the turned size).</summary>
        internal static (int X, int Y, int Class) Turned((int X, int Y, int Class) cell, int turn, int width, int height)
        {
            var (x, y) = (turn & 4) != 0 ? (cell.Y, cell.X) : (cell.X, cell.Y);
            var (w, h) = (turn & 4) != 0 ? (height, width) : (width, height);
            return ((turn & 1) != 0 ? w - 1 - x : x, (turn & 2) != 0 ? h - 1 - y : y, cell.Class);
        }

        /// <summary>Two boxes share more than half of the smaller one.</summary>
        internal static bool Overlaps(NamedRoom a, NamedRoom b)
        {
            var width = Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X);
            var height = Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y);
            return width > 0 && height > 0 && 2L * width * height > Math.Min((long)a.Width * a.Height, (long)b.Width * b.Height);
        }
    }
}
