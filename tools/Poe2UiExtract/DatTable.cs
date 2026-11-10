using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;

/// <summary>
///     Minimal reader for an English .datc64 table of the game's Bundles2 (row count, fixed-size rows, then a variable
///     section after the 0xBB marker). Fields are read by byte offset inside a row: strings are 8-byte offsets into the
///     variable section (UTF-16LE), foreign keys are an 8-byte row index (a null key is all 0xFE bytes).
/// </summary>
internal sealed partial class DatTable
{
    private static readonly byte[] Marker = { 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB, 0xBB };
    private readonly byte[] data;
    private readonly int varStart;

    private const int HeaderBytes = sizeof(int);

    private DatTable(byte[] data, string name)
    {
        this.data = data;
        this.Name = name;
        if (data.Length < HeaderBytes + Marker.Length)
        {
            throw Unreadable(name, $"{data.Length} bytes is too short for a row count and the variable-data marker");
        }

        this.RowCount = BitConverter.ToInt32(data, 0);
        if (this.RowCount <= 0)
        {
            throw Unreadable(name, $"the header says {this.RowCount} rows");
        }

        var marker = data.AsSpan(HeaderBytes).IndexOf(Marker);
        if (marker < 0)
        {
            throw Unreadable(name, "the variable-data marker (eight 0xBB bytes) is not in the file");
        }

        this.varStart = marker + HeaderBytes;
        if (marker % this.RowCount != 0 || marker == 0)
        {
            throw Unreadable(name, $"the marker is at byte {this.varStart}, which {this.RowCount} rows from the header cannot reach: the file is not a whole number of rows");
        }

        this.RowSize = marker / this.RowCount;
        this.reads = new byte[this.RowSize];
    }

    private static InvalidDataException Unreadable(string name, string why) => new($"{name}: not a table this reader understands ({why})");

    public int RowCount { get; }

    public int RowSize { get; }

    /// <summary>What the table was opened as: its path in the bundles.</summary>
    public string Name { get; }

    /// <summary>The bytes of the variable section, its marker included: string and list offsets count from its start.</summary>
    public int VarLength => this.data.Length - this.varStart;

    /// <summary>A table from the bytes of a .datc64 file (the row count, the rows, the 0xBB marker, the variable section).</summary>
    public static DatTable FromBytes(byte[] data, string name = "table") => new(data, name);

    public string String(int row, int offset)
    {
        this.Note(offset, DatRead.Text);
        return this.ReadString(BitConverter.ToInt64(this.data, this.RowStart(row) + offset));
    }

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

