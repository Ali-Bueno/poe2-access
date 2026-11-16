namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Finds the catalog's named rooms by the entities they place, where the tiles are generic ground (outdoors): a room's
    ///     objects under the eight turns and mirrors with its corner on the sub-tile grid, two of them where live entities of
    ///     their key stand, or one that only its label places. Pure; docs/research/rooms.md.
    /// </summary>
    public static class RoomObjectMatcher
    {
        /// <summary>An entity this many grid cells from where its room puts the object is that object (0-3 on the dumps, 2026-11-16).</summary>
        private const float ToleranceCells = 3f;

        /// <summary>Rooms lie on the sub-tile grid: a corner this many cells off a sub-tile's is still on it.</summary>
        private const int AlignCells = 3;

        /// <summary>A family with this share of the area's sub-tiles (1 in 10) is what the area is built from (<see cref="RoomMatcher"/>).</summary>
        private const int FamilyShare = 10;

        /// <summary>A room covering half of the area is the area itself (a town map): its name says nothing.</summary>
        private const int WholeAreaShare = 2;

        /// <summary>More separate places than this for one label is not believed.</summary>
        private const int MaxPlaces = 3;

        /// <summary>A place found: the room's box, how many live entities agree and which (their indexes in the live list).</summary>
        private sealed record Hit(NamedRoom Room, int Score, HashSet<int> Evidence);

        /// <summary>
        ///     The rooms the area's entities may place: rooms with objects in a room folder the area's tiles use (one of its rooms
        ///     is of a family covering a tenth of the area), smaller than half the area.
        /// </summary>
        public static IReadOnlyList<RoomShape> Candidates(RoomCatalog catalog, IReadOnlyList<TilePlacement> placements)
        {
            if (catalog.ObjectKeys.Count == 0 || placements.Count == 0)
            {
                return Array.Empty<RoomShape>();
            }

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var placement in placements)
            {
                if (RoomCatalog.FamilyOf(placement.Path) is { } family)
                {
                    counts[family] = counts.GetValueOrDefault(family) + 1;
                }
            }

            var present = catalog.Families.Select(family => counts.GetValueOrDefault(family) * FamilyShare >= placements.Count).ToArray();
            var sets = catalog.Rooms.Where(room => room.Families.Any(family => present[family])).Select(room => room.Set).ToHashSet(StringComparer.Ordinal);
            return catalog.Rooms.Where(room => room.Objects.Count > 0 && sets.Contains(room.Set) && room.Width * room.Height * WholeAreaShare < placements.Count).ToList();
        }

        /// <summary>The named rooms the live entities (object key, grid cell) place, at most one per place and evidence.</summary>
        public static List<NamedRoom> Match(RoomCatalog catalog, IReadOnlyList<RoomShape> candidates, IReadOnlyList<(int Key, Vector2 Grid)> live, int subTileCells)
        {
            var byKey = new Dictionary<int, List<(Vector2 Grid, int Index)>>();
            for (var i = 0; i < live.Count; i++)
            {
                (byKey.TryGetValue(live[i].Key, out var known) ? known : byKey[live[i].Key] = new List<(Vector2, int)>()).Add((live[i].Grid, i));
            }

            var hits = new List<Hit>();
            foreach (var room in candidates)
            {
                if (room.Objects.Any(spot => byKey.ContainsKey(spot.Key)))
                {
                    hits.AddRange(Places(room, catalog, byKey, subTileCells));
                }
            }

            return Choose(hits);
        }

        /// <summary>The tile rooms and the rooms found by objects that are not one of them again.</summary>
        public static List<NamedRoom> Merge(IReadOnlyList<NamedRoom> byTiles, IEnumerable<NamedRoom> byObjects) =>
            byTiles.Concat(byObjects.Where(room => !byTiles.Any(tile => tile.Label == room.Label && RoomMatcher.Overlaps(tile, room)))).ToList();

        /// <summary>Every turn and grid-aligned corner where two of the room's objects, or one only its label has, stand live.</summary>
        private static IEnumerable<Hit> Places(RoomShape room, RoomCatalog catalog, Dictionary<int, List<(Vector2 Grid, int Index)>> byKey, int subTileCells)
        {
            var (width, height) = (room.Width * subTileCells, room.Height * subTileCells);
            for (var turn = 0; turn < 8; turn++)
            {
                var spots = room.Objects.Select(spot => RoomMatcher.Turned(spot, turn, width, height)).ToArray();
                var corners = new HashSet<(int X, int Y)>();
                foreach (var spot in spots)
                {
                    foreach (var (grid, _) in byKey.GetValueOrDefault(spot.Class) ?? new List<(Vector2, int)>())
                    {
                        if (OnGrid(grid.X - spot.X, subTileCells) is { } x && OnGrid(grid.Y - spot.Y, subTileCells) is { } y)
                        {
                            corners.Add((x, y));
                        }
                    }
                }

                foreach (var (x, y) in corners)
                {
                    var evidence = new HashSet<int>();
                    var unique = false;
                    foreach (var spot in spots)
                    {
                        var at = new Vector2(x + spot.X, y + spot.Y);
                        foreach (var (grid, index) in byKey.GetValueOrDefault(spot.Class) ?? new List<(Vector2, int)>())
                        {
                            if (MathF.Max(MathF.Abs(grid.X - at.X), MathF.Abs(grid.Y - at.Y)) <= ToleranceCells && evidence.Add(index))
                            {
                                unique |= catalog.UniqueKeys[spot.Class];
                                break;
                            }
                        }
                    }

                    // One object alone places nothing (a lone checkpoint is everywhere) unless only this label has it.
                    if (evidence.Count >= 2 || (evidence.Count == 1 && unique))
                    {
                        var (w, h) = (turn & 4) != 0 ? (height, width) : (width, height);
                        yield return new Hit(new NamedRoom(room.Label, x, y, w, h), evidence.Count, evidence);
                    }
                }
            }
        }

        /// <summary>The corner a room would have on the sub-tile grid, or null when the cell is off it.</summary>
        private static int? OnGrid(float cell, int subTileCells)
        {
            var snapped = (int)MathF.Round(cell / subTileCells) * subTileCells;
            return MathF.Abs(cell - snapped) <= AlignCells ? snapped : null;
        }

        /// <summary>
        ///     Strongest places first; a weaker one resting on an entity a stronger one took is that evidence read another way,
        ///     and two equal places sharing an entity (a room whose objects fit it mirrored) are both left until another shows.
        /// </summary>
        private static List<NamedRoom> Choose(List<Hit> hits)
        {
            var kept = new List<Hit>();
            var ambiguous = new HashSet<Hit>();
            foreach (var hit in hits.OrderByDescending(hit => hit.Score).ThenBy(hit => hit.Room.Label, StringComparer.Ordinal).ThenBy(hit => hit.Room.X).ThenBy(hit => hit.Room.Y))
            {
                if (kept.Any(other => other.Room.Label == hit.Room.Label && RoomMatcher.Overlaps(other.Room, hit.Room)))
                {
                    continue; // the same place by another variant or turn
                }

                var rival = kept.FirstOrDefault(other => other.Evidence.Overlaps(hit.Evidence));
                if (rival != null)
                {
                    if (rival.Score == hit.Score)
                    {
                        ambiguous.Add(rival);
                    }

                    continue;
                }

                if (kept.Count(other => other.Room.Label == hit.Room.Label) < MaxPlaces)
                {
                    kept.Add(hit);
                }
            }

            return kept.Where(hit => !ambiguous.Contains(hit)).Select(hit => hit.Room).ToList();
        }
    }
}
