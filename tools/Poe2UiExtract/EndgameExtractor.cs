using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `endgame` command: the endgame's words and layout from the game's tables, for the Atlas and the maps
///     (docs/research/endgame.md): each map objective with the minimap icon of what it asks for and the keyword naming it,
///     each map content, biome and endgame area, every node of the skill graphs other than the main tree (the Atlas map's
///     layout, the Atlas passive tree, the league trees) with its position, links and, for an Atlas map node, its pin,
///     maps, biomes and messages. Writes { objectives, contents, nodes, text: { language: { key: text } } }.
///     Column offsets: the community dat-schema (poe2/_Core.gql of 2026-11-05) on this install's row sizes (2026-11-08);
///     each column is checked by what it holds, and a column that moved fails the step. Read-only on the game.
/// </summary>
internal static class EndgameExtractor
{
    private const int IdOffset = 0x0;

    /// <summary>A dat file's null foreign key: all 0xFE bytes.</summary>
    private const long NullKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

    // EndgameMapObjectives
    private const int ObjectiveTextOffset = 0x8;
    private const int ObjectiveDoneOffset = 0x10;
    private const int ObjectiveIconOffset = 0x20;

    // EndgameMapContent
    private const int ContentTextOffset = 0x28;
    private const int ContentNameOffset = 0x30;
    private const int ContentObjectiveOffset = 0x3C;

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

    /// <summary>A checked column must hold what it should in at least this share of the rows that hold anything there.</summary>
    private const double CheckedShare = 0.9;

    /// <summary>Every graph id of a graph file must lead to a PassiveSkills row; fewer is a moved graph id column.</summary>
    private const double ResolvedShare = 0.99;

    public static int Run(Index index, string outFile)
    {
        var tables = new Dictionary<string, DatTable>(StringComparer.Ordinal);
        foreach (var name in new[] { "endgamemapobjectives", "endgamemapcontent", "endgamemapbiomes", "endgamemaps", "endgamemappins", "endgamemapatlas", "keywordpopups", "clientstrings2", "minimapicons", "passiveskills", "worldareas" })
        {
            if (DatTable.Open(index, name) is not { } table)
            {
                Console.WriteLine($"  no {name}");
                return 1;
            }

            tables[name] = table;
        }

        if (Moved(tables) is { } moved)
        {
            Console.WriteLine($"  {moved} moved (check with dat-strings)");
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

            var icon = IdOf(icons, objectives.Key(row, ObjectiveIconOffset));
            var keyword = GameMarkup.FirstKeyword(objectives.StringOrNull(row, ObjectiveTextOffset) ?? string.Empty);
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
                contentOut[id] = new ContentEntry(Present(IdOf(objectives, contents.Key(row, ContentObjectiveOffset))));
            }
        }

        // Every skill graph but the main tree: their nodes by PassiveSkills id, with each node's links.
        var byGraphId = new Dictionary<int, int>();
        for (var row = 0; row < passives.RowCount; row++)
        {
            byGraphId.TryAdd(passives.UInt16(row, PassiveGraphIdOffset), row);
        }

