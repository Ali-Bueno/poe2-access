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

    /// <summary>The row a foreign-key column points to, or -1 for a null key.</summary>
    public long Key(int row, int offset)
    {
        var value = BitConverter.ToInt64(this.data, this.RowStart(row) + offset);
        return value is >= 0 and < int.MaxValue ? value : -1;
    }

    private int RowStart(int row) => 4 + (row * this.RowSize);

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