        this.Note(offset, DatRead.Text);
        var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        return this.IsStringStart(value) ? this.ReadString(value) : null;
    }

    /// <summary>8 bytes at any offset of a row, aligned or not (GrantedEffects' animation key sits at +0x71).</summary>
    public long Int64(int row, int offset)
    {
        this.Note(offset, DatRead.Key);
        return this.Raw64(row, offset);
    }

    /// <summary>8 bytes of a row for a judge of the bytes, not counted as a column read; -1 past the row's end.</summary>
    internal long Raw64(int row, int offset) => offset >= 0 && offset + 8 <= this.RowSize ? BitConverter.ToInt64(this.data, this.RowStart(row) + offset) : -1;

    /// <summary>One byte of a row for a judge of the bytes; -1 past the row's end.</summary>
    internal int RawByte(int row, int offset) => offset >= 0 && offset < this.RowSize ? this.data[this.RowStart(row) + offset] : -1;

    /// <summary>8 bytes of the variable section at a position counted from its marker; -1 past its end.</summary>
    internal long Pool64(long position) => position >= 0 && position <= this.VarLength - 8 ? BitConverter.ToInt64(this.data, this.varStart + (int)position) : -1;

    /// <summary>A list column of strings: a count, then where in the variable section its string offsets start.</summary>
    public IReadOnlyList<string> StringList(int row, int offset)
    {
        this.Note(offset, DatRead.TextList);
        return this.ListItems(row, offset, 8).Select(at => this.ReadString(BitConverter.ToInt64(this.data, at))).ToList();
    }

    /// <summary>4 bytes at any offset of a row; -1 past its end.</summary>
    public int Int32(int row, int offset)
    {
        this.Note(offset, DatRead.Number);
        return offset >= 0 && offset + 4 <= this.RowSize ? BitConverter.ToInt32(this.data, this.RowStart(row) + offset) : -1;
    }

    /// <summary>A one-byte flag column.</summary>
    public bool Bool(int row, int offset)
    {
        this.Note(offset, DatRead.Flag);
        return offset >= 0 && offset < this.RowSize && this.data[this.RowStart(row) + offset] != 0;
    }

    /// <summary>One byte at any offset of a row; -1 past its end.</summary>
    public int Byte(int row, int offset)
    {
        this.Note(offset, DatRead.Number);
        return this.RawByte(row, offset);
    }

    /// <summary>2 bytes at any offset of a row; -1 past its end.</summary>
    public int UInt16(int row, int offset)
    {
        this.Note(offset, DatRead.Number);
        return offset >= 0 && offset + 2 <= this.RowSize ? BitConverter.ToUInt16(this.data, this.RowStart(row) + offset) : -1;
    }

    /// <summary>A one-byte flag column: every row holds 0 or 1, and both occur.</summary>
    public bool IsFlagColumn(int offset)
    {
        using var probe = Probe();

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
    public IReadOnlyList<long> KeyList(int row, int offset)
    {
        this.Note(offset, DatRead.KeyList);
        return this.ListItems(row, offset, 16).Select(at => BitConverter.ToInt64(this.data, at)).ToList();
    }

    /// <summary>A list column of keys into the same table (8 bytes each).</summary>
    public IReadOnlyList<long> RowList(int row, int offset)
    {
        this.Note(offset, DatRead.RowList);
        return this.ListItems(row, offset, 8).Select(at => BitConverter.ToInt64(this.data, at)).ToList();
    }

    /// <summary>A list column of 4-byte integers.</summary>
    public IReadOnlyList<int> Int32List(int row, int offset)
    {
        this.Note(offset, DatRead.NumberList);
        return this.ListItems(row, offset, 4).Select(at => BitConverter.ToInt32(this.data, at)).ToList();
    }

    /// <summary>The row a foreign-key column points to, or -1 for a null key.</summary>
    public long Key(int row, int offset)
    {
        this.Note(offset, DatRead.Key);
        var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        return value is >= 0 and < int.MaxValue ? value : -1;
    }

    /// <summary>
    ///     A foreign-key column into a table of <paramref name="targetRows"/> rows: every row holds a null key or a row
    ///     index, with some variety (a column of zeros or small counts is not a key).
    /// </summary>
    public bool IsKeyColumn(int offset, int targetRows)
    {
        using var probe = Probe();

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
        using var probe = Probe();

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

    /// <summary>A list column of strings: every item of every row matches, with enough rows filled.</summary>
    public bool IsStringListColumn(int offset, Func<string, bool> matches)
    {
        using var probe = Probe();

        var filled = 0;
        for (var row = 0; row < this.RowCount; row++)
        {
            var items = this.ListItems(row, offset, 8).ToList();
            filled += items.Count > 0 ? 1 : 0;
            foreach (var at in items)
            {
                var value = BitConverter.ToInt64(this.data, at);
                if (value < Marker.Length || value >= this.data.Length - this.varStart || !matches(this.ReadString(value)))
                {
                    return false;
                }
            }
        }

        return filled >= MinFilledRows;
    }

    /// <summary>A list column of keys into a table of <paramref name="targetRows"/> rows, with enough rows filled.</summary>
    public bool IsKeyListColumn(int offset, int targetRows)
    {
        using var probe = Probe();

        var filled = 0;
        for (var row = 0; row < this.RowCount; row++)
        {
            var keys = this.KeyList(row, offset);
            filled += keys.Count > 0 ? 1 : 0;
            if (keys.Any(key => key != NullKey && (key < 0 || key >= targetRows)))
            {
                return false;
            }
        }

        return filled >= MinFilledRows;
    }

    private const double NearlyAll = 0.9;

    private const int MinFilledRows = 50;

    private const long NullKey = unchecked((long)0xFEFEFEFEFEFEFEFE);

    private const int MinDistinctKeys = 10;

    private int RowStart(int row) => 4 + (row * this.RowSize);

    /// <summary>Whether a variable-section offset is the first character of a string: past the marker and after a NUL pair.</summary>
    internal bool IsStringStart(long value)
    {
        // Against the section's length, never added to its start: the 8 bytes a search tries at every offset can be any
        // number, and one near the top of the range wrapped the sum to a negative place and threw, failing the step.
        if (value < Marker.Length || value >= this.VarLength)
        {
            return false;
        }

        var at = this.varStart + (int)value;
        return value == Marker.Length || (this.data[at - 1] == 0 && this.data[at - 2] == 0);
    }

    /// <summary>Byte positions of a list column's items; none when the count or position is implausible.</summary>
    private IEnumerable<int> ListItems(int row, int offset, int itemSize)
    {
        if (offset < 0 || offset + 16 > this.RowSize)
        {
            yield break;
        }

        var count = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        var start = BitConverter.ToInt64(this.data, this.RowStart(row) + offset + 8);
        if (count is <= 0 or > MaxListItems || start < 0 || start > this.VarLength - (count * itemSize))
        {
            yield break;
        }

        for (var i = 0; i < count; i++)
        {
            yield return this.varStart + (int)start + (i * itemSize);
        }
    }

    internal const int MaxListItems = 4096;

    /// <summary>Whether the string at a variable-section offset has any character.</summary>
    internal bool IsFilledString(long value) => value >= 0 && value < this.VarLength - 1 && (this.data[this.varStart + (int)value] != 0 || this.data[this.varStart + (int)value + 1] != 0);

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
