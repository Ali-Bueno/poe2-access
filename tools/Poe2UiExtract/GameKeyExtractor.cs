using Index = LibBundle3.Index;

/// <summary>
///     `game-keys` command: the Ids of the tables a player meets, a table and a key a line, so the difference between two
///     extractions is what a patch added (`GameNews` in the plugin; docs/compatibility.md). The file's own git history shows
///     the same difference to whoever keeps the mod.
/// </summary>
internal static class GameKeyExtractor
{
    private const string Skipped = "game keys";
    private const string ScreenTable = "screens";
    private const string ScreenFolder = "metadata/ui/";
    private const string ScreenExtension = ".ui";

    // Every one holds its Id as a string at the row's start (opened one by one, 2026-11-06).
    private static readonly string[] Tables =
    {
        "buffdefinitions", "worldareas", "monstervarieties", "activeskills", "minimapicons", "chests", "shrines",
        "npcs", "npctalkcategory", "groundeffecttypes", "questrewardtype", "keywordpopups",
    };

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var lines = new List<string>
        {
            "# What the game holds, a table and an Id a line (tools/Poe2UiExtract game-keys), regenerated after patches.",
        };
        foreach (var name in Tables)
        {
            var table = DatTable.Open(index, name);
            if (table == null)
            {
                Console.WriteLine($"Table {name} missing: {Skipped} left out.");
                return 1;
            }

            for (var row = 0; row < table.RowCount; row++)
            {
                if (table.String(row, 0) is { Length: > 0 } key)
                {
                    lines.Add($"{name}\t{key}");
                }
            }
        }

        lines.AddRange(index.Files.Values
            .Select(file => file.Path)
            .Where(path => path != null && path.StartsWith(ScreenFolder, StringComparison.OrdinalIgnoreCase) && path.EndsWith(ScreenExtension, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(path => $"{ScreenTable}\t{path}"));
        if (lines.Count <= 1)
        {
            Console.WriteLine($"No keys read: {Skipped} left out.");
            return 1;
        }

        File.WriteAllLines(outFile, lines);
        Console.WriteLine($"{lines.Count - 1} keys written to {outFile}");
        return 0;
    }
}
