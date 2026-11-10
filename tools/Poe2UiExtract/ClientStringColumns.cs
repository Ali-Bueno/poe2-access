using System;
using System.Collections.Generic;

/// <summary>
///     ClientStrings' key and text columns, found in the English table by a known row (its key and what it says) and read at
///     the same offsets in every translation, a copy of the table with the same rows. Measured +0x0 and +0x8 (0.5.5f). Pure.
/// </summary>
internal static class ClientStringColumns
{
    internal const string Table = "clientstrings";
    internal const int KeyOffset = 0x0;
    internal const int TextOffset = 0x8;

    /// <summary>The world map's pin popup for a town: a key and an English text no patch has reworded.</summary>
    internal const string KnownKey = "WorldPanelPinPopupTown";
    internal const string KnownText = "Town";

    /// <summary>The two columns; null, with the search saying which, when one is not found.</summary>
    public static (int Key, int Text)? Find(DatTable english, DatColumnSearch search)
    {
        var key = search.Find(english, "ClientStrings.Id", KeyOffset, offset => DatColumnSearch.IsText(english, offset, _ => true) && DatAnchors.RowWith(english, offset, KnownKey) >= 0);
        if (key < 0)
        {
            return null;
        }

        var text = search.Text(english, "ClientStrings.Text", TextOffset, after: key, known: (DatAnchors.RowWith(english, key, KnownKey), said => said == KnownText));
        return text < 0 ? null : (key, text);
    }

    /// <summary>Whether a translation is the same table in another language: the offsets found in English read it.</summary>
    public static bool SameShape(DatTable english, DatTable translated) => translated.RowSize == english.RowSize && translated.RowCount == english.RowCount;

    /// <summary>Every key with a text, trimmed.</summary>
    public static Dictionary<string, string> Read(DatTable table, (int Key, int Text) columns)
    {
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.StringOrNull(row, columns.Key) is { Length: > 0 } key && table.StringOrNull(row, columns.Text)?.Trim() is { Length: > 0 } text)
            {
                texts[key] = text;
            }
        }

        return texts;
    }
}
