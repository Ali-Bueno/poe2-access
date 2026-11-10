using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `dat-columns` command: finds, in the player's own install, the dat columns the plugin reads, so a game update that
///     moves them is followed without a new plugin build. A display name is the column that holds a known row's name in the
///     English table (<see cref="NameAnchor"/>) and reads as text in a translated copy; flags and keys are found by what they
///     hold (<see cref="DatContentColumns"/>). The file's column offset is the row offset in memory, a string or key being as
///     wide as its pointers. Writes { "Table.Column": offset }; a column not found fails the step.
/// </summary>
internal static class DatColumnExtractor
{
    /// <summary>Every column the file must hold: the name columns and <see cref="DatContentColumns.Names"/>.</summary>
    private static IEnumerable<string> ExpectedColumns => NameAnchor.Written.Select(anchor => anchor.Column).Concat(DatContentColumns.Names);

    private const string Translation = "spanish";

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var anchor in NameAnchor.Written)
        {
            if (NameColumn(index, anchor) is { } offset)
            {
                found[anchor.Column] = offset;
                Say.Out.WriteLine($"{anchor.Column}: +0x{offset:X} (measured +0x{anchor.Measured:X})");
            }
        }

        foreach (var (column, offset, measured) in DatContentColumns.Find(index))
        {
            if (offset is { } at)
            {
                found[column] = at;
            }

            Say.Out.WriteLine(offset is { } where ? $"{column}: +0x{where:X} (measured +0x{measured:X})" : $"{column}: not recognised");
        }

        // A column left out would be read by the plugin at its compiled offset, unjudged: the step fails instead and the
        // previous catalog stays, with the game data reported as not regenerated.
        var missing = ExpectedColumns.Where(column => !found.ContainsKey(column)).ToList();
        if (missing.Count > 0)
        {
            Say.Out.WriteLine($"dat-columns: not found: {string.Join(", ", missing)}");
            return 1;
        }

        if (DatTable.Open(index, "minimapicons") is { } icons)
        {
            found["MinimapIcons.RowSize"] = icons.RowSize;
        }

        File.WriteAllText(outFile, JsonSerializer.Serialize(found, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    /// <summary>The display name's column of a table, for an extractor that reads it; null (said) when it cannot be told.</summary>
    internal static int? NameColumn(Index index, NameAnchor anchor)
    {
        var english = DatTable.Open(index, anchor.Table);
        var translated = DatTable.OpenTranslated(index, anchor.Table, Translation);
        if (english == null || translated == null)
        {
            Say.Out.WriteLine($"{anchor.Column}: the {anchor.Table} table is missing in English or {Translation}");
            return null;
        }

        var search = new DatColumnSearch();
        var offset = NameColumns.Find(english, translated, anchor, search);
        return search.Report(anchor.Table) && offset >= 0 ? offset : null;
    }
}
