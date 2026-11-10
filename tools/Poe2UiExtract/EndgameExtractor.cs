using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `endgame` command: the endgame's words and layout from the game's tables, for the Atlas and the maps
///     (docs/research/endgame.md): each map objective with the minimap icon of what it asks for and the keyword naming it,
///     each map content, biome and endgame area, every node of the skill graphs other than the main tree (the Atlas map's
///     layout, the Atlas passive tree, the league trees) with its position, links and, for an Atlas map node, its pin,
///     maps, biomes and messages. Writes { objectives, contents, nodes, text: { language: { key: text } } }.
///     Column offsets: the community dat-schema (poe2/_Core.gql of 2026-11-05) on this install's row sizes (2026-11-08) are
///     where each column is looked for first; every column is found again by what it holds (<see cref="DatColumnSearch"/>),
///     and one not found fails the step, which keeps the previous catalog. Read-only on the game.
/// </summary>
internal static class EndgameExtractor
{
    private const int IdOffset = 0x0;

    // Measured offsets (0.5.5f), where each column is looked for first. EndgameMapObjectives
    private const int ObjectiveTextOffset = 0x8;
    private const int ObjectiveDoneOffset = 0x10;
    private const int ObjectiveArtOffset = 0x18;
    private const int ObjectiveIconOffset = 0x20;

    // EndgameMapContent
    private const int ContentTextOffset = 0x28;
    private const int ContentNameOffset = 0x30;
    private const int ContentObjectiveOffset = 0x3C;
    private const int ContentVisualOffset = 0x4C;

    // EndgameMapContentVisualIdentity: the icon an Atlas node shows for a content
    private const int VisualIconOffset = 0x8;

    // EndgameMapNodeStats: what a node's stat adds to it (a Delirium fog bank), with its own icon and words
    private const int NodeStatVisualOffset = 0x44;
    private const int NodeStatNameOffset = 0x54;
    private const int NodeStatTextOffset = 0x64;

    // EndgameMapBiomes
    private const int BiomeNameOffset = 0x28;

    // EndgameMaps
    private const int MapAreaOffset = 0x0;
    private const int MapPinOffset = 0x3C;

    // EndgameMapAtlas: one row per Atlas node, keyed by its PassiveSkills row
    private const int NodePassiveOffset = 0x0;
    private const int NodeMapsOffset = 0x10;
    private const int NodeBiomesOffset = 0x24;
    private const int NodePinOffset = 0x68;
    private const int NodeBlockedOffset = 0x7C;
    private const int NodeObjectiveOffset = 0xA7;
    private const int NodeHintOffset = 0x10D;

    // EndgameMapAtlasTowers: the Atlas nodes a tower reaches, and the tower's own node
    private const int TowerReachOffset = 0x14;
    private const int TowerSelfOffset = 0x34;

    // KeywordPopups, ClientStrings2, WorldAreas
    private const int KeywordTermOffset = 0x8;
    private const int KeywordHelpOffset = 0x10;
    private const int ClientTextOffset = 0x8;
    private const int AreaNameOffset = 0x8;

    // PassiveSkills (unaligned, as dat-columns finds them)
    private const int PassiveGraphIdOffset = 0x30;
    private const int PassiveNameOffset = 0x32;

    /// <summary>The main tree, read from game memory by the plugin (GameLayout.PassiveTree.GraphFileName).</summary>
    private const string MainGraph = "metadata/passiveskillgraph.psg";

    /// <summary>Nodes the game names "do not translate" (the Atlas map's layout nodes) have no name worth saying.</summary>
    private const string UntranslatedMark = "[DNT]";

    /// <summary>Rows the value checks look at while the data has them: an objective, a content, an endgame area and an Atlas node of 0.5.5f.</summary>
    private const string KnownObjective = "Breach";

    private const string KnownContent = "Abyss";

    private const string KnownArea = "Abyss_Pinnacle";

    private const string KnownNode = "Atlas4thReactor";

    private const string KnownKeyword = "ContainsBreach";

    private const string KnownClient = "EndgameMapObjectiveClickPrecursorBeacon";

    private const string ArtPrefix = "Art/";

    /// <summary>Every graph id of a graph file must lead to a PassiveSkills row; fewer is a moved graph id column.</summary>
    private const double ResolvedShare = 0.99;

