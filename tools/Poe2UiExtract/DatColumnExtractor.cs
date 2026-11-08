using System.Text.Json;
using Index = LibBundle3.Index;

/// <summary>
///     `dat-columns` command: finds, in the player's own install, the dat columns the plugin reads, so a game update that
///     moves them is followed without a new plugin build. A display name is the first string column whose text differs
///     between the English table and a translated copy (the Id column is the same in both); flags and keys are found by
///     what they hold (<see cref="DatContentColumns"/>). The file's column offset is the row offset in memory, a string
///     or key being as wide as its pointers. Writes { "Table.Column": offset }.
/// </summary>
internal static class DatColumnExtractor
{
    /// <summary>Table, the plugin's name for its column, and where it was measured (0.5.5e, 2026-10-24).</summary>
    private static readonly (string Table, string Column, int Measured)[] Columns =
    {
        ("mods", "Mods.Name", 0x62),
        ("passiveskills", "PassiveSkills.Name", 0x32),
        ("activeskills", "ActiveSkills.DisplayedName", 0x8),
        ("minimapicons", "MinimapIcons.Label", 0x1A),
        ("characters", "Characters.Name", 0x8),
        ("worldareas", "WorldAreas.Name", 0x8),
    };

    private const string Translation = "spanish";

    /// <summary>A string column holds a string's start in at least this share of rows of both tables.</summary>
    private const double StringRows = 0.8;

    /// <summary>A display name differs from the English text in at least this share of the rows that have one.</summary>
    private const double TranslatedRows = 0.25;

    private const int MinTranslatedRows = 20;

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var found = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var (table, column, measured) in Columns)
        {
            var english = DatTable.Open(index, table);
            var translated = DatTable.OpenTranslated(index, table, Translation);
            if (english == null || translated == null || english.RowCount != translated.RowCount || english.RowSize != translated.RowSize)
            {
                Say.Out.WriteLine($"{column}: tables missing or not alike, left out");
                continue;
            }

            if (FirstTranslated(english, translated) is { } offset)
            {
                found[column] = offset;
                Say.Out.WriteLine($"{column}: +0x{offset:X} (measured +0x{measured:X}), row size 0x{english.RowSize:X}");
            }
            else
            {
                Say.Out.WriteLine($"{column}: no translated column found, left out");
            }
        }

        foreach (var (column, offset, measured) in DatContentColumns.Find(index))
        {
            if (offset is { } at)
            {
                found[column] = at;
            }

            Say.Out.WriteLine(offset is { } where ? $"{column}: +0x{where:X} (measured +0x{measured:X})" : $"{column}: not recognised, left out");
        }

        if (found.Count == 0)
        {
            return 1;
        }

        if (DatTable.Open(index, "minimapicons") is { } icons)
        {
            found["MinimapIcons.RowSize"] = icons.RowSize;
        }

        File.WriteAllText(outFile, JsonSerializer.Serialize(found, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    /// <summary>The display name's column of a table, for an extractor that reads it; null when it cannot be told.</summary>
    internal static int? NameColumn(Index index, string table) =>
        DatTable.Open(index, table) is { } english && DatTable.OpenTranslated(index, table, Translation) is { } translated &&
        english.RowCount == translated.RowCount && english.RowSize == translated.RowSize
            ? FirstTranslated(english, translated)
            : null;

    private static int? FirstTranslated(DatTable english, DatTable translated)
    {
        // A table of a dozen classes has fewer rows than the rule asks of a long one.
        var minTranslated = Math.Min(MinTranslatedRows, english.RowCount / 2);
        for (var offset = 1; offset + 8 <= english.RowSize; offset++)
        {
            int strings = 0, named = 0, differing = 0;
            for (var row = 0; row < english.RowCount; row++)
            {
                if (english.StringOrNull(row, offset) is not { } source || translated.StringOrNull(row, offset) is not { } text)
                {
                    continue;
                }

                strings++;
                if (source.Length > 0)
                {
                    named++;
                    differing += source == text ? 0 : 1;
                }
            }

            if (strings >= english.RowCount * StringRows && differing >= minTranslated && differing >= named * TranslatedRows)
            {
                return offset;
            }
        }

        return null;
    }
}
