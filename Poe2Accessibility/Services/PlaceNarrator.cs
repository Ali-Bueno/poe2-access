namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading;
    using System.Threading.Tasks;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
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

        /// <summary>A prop this near (grid cells, about two and a half steps) is named; again after going six steps away.</summary>
        private const float ReachCells = 25f;
        private const float ForgetCells = 60f;

        /// <summary>Scanner ids of places, between the tile landmarks and the death place (<see cref="EntityCatalog"/>).</summary>
        private const uint PlaceIdBase = 0xA0000000;

        private static readonly (string Path, string Kind)[] AnchorPaths =
        {
            ("Waypoint", "waypoint"), ("HealingWell", "well"), ("Checkpoint", "checkpoint"), ("Stash", "stash"),
        };

        /// <summary>More sub-tiles than this is a broken read, not an area (the largest maps hold a few tens of thousands).</summary>
        private const int MaxSubTiles = 1 << 20;

        /// <summary>A rotation selector names one of the eight turns and mirrors of a tile (0.5.5e: 0..7 seen).</summary>
        private const int MaxRotation = 7;

        private readonly PlaceCatalog catalog;
        private readonly HostMemoryReader memory;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private Task<(PlaceMap Map, int Count, int Generation)?>? building;
        private int generation;
        private PlaceTracker tracker = NewTracker();
        private PlaceMap? map;
        private (int Count, IntPtr First) builtFor;
        private long lastPoll = long.MinValue / 2;
        private long lastAnchorPoll = long.MinValue / 2;

        /// <summary>The last build's verdict on the tile grid layout (1 fits, -1 does not, 0 none new), for the frame thread.</summary>
        private int tileLayout;

        public PlaceNarrator(PlaceCatalog catalog, HostMemoryReader memory, IAccessibilityOutputService output, PluginLog log)
        {
            this.catalog = catalog;
            this.memory = memory;
            this.output = output;
            this.log = log;
        }

        /// <summary>Where a tile grid that no longer reads after a game update is reported (docs/compatibility.md).</summary>
        public CompatibilityReport? Report { get; set; }

        /// <summary>A new area: its map is built again from its tiles.</summary>
        public void Reset()
        {
            this.map = null;
            this.builtFor = default;
            this.generation++; // a build still running belongs to the area left
            this.tracker = NewTracker();
            this.lastAnchorPoll = long.MinValue / 2;
        }

        /// <summary>Per frame: keeps the area's map current and, while playing with the option on, names new places.</summary>
        public void Tick(bool playing, bool speak)
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
            if (!area.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var here = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var said = this.tracker.Update(places.KindAt(here), here, places.Props);
            if (said.Count > 0)
            {
                this.output.Say(SpeechKind.Places, Announcements.Sentences(said.Select(Announcements.PlaceName)));
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
        }

        /// <summary>Research: every sub-tile (grid cell, file, placed ids, rotation selector) and the anchor entities, to a file.</summary>
        public void Dump(string file)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var terrain = area.TerrainMetadata;
            var lines = new List<string> { $"# tiles per row {terrain.TotalTiles.X}; x y path idX idY rotation (x, y: the sub-tile's top-left grid cell)" };
            lines.AddRange(this.ReadPlacements(terrain.TileDetailsPtr, (int)terrain.TotalTiles.X)
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

        private (PlaceMap Map, int Count, int Generation)? Build(GameOffsets.Natives.StdVector details, int perRow, int count, int generation)
        {
            try
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                var placements = this.ReadPlacements(details, perRow);
                Interlocked.Exchange(ref this.tileLayout, LayoutFits(placements, count) ? 1 : -1);
                if (placements.Count == 0)
                {
                    return null;
                }

                var built = PlaceMap.Build(this.catalog, placements, TileStructure.TileToGridConversion);
                this.log.Info($"Places: {built.Pieces.Count} map pieces, {built.Props.Count} props from {placements.Count} sub-tiles " +
                              $"({Kinds(built.Pieces.Concat(built.Props))}) in {timer.ElapsedMilliseconds} ms" +
                              (built.UnknownRotations.Count > 0 ? $"; tile props left out for rotations {string.Join(",", built.UnknownRotations)}" : string.Empty));
                return (built, count, generation);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                this.log.Warn($"Places: map not built: {ex.Message}");
                return null;
            }
        }

        /// <summary>The area's tile grid (GameHelper's TileDetailsPtr, one TileStructure per sub-tile, row by row).</summary>
        private List<TilePlacement> ReadPlacements(GameOffsets.Natives.StdVector details, int tilesPerRow)
        {
            var paths = new Dictionary<IntPtr, string>();
            var tiles = this.memory.ReadStdVector<TileStructure>(details, MaxSubTiles);
            var placements = new List<TilePlacement>(tiles.Length);
            var step = TileStructure.TileToGridConversion;
            for (var i = 0; i < tiles.Length; i++)
            {
                if (this.TilePath(paths, tiles[i].TgtFilePtr) is { Length: > 0 } path)
                {
                    var grid = new Vector2(i % tilesPerRow, i / tilesPerRow) * step;
                    placements.Add(new TilePlacement(path, tiles[i].tileIdX, tiles[i].tileIdY, tiles[i].RotationSelector, grid));
                }
            }

            return placements;
        }

        /// <summary>
        ///     GameHelper's tile grid still reads (TileStructure, TgtFileStruct): half the sub-tiles name a tile file and
        ///     nearly all of those carry a rotation selector in range (the Act 1 town: 990 of 990 sub-tiles, 2026-10-25).
        /// </summary>
        private static bool LayoutFits(IReadOnlyList<TilePlacement> placements, int subTiles)
        {
            var named = placements.Count(placement => placement.Path.EndsWith(".tdt", StringComparison.Ordinal));
            var turned = placements.Count(placement => placement.Rotation is >= 0 and <= MaxRotation);
            return named * 2 >= subTiles && turned * 10 >= placements.Count * 9;
        }

        private string TilePath(Dictionary<IntPtr, string> paths, IntPtr tgtFile)
        {
            if (!paths.TryGetValue(tgtFile, out var path))
            {
                path = this.memory.TryRead<TgtFileStruct>(tgtFile, out var file) ? this.memory.ReadStdWString(file.TgtPath).ToLowerInvariant() : string.Empty;
                paths[tgtFile] = path;
            }

            return path;
        }

        private static string Kinds(IEnumerable<(Vector2 Grid, string Kind)> places) =>
            string.Join(", ", places.GroupBy(place => place.Kind).Select(group => $"{group.Key} {group.Count()}"));

        private static PlaceTracker NewTracker() => new(TileStructure.TileToGridConversion, ReachCells, ForgetCells);

        /// <summary>The map of the current area, built once its tiles are known and given the rooms its anchors place.</summary>
        private PlaceMap? Current()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var terrain = area.TerrainMetadata;
            var count = (int)((terrain.TileDetailsPtr.Last.ToInt64() - terrain.TileDetailsPtr.First.ToInt64()) / System.Runtime.CompilerServices.Unsafe.SizeOf<TileStructure>());
            if (count <= 0 || terrain.TotalTiles.X <= 0)
            {
                return null;
            }

            switch (Interlocked.Exchange(ref this.tileLayout, 0))
            {
                case 1:
                    this.Report?.Found("tile grid");
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
                }
            }

            // Built again when the grid changes (its size or its storage: the next area may share the size); a grid that
            // failed is not read again until it changes.
            var grid = (count, terrain.TileDetailsPtr.First);
            if (grid != this.builtFor && this.building == null)
            {
                // Off the frame thread: 58 ms for the town's 990 sub-tiles (2026-10-25), large areas hold thousands.
                this.builtFor = grid;
                var (details, perRow, generation) = (terrain.TileDetailsPtr, (int)terrain.TotalTiles.X, this.generation);
                this.building = Task.Run(() => this.Build(details, perRow, count, generation));
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
                    if (entity.IsValid && entity.Path is { } path && entity.TryGetComponent<Render>(out var render) &&
                        AnchorPaths.FirstOrDefault(anchor => path.Contains(anchor.Path, StringComparison.Ordinal)).Kind is { } kind)
                    {
                        anchors.Add((kind, new Vector2(render.GridPosition.X, render.GridPosition.Y)));
                    }
                }

                if (this.map.MatchRooms(anchors))
                {
                    this.log.Info($"Places: rooms placed by {anchors.Count} anchors, {this.map.Props.Count} props now");
                }
            }

            return this.map;
        }
    }
}
