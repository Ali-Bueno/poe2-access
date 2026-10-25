using System.Text;
using Index = LibBundle3.Index;

/// <summary>
///     Minimal reader for an English .datc64 table of the game's Bundles2 (row count, fixed-size rows, then a variable
///     section after the 0xBB marker). Fields are read by byte offset inside a row: strings are 8-byte offsets into the
///     variable section (UTF-16LE), foreign keys are an 8-byte row index (a null key is all 0xFE bytes).
/// </summary>
internal sealed class DatTable
{
    private static readonly byte[] Marker = { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB };
    private readonly byte[] data;
    private readonly int varStart;

    private DatTable(byte[] data)
    {
        this.data = data;
        this.RowCount = BitConverter.ToInt32(data, 0);
        this.varStart = data.AsSpan(4).IndexOf(Marker) + 4;
        this.RowSize = (this.varStart - 4) / this.RowCount;
    }

    public int RowCount { get; }

    public int RowSize { get; }

    /// <summary>Opens <c>data/[balance/]&lt;name&gt;.datc64</c> (English); null when the index has no such table.</summary>
    public static DatTable? Open(Index index, string name)
    {
        var file = index.Files.Values.FirstOrDefault(f => f.Path != null
            && (f.Path.Equals($"data/balance/{name}.datc64", StringComparison.OrdinalIgnoreCase)
                || f.Path.Equals($"data/{name}.datc64", StringComparison.OrdinalIgnoreCase)));
        return file == null ? null : new DatTable(file.Read().ToArray());
    }

    /// <summary>Opens <c>data/[balance/]&lt;language&gt;/&lt;name&gt;.datc64</c>, a translated copy; null when absent.</summary>
    public static DatTable? OpenTranslated(Index index, string name, string language)
    {
        var file = index.Files.Values.FirstOrDefault(f => f.Path != null
            && (f.Path.Equals($"data/balance/{language}/{name}.datc64", StringComparison.OrdinalIgnoreCase)
                || f.Path.Equals($"data/{language}/{name}.datc64", StringComparison.OrdinalIgnoreCase)));
        return file == null ? null : new DatTable(file.Read().ToArray());
    }

    public string String(int row, int offset) => this.ReadString(BitConverter.ToInt64(this.data, this.RowStart(row) + offset));

    /// <summary>
    ///     The string a column holds in a row, or null when the 8 bytes there are not the offset of a string's first
    ///     character (a misaligned column lands inside another string, after a non-NUL character).
    /// </summary>
    public string? StringOrNull(int row, int offset)
    {
        if (offset < 0 || offset + 8 > this.RowSize)
        {
            return null;
        }

        var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        var at = this.varStart + value;
        if (value < Marker.Length || at >= this.data.Length || (value > Marker.Length && (this.data[at - 1] != 0 || this.data[at - 2] != 0)))
        {
            return null;
        }

        return this.ReadString(value);
    }

    /// <summary>8 bytes at any offset of a row, aligned or not (GrantedEffects' animation key sits at +0x71).</summary>
    public long Int64(int row, int offset) => offset >= 0 && offset + 8 <= this.RowSize ? BitConverter.ToInt64(this.data, this.RowStart(row) + offset) : -1;

    /// <summary>A list column of strings: a count, then where in the variable section its string offsets start.</summary>
    public IReadOnlyList<string> StringList(int row, int offset) =>
        this.ListItems(row, offset, 8).Select(at => this.ReadString(BitConverter.ToInt64(this.data, at))).ToList();

    /// <summary>4 bytes at any offset of a row; -1 past its end.</summary>
    public int Int32(int row, int offset) => offset >= 0 && offset + 4 <= this.RowSize ? BitConverter.ToInt32(this.data, this.RowStart(row) + offset) : -1;

    /// <summary>A one-byte flag column.</summary>
    public bool Bool(int row, int offset) => offset >= 0 && offset < this.RowSize && this.data[this.RowStart(row) + offset] != 0;

    /// <summary>2 bytes at any offset of a row; -1 past its end.</summary>
    public int UInt16(int row, int offset) => offset >= 0 && offset + 2 <= this.RowSize ? BitConverter.ToUInt16(this.data, this.RowStart(row) + offset) : -1;

