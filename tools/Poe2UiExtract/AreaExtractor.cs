using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `areas` command: reads WorldAreas.datc64 (English plus every localized copy under a language folder) from the
///     game's Bundles2 and writes { language: { areaId: name } }. Column 0 = Id, column 1 = Name (each an 8-byte
///     offset into the variable section, UTF-16LE). Read-only on the game.
/// </summary>
internal static class AreaExtractor
{
    // WorldAreas: Id (+0x0, read in place), Name (measured +0x8, where its search starts); G1_1 is the first area of the campaign.
    private const int NameOffset = 0x8;
    private const string KnownArea = "G1_1";

    internal static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spanish"] = "es", ["French"] = "fr", ["German"] = "de", ["Portuguese"] = "pt", ["Russian"] = "ru",
        ["Thai"] = "th", ["Japanese"] = "ja", ["Korean"] = "ko", ["Traditional Chinese"] = "zh-TW",
        ["Simplified Chinese"] = "zh-CN",
    };

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var files = index.Files.Values
            .Where(f => f.Path != null && f.Path.EndsWith("/worldareas.datc64", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        foreach (var f in files.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            var parts = f.Path!.Split('/');
            // data/[balance/]<Language>/worldareas.datc64 ; no language folder = English.
            var folder = parts.Length >= 2 ? parts[^2] : "";
            var isBalanceRoot = folder.Equals("balance", StringComparison.OrdinalIgnoreCase)
                                || folder.Equals("data", StringComparison.OrdinalIgnoreCase);
            string code;
            if (isBalanceRoot)
            {
                code = "en";
            }
            else if (!LanguageCodes.TryGetValue(folder, out code!))
            {
                Say.Out.WriteLine($"  skip unknown folder: {f.Path}");
                continue;
            }

            var table = DatTable.FromBytes(f.Read().ToArray());
            var search = new DatColumnSearch();
            var nameColumn = search.Text(table, "WorldAreas.Name", NameOffset, known: (DatColumnSearch.RowOf(table, KnownArea), text => text.Length > 0));
            if (!search.Report("areas"))
            {
                return 1;
            }

            var names = Parse(table, nameColumn);
            Say.Out.WriteLine($"  {f.Path} -> {code}: {names.Count} areas");
            result[code] = names;
        }

        if (!result.ContainsKey("en"))
        {
            Say.Out.WriteLine("English WorldAreas.datc64 not found.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Say.Out.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }

    private static SortedDictionary<string, string> Parse(DatTable table, int nameColumn)
    {
        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < table.RowCount; row++)
        {
            var id = table.String(row, 0);
            var name = table.String(row, nameColumn);
            if (id.Length > 0 && name.Length > 0)
            {
                names[id] = name.Trim();
            }
        }

        return names;
    }
}
