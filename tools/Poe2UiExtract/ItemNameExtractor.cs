using Index = LibBundle3.Index;

/// <summary>
///     `item-names` command: the names of currency items in every language, { language: { idTail: name } }, for the
///     inline item art of legends ("Identificar &lt;&lt;itemvisual:CurrencyIdentification&gt;&gt; x 1" costs a Scroll of
///     Wisdom). BaseItemTypes column +0x0 = Id (the metadata path, its last segment the art's id), +0x20 = Name
///     (dat-strings, 0.5.5e). Read-only on the game.
/// </summary>
internal static class ItemNameExtractor
{
    private const int IdOffset = 0x0;
    private const string CurrencyPrefix = "Metadata/Items/Currency/";

    public static int Run(string gameDir, string outFile)
    {
        using var index = Extractor.OpenLoose(gameDir);
        if (DatColumnExtractor.NameColumn(index, NameAnchor.Items) is not { } nameColumn)
        {
            return 1;
        }

        var result = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
        var languages = AreaExtractor.Languages;
        foreach (var (code, folder) in languages)
        {
            var table = folder == null ? DatTable.Open(index, "baseitemtypes") : DatTable.OpenTranslated(index, "baseitemtypes", folder);
            if (table == null)
            {
                Say.Out.WriteLine($"  {code}: no BaseItemTypes");
                continue;
            }

            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            for (var row = 0; row < table.RowCount; row++)
            {
                var id = table.StringOrNull(row, IdOffset);
                var name = table.StringOrNull(row, nameColumn)?.Trim();
                if (id != null && id.StartsWith(CurrencyPrefix, StringComparison.Ordinal) && !string.IsNullOrEmpty(name))
                {
                    names[id[CurrencyPrefix.Length..]] = name;
                }
            }

            // A moved column reads metadata paths or nothing: the offsets need updating from dat-strings.
            if (names.Count == 0 || names.Values.Any(name => name.StartsWith("Metadata/", StringComparison.Ordinal)))
            {
                Say.Out.WriteLine($"  {code}: the Id/Name columns moved (check with dat-strings)");
                return 1;
            }

            Say.Out.WriteLine($"  {code}: {names.Count} currency items");
            result[code] = names;
        }

        AreaExtractor.WriteJson(outFile, result);
        Say.Out.WriteLine($"Wrote {outFile}: {string.Join(", ", result.Keys)}");
        return 0;
    }
}
