using System;

/// <summary>
///     A table's display-name column, told by the stable name its anchor row holds in the English table and then used in the
///     player's language (the same column in every copy of a table). Pure.
/// </summary>
internal static class NameColumns
{
    /// <summary>A display name differs from the English text in at least this share of the rows that have one.</summary>
    private const double TranslatedRows = 0.25;

    private const int MinTranslatedRows = 20;

    /// <summary>A string column holds a string's start in at least this share of rows of both tables.</summary>
    private const double StringRows = 0.8;

    /// <summary>
    ///     The column of <paramref name="anchor"/> in <paramref name="english"/>, -1 (recorded in <paramref name="search"/>) when
    ///     none holds the anchor's name or <paramref name="translated"/> reads no text there. An anchor row the table no longer
    ///     has cannot judge: the column is then the first one a translation changes, and the log says so.
    /// </summary>
    public static int Find(DatTable english, DatTable translated, NameAnchor anchor, DatColumnSearch search)
    {
        if (english.RowCount != translated.RowCount || english.RowSize != translated.RowSize)
        {
            search.Fail($"{anchor.Column} (the English and translated {anchor.Table} tables differ in rows or row size)");
            return -1;
        }

        var row = DatColumnSearch.RowOf(english, anchor.Id);
        if (row < 0)
        {
            search.Skipped(anchor.Column, $"anchor row {anchor.Id} is not in the table, so the column is the first that a translation changes");
            if (FirstTranslated(english, translated) is { } byTranslation)
            {
                return byTranslation;
            }

            search.Fail($"{anchor.Column} near +0x{anchor.Measured:X}");
            return -1;
        }

        var offset = search.Text(english, anchor.Column, anchor.Measured, known: (row, text => text == anchor.Name));
        if (offset >= 0 && !DatColumnSearch.IsText(translated, offset, _ => true))
        {
            search.Fail($"{anchor.Column} (no text at +0x{offset:X} in the translated table)");
            return -1;
        }

        return offset;
    }

    /// <summary>The first string column whose text differs between the tables; the way names were told before anchors.</summary>
    internal static int? FirstTranslated(DatTable english, DatTable translated)
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
