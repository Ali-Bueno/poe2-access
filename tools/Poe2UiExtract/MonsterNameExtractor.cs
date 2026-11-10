using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `monster-names` command: the radar table's labels that name a monster or an NPC, as the game names them in every
///     language, { language: { label piece: name } }. A label is cut into pieces at " - ", slashes, ampersands, commas
///     and brackets ("Boss - Captain Hartlin" gives "Captain Hartlin"). MonsterVarieties (Name at +0x110, 0.5.5e) and NPCs
///     (Name at +0x8) are searched for each piece: an equal name, else for a whole label the shortest monster it begins
///     ("Beira of the Rotten" is "Beira of the Rotten Pack", "Asinia" is "Asinia, the Praetor's Consort"); a piece of a
///     longer label only an equal name or one it begins before a comma ("Krillson, Master Fisherman"). The row's
///     translated name is the piece's. The name column is the one that finds most pieces, the detected one or the
///     measured one (the detection took a garbage column in 0.5.6). Read-only on the game.
/// </summary>
internal static class MonsterNameExtractor
{
    private const int MeasuredName = 0x110;
    private const int NpcMeasuredName = 0x8;
    private const int MinPrefixLabel = 4;
    private const string BossPrefix = "Boss - ";
    private static readonly char[] PieceSeparators = { '/', '&', ',', '(', ')' };

    public static int Run(string gameDir, string radarFiles, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        var wholes = RadarNames(radarFiles);
        var wanted = wholes.Concat(RadarPieces(radarFiles)).Distinct().ToList();
        var sources = new List<Source>();
        foreach (var (table, measured, prefix) in new[] { ("monstervarieties", MeasuredName, true), ("npcs", NpcMeasuredName, false) })
        {
            var english = DatTable.Open(index, table);
            if (english == null)
            {
                Say.Out.WriteLine($"{table} not found.");
                continue;
            }

            var column = BestColumn(index, english, table, measured, wanted);
            Say.Out.WriteLine($"  {table}: name column +0x{column:X}");
            sources.Add(new Source(table, english, column, prefix));
        }

        if (sources.Count == 0)
        {
            return 1;
        }

        var rowByLabel = new SortedDictionary<string, (Source Source, int Row)>(StringComparer.Ordinal);
        foreach (var label in wanted)
        {
            var whole = wholes.Contains(label);
            (Source Source, int Row)? best = null;
            var bestName = string.Empty;
            foreach (var source in sources)
            {
                for (var row = 0; row < source.English.RowCount; row++)
                {
                    var name = source.English.StringOrNull(row, source.Column)?.Trim();
                    if (!string.IsNullOrEmpty(name) && Matches(label, name, whole && source.Prefix) && (best == null || Rank(label, name) < Rank(label, bestName)))
                    {
                        best = (source, row);
                        bestName = name;
                    }
                }
            }

            if (best != null)
            {
                rowByLabel[label] = best.Value;
                Say.Out.WriteLine($"  {label} -> {bestName}");
            }
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var tables = sources.ToDictionary(
                source => source.Table,
                source => folder == null ? source.English : DatTable.OpenTranslated(index, source.Table, folder));
            if (tables.Values.Any(table => table == null))
            {
                Say.Out.WriteLine($"  {code}: a table is missing");
                continue;
            }

            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (label, found) in rowByLabel)
            {
                var table = tables[found.Source.Table]!;
                var name = found.Row < table.RowCount ? table.StringOrNull(found.Row, found.Source.Column)?.Trim() : null;
                if (!string.IsNullOrEmpty(name))
                {
                    names[label] = name;
                }
            }

            Say.Out.WriteLine($"  {code}: {names.Count} of {rowByLabel.Count} names");
            result[code] = names;
        }

        foreach (var missing in wanted.Where(name => !rowByLabel.ContainsKey(name)).OrderBy(name => name, StringComparer.Ordinal))
        {
            Say.Out.WriteLine($"  unmatched: {missing}");
        }

        AreaExtractor.WriteJson(outFile, result);
        Say.Out.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }

    private sealed record Source(string Table, DatTable English, int Column, bool Prefix);

    /// <summary>The detected name column or the measured one, whichever finds more of the wanted names.</summary>
    private static int BestColumn(Index index, DatTable english, string table, int measured, List<string> wanted)
    {
        var detected = DatColumnExtractor.NameColumn(index, table);
        int Hits(int column)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var row = 0; row < english.RowCount; row++)
            {
                if (english.StringOrNull(row, column)?.Trim() is { Length: > 0 } name)
                {
                    names.Add(name);
                }
            }

            return wanted.Count(names.Contains);
        }

        return detected is { } found && found != measured && Hits(found) > Hits(measured) ? found : measured;
    }

    private static bool Matches(string label, string name, bool prefixToo) =>
        name.Equals(label, StringComparison.OrdinalIgnoreCase)
        || (label.Length >= MinPrefixLabel && name.StartsWith(label, StringComparison.OrdinalIgnoreCase) && name.Length > label.Length
            && (name[label.Length] == ',' || (prefixToo && name[label.Length] == ' ')));

    // An equal name wins, then the shortest one that begins with the label.
    private static int Rank(string label, string name) => name.Equals(label, StringComparison.OrdinalIgnoreCase) ? 0 : name.Length;

    private static IEnumerable<string> RadarLabels(string radarFiles) =>
        radarFiles.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(file => JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(file)) ?? new())
            .SelectMany(tables => tables.Values.SelectMany(tiles => tiles.Values));

    /// <summary>The names the radar table's labels carry: without the note in brackets and the "Boss - " prefix.</summary>
    private static HashSet<string> RadarNames(string radarFiles)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in RadarLabels(radarFiles))
        {
            var note = label.IndexOf(" (", StringComparison.Ordinal);
            var name = (note > 0 ? label[..note] : label).Trim();
            names.Add(name.StartsWith(BossPrefix, StringComparison.OrdinalIgnoreCase) ? name[BossPrefix.Length..].Trim() : name);
        }

        return names;
    }

    /// <summary>Every piece of every label, cut at " - ", slashes, ampersands, commas and brackets.</summary>
    private static HashSet<string> RadarPieces(string radarFiles)
    {
        var pieces = new HashSet<string>(StringComparer.Ordinal);
        foreach (var label in RadarLabels(radarFiles))
        {
            foreach (var dashed in label.Split(" - ", StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (var piece in dashed.Split(PieceSeparators, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = piece.Trim();
                    if (trimmed.Length >= MinPrefixLabel)
                    {
                        pieces.Add(trimmed);
                    }
                }
            }
        }

        return pieces;
    }
}
