namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Newtonsoft.Json;

    /// <summary>One cue of catalog/map-hints.json: found by <see cref="Match"/>, <see cref="Tiles"/>, <see cref="Sign"/> or <see cref="After"/>.</summary>
    public sealed class MapHintRow
    {
        /// <summary>Part of an entity's path or of a doodad's model.</summary>
        public string Match { get; set; } = string.Empty;

        /// <summary>Parts of a tile's key, any of them.</summary>
        public List<string> Tiles { get; set; } = new();

        /// <summary>Part of the path of an object that shows the cue once it is finished (a ritual's altar).</summary>
        public string After { get; set; } = string.Empty;

        /// <summary>What a sign says, as an English label: its goal is not the place it names (the road's sign, the exit before).</summary>
        public string Sign { get; set; } = string.Empty;

        /// <summary>Which line says it (<see cref="Announcements.MapHint"/>).</summary>
        public string Hint { get; set; } = string.Empty;

        /// <summary>Where it leads: <see cref="MapHintTable.BossGoal"/>, <see cref="MapHintTable.RitualGoal"/> or a place's English label; empty when nothing says.</summary>
        public string Goal { get; set; } = string.Empty;

        /// <summary>The label of the tile that may be the goal before the game loads it, when several carry it and only one is open.</summary>
        public string GoalTile { get; set; } = string.Empty;

        /// <summary>Part of the models that close a <see cref="GoalTile"/> which is no way on (the brambles across a road's dead end).</summary>
        public string Blocked { get; set; } = string.Empty;

        /// <summary>It runs somewhere, to be followed (<see cref="HintTrails"/>); else it is a place, said on passing.</summary>
        public bool Line { get; set; }

        /// <summary>How near a spot of it the player walks on the trail, in cells.</summary>
        public float Reach { get; set; } = 35f;

        /// <summary>The game shows the goal on the minimap from here: the scanner lists it too.</summary>
        public bool Reveals { get; set; }
    }

    /// <summary>
    ///     The cues GGG placed on the maps, by area id (docs/research/map-hints-by-act.md): no game file lists them, so they are
    ///     listed by hand. Story signs need no row: any of them is said; a row only gives one its goal.
    /// </summary>
    public sealed class MapHintTable
    {
        /// <summary>The goal that means the area's boss room.</summary>
        public const string BossGoal = "boss";

        /// <summary>The goal that means the nearest ritual altar not finished, then the boss's.</summary>
        public const string RitualGoal = "ritual";

        private static readonly IReadOnlyList<MapHintRow> None = Array.Empty<MapHintRow>();

        private readonly Dictionary<string, List<MapHintRow>> areas;

        private MapHintTable(Dictionary<string, List<MapHintRow>> areas) => this.areas = areas;

        public IEnumerable<string> Areas => this.areas.Keys;

        public static MapHintTable Parse(string json) =>
            new(JsonConvert.DeserializeObject<Dictionary<string, List<MapHintRow>>>(json) ?? new());

        public static MapHintTable Load(PluginLog log, string file)
        {
            try
            {
                var table = Parse(File.ReadAllText(file));
                log.Info($"Map hints: {table.areas.Count} areas from {file}");
                return table;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                log.Warn($"Map hints: cannot read {file}: {ex.Message}");
                return new MapHintTable(new());
            }
        }

        public IReadOnlyList<MapHintRow> For(string areaId) => this.areas.TryGetValue(areaId, out var rows) ? rows : None;

        /// <summary>The row whose match is in this path or model; null when none is.</summary>
        public static MapHintRow? Matching(IEnumerable<MapHintRow> rows, string path) =>
            rows.FirstOrDefault(row => row.Match.Length > 0 && path.Contains(row.Match, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     What has been said, and where the player stood. A cue spreads: 411 mushrooms in one wood are one line, and it comes
    ///     back only once the player has gone on, when its side may have changed. Pure.
    /// </summary>
    public sealed class MapHintMemory
    {
        /// <summary>Past the whole 250-cell patch of mushrooms from its middle: the same cue is another place from here on.</summary>
        public const float AgainCells = 200f;

        private readonly Dictionary<string, List<Vector2>> said = new(StringComparer.Ordinal);

        /// <summary>True when this cue is news here, and it counts as said.</summary>
        public bool Note(string key, Vector2 cell)
        {
            if (!this.said.TryGetValue(key, out var places))
            {
                this.said[key] = places = new List<Vector2>();
            }

            if (places.Any(place => Vector2.Distance(place, cell) < AgainCells))
            {
                return false;
            }

            places.Add(cell);
            return true;
        }
    }
}
