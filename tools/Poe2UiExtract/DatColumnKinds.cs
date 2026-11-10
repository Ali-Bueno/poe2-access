using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What sits at an offset of a .datc64 row, as its bytes alone tell it.</summary>
internal enum DatColumnKind
{
    /// <summary>The same bytes on every row: nothing tells what they are.</summary>
    Blank,
    Number,
    Flag,
    Text,
    Key,
    List,
    TextList,
    KeyList,
}

/// <summary>What the bytes say of a column an extractor read.</summary>
internal enum DatKindVerdict
{
    Agrees,

    /// <summary>A blank or constant column, or a table too small: the bytes neither confirm nor contradict.</summary>
    CannotTell,
    Differs,
}

/// <summary>
///     The judge of what kind of column sits at each offset of a table, from the bytes alone: a .datc64 carries no column
///     names, but its columns describe themselves. A text is the offset of a string's first character in the variable
///     section; a key is a row index or the null key (all 0xFE), followed by eight zero (or 0xFE) bytes when it points
///     into another table; a list is a count and the offset of that many items; a flag is a byte of 0 or 1. Used as a
///     second opinion on every column the extractors read (<see cref="DatColumnAudit"/>) and to lay a table out for a
///     person (`dat-kinds`). Pure.
/// </summary>
internal static class DatColumnKinds
{
    /// <summary>A text column holds a string's start in this share of its rows (a translated table pools its strings).</summary>
    public const double TextShare = DatColumnSearch.TextShare;

    /// <summary>A table under this many rows cannot prove a column is text: a few numbers land on string starts by chance.</summary>
    public const int FewRows = 8;

    private const int TextBytes = 8;
    private const int KeyBytes = 8;
    private const int WideKeyBytes = 16;
    private const int ListBytes = 16;
    private const int SampledLists = 64;
    private const int DistinctEnough = 2;

    /// <summary>What every row holds at one offset, counted in rows.</summary>
    /// <param name="Width">The bytes from the offset to the row's end.</param>
    /// <param name="Keys">Rows holding the null key or a non-negative 32-bit number in 8 bytes.</param>
    /// <param name="WideKeys">Rows whose next 8 bytes are a key's second half (zero, or 0xFE on a null key).</param>
    /// <param name="Starts">Rows whose 8 bytes are the offset of a string's first character.</param>
    /// <param name="Filled">Of those, the strings with a character.</param>
    /// <param name="Lists">Rows whose 16 bytes are a count and an offset that fit the variable section.</param>
    /// <param name="TextItems">Every item of the sampled lists is a string's start.</param>
    /// <param name="KeyItems">Every item of the sampled lists is a 16-byte key.</param>
    /// <param name="RowItems">Every item of the sampled lists is an 8-byte key.</param>
    internal readonly record struct Facts(
        int Rows, int Width, bool Constant, int Nulls, int Keys, int WideKeys, int Starts, int Filled, bool VariedText,
        int Lists, int FilledLists, bool TextItems, bool KeyItems, bool RowItems, int Flags, int Ones)
    {
        /// <summary>A string on nearly every row, some filled, not one string repeated: no number column reads so by chance.</summary>
        public bool IsText => this.Width >= TextBytes && this.Rows > 0 && this.Starts >= this.Rows * TextShare && this.Filled > 0 && (this.VariedText || this.Rows < DistinctEnough);

        public bool IsList => this.Width >= ListBytes && this.Rows > 0 && this.Lists == this.Rows && this.FilledLists > 0;

        /// <summary>A key on every row with a null among them: the 0xFE bytes are no number an extractor reads.</summary>
        public bool IsKey => this.Width >= KeyBytes && this.Rows > 0 && this.Keys == this.Rows && this.Nulls > 0;

        public bool IsFlag => this.Rows > 0 && this.Flags == this.Rows && this.Ones > 0 && this.Ones < this.Rows;

        /// <summary>A key into another table on every row, none of them null: sixteen bytes that could also be a number before zeros.</summary>
        public bool LooksWideKey => this.Width >= WideKeyBytes && this.Rows > 0 && this.WideKeys == this.Rows && !this.Constant;
    }

