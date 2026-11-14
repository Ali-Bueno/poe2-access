using System.Text.Json.Serialization;
using Index = LibBundle3.Index;

/// <summary>
///     `shrines` command: every row of the Shrines table as { "shrines": [ { id, lesser, buff, seconds, names, descriptions } ] }.
///     `buff` is the BuffDefinitions Id of the buff the shrine gives the player: Shrines.Player_ShrineBuffsKey (+0xD) is a row
///     of BuffTemplates, whose key at +0x8 is a BuffDefinitions row. Name and description are keys into ClientStrings
///     (+0x91 and +0x81), read in every language. Each column is found by what it holds and judged on the rows named
///     below (0.5.5f); the step fails, and writes nothing, when one is not as expected. Read-only on the game.
/// </summary>
internal static class ShrineExtractor
{
    private const string Step = "shrines";
    private const string ShrinesTable = "shrines";
    private const string TemplatesTable = "bufftemplates";
    private const string BuffsTable = "buffdefinitions";

    private const int ExpectedRowSize = 0x122;

    // Measured offsets (0.5.5f, dat-kinds / dat-keys), where each column is looked for first.
    private const int BuffTemplateOffset = 0xD;
    private const int LesserOffset = 0x80;
    private const int SecondsOffset = 0x59;
    private const int DescriptionOffset = 0x81;
    private const int NameOffset = 0x91;
    private const int TemplateBuffOffset = 0x8;

    /// <summary>A lesser shrine, its template, and the buff the template keys.</summary>
    private const string LesserShrine = "AccelerationLesser";
    private const string LesserTemplate = "acceleration_lesser";
    private const string LesserBuff = "shrine_acceleration";

    /// <summary>An ordinary shrine (the one the plugin knows as Freezing) and its buff; the older "Cold" row holds the ClientStrings keys below.</summary>
    private const string OrdinaryShrine = "ColdShrine";
    private const string OrdinaryBuff = "shrine_cold_2";
    private const string TextShrine = "Cold";
    private const string OrdinaryName = "ShrineColdName";
    private const string OrdinaryDescription = "ShrineColdActivation";

    private sealed record Columns(int Template, int Lesser, int Seconds, int Description, int Name, int TemplateBuff);

