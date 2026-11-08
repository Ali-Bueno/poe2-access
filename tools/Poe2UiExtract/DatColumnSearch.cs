using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
///     Finds the columns an extractor reads by what each holds, nearest to where it was measured (docs/compatibility.md,
///     "Content changes such as 1.0"): its type (text, key into a table, key list, string list, integer, flag) and, where the
///     caller can verify one, a value on a row it knows. A column that moved is read where it is and said; one not found is
///     listed in <see cref="Missing"/>, and the extractor fails its step, so the launcher keeps the old catalog. Thresholds
///     are shares of the table's rows, never counts, so a smaller table after a content patch judges the same. Pure.
/// </summary>
internal sealed class DatColumnSearch
{
    /// <summary>A text column decodes as a string in this share of its rows (a translated table pools its strings).</summary>
    public const double TextShare = 0.9;

    /// <summary>A dat file's null foreign key: all 0xFE bytes.</summary>
    public const long NullKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

    private readonly List<string> missing = new();
    private readonly List<string> moved = new();

    /// <summary>The columns not found, as "Table.Column near +0x8".</summary>
    public IReadOnlyList<string> Missing => this.missing;

    /// <summary>The columns found elsewhere than measured, as "Table.Column moved from +0x8 to +0x10".</summary>
    public IReadOnlyList<string> Moved => this.moved;

    public bool AllFound => this.missing.Count == 0;

    /// <summary>The first row whose Id (the string at +0) is <paramref name="id"/>, or -1: a row the caller can verify a value on.</summary>
    public static int RowOf(DatTable table, string id)
    {
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.StringOrNull(row, 0) == id)
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>
    ///     The offset nearest <paramref name="measured"/> (and past <paramref name="after"/>, for a column known to follow
    ///     another of the same type) where <paramref name="fits"/> holds; -1 when none does, which is recorded.
    /// </summary>
    public int Find(DatTable table, string name, int measured, Func<int, bool> fits, int after = -1)
    {
        var found = Enumerable.Range(after + 1, Math.Max(0, table.RowSize - after - 1)).OrderBy(offset => Math.Abs(offset - measured)).Where(fits).Select(offset => (int?)offset).FirstOrDefault();
        if (found is not { } offset)
        {
            this.missing.Add($"{name} near +0x{measured:X}");
            return -1;
        }

        if (offset != measured)
        {
            this.moved.Add($"{name} moved from +0x{measured:X} to +0x{offset:X}");
        }

        return offset;
    }

    /// <summary>A table's Id column sits at +0; a text column searched by default lies after it.</summary>
    public const int IdColumn = 0;

    /// <summary>
    ///     A text column: a string in <see cref="TextShare"/> of the rows, at least one filled, every filled one passing
    ///     <paramref name="matches"/>; <paramref name="known"/> checks the text of a row the caller can name (skipped when
    ///     that row is absent from this table).
    /// </summary>
    public int Text(DatTable table, string name, int measured, Func<string, bool>? matches = null, int after = IdColumn, (int Row, Func<string, bool> Holds)? known = null) =>
        this.Find(table, name, measured, offset => IsText(table, offset, matches ?? (_ => true)) && (known is not { Row: >= 0 } check || check.Holds(table.StringOrNull(check.Row, offset) ?? string.Empty)), after);

    /// <summary>
    ///     A key column into <paramref name="target"/>: every row a null key or a row of the target, at least one of the
    ///     latter; <paramref name="known"/> checks the target row of a row the caller can name.
    /// </summary>
    public int Key(DatTable table, string name, int measured, DatTable target, int after = -1, (int Row, Func<long, bool> Holds)? known = null) =>
        this.Find(table, name, measured, offset => IsKey(table, offset, target.RowCount) && (known is not { Row: >= 0 } check || check.Holds(table.Key(check.Row, offset))), after);

    /// <summary>A list column of keys into <paramref name="target"/>: every item of every row a row of the target, some row with items.</summary>
    public int KeyList(DatTable table, string name, int measured, DatTable target, int after = -1, (int Row, Func<IReadOnlyList<long>, bool> Holds)? known = null) =>
        this.Find(table, name, measured, offset => IsKeyList(table, offset, target.RowCount) && (known is not { Row: >= 0 } check || check.Holds(table.KeyList(check.Row, offset))), after);