        var nodeOut = new SortedDictionary<string, NodeEntry>(StringComparer.Ordinal);
        var namedPassives = new SortedSet<int>();
        foreach (var file in index.Files.Values.Where(file => file.Path != null && file.Path.EndsWith(".psg", StringComparison.OrdinalIgnoreCase) &&
                                                                     !string.Equals(file.Path, MainGraph, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            var graphName = Path.GetFileNameWithoutExtension(file.Path!);
            if (SkillGraphFile.Parse(file.Read().ToArray()) is not { } graph)
            {
                Console.WriteLine($"  {file.Path}: not a version {SkillGraphFile.KnownVersion} graph, left out");
                continue;
            }

            var resolved = graph.Nodes.Count(node => byGraphId.ContainsKey(node.Id));
            if (resolved < graph.Nodes.Count * ResolvedShare)
            {
                Console.WriteLine($"  {file.Path}: {resolved} of {graph.Nodes.Count} nodes found in PassiveSkills: the graph id column moved");
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

            Console.WriteLine($"  {file.Path}: {graph.Nodes.Count} nodes{(twice > 0 ? $", {twice} listed twice" : string.Empty)}");
        }

        // What each Atlas node holds: the pin it shows, the maps and biomes it can be, its objective and messages.
        var clientRows = new SortedDictionary<string, int>(StringComparer.Ordinal);
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
            var areaRow = mapRow >= 0 && mapRow < maps.RowCount ? maps.Key((int)mapRow, MapAreaOffset) : -1;
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
            if (IdOf(passives, atlas.Key(row, NodePassiveOffset)) is not { Length: > 0 } passive || !nodeOut.TryGetValue(passive, out var placed))
            {
                continue;
            }

            nodeOut[passive] = placed with
            {
                Pin = Present(IdOf(pins, atlas.Key(row, NodePinOffset))),
                Maps = Present(atlas.KeyList(row, NodeMapsOffset).Select(MapArea).Where(id => id.Length > 0).Distinct().ToList()),
                Biomes = Present(atlas.KeyList(row, NodeBiomesOffset).Select(biome => IdOf(biomes, biome)).Where(id => id.Length > 0).ToList()),
                Objective = Present(IdOf(objectives, atlas.Key(row, NodeObjectiveOffset))),
                Blocked = Present(ClientId(atlas.Key(row, NodeBlockedOffset))),
                Hint = Present(ClientId(atlas.Key(row, NodeHintOffset))),
            };
        }

        // Each endgame area's pin (a map, a tower, a citadel...), and its name.
        var mapOut = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < maps.RowCount; row++)
        {
            if (MapArea(row) is { Length: > 0 } area)
            {
                mapOut[area] = IdOf(pins, maps.Key(row, MapPinOffset));
            }
        }

        var text = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var (code, folder) in AreaExtractor.LanguageCodes.Select(pair => (Code: pair.Value, Folder: (string?)pair.Key)).Prepend(("en", null)))
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
                        Put("objective/" + id, localObjectives.StringOrNull(row, ObjectiveTextOffset));
                        Put("done/" + id, localObjectives.StringOrNull(row, ObjectiveDoneOffset));
                    }
                }
            }

            if (Local("endgamemapcontent") is { } localContents)
            {
                for (var row = 0; row < localContents.RowCount; row++)
                {
                    if (contents.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("content/" + id, localContents.StringOrNull(row, ContentNameOffset));
                        Put("contentText/" + id, localContents.StringOrNull(row, ContentTextOffset));
                    }
                }
            }

            if (Local("endgamemapbiomes") is { } localBiomes)
            {
                for (var row = 0; row < localBiomes.RowCount; row++)
                {
                    if (biomes.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("biome/" + id, localBiomes.StringOrNull(row, BiomeNameOffset));
                    }
                }
            }

            if (Local("keywordpopups") is { } localKeywords)
            {
                foreach (var (id, row) in keywordRows)
                {
                    Put("term/" + id, localKeywords.StringOrNull(row, KeywordTermOffset));
                    Put("help/" + id, localKeywords.StringOrNull(row, KeywordHelpOffset));
                }
            }

            if (Local("clientstrings2") is { } localClients)
            {
                foreach (var (id, row) in clientRows)
                {
                    Put("client/" + id, localClients.StringOrNull(row, ClientTextOffset));
                }
            }

            if (Local("worldareas") is { } localAreas)
            {
                foreach (var (id, row) in areaRows)
                {
                    Put("area/" + id, localAreas.StringOrNull(row, AreaNameOffset));
                }
            }

            if (Local("passiveskills") is { } localPassives)
            {
                foreach (var row in namedPassives)
                {
                    var name = localPassives.StringOrNull(row, PassiveNameOffset);
                    var english = passives.StringOrNull(row, PassiveNameOffset) ?? string.Empty;
                    if (!english.StartsWith(UntranslatedMark, StringComparison.Ordinal) && passives.StringOrNull(row, IdOffset) is { Length: > 0 } id)
                    {
                        Put("passive/" + id, name);
                    }
                }
            }

            Console.WriteLine($"  {code}: {words.Count} texts");
            if (words.Count > 0)
            {
                text[code] = words;
            }
        }

        var result = new
        {
            objectives = objectiveOut,
            contents = contentOut,
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
        Console.WriteLine($"Wrote {outFile}: {objectiveOut.Count} objectives, {contentOut.Count} contents, {mapOut.Count} areas, {nodeOut.Count} graph nodes");
        return 0;
    }


    /// <summary>The first column that does not hold what it should, or null when all do.</summary>
    private static string? Moved(Dictionary<string, DatTable> tables)
    {
        bool Strings(string table, int offset) => Share(tables[table], row => tables[table].StringOrNull(row, offset) != null) >= CheckedShare;
        // A key column holds null keys or rows of its table, and at least one of the latter.
        bool Keys(string table, int offset, string target) =>
            Share(tables[table], row => tables[table].Int64(row, offset) is var key && (key == NullKey || (key >= 0 && key < tables[target].RowCount))) >= CheckedShare &&
            Enumerable.Range(0, tables[table].RowCount).Any(row => tables[table].Int64(row, offset) is >= 0 and var key && key < tables[target].RowCount);
        var checks = new (string Name, Func<bool> Holds)[]
        {
            ("EndgameMapObjectives.ObjectiveText", () => Strings("endgamemapobjectives", ObjectiveTextOffset) && Strings("endgamemapobjectives", ObjectiveDoneOffset)),
            ("EndgameMapObjectives.MiniMapIcon", () => Keys("endgamemapobjectives", ObjectiveIconOffset, "minimapicons")),
            ("EndgameMapContent.Name", () => Strings("endgamemapcontent", ContentNameOffset) && Strings("endgamemapcontent", ContentTextOffset)),
            ("EndgameMapContent.MapObjective", () => Keys("endgamemapcontent", ContentObjectiveOffset, "endgamemapobjectives")),
            ("EndgameMapBiomes.Name", () => Strings("endgamemapbiomes", BiomeNameOffset)),
            ("EndgameMaps.WorldArea", () => Keys("endgamemaps", MapAreaOffset, "worldareas") && Keys("endgamemaps", MapPinOffset, "endgamemappins")),
            ("EndgameMapAtlas.Passives", () => Keys("endgamemapatlas", NodePassiveOffset, "passiveskills") && Keys("endgamemapatlas", NodePinOffset, "endgamemappins")),
            ("EndgameMapAtlas.BlockedMessage", () => Keys("endgamemapatlas", NodeBlockedOffset, "clientstrings2") && Keys("endgamemapatlas", NodeHintOffset, "clientstrings2")),
            ("EndgameMapAtlas.MapObjective", () => Keys("endgamemapatlas", NodeObjectiveOffset, "endgamemapobjectives")),
            ("KeywordPopups.Term", () => Strings("keywordpopups", KeywordTermOffset)),
            ("ClientStrings2.Text", () => Strings("clientstrings2", ClientTextOffset)),
            ("PassiveSkills.Name", () => Strings("passiveskills", PassiveNameOffset)),
            ("WorldAreas.Name", () => Strings("worldareas", AreaNameOffset)),
        };
        return checks.FirstOrDefault(check => !check.Holds()).Name;
    }

    private static double Share(DatTable table, Func<int, bool> holds) =>
        table.RowCount == 0 ? 0 : Enumerable.Range(0, table.RowCount).Count(holds) / (double)table.RowCount;

    private static string IdOf(DatTable table, long row) =>
        row >= 0 && row < table.RowCount ? table.StringOrNull((int)row, IdOffset) ?? string.Empty : string.Empty;

    private sealed record ObjectiveEntry(string? Icon, string? Keyword);

    private sealed record ContentEntry(string? Objective);

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
    }
}
