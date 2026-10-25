namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
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

        private readonly PlaceCatalog catalog;
        private readonly HostMemoryReader memory;
        private readonly IAccessibilityOutputService output;
        private readonly PluginLog log;
        private readonly Dictionary<IntPtr, string> tilePaths = new();
        private PlaceTracker tracker = NewTracker();
        private PlaceMap? map;
        private int tileCount;
        private long lastPoll = long.MinValue / 2;
        private long lastAnchorPoll = long.MinValue / 2;

        public PlaceNarrator(PlaceCatalog catalog, HostMemoryReader memory, IAccessibilityOutputService output, PluginLog log)
        {
            this.catalog = catalog;
            this.memory = memory;
            this.output = output;
            this.log = log;
        }

        /// <summary>A new area: its map is built again from its tiles.</summary>
        public void Reset()
        {
            this.map = null;
            this.tileCount = 0;
            this.tilePaths.Clear();
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

        /// <summary>The area's tile grid (GameHelper's TileDetailsPtr, one TileStructure per sub-tile, row by row).</summary>
        private List<TilePlacement> ReadPlacements(GameOffsets.Natives.StdVector details, int tilesPerRow)
        {
            var tiles = this.memory.ReadStdVector<TileStructure>(details, MaxSubTiles);
            var placements = new List<TilePlacement>(tiles.Length);
            var step = TileStructure.TileToGridConversion;
            for (var i = 0; i < tiles.Length; i++)
            {
                if (this.TilePath(tiles[i].TgtFilePtr) is { Length: > 0 } path)
                {
                    var grid = new Vector2(i % tilesPerRow, i / tilesPerRow) * step;
                    placements.Add(new TilePlacement(path, tiles[i].tileIdX, tiles[i].tileIdY, tiles[i].RotationSelector, grid));
                }
            }

            return placements;
        }

        private string TilePath(IntPtr tgtFile)
        {
            if (!this.tilePaths.TryGetValue(tgtFile, out var path))
            {
                path = this.memory.TryRead<TgtFileStruct>(tgtFile, out var file) ? this.memory.ReadStdWString(file.TgtPath).ToLowerInvariant() : string.Empty;
                this.tilePaths[tgtFile] = path;
            }

            return path;
        }

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

            if (this.map == null || count != this.tileCount)
            {
                var placements = this.ReadPlacements(terrain.TileDetailsPtr, (int)terrain.TotalTiles.X);
                if (placements.Count == 0)
                {
                    return null;
                }

                this.map = PlaceMap.Build(this.catalog, placements, TileStructure.TileToGridConversion);
                this.tileCount = count;
                this.lastAnchorPoll = long.MinValue / 2;
                this.log.Info($"Places: {this.map.Pieces.Count} map pieces, {this.map.Props.Count} props from {placements.Count} sub-tiles" +
                              (this.map.UnknownRotations.Count > 0 ? $"; tile props left out for rotations {string.Join(",", this.map.UnknownRotations)}" : string.Empty));
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
