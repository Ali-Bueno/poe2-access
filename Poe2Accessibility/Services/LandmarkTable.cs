namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Newtonsoft.Json;

    /// <summary>
    ///     Named tiles per area ("G1_2": { tile key: "Mud Burrow" }): the exits and places a player is told about
    ///     before the game has loaded them as entities. GameHelper's Radar ships such a table
    ///     (important_tgt_files.txt); catalog/landmarks.json adds areas it lacks. A key without the ":x-y:y"
    ///     sub-tile suffix names the whole tile (its centre is used).
    /// </summary>
    public sealed class LandmarkTable
    {
        private static readonly IReadOnlyDictionary<string, string> None = new Dictionary<string, string>();

        private readonly Dictionary<string, Dictionary<string, string>> areas = new(StringComparer.Ordinal);

        public static LandmarkTable Load(PluginLog log, params string[] files)
        {
            var table = new LandmarkTable();
            foreach (var file in files)
            {
                if (!File.Exists(file))
                {
                    continue;
                }

                try
                {
                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(file)) ?? new();
                    foreach (var (area, tiles) in parsed)
                    {
                        if (!table.areas.TryGetValue(area, out var merged))
                        {
                            table.areas[area] = merged = new Dictionary<string, string>(StringComparer.Ordinal);
                        }

                        foreach (var (tile, label) in tiles)
                        {
                            merged[tile] = label;
                        }
                    }

                    log.Info($"Landmarks: {parsed.Count} areas from {file}");
                }
                catch (Exception ex) when (ex is IOException or JsonException)
                {
                    log.Warn($"Landmarks: cannot read {file}: {ex.Message}");
                }
            }

            return table;
        }

        public IReadOnlyDictionary<string, string> For(string areaId) =>
            this.areas.TryGetValue(areaId, out var tiles) ? tiles : None;
    }
}