    public static Facts Of(DatTable table, int offset)
    {
        int rows = table.RowCount, width = table.RowSize - offset;
        int nulls = 0, keys = 0, wideKeys = 0, starts = 0, filled = 0, lists = 0, filledLists = 0, flags = 0, ones = 0, sampled = 0;
        bool constant = true, variedText = false, textItems = true, keyItems = true, rowItems = true;
        long firstText = -1, firstValue = 0;
        var firstByte = 0;
        for (var row = 0; row < rows; row++)
        {
            var one = table.RawByte(row, offset);
            flags += one is 0 or 1 ? 1 : 0;
            ones += one == 1 ? 1 : 0;
            if (width < KeyBytes)
            {
                constant &= row == 0 || one == firstByte;
                firstByte = row == 0 ? one : firstByte;
                continue;
            }

            var value = table.Raw64(row, offset);
            constant &= row == 0 || value == firstValue;
            firstValue = row == 0 ? value : firstValue;
            var isKey = IsKeyValue(value);
            nulls += value == DatColumnSearch.NullKey ? 1 : 0;
            keys += isKey ? 1 : 0;
            if (table.IsStringStart(value))
            {
                starts++;
                if (table.IsFilledString(value))
                {
                    filled++;
                    variedText |= firstText >= 0 && value != firstText;
                    firstText = firstText < 0 ? value : firstText;
                }
            }

            if (width < ListBytes)
            {
                continue;
            }

            var second = table.Raw64(row, offset + KeyBytes);
            wideKeys += isKey && second == (value == DatColumnSearch.NullKey ? DatColumnSearch.NullKey : 0) ? 1 : 0;
            if (value < 0 || value > DatTable.MaxListItems || second < TextBytes || second + value > table.VarLength)
            {
                continue;
            }

            lists++;
            if (value == 0)
            {
                continue;
            }

            filledLists++;
            if (sampled++ < SampledLists)
            {
                JudgeItems(table, value, second, ref textItems, ref keyItems, ref rowItems);
            }
        }

        var any = filledLists > 0;
        return new Facts(rows, width, constant, nulls, keys, wideKeys, starts, filled, variedText, lists, filledLists, any && textItems, any && keyItems, any && rowItems, flags, ones);
    }

    private static bool IsKeyValue(long value) => value == DatColumnSearch.NullKey || value is >= 0 and < int.MaxValue;

    private static void JudgeItems(DatTable table, long count, long start, ref bool textItems, ref bool keyItems, ref bool rowItems)
    {
        keyItems &= start + (count * WideKeyBytes) <= table.VarLength;
        textItems &= start + (count * TextBytes) <= table.VarLength;
        rowItems &= start + (count * KeyBytes) <= table.VarLength;
        for (var item = 0; item < count && (textItems || keyItems || rowItems); item++)
        {
            if (textItems || rowItems)
            {
                var value = table.Pool64(start + (item * TextBytes));
                textItems &= table.IsStringStart(value);
                rowItems &= IsKeyValue(value);
            }

            if (keyItems)
            {
                var key = table.Pool64(start + (item * WideKeyBytes));
                var second = table.Pool64(start + (item * WideKeyBytes) + KeyBytes);
                keyItems &= IsKeyValue(key) && second == (key == DatColumnSearch.NullKey ? DatColumnSearch.NullKey : 0);
            }
        }
    }

    /// <summary>The kind the bytes at an offset most plainly are.</summary>
    public static DatColumnKind At(DatTable table, int offset) => KindOf(Of(table, offset));

    internal static DatColumnKind KindOf(Facts facts)
    {
        if (facts.IsText)
        {
            return DatColumnKind.Text;
        }

        if (facts.IsList)
        {
            return facts.TextItems ? DatColumnKind.TextList : facts.KeyItems ? DatColumnKind.KeyList : DatColumnKind.List;
        }

        return facts.IsKey || facts.LooksWideKey ? DatColumnKind.Key : facts.IsFlag ? DatColumnKind.Flag : facts.Constant ? DatColumnKind.Blank : DatColumnKind.Number;
    }

    /// <summary>Whether the bytes at an offset can be what an extractor read them as.</summary>
    public static DatKindVerdict Judge(DatTable table, int offset, DatRead read) => Judge(Of(table, offset), read);

    internal static DatKindVerdict Judge(Facts facts, DatRead read)
    {
        // Enough rows that landing on string starts, or on list bounds, is no accident.
        var proves = facts.Rows >= FewRows;
        switch (read)
        {
            case DatRead.Text:
                return facts.Width < TextBytes || facts.Starts < facts.Rows * TextShare ? DatKindVerdict.Differs
                    : facts.Filled == 0 ? DatKindVerdict.CannotTell : DatKindVerdict.Agrees;
            case DatRead.Key:
                return facts.Width < KeyBytes || facts.Keys < facts.Rows || (proves && facts.Nulls == 0 && facts.IsText) ? DatKindVerdict.Differs
                    : facts.Constant && facts.Nulls == 0 ? DatKindVerdict.CannotTell : DatKindVerdict.Agrees;
            case DatRead.TextList or DatRead.KeyList or DatRead.RowList or DatRead.NumberList:
                if (facts.Width < ListBytes || facts.Lists < facts.Rows)
                {
                    return DatKindVerdict.Differs;
                }

                if (facts.FilledLists == 0)
                {
                    return DatKindVerdict.CannotTell;
                }

                var items = read switch { DatRead.TextList => facts.TextItems, DatRead.KeyList => facts.KeyItems, DatRead.RowList => facts.RowItems, _ => true };
                return items ? DatKindVerdict.Agrees : DatKindVerdict.Differs;
            case DatRead.Flag:
                return facts.Flags < facts.Rows ? DatKindVerdict.Differs : facts.IsFlag ? DatKindVerdict.Agrees : DatKindVerdict.CannotTell;
            case DatRead.Number:
                return proves && (facts.IsText || facts.IsKey || (facts.IsList && (facts.TextItems || facts.KeyItems))) ? DatKindVerdict.Differs
                    : facts.Constant ? DatKindVerdict.CannotTell : DatKindVerdict.Agrees;
            default:
                return DatKindVerdict.CannotTell;
        }
    }