    /// <summary>A list column of strings, every item passing <paramref name="matches"/>, some row with items.</summary>
    public int StringList(DatTable table, string name, int measured, Func<string, bool> matches, int after = -1) =>
        this.Find(table, name, measured, offset => IsStringList(table, offset, matches), after);

    /// <summary>A 4-byte integer column whose every value passes <paramref name="fits"/>, with at least two values (a column of one constant tells nothing).</summary>
    public int Int32(DatTable table, string name, int measured, Func<int, bool> fits, int after = -1) =>
        this.Find(table, name, measured, offset => offset + sizeof(int) <= table.RowSize && Enumerable.Range(0, table.RowCount).All(row => fits(table.Int32(row, offset))) &&
                                                     Enumerable.Range(0, table.RowCount).Select(row => table.Int32(row, offset)).Distinct().Skip(1).Any(), after);

    /// <summary>A one-byte column whose every value passes <paramref name="fits"/> (a flag: 0 or 1, both present).</summary>
    public int Byte(DatTable table, string name, int measured, Func<int, bool> fits, int after = -1, (int Row, Func<int, bool> Holds)? known = null) =>
        this.Find(table, name, measured, offset => offset < table.RowSize && Enumerable.Range(0, table.RowCount).All(row => fits(table.Byte(row, offset))) &&
                                                     Enumerable.Range(0, table.RowCount).Select(row => table.Byte(row, offset)).Distinct().Skip(1).Any() &&
                                                     (known is not { Row: >= 0 } check || check.Holds(table.Byte(check.Row, offset))), after);

    /// <summary>A 2-byte column distinct and non-zero on every row (a graph id).</summary>
    public int UniqueUInt16(DatTable table, string name, int measured, int after = -1) =>
        this.Find(table, name, measured, offset => offset + sizeof(ushort) <= table.RowSize && Enumerable.Range(0, table.RowCount).Select(row => table.UInt16(row, offset)).All(value => value > 0) &&
                                                     Enumerable.Range(0, table.RowCount).Select(row => table.UInt16(row, offset)).Distinct().Count() == table.RowCount, after);

    public static bool IsText(DatTable table, int offset, Func<string, bool> matches)
    {
        int strings = 0, filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            if (table.StringOrNull(row, offset) is not { } text)
            {
                continue;
            }

            strings++;
            if (text.Length == 0)
            {
                continue;
            }

            if (!matches(text))
            {
                return false;
            }

            filled++;
        }

        return table.RowCount > 0 && strings >= table.RowCount * TextShare && filled > 0;
    }

    public static bool IsKey(DatTable table, int offset, int targetRows)
    {
        if (offset < 0 || offset + sizeof(long) > table.RowSize)
        {
            return false;
        }

        var resolving = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var key = table.Int64(row, offset);
            if (key == NullKey)
            {
                continue;
            }

            if (key < 0 || key >= targetRows)
            {
                return false;
            }

            resolving++;
        }

        return resolving > 0;
    }

    public static bool IsKeyList(DatTable table, int offset, int targetRows)
    {
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var keys = table.KeyList(row, offset);
            if (keys.Any(key => key < 0 || key >= targetRows))
            {
                return false;
            }

            filled += keys.Count > 0 ? 1 : 0;
        }

        return filled > 0;
    }

    public static bool IsStringList(DatTable table, int offset, Func<string, bool> matches)
    {
        var filled = 0;
        for (var row = 0; row < table.RowCount; row++)
        {
            var items = table.StringListOrNull(row, offset);
            if (items == null)
            {
                return false;
            }

            if (items.Count == 0)
            {
                continue;
            }

            if (!items.All(matches))
            {
                return false;
            }

            filled++;
        }

        return filled > 0;
    }

    /// <summary>Prints what moved and what is missing, and says whether the step may go on.</summary>
    public bool Report(string step)
    {
        foreach (var line in this.moved)
        {
            Say.Out.WriteLine($"  {line}");
        }

        foreach (var line in this.missing)
        {
            Say.Out.WriteLine($"  {line}: not found (check with dat-strings), {step} keeps its previous catalog");
        }

        return this.AllFound;
    }
}
