using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `endgame` command: the endgame's words and layout from the game's tables, for the Atlas and the maps
///     (docs/research/endgame.md): each map objective with the minimap icon of what it asks for and the keyword naming it,
///     each map content, biome and endgame area, every node of the skill graphs other than the main tree (the Atlas map's
///     layout, the Atlas passive tree, the league trees) with its position, links and, for an Atlas map node, its pin,
///     maps, biomes and messages. Writes { objectives, contents, nodes, text: { language: { key: text } } }.
///     The columns are found by <see cref="EndgameColumns"/>; one not found fails the step, which keeps the previous
///     catalog. Read-only on the game.
/// </summary>
internal static class EndgameExtractor
{
    private const int IdOffset = 0x0;

    /// <summary>The main tree, read from game memory by the plugin (GameLayout.PassiveTree.GraphFileName).</summary>
    private const string MainGraph = "metadata/passiveskillgraph.psg";

    /// <summary>Nodes the game names "do not translate" (the Atlas map's layout nodes) have no name worth saying.</summary>
    private const string UntranslatedMark = "[DNT]";

    /// <summary>Every graph id of a graph file must lead to a PassiveSkills row; fewer is a moved graph id column.</summary>
    private const double ResolvedShare = 0.99;

    public static int Run(Index index, string outFile)
    {
        var tables = new Dictionary<string, DatTable>(StringComparer.Ordinal);
        foreach (var name in new[] { "endgamemapobjectives", "endgamemapcontent", "endgamemapbiomes", "endgamemaps", "endgamemappins", "endgamemapatlas", "endgamemapcontentvisualidentity",
                     "endgamemapnodestats", "endgamemapatlastowers", "keywordpopups", "clientstrings2", "minimapicons", "passiveskills", "worldareas", "stats" })
        {
            if (DatTable.Open(index, name) is not { } table)
            {
                Say.Out.WriteLine($"  no {name}");
                return 1;
            }

            tables[name] = table;
        }

        if (EndgameColumns.Find(tables) is not { } c)
        {
            return 1;
        }

        var objectives = tables["endgamemapobjectives"];
        var contents = tables["endgamemapcontent"];
        var biomes = tables["endgamemapbiomes"];
        var maps = tables["endgamemaps"];
        var pins = tables["endgamemappins"];
        var atlas = tables["endgamemapatlas"];
        var keywords = tables["keywordpopups"];
        var clients = tables["clientstrings2"];
        var icons = tables["minimapicons"];
        var passives = tables["passiveskills"];
        var areas = tables["worldareas"];

        var objectiveOut = new SortedDictionary<string, ObjectiveEntry>(StringComparer.Ordinal);
        var keywordRows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var keywordIndex = Enumerable.Range(0, keywords.RowCount).Select(row => (Row: row, Id: keywords.StringOrNull(row, IdOffset)))
            .Where(pair => !string.IsNullOrEmpty(pair.Id)).GroupBy(pair => pair.Id!).ToDictionary(group => group.Key, group => group.First().Row, StringComparer.Ordinal);
        for (var row = 0; row < objectives.RowCount; row++)
        {
            var id = objectives.StringOrNull(row, IdOffset);
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var icon = IdOf(icons, objectives.Key(row, c.ObjectiveIcon));
            var keyword = GameMarkup.FirstKeyword(objectives.StringOrNull(row, c.ObjectiveText) ?? string.Empty);
            if (keyword.Length > 0 && keywordIndex.TryGetValue(keyword, out var keywordRow))
            {
                keywordRows[keyword] = keywordRow;
            }

            objectiveOut[id] = new ObjectiveEntry(Present(icon), Present(keywordIndex.ContainsKey(keyword) ? keyword : string.Empty));
        }

        var contentOut = new SortedDictionary<string, ContentEntry>(StringComparer.Ordinal);
        for (var row = 0; row < contents.RowCount; row++)
        {
            if (contents.StringOrNull(row, IdOffset) is { Length: > 0 } id)
            {
                contentOut[id] = new ContentEntry(Present(IdOf(objectives, contents.Key(row, c.ContentObjective))));
            }
        }

        // The icons an Atlas node shows for what it holds, by art file name, with the words that name each: a content's, else a
        // node stat's (the Delirium fog bank). ClientStrings2 rows are kept for their texts below.
        var visuals = tables["endgamemapcontentvisualidentity"];
        var nodeStats = tables["endgamemapnodestats"];
        var iconOut = new SortedDictionary<string, IconEntry>(StringComparer.Ordinal);
        string IconFile(long visualRow) => visualRow >= 0 && visualRow < visuals.RowCount && visuals.StringOrNull((int)visualRow, c.VisualIcon) is { Length: > 0 } art
            ? art[(art.LastIndexOf('/') + 1)..]
            : string.Empty;
        // An objective's own icon is named by its keyword ("Breach"); an icon several contents share by none of theirs.
        for (var row = 0; row < objectives.RowCount; row++)
        {
            if (objectives.StringOrNull(row, IdOffset) is { Length: > 0 } id && objectiveOut.TryGetValue(id, out var objective) && objective.Keyword is { } keyword &&
                objectives.StringOrNull(row, c.ObjectiveArt) is { Length: > 0 } art)
            {
                iconOut.TryAdd(art[(art.LastIndexOf('/') + 1)..], new IconEntry("term/" + keyword, "objective/" + id));
            }
        }

        var contentIcons = Enumerable.Range(0, contents.RowCount)
            .Select(row => (Id: contents.StringOrNull(row, IdOffset) ?? string.Empty, File: IconFile(contents.Key(row, c.ContentVisual))))
            .Where(pair => pair.Id.Length > 0 && pair.File.Length > 0).GroupBy(pair => pair.File).Where(group => group.Count() == 1).Select(group => group.First());
        foreach (var (id, file) in contentIcons)
        {
            iconOut.TryAdd(file, new IconEntry("content/" + id, "contentText/" + id));
        }

        var statClients = new List<(string Id, int Row)>();
        for (var row = 0; row < nodeStats.RowCount; row++)
        {
            var name = IdOf(clients, nodeStats.Key(row, c.NodeStatName));
            var about = IdOf(clients, nodeStats.Key(row, c.NodeStatText));
            if (name.Length > 0 && IconFile(nodeStats.Key(row, c.NodeStatVisual)) is { Length: > 0 } file)
            {
                iconOut.TryAdd(file, new IconEntry("client/" + name, about.Length > 0 ? "client/" + about : null));
                statClients.Add((name, (int)nodeStats.Key(row, c.NodeStatName)));
                if (about.Length > 0)
                {
                    statClients.Add((about, (int)nodeStats.Key(row, c.NodeStatText)));
                }
            }
        }

        // Every skill graph but the main tree: their nodes by PassiveSkills id, with each node's links.
        var byGraphId = new Dictionary<int, int>();
        for (var row = 0; row < passives.RowCount; row++)
        {
            byGraphId.TryAdd(passives.UInt16(row, c.PassiveGraphId), row);
        }

        var nodeOut = new SortedDictionary<string, NodeEntry>(StringComparer.Ordinal);
        var namedPassives = new SortedSet<int>();
        foreach (var file in index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(".psg", StringComparison.OrdinalIgnoreCase) &&
                                                                     !string.Equals(file.Path, MainGraph, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            var graphName = Path.GetFileNameWithoutExtension(file.Path!);
            // A file the known layout does not read, or of another version that reads but does not hold together, fails the step.
            var graph = SkillGraphFile.Parse(file.Read().ToArray());
            if (graph == null || (!graph.VersionKnown && !graph.IsConsistent))
            {
                Say.Out.WriteLine($"  {file.Path}: version {(graph?.Version ?? file.Read().ToArray().FirstOrDefault())} not read as a graph (known: {SkillGraphFile.KnownVersion}); endgame keeps its previous catalog");
                return 1;
            }

            if (!graph.VersionKnown)
            {
                Say.Out.WriteLine($"  {file.Path}: version {graph.Version} read with the version {SkillGraphFile.KnownVersion} layout; it holds together");
            }

            var resolved = graph.Nodes.Count(node => byGraphId.ContainsKey(node.Id));
            if (resolved < graph.Nodes.Count * ResolvedShare)
            {
                Say.Out.WriteLine($"  {file.Path}: {resolved} of {graph.Nodes.Count} nodes found in PassiveSkills: the graph id column moved");
                return 1;
            }

            var links = graph.Links();
            string PassiveId(int graphId) => byGraphId.TryGetValue(graphId, out var row) ? passives.StringOrNull(row, IdOffset) ?? string.Empty : string.Empty;
            var twice = 0;
            foreach (var node in graph.Nodes.Where(node => PassiveId(node.Id).Length > 0))
            {
                // A node listed twice keeps its first place; its links are merged.
                twice += nodeOut.TryAdd(PassiveId(node.Id), new NodeEntry(graphName, (int)MathF.Round(node.X), (int)MathF.Round(node.Y), node.Orbit, node.Slot,
                    Present(links[node.Id].Select(PassiveId).Where(id => id.Length > 0).ToList()))) ? 0 : 1;
                namedPassives.Add(byGraphId[node.Id]);
            }

            Say.Out.WriteLine($"  {file.Path}: {graph.Nodes.Count} nodes{(twice > 0 ? $", {twice} listed twice" : string.Empty)}");
        }

        // What each Atlas node holds: the pin it shows, the maps and biomes it can be, its objective and messages.
        var clientRows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (id, row) in statClients)
        {
            clientRows[id] = row;
        }

        var areaRows = new SortedDictionary<string, int>(StringComparer.Ordinal);
        string ClientId(long row)
        {
            var id = IdOf(clients, row);
            if (id.Length > 0)
            {
                clientRows[id] = (int)row;
            }

            return id;
        }

        string MapArea(long mapRow)
        {
            var areaRow = mapRow >= 0 && mapRow < maps.RowCount ? maps.Key((int)mapRow, c.MapArea) : -1;
            var id = IdOf(areas, areaRow);
            if (id.Length > 0)
            {
                areaRows[id] = (int)areaRow;
            }

            return id;
        }

        for (var row = 0; row < atlas.RowCount; row++)
        {
            // A row of a node drawn in no graph is never seen on screen.
            if (IdOf(passives, atlas.Key(row, c.NodePassive)) is not { Length: > 0 } passive || !nodeOut.TryGetValue(passive, out var placed))
            {
                continue;
            }

            nodeOut[passive] = placed with
            {
                Pin = Present(IdOf(pins, atlas.Key(row, c.NodePin))),
                Maps = Present(atlas.KeyList(row, c.NodeMaps).Select(MapArea).Where(id => id.Length > 0).Distinct().ToList()),
                Biomes = Present(atlas.KeyList(row, c.NodeBiomes).Select(biome => IdOf(biomes, biome)).Where(id => id.Length > 0).ToList()),
                Objective = Present(IdOf(objectives, atlas.Key(row, c.NodeObjective))),
                Blocked = Present(ClientId(atlas.Key(row, c.NodeBlocked))),
                Hint = Present(ClientId(atlas.Key(row, c.NodeHint))),
            };
        }

        // Which towers reach a node: a tower's tablets work on the maps in its reach.
        var towers = tables["endgamemapatlastowers"];
        string AtlasPassive(long atlasRow) => atlasRow >= 0 && atlasRow < atlas.RowCount ? IdOf(passives, atlas.Key((int)atlasRow, c.NodePassive)) : string.Empty;
        for (var row = 0; row < towers.RowCount; row++)
        {
            var tower = towers.KeyList(row, c.TowerSelf).Select(AtlasPassive).FirstOrDefault(id => id.Length > 0 && nodeOut.ContainsKey(id));
            if (tower == null)
            {
                continue;
            }

            foreach (var reached in towers.KeyList(row, c.TowerReach).Select(AtlasPassive).Where(id => id != tower && nodeOut.ContainsKey(id)))
            {
                nodeOut[reached] = nodeOut[reached] with { Towers = (nodeOut[reached].Towers ?? new List<string>()).Append(tower).Distinct().ToList() };
            }
        }

        // Each endgame area's pin (a map, a tower, a citadel...), and its name.
        var mapOut = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < maps.RowCount; row++)
        {
            if (MapArea(row) is { Length: > 0 } area)
            {
                mapOut[area] = IdOf(pins, maps.Key(row, c.MapPin));
            }
        }

