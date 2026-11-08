using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `currency-art` command: the item a currency icon shows, { language: { art file lower-case: name } }, for prices
///     ("Art/2DItems/Currency/CurrencyIdentification.dds" is the Scroll of Wisdom). BaseItemTypes Id +0x0 and Name
///     (found by translation) lead through its ItemVisualIdentity key column to that table's DDSFile (+0x8, dat-strings
///     0.5.5e). An icon shared by several items stays unnamed. Read-only on the game.
/// </summary>
internal static class CurrencyArtExtractor
{
    private const int IdOffset = 0x0;
    private const int NameOffset = 0x20;
    private const int DdsOffset = 0x8;
    private const string DdsPrefix = "Art/";
    private const string CurrencyPrefix = "Metadata/Items/Currency/";
    private const string ArtPrefix = "Art/2DItems/Currency/";
    private const double MinFound = 0.9;
    private const double MinDistinct = 0.3;

    public static int Run(Index index, string outFile)
    {
        var items = DatTable.Open(index, "baseitemtypes");
        var visuals = DatTable.Open(index, "itemvisualidentity");
        var search = new DatColumnSearch();
        var ddsColumn = visuals == null ? -1 : search.Text(visuals, "ItemVisualIdentity.DDSFile", DdsOffset, text => text.StartsWith(DdsPrefix, StringComparison.OrdinalIgnoreCase));
        if (visuals != null && !search.Report("currency-art"))
        {
            return 1;
        }

        var nameColumn = DatColumnExtractor.NameColumn(index, "baseitemtypes") ?? NameOffset;
        if (items == null || visuals == null || VisualColumn(items, visuals, ddsColumn) is not { } visualColumn)
        {
            Say.Out.WriteLine("  the ItemVisualIdentity column of BaseItemTypes was not found (check with dat-strings)");
            return 1;
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.LanguageCodes.Select(pair => (Code: pair.Value, Folder: (string?)pair.Key)).Prepend(("en", null));
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? items : DatTable.OpenTranslated(index, "baseitemtypes", folder);
            if (table == null)
            {
                continue;
            }

            var byArt = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var row = 0; row < table.RowCount; row++)
            {
                var name = table.StringOrNull(row, nameColumn)?.Trim();
                if (string.IsNullOrEmpty(name) || ArtOf(items, visuals, row, visualColumn, ddsColumn) is not { } art)
                {
                    continue;
                }

                byArt[art] = byArt.TryGetValue(art, out var other) && other != name ? null : name;
            }

            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (art, name) in byArt)
            {
                if (name != null)
                {
                    names[art.ToLowerInvariant()] = name;
                }
            }

            Say.Out.WriteLine($"  {code}: {names.Count} currency icons ({byArt.Count(pair => pair.Value == null)} shared by several items)");
            result[code] = names;
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

    private static string? ArtOf(DatTable items, DatTable visuals, int row, int column, int ddsColumn)
    {
        if (items.StringOrNull(row, IdOffset) is not { } id || !id.StartsWith(CurrencyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var key = items.Key(row, column);
        var art = key >= 0 && key < visuals.RowCount ? visuals.StringOrNull((int)key, ddsColumn) : null;
        return art != null && art.StartsWith(ArtPrefix, StringComparison.OrdinalIgnoreCase) ? art : null;
    }

    /// <summary>
    ///     The key column whose rows, for the currency items, lead to a currency icon file, each item mostly to a file of its
    ///     own (a column every row shares, an item class, leads to one file).
    /// </summary>
    private static int? VisualColumn(DatTable items, DatTable visuals, int ddsColumn)
    {
        var rows = Enumerable.Range(0, items.RowCount)
            .Where(row => items.StringOrNull(row, IdOffset) is { } id && id.StartsWith(CurrencyPrefix, StringComparison.Ordinal)).ToList();
        int? best = null;
        var bestFiles = 0;
        for (var offset = 1; offset + 8 <= items.RowSize && rows.Count > 0; offset++)
        {
            if (!items.IsKeyColumn(offset, visuals.RowCount))
            {
                continue;
            }

            var arts = rows.Select(row => ArtOf(items, visuals, row, offset, ddsColumn)).Where(art => art != null).ToList();
            var files = arts.Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (arts.Count >= rows.Count * MinFound && files > bestFiles)
            {
                (best, bestFiles) = (offset, files);
            }
        }

        return bestFiles >= rows.Count * MinDistinct ? best : null;
    }
}
