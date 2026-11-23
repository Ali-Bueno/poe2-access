namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    ///     Whether an area's map changes: how many layouts the game has for it (catalog/area-layouts.json), and how the maps the
    ///     player met there before were laid out (their frame: <see cref="AreaMemory.Frame"/>), kept across sessions.
    /// </summary>
    public sealed class AreaLayouts
    {
        /// <summary>Instances kept an area: enough to say "the same as the last times".</summary>
        private const int KeptInstances = 12;

        private const char Separator = '\t';

        private readonly Dictionary<string, int> layouts;
        private readonly Dictionary<string, List<(string Instance, ulong Frame)>> met = new(StringComparer.Ordinal);
        private readonly string path;

        /// <param name="catalog">The catalog's text: { areaId: layouts }.</param>
        /// <param name="path">Where the maps met are kept: "area, instance, frame" lines, oldest first.</param>
        public AreaLayouts(string catalog, string path)
        {
            this.layouts = JsonConvert.DeserializeObject<Dictionary<string, int>>(catalog) ?? new();
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

        public static AreaLayouts Load(PluginLog log, string catalog, string path)
        {
            try
            {
                return new AreaLayouts(File.ReadAllText(catalog), path);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Area layouts: cannot read {catalog}: {ex.Message}");
                return new AreaLayouts("{}", path);
            }
        }

        /// <summary>
        ///     The area's line, empty when the game's data do not tell: "Fixed map", "Variable map, the same as last time".
        ///     The instance counts as met from now on.
        /// </summary>
        /// <param name="frame">The map's frame; 0 when its layout has none to compare.</param>
        public string Line(string area, string instance, ulong frame)
        {
            if (!this.layouts.TryGetValue(area, out var count))
            {
                return string.Empty;
            }

            if (count == 1)
            {
                return Announcements.FixedMap;
            }

            if (frame == 0 || instance.Length == 0)
            {
                return Announcements.VariableMap(0, false);
            }

            var before = this.met.TryGetValue(area, out var list) ? list.Where(one => one.Instance != instance).ToList() : new();
            if (list == null || list.All(one => one.Instance != instance || one.Frame != frame))
            {
                this.Keep(area, instance, frame);
            }

            var same = before.AsEnumerable().Reverse().TakeWhile(one => one.Frame == frame).Count();
            return Announcements.VariableMap(same, before.Count > 0);
        }

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
    }
}
