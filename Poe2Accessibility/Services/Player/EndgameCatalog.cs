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
        private readonly CatalogData data;
        private readonly Dictionary<string, string> objectiveByIcon;
        private readonly Dictionary<string, int> towerReach;
        private readonly Dictionary<string, HashSet<string>> contentNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> contentNamesByLength = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string[]> biomeNames = new(StringComparer.Ordinal);
        private readonly List<string> biomes;
        private readonly List<string> contentIds;
        private readonly HashSet<string> rootedGraphs;
        private readonly HashSet<string> rarePins;

        /// <summary>A pin on no more than this share of the Atlas map's nodes marks a one-off place (in 0.5: Kingsmarch, the gates, the waypoints).</summary>
        private const double RarePinShare = 0.01;

        public EndgameCatalog(CatalogData data)
        {
            // A graph whose nodes roll biomes is the map's layout, never a tree, whatever roots it gains. Pins and maps
            // do not tell: the Atlas skill tree's nodes carry pins too (PrecursorMap, PrecursorNotable) and its root
            // the town's map (BaseCamp, G_Endgame_Town; catalog of 2026-11-25), which once made it a map and silenced
            // its finder (the owner, 2026-11-27).
            var mapGraphs = data.Nodes.Values.Where(node => node.Biomes != null).Select(node => node.Graph).ToHashSet(StringComparer.Ordinal);
            this.rootedGraphs = data.Nodes.Values.Where(node => node.Root && !mapGraphs.Contains(node.Graph)).Select(node => node.Graph).ToHashSet(StringComparer.Ordinal);
            var mapNodes = data.Nodes.Values.Where(node => !this.rootedGraphs.Contains(node.Graph)).ToList();
            var rareAtMost = Math.Max(1, (int)(mapNodes.Count * RarePinShare));
            this.rarePins = mapNodes.Where(node => node.Pin != null).GroupBy(node => node.Pin!, StringComparer.Ordinal)
                .Where(group => group.Count() <= rareAtMost).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            this.data = data;
            this.objectiveByIcon = data.Objectives.Where(pair => !string.IsNullOrEmpty(pair.Value.Icon))
                .GroupBy(pair => pair.Value.Icon!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);
            this.towerReach = data.Nodes.Values.SelectMany(node => node.Towers ?? new List<string>()).GroupBy(tower => tower, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            this.biomes = data.Text.Values.SelectMany(texts => texts.Keys)
                .Where(key => key.StartsWith("biome/", StringComparison.Ordinal)).Select(key => key["biome/".Length..])
                .Distinct(StringComparer.Ordinal).ToList();
            this.contentIds = data.Text.Values.SelectMany(texts => texts.Keys)
                .Where(key => key.StartsWith("content/", StringComparison.Ordinal)).Select(key => key["content/".Length..])
                .Distinct(StringComparer.Ordinal).ToList();
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

        /// <summary>An objective's own short word ("Ritual", from the keyword its description links), for lists of places.</summary>
        public string ObjectiveWord(string? objective, string language) =>
            objective != null && this.data.Objectives.TryGetValue(objective, out var named) && !string.IsNullOrEmpty(named.Keyword)
                ? this.Text("term/" + named.Keyword, language)
                : string.Empty;

        /// <summary>
        ///     The game's own word for an objective icon's object, singular ("Fisura" for the Breach icon, from the keyword its
        ///     objective links); empty when the catalog has none.
        /// </summary>
        public string DeviceName(string icon, string language) =>
            this.data.Objectives.TryGetValue(this.ObjectiveOf(icon), out var objective) && !string.IsNullOrEmpty(objective.Keyword)
                ? this.Text("term/" + objective.Keyword, language)
                : string.Empty;

        /// <summary>A text of the catalog in the language, else in English, else empty.</summary>
        public string Text(string key, string language) => Localization.Pick(this.data.Text, language, key) ?? string.Empty;

        public CatalogNode? Node(string passiveId) => this.data.Nodes.TryGetValue(passiveId, out var node) ? node : null;

        /// <summary>
        ///     Whether the D-pad finder walks a graph: a tree with roots in the files (the map layout has none) in one of
        ///     <see cref="AtlasTreeMap.FinderFolders"/>, so a renamed or reworked tree file there still counts and no other
        ///     tree does; a catalog made before the graph paths knows the 0.5 Atlas tree by name.
        /// </summary>
        public bool HasTreeFinder(string graph) =>
            this.data.Graphs.TryGetValue(graph, out var path)
                ? this.rootedGraphs.Contains(graph) && AtlasTreeMap.FinderFolders.Any(folder => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                : graph == AtlasTreeMap.LegacyGraphName;

        /// <summary>Whether a graph is a skill tree (rooted, no biomes), not the map's layout: its nodes are passives, never areas.</summary>
        public bool IsTree(string graph) => this.rootedGraphs.Contains(graph);

        /// <summary>A node's PassiveSkills marks and whether it is a root of its graph, for the tree finder's kinds.</summary>
        public (string? Flags, bool Root) Marks(string passiveId) => this.Node(passiveId) is { } node ? (node.Flags, node.Root) : (null, false);

        /// <summary>Every node of one graph, for a walk over the whole of it (the Atlas tree's finder).</summary>
        public IReadOnlyList<(string Id, int X, int Y, IReadOnlyList<string>? Links)> GraphNodes(string graph) =>
            this.data.Nodes.Where(pair => pair.Value.Graph == graph)
                .Select(pair => (pair.Key, pair.Value.X, pair.Value.Y, (IReadOnlyList<string>?)pair.Value.Links))
                .ToList();

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

            // A tree's node is a passive: the Atlas tree's root names the town's map (G_Endgame_Town), never its name.
            if (node.Maps is { Count: 1 } maps && !this.IsTree(node.Graph) && this.Text("area/" + maps[0], language) is { Length: > 0 } area)
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

        /// <summary>
        ///     Whether a text is exactly a content's name in the language (an Atlas node's content icons carry their
        ///     name as text; their art does not read on the canvas-drawn elements, live 2026-11-25).
        /// </summary>
        public bool IsContentName(string text, string language) => this.ContentNamePool(language).Contains(text);

        /// <summary>Every name a content goes by in the language: the contents' own names and their icons' words.</summary>
        private HashSet<string> ContentNamePool(string language)
        {
            if (!this.contentNames.TryGetValue(language, out var names))
            {
                names = new HashSet<string>(
                    this.contentIds.Select(id => this.Text("content/" + id, language))
                        .Concat(this.data.Icons.Values.Select(icon => this.Text(icon.Name, language)))
                        .Where(name => name.Length > 0),
                    StringComparer.Ordinal);
                this.contentNames[language] = names;
            }

            return names;
        }

        /// <summary>The content name pool longest first, sorted once per language (ContentNamesIn runs per popup text while a landing settles).</summary>
        private string[] ContentNamesByLength(string language)
        {
            if (!this.contentNamesByLength.TryGetValue(language, out var names))
            {
                names = this.ContentNamePool(language).OrderByDescending(name => name.Length).ToArray();
                this.contentNamesByLength[language] = names;
            }

            return names;
        }

        /// <summary>The biomes' names in the language, read once per language.</summary>
        private string[] BiomeNames(string language)
        {
            if (!this.biomeNames.TryGetValue(language, out var names))
            {
                names = this.biomes.Select(id => this.Text("biome/" + id, language)).Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
                this.biomeNames[language] = names;
            }

            return names;
        }

        /// <summary>
        ///     The biome a text ends in ("Biome: Forest" on a node's popup), in the language; empty when it ends in no
        ///     known biome. A node's rolled biome is only on its popup: 410 of 610 map nodes list none, and one area
        ///     rolls several (Port at Water and at Grass, live 2026-11-25).
        /// </summary>
        public string BiomeNameIn(string text, string language)
        {
            foreach (var name in this.BiomeNames(language))
            {
                // The popup's line is shaped "Biome: Forest": a bare suffix also names a biome off a map
                // title that merely ends in one ("Bosque" the area, "Obscure Island"), spoken as its biome.
                if (text.EndsWith(name, StringComparison.Ordinal) &&
                    (text.Length == name.Length || (text.Length >= name.Length + 2 && text.AsSpan(text.Length - name.Length - 2, 2).SequenceEqual(": "))))
                {
                    return name;
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     The content names a text carries ("Area contains a Mirror of Delirium" on a node's popup), in the order
        ///     they stand in it: contents are rolled from the node's stats, so the popup's own words are their only
        ///     reliable source (live 2026-11-25: canvas icons exist only at some zooms and give no readable art).
        ///     A match must stand free of letters on both sides, and a longer name wins its place over a shorter
        ///     inside it ("Powerful Map Boss" over "Map Boss").
        /// </summary>
        public IReadOnlyList<string> ContentNamesIn(string text, string language)
        {
            var found = new List<(int At, string Name)>();
            foreach (var name in this.ContentNamesByLength(language))
            {
                for (var at = text.IndexOf(name, StringComparison.Ordinal); at >= 0; at = text.IndexOf(name, at + 1, StringComparison.Ordinal))
                {
                    var end = at + name.Length;
                    if ((at == 0 || !char.IsLetter(text[at - 1])) && (end == text.Length || !char.IsLetter(text[end])) &&
                        !found.Any(other => at < other.At + other.Name.Length && other.At < end))
                    {
                        found.Add((at, name));
                        break;
                    }
                }
            }

            return found.OrderBy(match => match.At).Select(match => match.Name).ToList();
        }

        /// <summary>Whether a node's pin is one few Atlas map nodes show, so it names a place to steer by whatever the pin is called.</summary>
        public bool HasRarePin(string passiveId) => this.Node(passiveId)?.Pin is { } pin && this.rarePins.Contains(pin);

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

            /// <summary>Each graph's file in the game's bundles, by the graph name its nodes carry.</summary>
            public Dictionary<string, string> Graphs { get; set; } = new(StringComparer.Ordinal);

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

            /// <summary>Its marks from PassiveSkills' flag columns: k keystone, n notable, i icon only (a mastery); null in older catalogs.</summary>
            public string? Flags { get; set; }

            /// <summary>A root of its graph file, where a tree starts.</summary>
            public bool Root { get; set; }

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
