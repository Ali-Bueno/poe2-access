using System.Text;
using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `areas` command: reads WorldAreas.datc64 (English plus every localized copy under a language folder) from the
///     game's Bundles2 and writes { language: { areaId: name } }. Column 0 = Id, column 1 = Name (each an 8-byte
///     offset into the variable section, UTF-16LE). Read-only on the game.
/// </summary>
internal static class AreaExtractor
{
    private static readonly byte[] Marker = { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB };

    internal static readonly Dictionary<string, string> LanguageCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spanish"] = "es", ["French"] = "fr", ["German"] = "de", ["Portuguese"] = "pt", ["Russian"] = "ru",
        ["Thai"] = "th", ["Japanese"] = "ja", ["Korean"] = "ko", ["Traditional Chinese"] = "zh-TW",
        ["Simplified Chinese"] = "zh-CN",
    };

    public static int Run(string gameDir, string outFile)
    {
        var indexPath = Path.Combine(gameDir, "Bundles2", "_.index.bin");
        using var index = SharedBundles.Open(indexPath);
        Console.WriteLine($"ParsePaths failed count: {index.ParsePaths()}");

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
                Console.WriteLine($"  skip unknown folder: {f.Path}");
                continue;
            }

            var names = Parse(f.Read().ToArray());
            Console.WriteLine($"  {f.Path} -> {code}: {names.Count} areas");
            result[code] = names;
        }

        if (!result.ContainsKey("en"))
        {
            Console.WriteLine("English WorldAreas.datc64 not found.");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }));
        Console.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }

    private static SortedDictionary<string, string> Parse(byte[] d)
    {
        int rows = BitConverter.ToInt32(d, 0);
        int marker = d.AsSpan(4).IndexOf(Marker) + 4;
        int rowSize = (marker - 4) / rows;
        int varStart = marker;
        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < rows; i++)
        {
            int row = 4 + (i * rowSize);
            var id = ReadString(d, varStart, BitConverter.ToInt64(d, row));
            var name = ReadString(d, varStart, BitConverter.ToInt64(d, row + 8));
            if (id.Length > 0 && name.Length > 0)
            {
                names[id] = name.Trim();
            }
        }

        return names;
    }

    private static string ReadString(byte[] d, int varStart, long offset)
    {
        int start = varStart + (int)offset;
        int end = start;
        while (end + 1 < d.Length && (d[end] != 0 || d[end + 1] != 0))
        {
            end += 2;
        }

        return Encoding.Unicode.GetString(d, start, end - start);
    }
}
