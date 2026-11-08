namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;

    /// <summary>
    ///     The endgame's words and the Atlas's graphs from the game's tables (catalog/endgame.json, `Poe2UiExtract endgame`;
    ///     docs/research/endgame.md): each map objective and the minimap icon of what it asks for, the nodes of the Atlas map,
    ///     the Atlas passive tree and the league trees with their links. Pure apart from <see cref="Load"/>.
    /// </summary>
    public sealed class EndgameCatalog
    {
        private const string Fallback = "en";

        private readonly CatalogData data;
        private readonly Dictionary<string, string> objectiveByIcon;
        private readonly Dictionary<string, int> towerReach;

        public EndgameCatalog(CatalogData data)
        {
            this.data = data;
            this.objectiveByIcon = data.Objectives.Where(pair => !string.IsNullOrEmpty(pair.Value.Icon))
                .GroupBy(pair => pair.Value.Icon!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
            this.towerReach = data.Nodes.Values.SelectMany(node => node.Towers ?? new List<string>()).GroupBy(tower => tower, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        }

        /// <summary>The catalog the plugin loaded; empty until then, so every lookup finds nothing.</summary>
        public static EndgameCatalog Shared { get; private set; } = new(new CatalogData());

        public int NodeCount => this.data.Nodes.Count;

        public static void Load(PluginLog log, string path)
        {
            try
            {
                if (File.Exists(path) && JsonConvert.DeserializeObject<CatalogData>(File.ReadAllText(path)) is { } loaded)
                {
                    Shared = new EndgameCatalog(loaded);
                }
            }
            catch (JsonException error)
            {
                log.Info($"Endgame catalog unreadable: {error.Message}");
            }

            log.Info($"Endgame: {Shared.data.Objectives.Count} map objectives, {Shared.objectiveByIcon.Count} with a minimap icon, {Shared.NodeCount} graph nodes from the game files");
        }

        /// <summary>
        ///     Whether a minimap icon marks what a map objective asks for ("Breach" for "Open all Unstable Breaches"): such an
        ///     object is the map's content to find. A spent one shows another icon ("AbyssPitInactive"), or none.
        /// </summary>
        public bool IsObjectiveIcon(string? icon) => icon != null && this.objectiveByIcon.ContainsKey(icon);

        /// <summary>The objective an icon's object is for, empty for none.</summary>
        public string ObjectiveOf(string icon) => this.objectiveByIcon.TryGetValue(icon, out var objective) ? objective : string.Empty;

        /// <summary>
        ///     The game's own word for an objective icon's object, singular ("Fisura" for the Breach icon, from the keyword its
        ///     objective links); empty when the catalog has none.
        /// </summary>
        public string DeviceName(string icon, string language) =>
            this.data.Objectives.TryGetValue(this.ObjectiveOf(icon), out var objective) && !string.IsNullOrEmpty(objective.Keyword)
                ? this.Text("term/" + objective.Keyword, language)
                : string.Empty;

        /// <summary>A text of the catalog in the language, else in English, else empty.</summary>
        public string Text(string key, string language) =>
            (this.data.Text.TryGetValue(language, out var own) && own.TryGetValue(key, out var text)) ||
            (this.data.Text.TryGetValue(Fallback, out var english) && english.TryGetValue(key, out text))
                ? text
                : string.Empty;

        public CatalogNode? Node(string passiveId) => this.data.Nodes.TryGetValue(passiveId, out var node) ? node : null;

        /// <summary>
        ///     A node by what is known before it is hovered: an Atlas passive's name, the one map an Atlas map node can be, or the
        ///     biomes it can be in ("Bosque o Pantano"); empty when the data holds none of these (the map is drawn at random).
        /// </summary>
        public string NodeName(string passiveId, string language)
        {
            if (this.Node(passiveId) is not { } node)
            {
                return string.Empty;
            }

            if (this.Text("passive/" + passiveId, language) is { Length: > 0 } name)
            {
                return name;
            }

            if (node.Maps is { Count: 1 } maps && this.Text("area/" + maps[0], language) is { Length: > 0 } area)
            {
                return area;
            }

            var biomes = (node.Biomes ?? new List<string>()).Select(biome => this.Text("biome/" + biome, language)).Where(text => text.Length > 0).Distinct().ToList();
            return string.Join(Announcements.ButtonSeparator, biomes);
        }

        /// <summary>What an Atlas map node asks or needs, in the game's words: its objective, its hint, what opens it.</summary>
        public IReadOnlyList<string> NodeDetails(string passiveId, string language)
        {
            if (this.Node(passiveId) is not { } node)
            {
                return Array.Empty<string>();
            }

            var keys = new[] { node.Objective is { Length: > 0 } objective ? "objective/" + objective : string.Empty, Client(node.Hint), Client(node.Blocked) };
            return keys.Where(key => key.Length > 0).Select(key => this.Text(key, language)).Where(text => text.Length > 0).Distinct().ToList();

            static string Client(string? id) => string.IsNullOrEmpty(id) ? string.Empty : "client/" + id;
        }

        /// <summary>
        ///     What an Atlas content icon stands for, by the art it draws ("…/AtlasIconContentBreach.dds" is "Fisura"): an
        ///     objective's keyword, a node stat's or a content's name; empty for any other art.
        /// </summary>
        public string IconName(string textureKey, string language)
        {
            var file = textureKey.Split('|')[0].Replace('\\', '/');
            file = file[(file.LastIndexOf('/') + 1)..];
            var dot = file.IndexOf('.', StringComparison.Ordinal);
            file = dot > 0 ? file[..dot] : file;
            return this.data.Icons.TryGetValue(file, out var icon) ? this.Text(icon.Name, language) : string.Empty;
        }

        /// <summary>How many nodes a tower's reach holds; 0 for a node that is no tower.</summary>
        public int TowerReach(string passiveId) => this.towerReach.TryGetValue(passiveId, out var count) ? count : 0;

        /// <summary>Whether two nodes are drawn in the same graph (a link across graphs is never drawn).</summary>
        public bool SameGraph(string a, string b) => this.Node(a) is { } first && this.Node(b) is { } second && first.Graph == second.Graph;

        /// <summary>The catalog file: objectives, map contents, endgame areas' pins, graph nodes, texts by language.</summary>
        public sealed class CatalogData
        {
            public Dictionary<string, CatalogObjective> Objectives { get; set; } = new(StringComparer.Ordinal);

            public Dictionary<string, CatalogContent> Contents { get; set; } = new(StringComparer.Ordinal);

            /// <summary>Atlas content icons by art file name (no folder, no extension).</summary>
            public Dictionary<string, CatalogIcon> Icons { get; set; } = new(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, string> Maps { get; set; } = new(StringComparer.Ordinal);

            public Dictionary<string, CatalogNode> Nodes { get; set; } = new(StringComparer.Ordinal);

            public Dictionary<string, Dictionary<string, string>> Text { get; set; } = new(StringComparer.Ordinal);
        }

        /// <summary>A map objective: the minimap icon of what it asks for, and the keyword its text links.</summary>
        public sealed class CatalogObjective
        {
            public string? Icon { get; set; }

            public string? Keyword { get; set; }
        }

        public sealed class CatalogContent
        {
            public string? Objective { get; set; }
        }

        /// <summary>An Atlas content icon: the text keys of its name and of what it does.</summary>
        public sealed class CatalogIcon
        {
            public string Name { get; set; } = string.Empty;

            public string? Text { get; set; }
        }

        /// <summary>A node of a skill graph by its PassiveSkills id: where its group is, its links, and for an Atlas map node what it holds.</summary>
        public sealed class CatalogNode
        {
            public string Graph { get; set; } = string.Empty;

            /// <summary>Its group's centre in the graph's units, y growing downwards as on screen.</summary>
            public int X { get; set; }

            public int Y { get; set; }

            public int Orbit { get; set; }

            public int Slot { get; set; }

            public List<string>? Links { get; set; }

            public string? Pin { get; set; }

            public List<string>? Maps { get; set; }

            public List<string>? Biomes { get; set; }

            public string? Objective { get; set; }

            public string? Blocked { get; set; }

            public string? Hint { get; set; }

            /// <summary>The towers whose reach holds it: their tablets work on its map.</summary>
            public List<string>? Towers { get; set; }
        }
    }
}
