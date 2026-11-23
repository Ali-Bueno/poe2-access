namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Newtonsoft.Json;

    /// <summary>
    ///     Whether an area's map changes: how many layouts the game has for it (catalog/area-layouts.json), what was met of it in
    ///     play (catalog/area-maps.json: the game does not draw from all its layouts, Clearfell has 33 and one was met in 65
    ///     instances), and where the exits of the maps this player met there before lay, kept across sessions.
    /// </summary>
    public sealed class AreaLayouts
    {
        /// <summary>Instances kept an area: enough to say "the same as the last times".</summary>
        private const int KeptInstances = 12;

        private const char Separator = '\t';

        private readonly Dictionary<string, int> layouts;
        private readonly Measured measured;
        private readonly Dictionary<string, List<(string Instance, ulong Frame)>> met = new(StringComparer.Ordinal);
        private readonly string path;

        /// <param name="catalog">The files' text: { areaId: layouts }.</param>
        /// <param name="measured">What was met in play: { "fixed": [areaId], "varies": [areaId] }.</param>
        /// <param name="path">Where the maps met are kept: "area, instance, exits" lines, oldest first.</param>
        public AreaLayouts(string catalog, string measured, string path)
        {
            this.layouts = JsonConvert.DeserializeObject<Dictionary<string, int>>(catalog) ?? new();
            this.measured = JsonConvert.DeserializeObject<Measured>(measured) ?? new();
            this.path = path;
            try
            {
                foreach (var line in File.Exists(path) ? File.ReadLines(path) : Array.Empty<string>())
                {
                    if (line.Split(Separator) is { Length: 3 } parts && ulong.TryParse(parts[2], out var frame))
                    {
                        this.Add(parts[0], parts[1], frame);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable: the maps met are news again.
            }
        }

        public static AreaLayouts Load(PluginLog log, string catalog, string measured, string path) =>
            new(ReadOrEmpty(log, catalog), ReadOrEmpty(log, measured), path);

        /// <summary>
        ///     Where the map's ways out lie: the tiles the landmark table names by an area, and the area-transition tiles
        ///     (<see cref="AreaMemory.Frame"/>), with the grid's size; 0 when the map has neither, for most of Act 2 has no
        ///     transition tiles.
        /// </summary>
        public static ulong Exits(IReadOnlyDictionary<string, List<Vector2>> tiles, IEnumerable<string> exits, int bytesPerRow, int gridBytes)
        {
            var hash = AreaMemory.Frame(tiles, bytesPerRow, gridBytes);
            foreach (var key in exits.Where(tiles.ContainsKey).OrderBy(key => key, StringComparer.Ordinal))
            {
                foreach (var spot in tiles[key].OrderBy(spot => spot.X).ThenBy(spot => spot.Y))
                {
                    hash = Mix(Mix(Mix(hash, (uint)bytesPerRow), (uint)(int)spot.X), (uint)(int)spot.Y);
                }
            }

            return hash;
        }

        /// <summary>
        ///     The area's line, empty when the game's data do not tell: "Fixed map", "Variable map, the same as last time", "The
        ///     map may change" for one whose files hold several layouts and that was not met often enough to tell. The
        ///     instance counts as met from now on.
        /// </summary>
        /// <param name="frame">Where its exits lie (<see cref="Exits"/>); 0 when there is nothing to compare.</param>
        public string Line(string area, string instance, ulong frame)
        {
            if (!this.layouts.TryGetValue(area, out var count))
            {
                return string.Empty;
            }

            var varies = this.measured.Varies.Contains(area);
            if (this.measured.Fixed.Contains(area) || (count == 1 && !varies))
            {
                return Announcements.FixedMap;
            }

            if (frame == 0 || instance.Length == 0)
            {
                return Announcements.VariableMap(varies, 0, false);
            }

            var before = this.met.TryGetValue(area, out var list) ? list.Where(one => one.Instance != instance).ToList() : new();
            if (list == null || list.All(one => one.Instance != instance || one.Frame != frame))
            {
                this.Keep(area, instance, frame);
            }

            var same = before.AsEnumerable().Reverse().TakeWhile(one => one.Frame == frame).Count();
            return Announcements.VariableMap(varies || before.Any(one => one.Frame != frame), same, before.Count > 0);
        }

        private static string ReadOrEmpty(PluginLog log, string file)
        {
            try
            {
                var text = File.ReadAllText(file);
                _ = Newtonsoft.Json.Linq.JToken.Parse(text);
                return text;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Area layouts: cannot read {file}: {ex.Message}");
                return "{}";
            }
        }

        private static ulong Mix(ulong hash, uint value) => ((hash ^ value) * 0x100000001B3) ^ (hash >> 29);

        private void Keep(string area, string instance, ulong frame)
        {
            this.Add(area, instance, frame);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(this.path))!);
                File.WriteAllLines(this.path, this.met.SelectMany(one => one.Value.Select(map => string.Join(Separator, one.Key, map.Instance, map.Frame))));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Not written: known for this session only.
            }
        }

        private void Add(string area, string instance, ulong frame)
        {
            if (!this.met.TryGetValue(area, out var list))
            {
                this.met[area] = list = new();
            }

            list.RemoveAll(one => one.Instance == instance); // a hash the game gave to another map since
            list.Add((instance, frame));
            if (list.Count > KeptInstances)
            {
                list.RemoveAt(0);
            }
        }

        private sealed class Measured
        {
            [JsonProperty("fixed")]
            public HashSet<string> Fixed { get; } = new(StringComparer.Ordinal);

            [JsonProperty("varies")]
            public HashSet<string> Varies { get; } = new(StringComparer.Ordinal);
        }
    }
}
