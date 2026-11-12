namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>The scanner's named tiles of the area layout: their places as landmarks, and the boss arenas the landmark table does not name.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>Boss arenas the landmark table does not name, between the named tiles and the mod's places.</summary>
        private const uint ArenaIdBase = LandmarkIdBase + 0x10000000;

        /// <summary>Pieces of one arena tile this many tiles apart are one arena.</summary>
        private const int ArenaReachTiles = 2;

        /// <summary>
        ///     Ids a named tile's places take (one per group of its pieces): Ogham Manor's "Stairs Down" stands on two floors,
        ///     and their average was a point between them that no way leads to (2026-11-08).
        /// </summary>
        private const uint LandmarkPlaceStride = 0x100;

        /// <summary>
        ///     A named exit tile this near (grid cells) the game's exit of the same name is that exit, said once: Clearfell's
        ///     forest transition is a metatile nine 23-cell pieces wide, its exit entity 65 cells from the named piece.
        /// </summary>
        private const float TileExitMatchCells = 250f;

        /// <summary>The unnamed boss arenas of the last tile layout read (<see cref="UnnamedArenas"/>).</summary>
        private (Dictionary<string, List<Vector2>>? Tiles, int Count, string Area, IReadOnlyList<ScanEntry> Entries) arenas = (null, 0, string.Empty, Array.Empty<ScanEntry>());

        /// <summary>Where each named tile stands in the tile table last asked about (<see cref="TileCentres"/>).</summary>
        private (Dictionary<string, List<Vector2>>? Tiles, int Count, float Reach, Dictionary<string, Vector2[]> Centres) tileCentres = (null, 0, 0, new(StringComparer.Ordinal));

        /// <summary>
        ///     Where the named tile <paramref name="key"/> stands, one centre (grid cells) per group of its pieces: worked out
        ///     once per tile table (looking a file's pieces up walks every tile of the area) and group reach.
        /// </summary>
        private Vector2[] TileCentres(Dictionary<string, List<Vector2>> tiles, string key)
        {
            // The host empties the same dictionary when the area is left, so its size is part of the key (as for the arenas).
            var reach = ArenaReachTiles * TileStructure.TileToGridConversion;
            if (!ReferenceEquals(this.tileCentres.Tiles, tiles) || this.tileCentres.Count != tiles.Count || this.tileCentres.Reach != reach)
            {
                this.tileCentres.Centres.Clear();
                this.tileCentres = (tiles, tiles.Count, reach, this.tileCentres.Centres);
            }

            if (!this.tileCentres.Centres.TryGetValue(key, out var centres))
            {
                centres = TilePlaces.Centres(tiles, key, reach);
                this.tileCentres.Centres[key] = centres;
            }

            return centres;
        }

        /// <summary>The game's exit and a named tile of the same name this near it are one exit.</summary>
        private static bool SameExit(ScanEntry exit, ScanEntry tile, float worldPerCell) =>
            string.Equals(exit.Name, tile.Name, StringComparison.OrdinalIgnoreCase) &&
            Vector2.Distance(new Vector2(exit.WorldX, exit.WorldY), new Vector2(tile.WorldX, tile.WorldY)) <= TileExitMatchCells * worldPerCell;

        /// <summary>
        ///     Named tiles of this area (<see cref="LandmarkTable"/>), placed from the area's tile grid, and the boss arenas
        ///     among its tiles: a named one as "Boss room: Asinia", one the table lacks by its file alone.
        /// </summary>
        private IEnumerable<ScanEntry> TileLandmarks(AreaInstance area, float height)
        {
            var tiles = area.TgtTilesLocations;
            var toWorld = area.WorldToGridConvertor;
            var source = this.landmarks.Source(this.areaId());
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namedArenas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0u;
            foreach (var (key, label) in this.landmarks.For(this.areaId()))
            {
                index++;
                var places = this.TileCentres(tiles, key);
                if (places.Length == 0)
                {
                    continue;
                }

                named.Add(MinimapMarks.TileFile(key));
                var english = source.TryGetValue(key, out var written) ? written : label;
                var boss = this.landmarks.IsAreaName(english) ? null : MinimapMarks.ArenaBoss(key, english);
                if (boss != null)
                {
                    namedArenas.Add(MinimapMarks.TileFile(key));
                }

                // One place per group of the tile's pieces: the same tile on two floors is two places, not their middle.
                for (var place = 0; place < places.Length; place++)
                {
                    var centre = places[place] * toWorld;
                    var id = LandmarkIdBase + (index * LandmarkPlaceStride) + (uint)place;
                    if (boss != null)
                    {
                        // The Tomb's and the Mausoleum's arenas were "Asinia" and "MausoleumBoss_ArenaFloor" among the
                        // exits, a maze's boss not found by that (user 2026-10-29); the boss's name lets the quest find it.
                        var name = boss.Length == 0 ? string.Empty : boss == english.Trim() ? label : this.landmarks.Localize(boss);
                        yield return new ScanEntry(id, ScanCategory.PointsOfInterest, Announcements.BossRoom(name),
                                                   centre.X, centre.Y, height, MinimapMarks.BossRoomKind, Subject: MinimapMarks.WithoutNote(name));
                        continue;
                    }

                    yield return new ScanEntry(id, ScanCategory.Exits, label, centre.X, centre.Y, height,
                                               this.landmarks.IsAreaName(label) ? string.Empty : QuestMatcher.TileNote);
                }
            }

            foreach (var arena in this.UnnamedArenas(tiles, named, namedArenas))
            {
                yield return arena with { Name = Announcements.BossRoom(string.Empty), WorldX = arena.WorldX * toWorld, WorldY = arena.WorldY * toWorld, WorldZ = height };
            }
        }

        /// <summary>
        ///     Boss arenas among the area's tiles that the landmark table does not name, at their grid cells: the pieces of
        ///     arena tiles side by side are one arena, left out when the table names a piece of it (an arena's floor named,
        ///     its walls not). Worked out once per area layout.
        /// </summary>
        private IReadOnlyList<ScanEntry> UnnamedArenas(Dictionary<string, List<Vector2>> tiles, HashSet<string> named, HashSet<string> namedArenas)
        {
            // GameHelper empties the same dictionary when the area is left, so its size is part of the key; the area's id
            // too, in case the first look at a layout came before the id did (its named arenas would pass for unnamed).
            var area = this.areaId();
            if (ReferenceEquals(this.arenas.Tiles, tiles) && this.arenas.Count == tiles.Count && this.arenas.Area == area)
            {
                return this.arenas.Entries;
            }

            var pieces = new List<(Vector2 Spot, bool Named)>();
            foreach (var (key, spots) in tiles)
            {
                var file = MinimapMarks.TileFile(key);
                var isNamed = namedArenas.Contains(file);
                if (isNamed || (!named.Contains(file) && MinimapMarks.IsArenaTile(key)))
                {
                    pieces.AddRange(spots.Select(spot => (spot, isNamed)));
                }
            }

            var entries = new List<ScanEntry>();
            foreach (var group in MinimapMarks.Groups(pieces.Select(piece => piece.Spot).ToList(), ArenaReachTiles * TileStructure.TileToGridConversion))
            {
                if (group.Any(member => pieces[member].Named))
                {
                    continue;
                }

                var centre = group.Aggregate(Vector2.Zero, (sum, member) => sum + pieces[member].Spot) / group.Count;
                entries.Add(new ScanEntry(ArenaIdBase + (uint)entries.Count, ScanCategory.PointsOfInterest, string.Empty,
                                          centre.X, centre.Y, 0, MinimapMarks.BossRoomKind));
            }

            if (entries.Count > 0)
            {
                this.log.Info($"Boss arenas without a landmark name: {entries.Count}");
            }

            this.arenas = (tiles, tiles.Count, area, entries);
            return entries;
        }
    }
}