    /// <summary>
    ///     The row laid out from its first byte: each text, key, list and flag where the bytes show one, the bytes between
    ///     them as runs of numbers or blanks. A reading aid for a person finding a column again, not a schema.
    /// </summary>
    public static IEnumerable<(int Offset, DatColumnKind Kind, int Width)> Layout(DatTable table)
    {
        var offset = 0;
        (int Offset, DatColumnKind Kind)? run = null;
        while (offset < table.RowSize)
        {
            var facts = Of(table, offset);
            var kind = KindOf(facts);
            var width = kind switch
            {
                DatColumnKind.Text => TextBytes,
                DatColumnKind.Key => facts.Width >= WideKeyBytes && facts.WideKeys == facts.Rows ? WideKeyBytes : KeyBytes,
                DatColumnKind.List or DatColumnKind.TextList or DatColumnKind.KeyList => ListBytes,
                _ => 1,
            };
            if (kind is DatColumnKind.Number or DatColumnKind.Blank)
            {
                if (run is { } open && open.Kind != kind)
                {
                    yield return (open.Offset, open.Kind, offset - open.Offset);
                    run = null;
                }

                run ??= (offset, kind);
            }
            else
            {
                if (run is { } open)
                {
                    yield return (open.Offset, open.Kind, offset - open.Offset);
                    run = null;
                }

                yield return (offset, kind, width);
            }

            offset += width;
        }

        if (run is { } last)
        {
            yield return (last.Offset, last.Kind, offset - last.Offset);
        }
    }

    /// <summary>
    ///     Whether the text column at a table's start still reads as its Id: identifiers and paths have no blank in them,
    ///     display text does. Null when the column is not text or has too few filled rows to say.
    /// </summary>
    public static bool? StartsWithIds(DatTable table)
    {
        using var probe = DatTable.Probe();
        int filled = 0, spaced = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.StringOrNull(row, DatColumnSearch.IdColumn) is { Length: > 0 } text)
            {
                filled++;
                spaced += text.AsSpan().Trim().IndexOf(' ') >= 0 ? 1 : 0;
            }
        }

        return filled < FewRows ? null : spaced <= filled * MostIdsSpaced;
    }

    /// <summary>The share of an Id column's rows that may hold a blank (ItemClasses: 14 of 118, "One Hand Sword"; the other tables none).</summary>
    public const double MostIdsSpaced = 0.25;

    /// <summary>What the first row with something there holds, for a person reading the layout.</summary>
    public static string Sample(DatTable table, int offset, DatColumnKind kind, int width)
    {
        for (var row = 0; row < table.RowCount; row++)
        {
            if (Value(table, row, offset, kind, width) is { Length: > 0 } text)
            {
                return text;
            }
        }

        return string.Empty;
    }

    /// <summary>One row's value of a laid-out column, as a person reads it; empty for nothing there.</summary>
    public static string Value(DatTable table, int row, int offset, DatColumnKind kind, int width)
    {
        using var probe = DatTable.Probe();
        return kind switch
        {
            DatColumnKind.Text => table.StringOrNull(row, offset) ?? string.Empty,
            DatColumnKind.Key => table.Key(row, offset) is >= 0 and var key ? $"row {key}" : string.Empty,
            DatColumnKind.TextList => string.Join(" | ", table.StringList(row, offset)),
            DatColumnKind.KeyList => table.KeyList(row, offset) is { Count: > 0 } keys ? "rows " + string.Join(", ", keys) : string.Empty,
            DatColumnKind.List => table.Raw64(row, offset) is > 0 and var count ? $"{count} items" : string.Empty,
            DatColumnKind.Flag => table.RawByte(row, offset).ToString(),
            DatColumnKind.Number => string.Join(' ', Enumerable.Range(0, width).Select(at => table.RawByte(row, offset + at).ToString("X2"))) +
                                    (width == sizeof(int) ? $" = {table.Int32(row, offset)}" : string.Empty),
            _ => string.Empty,
        };
    }

    public static string Name(DatColumnKind kind) => kind switch
    {
        DatColumnKind.Text => "text",
        DatColumnKind.Key => "a key",
        DatColumnKind.List => "a list",
        DatColumnKind.TextList => "a list of texts",
        DatColumnKind.KeyList => "a list of keys",
        DatColumnKind.Flag => "a flag",
        DatColumnKind.Number => "numbers",
        _ => "the same bytes on every row",
    };

    public static string Name(DatRead read) => read switch
    {
        DatRead.Text => "text",
        DatRead.Key => "a key",
        DatRead.TextList => "a list of texts",
        DatRead.KeyList => "a list of keys",
        DatRead.RowList => "a list of rows",
        DatRead.NumberList => "a list of numbers",
        DatRead.Flag => "a flag",
        _ => "a number",
    };
}
