namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Says the named places the player walks into (D2R named places): a bridge, stairs, a door, ruins, then a cart, a
    ///     well or an anvil in reach, after what is being said; and lists them for the scanner. Design: docs/research/places.md.
    /// </summary>
    public sealed class PlaceNarrator
    {
        private const int PollMs = 250;

        /// <summary>Room anchors are looked for this often: the game wakes entities as the player nears them.</summary>
        private const int AnchorPollMs = 2000;

        /// <summary>Rooms are matched by the entities met at most this often: new ones wake at every step.</summary>
        private const int ObjectMatchMs = 10_000;

        /// <summary>A prop this near (grid cells, about a step and a half: right beside or over it) is named; again after going four and a half steps away.</summary>
        private const float ReachCells = 14f;
        private const float ForgetCells = 45f;

        /// <summary>A piece first met this near (half a sub-tile) is underfoot: the step's own line names it.</summary>
        private const float AheadUnderfootCells = 12f;

        /// <summary>Scanner ids of places, between the tile landmarks and the death place (<see cref="EntityCatalog"/>).</summary>
        private const uint PlaceIdBase = 0xA0000000;

        private static readonly (string Path, string Kind)[] AnchorPaths =
        {
            ("Waypoint", "waypoint"), ("HealingWell", "well"), ("Checkpoint", "checkpoint"), ("Stash", "stash"),
        };

        /// <summary>More sub-tiles than this is a broken read, not an area (the largest maps hold a few tens of thousands).</summary>
        private const int MaxSubTiles = 1 << 20;

        private readonly PlaceCatalog catalog;
        private readonly RoomCatalog rooms;
        private readonly HostMemoryReader memory;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private static readonly TileGridSearch.Layout Measured = new(
            GameLayout.TileGrid.Stride, GameLayout.TileGrid.FileOffset, GameLayout.TileGrid.PathOffset, GameLayout.TileGrid.IdsOffset);

        /// <summary>Where the grid's fields are read: as measured, or as a search found them; replaced whole (builds run off the frame thread).</summary>
        private volatile TileGridSearch.Layout layout = Measured;
        private Task<(PlaceMap Map, int Count, int Generation)?>? building;
        private int generation;
        private PlaceTracker tracker = NewTracker();

        /// <summary>Pieces said as they come in sight, before they are reached (docs/research/map-topology.md).</summary>
        private readonly LandmarkAhead ahead = new(ExplorationMap.SeenRadiusCells, AheadUnderfootCells, 2 * ExplorationMap.SeenRadiusCells);
        private PlaceMap? map;
        private (int Count, IntPtr First, IntPtr Last) builtFor;
        private long lastPoll = long.MinValue / 2;
        private long lastAnchorPoll = long.MinValue / 2;

        /// <summary>The entities a room may place met in this area, by entity id: object key and grid cell.</summary>
        private readonly Dictionary<uint, (int Key, Vector2 Grid)> roomObjects = new();

        /// <summary>How many of <see cref="roomObjects"/> the last object match saw (-1: match again), and when it ran.</summary>
        private int objectsMatched;
        private long lastObjectMatch = long.MinValue / 2;

        /// <summary>The last build's verdict on the tile grid layout (1 fits, 2 found moved, -1 neither, 0 none new), for the frame thread.</summary>
        private int tileLayout;

        /// <summary>The self-test's line on the tile grid, from the last build.</summary>
        private string gridTest = "not judged yet (needs an area)";

        public PlaceNarrator(PlaceCatalog catalog, RoomCatalog rooms, HostMemoryReader memory, IAccessibilityOutputService output, PluginLog log)
        {
            this.catalog = catalog;
            this.rooms = rooms;
            this.memory = memory;
            this.output = output;
            this.log = log;
        }

        /// <summary>Where a tile grid that no longer reads after a game update is reported (docs/compatibility.md).</summary>
        public CompatibilityReport? Report { get; set; }

        /// <summary>A new area: its map is built again from its tiles.</summary>
        public void Reset()
        {
            this.ahead.Reset();
            this.map = null;
            this.builtFor = default;
            this.generation++; // a build still running belongs to the area left
            this.tracker = NewTracker();
            this.lastAnchorPoll = long.MinValue / 2;
            this.roomObjects.Clear();
            this.objectsMatched = 0;
            this.lastObjectMatch = long.MinValue / 2;
        }

        /// <summary>What the player stands on when no tile names it, by grid cell: a narrow passage of the map's shape.</summary>
        public Func<Vector2, string?>? MoreKindAt { get; set; }

        /// <summary>The kind of map piece at a grid cell of the current area, or null.</summary>
        public string? KindAt(Vector2 grid) => this.map?.KindAt(grid);

        /// <summary>The setting of the ground at a grid cell of the current area (<see cref="TerrainBiome"/>), or null.</summary>
        public string? BiomeAt(Vector2 grid) => this.map?.BiomeAt(grid);

        /// <summary>The named room at a grid cell of the current area (its name: <see cref="Announcements.RoomName"/>), or null.</summary>
        public NamedRoom? RoomAt(Vector2 grid) => this.map is { } places ? RoomMatcher.At(places.NamedRooms, grid) : null;

        /// <summary>The map pieces of the current area (grid cells), for naming the places of the map's shape.</summary>
        public IReadOnlyList<(Vector2 Grid, string Kind)> Pieces() => this.map?.Pieces ?? Array.Empty<(Vector2 Grid, string Kind)>();

        /// <summary>More places to say ahead than the tiles name, in grid cells: the narrow passages of the map's shape.</summary>
        public Func<IEnumerable<(Vector2 Grid, string Kind)>>? MorePlaces { get; set; }

        /// <summary>
        ///     Per frame: keeps the area's map current and, while playing with the option on, names new places; with
        ///     <paramref name="sayAhead"/>, also the pieces that come in sight, with their side.
        /// </summary>
        public void Tick(bool playing, bool speak, bool sayAhead)
        {
            var now = Environment.TickCount64;
            if (now - this.lastPoll < PollMs)
            {
                return;
            }

            this.lastPoll = now;
            if (this.Current() is not { } places || !playing || !speak)
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var said = this.tracker.Update(places.KindAt(here) ?? this.MoreKindAt?.Invoke(here), here, places.Props, now);
            var lines = said.Select(Announcements.PlaceName).ToList();
            if (sayAhead && area.WorldToGridConvertor is > 0 and var toWorld)
            {
                var more = this.MorePlaces?.Invoke() ?? Enumerable.Empty<(Vector2 Grid, string Kind)>();
                foreach (var (grid, kind) in this.ahead.Update(here, places.Pieces.Concat(more)))
                {
                    var offset = (grid - here) * toWorld;
                    var side = Announcements.HintSides[ScreenProjection.Octant(player.WorldPosition, offset)];
                    lines.Add(Announcements.PlaceAhead(kind, side));
                }
            }

            if (lines.Count > 0)
            {
                this.output.Say(SpeechKind.Places, Announcements.Sentences(lines));
            }
        }

        /// <summary>The area's places for the scanner, map pieces and props, at world positions.</summary>
        public IEnumerable<ScanEntry> Entries(float height)
        {
            if (this.map is not { } places)
            {
                yield break;
            }

            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            var index = 0u;
            foreach (var (grid, kind) in places.Pieces.Concat(places.Props))
            {
                yield return new ScanEntry(PlaceIdBase + index++, ScanCategory.Places, Announcements.PlaceName(kind), grid.X * toWorld, grid.Y * toWorld, height, kind);
            }

            // The named rooms the player has seen, to walk back to as to any place: what a sighted player's map shows.
            foreach (var room in places.NamedRooms)
            {
                var centre = new Vector2(room.X + (room.Width / 2f), room.Y + (room.Height / 2f));
                if (Announcements.RoomName(room.Label) is { } name && this.Seen?.Invoke(centre) == true)
                {
                    yield return new ScanEntry(PlaceIdBase + index, ScanCategory.Places, name, centre.X * toWorld, centre.Y * toWorld, height, RoomKind);
                }

                index++;
            }
        }

        /// <summary>A scanner entry's kind for a named room.</summary>
        public const string RoomKind = "named_room";

        /// <summary>Whether a grid cell was seen (the explored ground): only rooms seen are listed.</summary>
        public Func<Vector2, bool>? Seen { get; set; }

        /// <summary>Research: every sub-tile (grid cell, file, placed ids, rotation selector) and the anchor entities, to a file.</summary>
        public void Dump(string file)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var terrain = area.TerrainMetadata;
            var lines = new List<string> { $"# tiles per row {terrain.TotalTiles.X}; x y path idX idY rotation (x, y: the sub-tile's top-left grid cell)" };
            lines.AddRange(this.ReadPlacements(this.ReadGrid(terrain.TileDetailsPtr), (int)terrain.TotalTiles.X, this.layout)
                .Select(tile => $"{tile.Grid.X} {tile.Grid.Y} {tile.Path} {tile.IdX} {tile.IdY} {tile.Rotation}"));
            lines.Add("# entities: grid x y path");
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.Path is { } path && entity.TryGetComponent<Render>(out var render))
                {
                    lines.Add($"entity {render.GridPosition.X:0} {render.GridPosition.Y:0} {path}");
                }
            }

            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            System.IO.File.WriteAllLines(file, lines);
            this.log.Info($"Tile grid written to {file} ({lines.Count} lines)");
        }

        /// <summary>Research (dev command "layout"): how the last area's tile grid read.</summary>
        public string SelfTest() => $"tile grid {this.gridTest}";

        /// <summary>A new game process: the grid is judged again from where it was measured.</summary>
        public void ResetLayout()
        {
            this.layout = Measured;
            this.gridTest = "not judged yet (needs an area)";
        }

        private (PlaceMap Map, int Count, int Generation)? Build(StdVector details, int perRow, int count, int generation)
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var grid = this.ReadGrid(details);
                var layout = this.layout;
                var placements = this.ReadPlacements(grid, perRow, layout);
                var fits = LayoutFits(placements, count);

                // Searched on every grid (a few ms): the search also checks the id bytes (neighbours one step apart),
                // which LayoutFits does not, so a layout it finds elsewhere wins.
                var found = this.FindLayout(grid, count, perRow);
                if (found != null && found != layout)
                {
                    (layout, this.layout) = (found, found);
                    placements = this.ReadPlacements(grid, perRow, layout);
                    fits = true;
                }

                var verdict = !fits ? -1 : layout == Measured ? 1 : 2;
                this.gridTest = verdict > 0
                    ? $"{(verdict == 1 ? "ok" : "moved")} ({placements.Count} of {count} sub-tiles name a tile; {layout}; " +
                      $"{(found != null ? "a search finds it too" : "a search finds nothing here")})"
                    : $"FAIL ({placements.Count} of {count} sub-tiles name a tile, {grid.Length} bytes; nothing near {Measured} fits)";
                Interlocked.Exchange(ref this.tileLayout, verdict);
                if (placements.Count == 0)
                {
                    return null;
                }

                var built = PlaceMap.Build(this.catalog, placements, TileStructure.TileToGridConversion);
                this.log.Info($"Places: {built.Pieces.Count} map pieces, {built.Props.Count} props from {placements.Count} sub-tiles " +
                              $"({Kinds(built.Pieces.Concat(built.Props))}) in {timer.ElapsedMilliseconds} ms" +
                              (built.UnknownRotations.Count > 0 ? $"; tile props left out for rotations {string.Join(",", built.UnknownRotations)}" : string.Empty));
                timer.Restart();
                built.NamedRooms = built.TileRooms = RoomMatcher.Match(this.rooms, placements, TileStructure.TileToGridConversion);
                built.ObjectRooms = RoomObjectMatcher.Candidates(this.rooms, placements);
                this.log.Info($"Rooms: {built.NamedRooms.Count} named in {timer.ElapsedMilliseconds} ms" +
                              (built.NamedRooms.Count > 0 ? $" ({string.Join(", ", built.NamedRooms.Select(room => $"{room.Label} at {room.X},{room.Y}"))})" : string.Empty) +
                              $"; {built.ObjectRooms.Count} more its entities may place");
                return (built, count, generation);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.log.Warn($"Places: map not built: {ex.Message}");
                return null;
            }
        }

        /// <summary>The area's tile grid as raw bytes (GameHelper's TileDetailsPtr); empty when it cannot be read.</summary>
        private byte[] ReadGrid(StdVector details)
        {
            var bytes = details.Last.ToInt64() - details.First.ToInt64();
            return bytes is > 0 and <= (long)MaxSubTiles * GameLayout.TileGrid.Stride ? this.memory.ReadBytes(details.First, (int)bytes) : Array.Empty<byte>();
        }

        /// <summary>The sub-tiles that name a tile file, one GameLayout.TileGrid record each, row by row.</summary>
        private List<TilePlacement> ReadPlacements(byte[] grid, int tilesPerRow, TileGridSearch.Layout layout)
        {
            var (stride, file, path, ids) = layout;
            var paths = new Dictionary<long, string>();
            var count = grid.Length / stride;
            var placements = new List<TilePlacement>(count);
            var step = TileStructure.TileToGridConversion;
            for (var i = 0; i < count; i++)
            {
                var at = i * stride;
                if (this.TilePath(paths, BitConverter.ToInt64(grid, at + file), path) is { Length: > 0 } name)
                {
                    var cell = new Vector2(i % tilesPerRow, i / tilesPerRow) * step;
                    placements.Add(new TilePlacement(name, grid[at + ids], grid[at + ids + 1], grid[at + ids + 2], cell));
                }
            }

            return placements;
        }

        /// <summary>The grid's layout found again by what its records hold (TileGridSearch), searched from where it was measured.</summary>
        private TileGridSearch.Layout? FindLayout(byte[] grid, int subTiles, int perRow)
        {
            var paths = new Dictionary<(long, int), string>();
            return TileGridSearch.Find(grid, subTiles, perRow, Measured, (file, path) =>
            {
                if (!paths.TryGetValue((file, path), out var name))
                {
                    name = this.ReadTilePath(file, path);
                    paths[(file, path)] = name;
                }

                return name.EndsWith(".tdt", StringComparison.Ordinal);
            });
        }

        /// <summary>
        ///     GameHelper's tile grid still reads (TileStructure, TgtFileStruct): half the sub-tiles name a tile file and
        ///     nearly all of those carry a rotation selector in range (the Act 1 town: 990 of 990 sub-tiles, 2026-10-25).
        /// </summary>
        private static bool LayoutFits(IReadOnlyList<TilePlacement> placements, int subTiles)
        {
            var named = placements.Count(placement => placement.Path.EndsWith(".tdt", StringComparison.Ordinal));
            var turned = placements.Count(placement => placement.Rotation is >= 0 and <= TileGridSearch.MaxRotation);
            return named * 2 >= subTiles && turned * 10 >= placements.Count * 9;
        }

        private string TilePath(Dictionary<long, string> paths, long tgtFile, int pathOffset)
        {
            if (!paths.TryGetValue(tgtFile, out var path))
            {
                path = this.ReadTilePath(tgtFile, pathOffset);
                paths[tgtFile] = path;
            }

            return path;
        }

        /// <summary>The lowercase path a tile file holds at the offset; empty when the pointer leads to none.</summary>
        private string ReadTilePath(long tgtFile, int pathOffset)
        {
            var at = new IntPtr(tgtFile) + pathOffset;
            return tgtFile != 0 && this.memory.IsValidAddress(at) && this.memory.TryRead<StdWString>(at, out var text)
                ? this.memory.ReadStdWString(text).ToLowerInvariant()
                : string.Empty;
        }

        private static string Kinds(IEnumerable<(Vector2 Grid, string Kind)> places) =>
            string.Join(", ", places.GroupBy(place => place.Kind).Select(group => $"{group.Key} {group.Count()}"));

        private static PlaceTracker NewTracker() => new(TileStructure.TileToGridConversion, ReachCells, ForgetCells);

        /// <summary>The map of the current area, built once its tiles are known and given the rooms its anchors place.</summary>
        private PlaceMap? Current()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var terrain = area.TerrainMetadata;
            var (perRow, rows) = (terrain.TotalTiles.X, terrain.TotalTiles.Y);
            if (perRow is <= 0 or > MaxSubTiles || rows is <= 0 or > MaxSubTiles || perRow * rows > MaxSubTiles ||
                terrain.TileDetailsPtr.Last.ToInt64() <= terrain.TileDetailsPtr.First.ToInt64())
            {
                return null;
            }

            switch (Interlocked.Exchange(ref this.tileLayout, 0))
            {
                case 1:
                    this.Report?.Found("tile grid");
                    break;
                case 2:
                    var (from, to) = TileGridSearch.FirstMoved(Measured, this.layout);
                    this.Report?.Moved("tile grid", from, to);
                    break;
                case -1:
                    this.Report?.Missing("tile grid");
                    break;
            }

            if (this.building is { IsCompleted: true } built)
            {
                this.building = null;
                if (built.Result is { } fresh && fresh.Generation == this.generation)
                {
                    this.map = fresh.Map;
                    this.lastAnchorPoll = long.MinValue / 2;
                    this.objectsMatched = -1; // the new map holds its tile rooms only
                    this.lastObjectMatch = long.MinValue / 2;
                }
            }

            // Built again when the grid changes (its size or its storage: the next area may share the size; a grid still
            // filling grows); a grid that failed is not read again until it changes.
            var count = (int)(perRow * rows);
            var grid = (count, terrain.TileDetailsPtr.First, terrain.TileDetailsPtr.Last);
            if (grid != this.builtFor && this.building == null)
            {
                // Off the frame thread: 58 ms for the town's 990 sub-tiles (2026-10-25), large areas hold thousands.
                this.builtFor = grid;
                var (details, tilesPerRow, generation) = (terrain.TileDetailsPtr, (int)perRow, this.generation);
                this.building = Task.Run(() => this.Build(details, tilesPerRow, count, generation));
            }

            if (this.map == null)
            {
                return null;
            }

            var now = Environment.TickCount64;
            if (now - this.lastAnchorPoll >= AnchorPollMs)
            {
                this.lastAnchorPoll = now;
                var anchors = new List<(string Kind, Vector2 Grid)>();
                foreach (var entity in area.AwakeEntities.Values)
                {
                    if (entity.IsValid && entity.Path is { } path && entity.TryGetComponent<Render>(out var render))
                    {
                        var cell = new Vector2(render.GridPosition.X, render.GridPosition.Y);
                        if (AnchorPaths.FirstOrDefault(anchor => path.Contains(anchor.Path, StringComparison.Ordinal)).Kind is { } kind)
                        {
                            anchors.Add((kind, cell));
                        }

                        // Where each object a room may place was first met: entities wake only near the player.
                        if (this.map.ObjectRooms.Count > 0 && !this.roomObjects.ContainsKey(entity.Id) && this.rooms.KeyOf(path) is >= 0 and var key)
                        {
                            this.roomObjects[entity.Id] = (key, cell);
                        }
                    }
                }

                if (this.map.MatchRooms(anchors))
                {
                    this.log.Info($"Places: rooms placed by {anchors.Count} anchors, {this.map.Props.Count} props now");
                }

                this.MatchObjectRooms(this.map, now);
            }

            return this.map;
        }

        /// <summary>Places the rooms the entities met tell (<see cref="RoomObjectMatcher"/>) when new ones were met, at most every <see cref="ObjectMatchMs"/>.</summary>
        private void MatchObjectRooms(PlaceMap places, long now)
        {
            if (this.roomObjects.Count == this.objectsMatched || now - this.lastObjectMatch < ObjectMatchMs)
            {
                return;
            }

            (this.objectsMatched, this.lastObjectMatch) = (this.roomObjects.Count, now);
            var found = RoomObjectMatcher.Match(this.rooms, places.ObjectRooms, this.roomObjects.Values.ToList(), TileStructure.TileToGridConversion);
            var named = RoomObjectMatcher.Merge(places.TileRooms, found);
            if (!named.SequenceEqual(places.NamedRooms))
            {
                places.NamedRooms = named;
                this.log.Info($"Rooms by objects: {string.Join(", ", named.Skip(places.TileRooms.Count).Select(room => $"{room.Label} at {room.X},{room.Y}"))} " +
                              $"({this.roomObjects.Count} objects met)");
            }
        }
    }
}
