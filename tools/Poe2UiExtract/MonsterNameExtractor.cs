using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `monster-names` command: the radar table's boss labels as the game names them in every language,
///     { language: { radar label: name } }. MonsterVarieties column +0x110 = Name (dat-strings, 0.5.5e). A label (before its
///     bracketed note, after a "Boss - " prefix) matches the English name equal to it, else the shortest one it begins
///     ("Beira of the Rotten" is "Beira of the Rotten Pack", "Asinia" is "Asinia, the Praetor's Consort"); the row's
///     translated name is the label's. Read-only on the game.
/// </summary>
internal static class MonsterNameExtractor
{
    private const int MeasuredName = 0x110;
    private const int MinPrefixLabel = 4;
    private const string BossPrefix = "Boss - ";

    public static int Run(string gameDir, string radarFile, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        var wanted = RadarNames(radarFile);
        var nameColumn = DatColumnExtractor.NameColumn(index, "monstervarieties") ?? MeasuredName;
        var english = DatTable.Open(index, "monstervarieties");
        if (english == null)
        {
            Console.WriteLine("MonsterVarieties not found.");
            return 1;
        }

        var rowByLabel = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var label in wanted)
        {
            var best = -1;
            var bestName = string.Empty;
            for (var row = 0; row < english.RowCount; row++)
            {
                var name = english.StringOrNull(row, nameColumn)?.Trim();
                if (!string.IsNullOrEmpty(name) && Matches(label, name) && (best < 0 || Rank(label, name) < Rank(label, bestName)))
                {
                    best = row;
                    bestName = name;
                }
            }

            if (best >= 0)
            {
                rowByLabel[label] = best;
                Console.WriteLine($"  {label} -> {bestName}");
            }
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.LanguageCodes.Select(pair => (Code: pair.Value, Folder: (string?)pair.Key)).Prepend(("en", null));
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? english : DatTable.OpenTranslated(index, "monstervarieties", folder);
            if (table == null)
            {
                Console.WriteLine($"  {code}: no MonsterVarieties");
                continue;
            }

            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (label, row) in rowByLabel)
            {
                var name = row < table.RowCount ? table.StringOrNull(row, nameColumn)?.Trim() : null;
                if (!string.IsNullOrEmpty(name))
                {
                    names[label] = name;
                }
            }

            Console.WriteLine($"  {code}: {names.Count} of {rowByLabel.Count} names");
            result[code] = names;
        }

        foreach (var missing in wanted.Where(name => !rowByLabel.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal))
        {
            Console.WriteLine($"  unmatched: {missing}");
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

    private static bool Matches(string label, string name) =>
        name.Equals(label, StringComparison.OrdinalIgnoreCase)
        || (label.Length >= MinPrefixLabel && name.StartsWith(label, StringComparison.OrdinalIgnoreCase) && name.Length > label.Length && name[label.Length] is ',' or ' ');

    // An equal name wins, then the shortest one that begins with the label.
    private static int Rank(string label, string name) => name.Equals(label, StringComparison.OrdinalIgnoreCase) ? 0 : name.Length;

    /// <summary>The names the radar table's labels carry: without the note in brackets and the "Boss - " prefix.</summary>
    private static HashSet<string> RadarNames(string radarFile)
    {
        var tables = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(radarFile)) ?? new();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in tables.Values.SelectMany(tiles => tiles.Values))
        {
            var note = label.IndexOf(" (", StringComparison.Ordinal);
            var name = (note > 0 ? label[..note] : label).Trim();
            names.Add(name.StartsWith(BossPrefix, StringComparison.OrdinalIgnoreCase) ? name[BossPrefix.Length..].Trim() : name);
        }

        return names;
    }
}