    public sealed record Entry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("lesser")] bool Lesser,
        [property: JsonPropertyName("buff")] string Buff,
        [property: JsonPropertyName("seconds")] int Seconds,
        [property: JsonPropertyName("names")] SortedDictionary<string, string> Names,
        [property: JsonPropertyName("descriptions")] SortedDictionary<string, string> Descriptions);

    public static int Run(Index index, string outFile)
    {
        var tables = (Shrines: DatTable.Open(index, ShrinesTable), Templates: DatTable.Open(index, TemplatesTable), Buffs: DatTable.Open(index, BuffsTable), Strings: DatTable.Open(index, ClientStringColumns.Table));
        if (tables.Shrines == null || tables.Templates == null || tables.Buffs == null || tables.Strings == null)
        {
            Say.Out.WriteLine($"Shrines, BuffTemplates, BuffDefinitions or ClientStrings not found: {Step} left out.");
            return 1;
        }

        var (shrines, templates, buffs, strings) = (tables.Shrines, tables.Templates, tables.Buffs, tables.Strings);
        if (shrines.RowSize != ExpectedRowSize)
        {
            Say.Out.WriteLine($"Shrines rows are 0x{shrines.RowSize:X} bytes, not 0x{ExpectedRowSize:X}: its columns moved, {Step} keeps its previous catalog");
            return 1;
        }

        var search = new DatColumnSearch();
        var stringColumns = ClientStringColumns.Find(strings, search);
        var columns = stringColumns == null ? null : Find(shrines, templates, buffs, strings, search);
        if (!search.Report(Step) || columns == null || stringColumns is not { } textColumns)
        {
            return 1;
        }

        var entries = new List<Entry>();
        for (var row = 0; row < shrines.RowCount; row++)
        {
            var template = shrines.Key(row, columns.Template);
            var buffRow = template >= 0 && template < templates.RowCount ? templates.Key((int)template, columns.TemplateBuff) : -1;
            entries.Add(new Entry(
                shrines.String(row, DatColumnSearch.IdColumn),
                shrines.Byte(row, columns.Lesser) == 1,
                DatAnchors.IdOf(buffs, buffRow) ?? string.Empty,
                shrines.Int32(row, columns.Seconds),
                new SortedDictionary<string, string>(StringComparer.Ordinal),
                new SortedDictionary<string, string>(StringComparer.Ordinal)));
        }

        foreach (var (code, folder) in AreaExtractor.Languages)
        {
            var table = folder == null ? strings : DatTable.OpenTranslated(index, ClientStringColumns.Table, folder);
            if (table == null)
            {
                Say.Out.WriteLine($"  {code}: no ClientStrings");
                continue;
            }

            if (!ClientStringColumns.SameShape(strings, table))
            {
                Say.Out.WriteLine($"  {code}: ClientStrings differs from the English table in rows or row size: its columns cannot be told, {Step} left out");
                return 1;
            }

            var texts = ClientStringColumns.Read(table, textColumns);
            for (var row = 0; row < entries.Count; row++)
            {
                Add(entries[row].Names, code, texts, DatAnchors.IdOf(strings, shrines.Key(row, columns.Name)));
                Add(entries[row].Descriptions, code, texts, DatAnchors.IdOf(strings, shrines.Key(row, columns.Description)));
            }

            Say.Out.WriteLine($"  {code}: {entries.Count(entry => entry.Names.ContainsKey(code))} names, {entries.Count(entry => entry.Descriptions.ContainsKey(code))} descriptions");
        }

        if (!KnownRowsHold(entries))
        {
            return 1;
        }

        AreaExtractor.WriteJson(outFile, new SortedDictionary<string, object>(StringComparer.Ordinal) { ["shrines"] = entries });
        Say.Out.WriteLine($"Wrote {outFile}: {entries.Count} shrines, {entries.Count(entry => entry.Lesser)} lesser, {entries.Count(entry => entry.Buff.Length > 0)} with a buff");
        return 0;
    }

    private static void Add(SortedDictionary<string, string> into, string language, Dictionary<string, string> texts, string? key)
    {
        if (key != null && texts.TryGetValue(key, out var text))
        {
            into[language] = text;
        }
    }

    private static Columns? Find(DatTable shrines, DatTable templates, DatTable buffs, DatTable strings, DatColumnSearch search)
    {
        var lesserRow = DatAnchors.Row(shrines, LesserShrine, "Shrines.Player_ShrineBuffsKey");
        var templateRow = DatColumnSearch.RowOf(templates, LesserTemplate);
        var buffRow = DatColumnSearch.RowOf(buffs, LesserBuff);
        var ordinaryRow = DatColumnSearch.RowOf(shrines, TextShrine);
        search.Find(shrines, "Shrines.Id", DatColumnSearch.IdColumn, offset => DatColumnSearch.IsIdColumn(shrines, offset) && lesserRow >= 0 && ordinaryRow >= 0);
        var template = search.Key(shrines, "Shrines.Player_ShrineBuffsKey", BuffTemplateOffset, templates, known: (lesserRow, key => templateRow >= 0 && key == templateRow));
        var templateBuff = search.Key(templates, "BuffTemplates.BuffDefinitions", TemplateBuffOffset, buffs, known: (templateRow, key => buffRow >= 0 && key == buffRow));
        var lesser = search.Find(shrines, "Shrines.IsLesserShrine", LesserOffset, offset => IsLesserFlag(shrines, offset));
        var seconds = search.Find(shrines, "Shrines.Duration", SecondsOffset, offset => IsSeconds(shrines, offset, lesserRow));
        var description = search.Key(shrines, "Shrines.Description", DescriptionOffset, strings, known: (ordinaryRow, key => DatAnchors.IdOf(strings, key) == OrdinaryDescription));
        var name = search.Key(shrines, "Shrines.Name", NameOffset, strings, known: (ordinaryRow, key => DatAnchors.IdOf(strings, key) == OrdinaryName));
        return search.AllFound ? new Columns(template, lesser, seconds, description, name, templateBuff) : null;
    }

    /// <summary>A flag that is set on exactly the rows whose Id ends in "Lesser".</summary>
    private static bool IsLesserFlag(DatTable shrines, int offset)
    {
        using var probe = DatTable.Probe();
        var any = false;
        for (var row = 0; row < shrines.RowCount; row++)
        {
            var value = shrines.Byte(row, offset);
            if (value is not (0 or 1) || (value == 1) != (shrines.StringOrNull(row, DatColumnSearch.IdColumn)?.EndsWith("Lesser", StringComparison.Ordinal) ?? false))
            {
                return false;
            }

            any |= value == 1;
        }

        return any;
    }

    /// <summary>A duration in seconds: every row a small whole number, none zero, and the lesser shrine's the measured 20.</summary>
    private static bool IsSeconds(DatTable shrines, int offset, int lesserRow)
    {
        using var probe = DatTable.Probe();
        return offset + sizeof(int) <= shrines.RowSize && lesserRow >= 0 && shrines.Int32(lesserRow, offset) == 20 &&
               Enumerable.Range(0, shrines.RowCount).All(row => shrines.Int32(row, offset) is > 0 and <= 600);
    }

    private static bool KnownRowsHold(List<Entry> entries)
    {
        var ok = true;
        foreach (var (id, buff, lesser) in new[] { (LesserShrine, LesserBuff, true), (OrdinaryShrine, OrdinaryBuff, false) })
        {
            var entry = entries.FirstOrDefault(candidate => candidate.Id == id);
            if (entry == null || entry.Buff != buff || entry.Lesser != lesser || !entry.Names.ContainsKey("en") || !entry.Descriptions.ContainsKey("en"))
            {
                Say.Out.WriteLine($"  {id} should give {buff} with an English name and description, found {(entry == null ? "no row" : $"{entry.Buff} / {entry.Names.GetValueOrDefault("en")}")}: {Step} keeps its previous catalog");
                ok = false;
            }
        }

        return ok;
    }
}