    public static int Run(Index index, string outFile)
    {
        var tables = new Dictionary<string, DatTable>(StringComparer.Ordinal);
        foreach (var name in new[] { "endgamemapobjectives", "endgamemapcontent", "endgamemapbiomes", "endgamemaps", "endgamemappins", "endgamemapatlas", "endgamemapcontentvisualidentity",
                     "endgamemapnodestats", "endgamemapatlastowers", "keywordpopups", "clientstrings2", "minimapicons", "passiveskills", "worldareas" })
        {
            if (DatTable.Open(index, name) is not { } table)
            {
                Say.Out.WriteLine($"  no {name}");
                return 1;
            }

            tables[name] = table;
        }

        if (Columns.Find(tables) is not { } c)
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


    /// <summary>
    ///     The columns read, each found by its type near where it was measured and, on a row the data still has, by its
    ///     value (an objective's icon named like it, a content's objective, an endgame area's pin, an Atlas node's map).
    /// </summary>
    private sealed class Columns
    {
        public int ObjectiveText, ObjectiveDone, ObjectiveArt, ObjectiveIcon;
        public int ContentText, ContentName, ContentObjective, ContentVisual;
        public int VisualIcon;
        public int NodeStatVisual, NodeStatName, NodeStatText;
        public int BiomeName;
        public int MapArea, MapPin;
        public int NodePassive, NodeMaps, NodeBiomes, NodePin, NodeBlocked, NodeObjective, NodeHint;
        public int TowerReach, TowerSelf;
        public int KeywordTerm, KeywordHelp;
        public int ClientText, AreaName;
        public int PassiveGraphId, PassiveName;

        public static Columns? Find(Dictionary<string, DatTable> tables)
        {
            var search = new DatColumnSearch();
            var objectives = tables["endgamemapobjectives"];
            var contents = tables["endgamemapcontent"];
            var visuals = tables["endgamemapcontentvisualidentity"];
            var nodeStats = tables["endgamemapnodestats"];
            var biomes = tables["endgamemapbiomes"];
            var maps = tables["endgamemaps"];
            var pins = tables["endgamemappins"];
            var atlas = tables["endgamemapatlas"];
            var towers = tables["endgamemapatlastowers"];
            var keywords = tables["keywordpopups"];
            var clients = tables["clientstrings2"];
            var icons = tables["minimapicons"];
            var passives = tables["passiveskills"];
            var areas = tables["worldareas"];
            static bool Sentence(string text) => text.Contains(' ', StringComparison.Ordinal);
            static bool Art(string text) => text.StartsWith(ArtPrefix, StringComparison.Ordinal);
            static bool NotArt(string text) => !Art(text);
            static bool Filled(string text) => text.Length > 0;
            Func<long, bool> Named(DatTable target, string word) => key => IdOf(target, key).Contains(word, StringComparison.Ordinal);
            Func<long, bool> Is(DatTable target, string id) => key => IdOf(target, key) == id;

            var c = new Columns();
            var objective = DatColumnSearch.RowOf(objectives, KnownObjective);
            c.ObjectiveText = search.Text(objectives, "EndgameMapObjectives.ObjectiveText", ObjectiveTextOffset, NotArt, known: (objective, text => text.Contains(KnownKeyword, StringComparison.Ordinal)));
            c.ObjectiveDone = search.Text(objectives, "EndgameMapObjectives.CompletionText", ObjectiveDoneOffset, NotArt, c.ObjectiveText, (objective, Sentence));
            c.ObjectiveArt = search.Text(objectives, "EndgameMapObjectives.ContentIcon", ObjectiveArtOffset, Art, c.ObjectiveDone);
            c.ObjectiveIcon = search.Key(objectives, "EndgameMapObjectives.MiniMapIcon", ObjectiveIconOffset, icons, c.ObjectiveArt, (objective, Named(icons, KnownObjective)));

            var content = DatColumnSearch.RowOf(contents, KnownContent);
            c.ContentText = search.Text(contents, "EndgameMapContent.Description", ContentTextOffset, NotArt, known: (content, Sentence));
            c.ContentName = search.Text(contents, "EndgameMapContent.Name", ContentNameOffset, NotArt, c.ContentText, (content, Filled));
            c.ContentObjective = search.Key(contents, "EndgameMapContent.MapObjective", ContentObjectiveOffset, objectives, c.ContentName, (content, Is(objectives, KnownContent)));
            c.ContentVisual = search.Key(contents, "EndgameMapContent.VisualIdentity", ContentVisualOffset, visuals, c.ContentObjective);
            c.VisualIcon = search.Text(visuals, "EndgameMapContentVisualIdentity.AtlasIcon", VisualIconOffset, Art);

            c.NodeStatVisual = search.Key(nodeStats, "EndgameMapNodeStats.VisualIdentity", NodeStatVisualOffset, visuals);
            c.NodeStatName = search.Key(nodeStats, "EndgameMapNodeStats.Name", NodeStatNameOffset, clients, c.NodeStatVisual);
            c.NodeStatText = search.Key(nodeStats, "EndgameMapNodeStats.Description", NodeStatTextOffset, clients, c.NodeStatName);
            c.BiomeName = search.Text(biomes, "EndgameMapBiomes.Name", BiomeNameOffset, NotArt);

            // The known area's map row and the known node's Atlas row, looked up through the measured key columns (-1 when they moved: no value check then).
            var area = DatColumnSearch.RowOf(areas, KnownArea);
            var map = area >= 0 ? Enumerable.Range(0, maps.RowCount).FirstOrDefault(row => maps.Int64(row, MapAreaOffset) == area, -1) : -1;
            c.MapArea = search.Key(maps, "EndgameMaps.WorldArea", MapAreaOffset, areas);
            c.MapPin = search.Key(maps, "EndgameMaps.MapPin", MapPinOffset, pins, c.MapArea, (map, Named(pins, "Hub")));

            var passive = DatColumnSearch.RowOf(passives, KnownNode);
            var node = passive >= 0 ? Enumerable.Range(0, atlas.RowCount).FirstOrDefault(row => atlas.Int64(row, NodePassiveOffset) == passive, -1) : -1;
            c.NodePassive = search.Key(atlas, "EndgameMapAtlas.Passives", NodePassiveOffset, passives);
            c.NodeMaps = search.KeyList(atlas, "EndgameMapAtlas.Maps", NodeMapsOffset, maps, c.NodePassive, (node, list => list.Count > 0));
            c.NodeBiomes = search.KeyList(atlas, "EndgameMapAtlas.Biomes", NodeBiomesOffset, biomes, c.NodeMaps, (node, list => list.Count > 0));
            c.NodePin = search.Key(atlas, "EndgameMapAtlas.MapPin", NodePinOffset, pins, c.NodeBiomes, (node, key => key >= 0));
            c.NodeBlocked = search.Key(atlas, "EndgameMapAtlas.BlockedMessage", NodeBlockedOffset, clients, c.NodePin);
            c.NodeObjective = search.Key(atlas, "EndgameMapAtlas.MapObjective", NodeObjectiveOffset, objectives, c.NodeBlocked);
            c.NodeHint = search.Key(atlas, "EndgameMapAtlas.Hint", NodeHintOffset, clients, c.NodeObjective);

            c.TowerReach = search.KeyList(towers, "EndgameMapAtlasTowers.AtlasNodes", TowerReachOffset, atlas);
            c.TowerSelf = search.KeyList(towers, "EndgameMapAtlasTowers.Tower", TowerSelfOffset, atlas, c.TowerReach);

            var keyword = DatColumnSearch.RowOf(keywords, KnownKeyword);
            c.KeywordTerm = search.Text(keywords, "KeywordPopups.Term", KeywordTermOffset, NotArt, known: (keyword, text => Filled(text) && text != KnownKeyword));
            c.KeywordHelp = search.Text(keywords, "KeywordPopups.Definition", KeywordHelpOffset, NotArt, c.KeywordTerm, (keyword, Sentence));
            c.ClientText = search.Text(clients, "ClientStrings2.Text", ClientTextOffset, known: (DatColumnSearch.RowOf(clients, KnownClient), Sentence));
            c.AreaName = search.Text(areas, "WorldAreas.Name", AreaNameOffset, NotArt, known: (area, Sentence));
            c.PassiveGraphId = search.UniqueUInt16(passives, "PassiveSkills.GraphId", PassiveGraphIdOffset);
            c.PassiveName = search.Text(passives, "PassiveSkills.Name", PassiveNameOffset, NotArt);
            return search.Report("endgame") ? c : null;
        }
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
