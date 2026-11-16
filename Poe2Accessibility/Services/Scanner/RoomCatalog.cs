namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;

    /// <summary>A named room of the game's room files: its label key, folder, size in sub-tiles, tile families and comparable cells.</summary>
    public sealed record RoomShape(string Label, string Set, int Width, int Height, IReadOnlyList<int> Families, IReadOnlyList<(int X, int Y, int Class)> Cells);

    /// <summary>
    ///     The named rooms as patterns of tile classes and the class of each tile they can meet (catalog/rooms.json,
    ///     tools/Poe2UiExtract rooms; docs/research/rooms.md).
    /// </summary>
    public sealed class RoomCatalog
    {
        private const string TerrainPrefix = "metadata/terrain/";

        /// <summary>Class names: "wall", "floor", "excavation", "stairs", "tag:…" or "big:…".</summary>
        public IReadOnlyList<string> Classes { get; private set; } = Array.Empty<string>();

        /// <summary>By class: a decorative tag the generator may leave out.</summary>
        public IReadOnlyList<bool> Soft { get; private set; } = Array.Empty<bool>();

        /// <summary>By class: what a soft cell also takes (a wall, a floor, a closed door).</summary>
        public IReadOnlyList<bool> SoftAccepts { get; private set; } = Array.Empty<bool>();

        /// <summary>Tile families ("woods/manor"): the first two folders of a tile's path under metadata/terrain.</summary>
        public IReadOnlyList<string> Families { get; private set; } = Array.Empty<string>();

        /// <summary>Each tile's class by its lower-case path.</summary>
        public Dictionary<string, int> Tiles { get; } = new(StringComparer.Ordinal);

        public List<RoomShape> Rooms { get; } = new();

        /// <summary>Reads the catalog; a missing or broken one names no rooms.</summary>
        public static RoomCatalog Load(string file, PluginLog log)
        {
            if (!File.Exists(file))
            {
                log.Warn($"Rooms: {file} not found; rooms stay unnamed");
                return new RoomCatalog();
            }

            try
            {
                var catalog = Parse(File.ReadAllText(file));
                log.Info($"Rooms: {catalog.Rooms.Count} named rooms, {catalog.Tiles.Count} tiles, {catalog.Classes.Count} classes");
                return catalog;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException)
            {
                log.Warn($"Rooms: cannot read {file}: {ex.Message}");
                return new RoomCatalog();
            }
        }

        public static RoomCatalog Parse(string json)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var classes = root.GetProperty("classes").EnumerateArray().Select(name => name.GetString() ?? string.Empty).ToList();
            bool[] Flags(string name)
            {
                var flags = new bool[classes.Count];
                foreach (var id in root.GetProperty(name).EnumerateArray())
                {
                    flags[id.GetInt32()] = true;
                }

                return flags;
            }

            var catalog = new RoomCatalog
            {
                Classes = classes,
                Soft = Flags("soft"),
                SoftAccepts = Flags("softAccepts"),
                Families = root.GetProperty("families").EnumerateArray().Select(name => name.GetString() ?? string.Empty).ToList(),
            };
            foreach (var folder in root.GetProperty("tiles").EnumerateObject())
            {
                foreach (var tile in folder.Value.EnumerateObject())
                {
                    catalog.Tiles[$"{TerrainPrefix}{folder.Name}/{tile.Name}"] = tile.Value.GetInt32();
                }
            }

            foreach (var room in root.GetProperty("rooms").EnumerateArray())
            {
                var runs = room.GetProperty("runs").EnumerateArray().Select(number => number.GetInt32()).ToArray();
                var cells = new List<(int X, int Y, int Class)>();
                for (var i = 0; i + 3 < runs.Length; i += 4)
                {
                    for (var dx = 0; dx < runs[i + 2]; dx++)
                    {
                        cells.Add((runs[i + 1] + dx, runs[i], runs[i + 3]));
                    }
                }

                catalog.Rooms.Add(new RoomShape(
                    room.GetProperty("label").GetString() ?? string.Empty,
                    room.GetProperty("set").GetString() ?? string.Empty,
                    room.GetProperty("w").GetInt32(),
                    room.GetProperty("h").GetInt32(),
                    room.GetProperty("families").EnumerateArray().Select(id => id.GetInt32()).ToList(),
                    cells));
            }

            return catalog;
        }

        /// <summary>The family a tile path belongs to ("woods/manor"); null for a path with fewer folders.</summary>
        public static string? FamilyOf(string path)
        {
            if (!path.StartsWith(TerrainPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var parts = path[TerrainPrefix.Length..].Split('/');
            return parts.Length >= 3 ? $"{parts[0]}/{parts[1]}" : null;
        }
    }
}
