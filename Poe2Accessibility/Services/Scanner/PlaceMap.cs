namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text.Json;

    /// <summary>A spot of the catalog: grid cells from its tile's or room's origin, and what is there.</summary>
    public readonly record struct PlaceSpot(int X, int Y, string Kind);

    /// <summary>A room file with props, placed on the live map by the anchors the game shows as entities.</summary>
    public sealed record PlaceRoom(string Arm, IReadOnlyList<PlaceSpot> Anchors, IReadOnlyList<PlaceSpot> Props);

    /// <summary>The named places of the game's terrain files (catalog/places.json, tools/Poe2UiExtract places).</summary>
    public sealed class PlaceCatalog
    {
        public Dictionary<string, string> Tiles { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, IReadOnlyList<PlaceSpot>> TileProps { get; } = new(StringComparer.Ordinal);

        public List<PlaceRoom> Rooms { get; } = new();

        /// <summary>Reads the catalog; a missing or broken one names no places.</summary>
        public static PlaceCatalog Load(string file, PluginLog log)
        {
            if (!File.Exists(file))
            {
                log.Warn($"Places: {file} not found; map pieces stay unnamed");
                return new PlaceCatalog();
            }

            try
            {
                var catalog = Parse(File.ReadAllText(file));
                log.Info($"Places: {catalog.Tiles.Count} named tiles, {catalog.TileProps.Count} tiles and {catalog.Rooms.Count} rooms with props");
                return catalog;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                log.Warn($"Places: cannot read {file}: {ex.Message}");
                return new PlaceCatalog();
            }
        }

        public static PlaceCatalog Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var catalog = new PlaceCatalog();
            foreach (var tile in root.GetProperty("tiles").EnumerateObject())
            {
                catalog.Tiles[tile.Name] = tile.Value.GetString() ?? string.Empty;
            }

            foreach (var tile in root.GetProperty("tileProps").EnumerateObject())
            {
                catalog.TileProps[tile.Name] = Spots(tile.Value);
            }

            foreach (var room in root.GetProperty("rooms").EnumerateArray())
            {
                catalog.Rooms.Add(new PlaceRoom(room.GetProperty("arm").GetString() ?? string.Empty, Spots(room.GetProperty("anchors")), Spots(room.GetProperty("props"))));
            }

            return catalog;
        }

        private static IReadOnlyList<PlaceSpot> Spots(JsonElement array) =>
            array.EnumerateArray().Select(spot => new PlaceSpot(spot[0].GetInt32(), spot[1].GetInt32(), spot[2].GetString() ?? string.Empty)).ToList();
    }

    /// <summary>One placed sub-tile of the area's tile grid: its tile file (lower case), which sub-tile of the placed tile it is, the rotation selector and its top-left grid cell.</summary>
    public readonly record struct TilePlacement(string Path, int IdX, int IdY, int Rotation, Vector2 Grid);

    /// <summary>
    ///     One area's named places on its grid (D2R named places): the kind of map piece of each sub-tile, the pieces as
    ///     places, and the props of its tiles and matched rooms (docs/research/places.md).
    /// </summary>
    public sealed class PlaceMap
    {
        /// <summary>
        ///     A kind covering more of the area's ground than this is the area itself: the Act 1 town is one "lumbermill"
        ///     tile over 270 of its 927 sub-tiles that are not blank filler (2026-10-25); a bridge or a door is a few.
        /// </summary>
        private const float WholeAreaShare = 0.25f;

        /// <summary>Live anchors a room needs where its file has them (the town: waypoint, well and stash).</summary>
        private const int MinAnchors = 2;

        /// <summary>A room anchor this near (grid cells) a live entity of its kind is that entity.</summary>
        private const float AnchorToleranceCells = 2f;

        /// <summary>Two props of one kind this near (grid cells) are one (a room and its tile both list a campfire).</summary>
        private const float SamePropCells = 3f;

        private readonly Dictionary<(int, int), string> cells = new();
        private readonly List<(Vector2 Grid, string Kind)> props = new();
        private readonly List<(Vector2 Grid, string Kind)> pieces = new();
        private readonly HashSet<PlaceRoom> matched = new();
        private readonly HashSet<int> unknownRotations = new();
        private readonly IReadOnlyList<PlaceRoom> rooms;
        private readonly int subTileCells;

        private PlaceMap(IReadOnlyList<PlaceRoom> rooms, int subTileCells)
        {
            this.rooms = rooms;
            this.subTileCells = subTileCells;
        }

        /// <summary>Props on the grid: tiles' and matched rooms'.</summary>
        public IReadOnlyList<(Vector2 Grid, string Kind)> Props => this.props;

        /// <summary>Each run of touching sub-tiles of one kind, by its centre (grid cells).</summary>
        public IReadOnlyList<(Vector2 Grid, string Kind)> Pieces => this.pieces;

        /// <summary>Rotation selectors of tiles with props whose turn is not known, so their props were left out.</summary>
        public IReadOnlyCollection<int> UnknownRotations => this.unknownRotations;

        /// <summary>Builds the map from the area's tile grid (one placement per sub-tile).</summary>
        public static PlaceMap Build(PlaceCatalog catalog, IReadOnlyList<TilePlacement> placements, int subTileCells)
        {
            var map = new PlaceMap(catalog.Rooms, subTileCells);
            foreach (var placement in placements)
            {
                if (catalog.Tiles.TryGetValue(placement.Path, out var kind))
                {
                    map.cells[map.CellOf(placement.Grid)] = kind;
                }
            }

            var ground = placements.Count(placement => !Path.GetFileName(placement.Path).Contains("blank", StringComparison.Ordinal));
            foreach (var whole in map.cells.Values.GroupBy(kind => kind).Where(group => group.Count() > ground * WholeAreaShare).Select(group => group.Key).ToList())
            {
                foreach (var cell in map.cells.Where(pair => pair.Value == whole).Select(pair => pair.Key).ToList())
                {
                    map.cells.Remove(cell);
                }
            }

            var byGrid = placements.GroupBy(placement => map.CellOf(placement.Grid)).ToDictionary(group => group.Key, group => group.First());
            foreach (var origin in placements.Where(placement => placement.IdX == 0 && placement.IdY == 0))
            {
                if (catalog.TileProps.TryGetValue(origin.Path, out var tileProps))
                {
                    map.PlaceTileProps(origin, byGrid, tileProps);
                }
            }

            map.FindPieces();
            return map;
        }

        /// <summary>The kind of map piece at a grid cell, or null on plain ground.</summary>
        public string? KindAt(Vector2 grid) => this.cells.TryGetValue(this.CellOf(grid), out var kind) ? kind : null;

        /// <summary>
        ///     Places rooms at the grid origin when two of their anchors stand where live entities of their kind do (an
        ///     area's fixed layout: the town's waypoint, well and stash, 2026-10-25); true when a room was added.
        /// </summary>
        public bool MatchRooms(IReadOnlyList<(string Kind, Vector2 Grid)> anchors)
        {
            var added = false;
            foreach (var room in this.rooms)
            {
                if (this.matched.Contains(room) || room.Anchors.Count == 0)
                {
                    continue;
                }

                var hits = room.Anchors.Count(anchor => anchors.Any(live => live.Kind == anchor.Kind &&
                    Vector2.Distance(live.Grid, new Vector2(anchor.X, anchor.Y)) <= AnchorToleranceCells));
                // One anchor is any checkpoint near the room's own corner by chance (189 checkpoint rooms, review 2026-10-25).
                if (hits >= MinAnchors)
                {
                    this.matched.Add(room);
                    this.AddProps(room.Props.Select(prop => (new Vector2(prop.X, prop.Y), prop.Kind)));
                    added = true;
                }
            }

            return added;
        }

        /// <summary>
        ///     A tile file's cell in its placement: the sub-tile ids count in the placed tile (0,0 its top-left), the file's
        ///     cells turn with the selector. Selector 3 measured in the Act 1 town (2026-10-25): the encampment's campfire at
        ///     file (178, 286) is the room's fireplace at (353, 307) from the tile's corner (68, 138), its exit block at file
        ///     (299, 256) the exit at (324, 189): x' = y, y' = height - x.
        /// </summary>
        internal static Vector2? Turn(int rotation, Vector2 file, Vector2 footprint) => rotation switch
        {
            0 => file,
            3 => new Vector2(file.Y, footprint.Y - file.X),
            _ => null,
        };

        private (int, int) CellOf(Vector2 grid) => ((int)MathF.Floor(grid.X / this.subTileCells), (int)MathF.Floor(grid.Y / this.subTileCells));

        /// <summary>The props of one placed tile, whose sub-tiles are the cells right and below its corner with its path.</summary>
        private void PlaceTileProps(TilePlacement origin, Dictionary<(int, int), TilePlacement> byGrid, IReadOnlyList<PlaceSpot> tileProps)
        {
            var (x0, y0) = this.CellOf(origin.Grid);
            var width = 1;
            var height = 1;
            while (byGrid.TryGetValue((x0 + width, y0), out var next) && next.Path == origin.Path && next.IdX == width)
            {
                width++;
            }

            while (byGrid.TryGetValue((x0, y0 + height), out var next) && next.Path == origin.Path && next.IdY == height)
            {
                height++;
            }

            var footprint = new Vector2(width, height) * this.subTileCells;
            var turned = tileProps.Select(prop => (At: Turn(origin.Rotation, new Vector2(prop.X, prop.Y), footprint), prop.Kind)).ToList();
            if (turned.Any(prop => prop.At == null))
            {
                this.unknownRotations.Add(origin.Rotation);
                return;
            }

            this.AddProps(turned.Select(prop => (origin.Grid + prop.At!.Value, prop.Kind)));
        }

        private void AddProps(IEnumerable<(Vector2 Grid, string Kind)> spots)
        {
            foreach (var (grid, kind) in spots)
            {
                if (!this.props.Any(prop => prop.Kind == kind && Vector2.Distance(prop.Grid, grid) <= SamePropCells))
                {
                    this.props.Add((grid, kind));
                }
            }
        }

        private void FindPieces()
        {
            var seen = new HashSet<(int, int)>();
            foreach (var (start, kind) in this.cells)
            {
                if (!seen.Add(start))
                {
                    continue;
                }

                var run = new List<(int X, int Y)>();
                var queue = new Queue<(int X, int Y)>();
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    run.Add(cell);
                    foreach (var next in new[] { (cell.X + 1, cell.Y), (cell.X - 1, cell.Y), (cell.X, cell.Y + 1), (cell.X, cell.Y - 1) })
                    {
                        if (this.cells.TryGetValue(next, out var other) && other == kind && seen.Add(next))
                        {
                            queue.Enqueue(next);
                        }
                    }
                }

                var centre = new Vector2((float)run.Average(cell => cell.X) + 0.5f, (float)run.Average(cell => cell.Y) + 0.5f) * this.subTileCells;
                this.pieces.Add((centre, kind));
            }
        }
    }
}