        var text = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (code, folder) in AreaExtractor.Languages)
        {
            DatTable? Local(string name) => folder == null ? tables[name] : DatTable.OpenTranslated(index, name, folder) is { } local &&
                                                                              local.RowCount == tables[name].RowCount && local.RowSize == tables[name].RowSize ? local : null;
            var words = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Put(string key, string? value)
            {
                if (GameMarkup.Plain(value) is { Length: > 0 } plain)
                {
                    words[key] = plain;
                }
            }

            if (Local("endgamemapobjectives") is { } localObjectives)
            {
                for (var row = 0; row < localObjectives.RowCount; row++)
                {
                    if (objectives.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("objective/" + id, localObjectives.StringOrNull(row, c.ObjectiveText));
                        Put("done/" + id, localObjectives.StringOrNull(row, c.ObjectiveDone));
                    }
                }
            }

            if (Local("endgamemapcontent") is { } localContents)
            {
                for (var row = 0; row < localContents.RowCount; row++)
                {
                    if (contents.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("content/" + id, localContents.StringOrNull(row, c.ContentName));
                        Put("contentText/" + id, localContents.StringOrNull(row, c.ContentText));
                    }
                }
            }

            if (Local("endgamemapbiomes") is { } localBiomes)
            {
                for (var row = 0; row < localBiomes.RowCount; row++)
                {
                    if (biomes.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("biome/" + id, localBiomes.StringOrNull(row, c.BiomeName));
                    }
                }
            }

            if (Local("keywordpopups") is { } localKeywords)
            {
                foreach (var (id, row) in keywordRows)
                {
                    Put("term/" + id, localKeywords.StringOrNull(row, c.KeywordTerm));
                    Put("help/" + id, localKeywords.StringOrNull(row, c.KeywordHelp));
                }
            }

            if (Local("clientstrings2") is { } localClients)
            {
                foreach (var (id, row) in clientRows)
                {
                    Put("client/" + id, localClients.StringOrNull(row, c.ClientText));
                }
            }

            if (Local("worldareas") is { } localAreas)
            {
                foreach (var (id, row) in areaRows)
                {
                    Put("area/" + id, localAreas.StringOrNull(row, c.AreaName));
                }
            }

            if (Local("passiveskills") is { } localPassives)
            {
                foreach (var row in namedPassives)
                {
                    var name = localPassives.StringOrNull(row, c.PassiveName);
                    var english = passives.StringOrNull(row, c.PassiveName) ?? string.Empty;
                    if (!english.StartsWith(UntranslatedMark, StringComparison.Ordinal) && passives.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("passive/" + id, name);
                    }
                }
            }

            Say.Out.WriteLine($"  {code}: {words.Count} texts");
            if (words.Count > 0)
            {
                text[code] = words;
            }
        }

        var result = new
        {
            objectives = objectiveOut,
            contents = contentOut,
            icons = iconOut,
            maps = mapOut,
            nodes = nodeOut,
            text,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
        }));
        Say.Out.WriteLine($"Wrote {outFile}: {objectiveOut.Count} objectives, {contentOut.Count} contents, {mapOut.Count} areas, {nodeOut.Count} graph nodes");
        return 0;
    }

    private static string IdOf(DatTable table, long row) =>
        row >= 0 && row < table.RowCount ? table.StringOrNull((int)row, IdOffset) ?? string.Empty : string.Empty;

    private sealed record ObjectiveEntry(string? Icon, string? Keyword);

    private sealed record ContentEntry(string? Objective);

    /// <summary>An Atlas content icon's words: the text keys of its name and of what it does.</summary>
    private sealed record IconEntry(string Name, string? Text);

    /// <summary>Null for nothing, so the file leaves the field out.</summary>
    private static string? Present(string value) => value.Length > 0 ? value : null;

    private static List<string>? Present(List<string> values) => values.Count > 0 ? values : null;

    /// <param name="Graph">The graph file it is drawn in (its name without extension).</param>
    /// <param name="X">Its group's centre in the graph's units (whole units are plenty to tell directions).</param>
    private sealed record NodeEntry(string Graph, int X, int Y, int Orbit, int Slot, List<string>? Links)
    {
        public string? Pin { get; init; }

        public List<string>? Maps { get; init; }

        public List<string>? Biomes { get; init; }

        public string? Objective { get; init; }

        public string? Blocked { get; init; }

        public string? Hint { get; init; }

        /// <summary>The towers whose reach holds this node.</summary>
        public List<string>? Towers { get; init; }
    }
}