    /// <summary>A one-byte flag column: every row holds 0 or 1, and both occur.</summary>
    public bool IsFlagColumn(int offset)
    {
        if (offset < 0 || offset >= this.RowSize)
        {
            return false;
        }

        int set = 0;
        for (var row = 0; row < this.RowCount; row++)
        {
            var value = this.data[this.RowStart(row) + offset];
            if (value > 1)
            {
                return false;
            }

            set += value;
        }

        return set > 0 && set < this.RowCount;
    }

    /// <summary>A list column of foreign keys (16 bytes each, the row index first).</summary>
    public IReadOnlyList<long> KeyList(int row, int offset) =>
        this.ListItems(row, offset, 16).Select(at => BitConverter.ToInt64(this.data, at)).ToList();

    /// <summary>A list column of keys into the same table (8 bytes each).</summary>
    public IReadOnlyList<long> RowList(int row, int offset) =>
        this.ListItems(row, offset, 8).Select(at => BitConverter.ToInt64(this.data, at)).ToList();

    /// <summary>A list column of 4-byte integers.</summary>
    public IReadOnlyList<int> Int32List(int row, int offset) =>
        this.ListItems(row, offset, 4).Select(at => BitConverter.ToInt32(this.data, at)).ToList();

    /// <summary>The row a foreign-key column points to, or -1 for a null key.</summary>
    public long Key(int row, int offset)
    {
        var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        return value is >= 0 and < int.MaxValue ? value : -1;
    }

    /// <summary>
    ///     A foreign-key column into a table of <paramref name="targetRows"/> rows: every row holds a null key or a row
    ///     index, with some variety (a column of zeros or small counts is not a key).
    /// </summary>
    public bool IsKeyColumn(int offset, int targetRows)
    {
        if (offset < 0 || offset + 8 > this.RowSize)
        {
            return false;
        }

        var distinct = new HashSet<long>();
        for (var row = 0; row < this.RowCount; row++)
        {
            var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
            if (value == NullKey)
            {
                continue;
            }

            if (value < 0 || value >= targetRows)
            {
                return false;
            }

            distinct.Add(value);
        }

        return distinct.Count >= MinDistinctKeys;
    }

    /// <summary>
    ///     A string column (a string in nearly every row) whose filled rows nearly all match, with enough of them filled
    ///     (a sound column is empty where the effect has no such sound).
    /// </summary>
    public bool IsStringColumn(int offset, Func<string, bool> matches)
    {
        int strings = 0, filled = 0, hits = 0;
        for (var row = 0; row < this.RowCount; row++)
        {
            if (this.StringOrNull(row, offset) is not { } text)
            {
                continue;
            }

            strings++;
            filled += text.Length > 0 ? 1 : 0;
            hits += text.Length > 0 && matches(text) ? 1 : 0;
        }

        return strings >= this.RowCount * NearlyAll && filled >= MinFilledRows && hits >= filled * NearlyAll;
    }

    private const double NearlyAll = 0.9;

    private const int MinFilledRows = 50;

    private const long NullKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

    private const int MinDistinctKeys = 10;

    private int RowStart(int row) => 4 + (row * this.RowSize);

    /// <summary>Byte positions of a list column's items; none when the count or position is implausible.</summary>
    private IEnumerable<int> ListItems(int row, int offset, int itemSize)
    {
        if (offset < 0 || offset + 16 > this.RowSize)
        {
            yield break;
        }

        var count = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        var start = BitConverter.ToInt64(this.data, this.RowStart(row) + offset + 8);
        if (count is <= 0 or > MaxListItems || start < 0 || this.varStart + start + (count * itemSize) > this.data.Length)
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            yield return this.varStart + (int)start + (i * itemSize);
        }
    }

    private const int MaxListItems = 4096;

    private string ReadString(long offset)
    {
        int start = this.varStart + (int)offset, end = start;
        while (end + 1 < this.data.Length && (this.data[end] != 0 || this.data[end + 1] != 0))
        {
            end += 2;
        }

        return Encoding.Unicode.GetString(this.data, start, end - start);
    }
}
