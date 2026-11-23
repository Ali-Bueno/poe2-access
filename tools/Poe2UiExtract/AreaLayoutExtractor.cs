using System.Text.RegularExpressions;
using LibBundle3.Records;
using Index = LibBundle3.Index;

/// <summary>
///     `area-layouts` command: how many layouts (level graphs) the game has for each campaign area, as { areaId: count }:
///     the distinct graph files of the Topologies that WorldAreas lists for it, each as many times as the graphs it draws
///     from a group of them (the Dreadnought's one graph holds one of four). One layout is a map that does not change.
/// </summary>
internal static partial class AreaLayoutExtractor
{
    /// <summary>The Trials' floors are put together from rooms anew each time, though their area has one graph.</summary>
    private static readonly string[] TrialAreas = { "G2_13", "G3_10" };

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        if (DatTable.Open(index, "worldareas") is not { RowCount: > 1 } areas || DatTable.Open(index, "topologies") is not { RowCount: > 1 } topologies)
        {
            Say.Out.WriteLine("area-layouts: worldareas or topologies missing");
            return 1;
        }

        int graphColumn, listColumn;
        using (DatTable.Probe())
        {
            graphColumn = Enumerable.Range(0, topologies.RowSize / 4).Select(column => column * 4)
                .FirstOrDefault(offset => topologies.IsStringColumn(offset, IsGraph), -1);
            // Three lists point into Topologies (0.5.5f: +0x60 an area's own, +0x2A0 its hideout's, +0x133 others): the area's
            // own is the one that names the most different graphs.
            listColumn = graphColumn < 0 ? -1 : Enumerable.Range(0, areas.RowSize - 16)
                .Where(offset => areas.IsKeyListColumn(offset, topologies.RowCount))
                .OrderByDescending(offset => Enumerable.Range(0, areas.RowCount).SelectMany(row => areas.KeyList(row, offset)).Distinct().Count())
                .Append(-1).First();
        }

        if (graphColumn < 0 || listColumn < 0)
        {
            Say.Out.WriteLine($"area-layouts: columns not recognised (graph {graphColumn}, list {listColumn})");
            return 1;
        }

        var files = BundleFiles.ByLowerPath(index);
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (var row = 0; row < areas.RowCount; row++)
        {
            var id = areas.String(row, 0);
            var graphs = areas.KeyList(row, listColumn).Where(key => key >= 0 && key < topologies.RowCount)
                .Select(key => topologies.String((int)key, graphColumn)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (IsCampaign(id) && graphs.Count > 0 && !TrialAreas.Contains(id))
            {
                counts[id] = graphs.Sum(graph => Layouts(files, graph));
            }
        }

        AreaExtractor.WriteJson(outFile, counts);
        Say.Out.WriteLine($"area-layouts: {counts.Count} areas (list +0x{listColumn:X}, graph +0x{graphColumn:X}), {counts.Values.Count(count => count == 1)} with one layout");
        return counts.Count > 0 ? 0 : 1;
    }

    /// <summary>The layouts one graph stands for: 1, times each group its "graph" nodes draw from (one naming a file is fixed).</summary>
    private static int Layouts(Dictionary<string, FileRecord> files, string graph)
    {
        if (!files.TryGetValue(graph.ToLowerInvariant(), out var file))
        {
            return 1;
        }

        var text = file.ReadText();
        var layouts = 1;
        foreach (var group in GraphNode().Matches(text).Select(match => match.Groups[1].Value).Where(name => !IsGraph(name)))
        {
            layouts *= Math.Max(2, GroupSize(files, text, group)); // a group of unknown size still varies
        }

        return layouts;
    }

    /// <summary>How many graphs the group holds in the file groups of the graph's master file; 0 when it is not found.</summary>
    private static int GroupSize(Dictionary<string, FileRecord> files, string graphText, string group)
    {
        if (MasterFile().Match(graphText) is not { Success: true } named || !files.TryGetValue(named.Groups[1].Value.ToLowerInvariant(), out var master) ||
            FileGroups().Match(master.ReadText()) is not { Success: true } groups)
        {
            return 0;
        }

        var name = groups.Groups[1].Value.ToLowerInvariant();
        var path = name.Contains('/') ? name : $"{named.Groups[1].Value.ToLowerInvariant()[..named.Groups[1].Value.LastIndexOf('/')]}/{name}";
        return files.TryGetValue(path, out var file)
            ? file.ReadText().Split('\n').Select(line => line.Trim()).SkipWhile(line => line != $"\"{group}\"").Skip(1).TakeWhile(IsGraph).Count()
            : 0;
    }

    [GeneratedRegex("\"graph\" \\S+ \\d+ \"([^\"]+)\"")]
    private static partial Regex GraphNode();

    [GeneratedRegex("MasterFile: \"([^\"]+)\"")]
    private static partial Regex MasterFile();

    [GeneratedRegex("FileGroups\\s+\"([^\"]+)\"")]
    private static partial Regex FileGroups();

    /// <summary>An act's or the interlude's area: "G1_2", "P3_1"; not its page of the world map.</summary>
    private static bool IsCampaign(string id) =>
        id.Length > 2 && (id[0] is 'G' or 'P') && char.IsAsciiDigit(id[1]) && id[2] == '_' && !id.EndsWith("_WorldMap", StringComparison.Ordinal);

    private static bool IsGraph(string text) =>
        text.EndsWith(".dgr", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".tgr", StringComparison.OrdinalIgnoreCase);
}
