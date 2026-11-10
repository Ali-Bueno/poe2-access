using System;
using System.Collections.Generic;

/// <summary>How an extractor read a column: what it took the bytes there for.</summary>
[Flags]
internal enum DatRead : byte
{
    None = 0,
    Text = 1,
    Key = 2,
    TextList = 4,
    KeyList = 8,
    RowList = 0x10,
    NumberList = 0x20,
    Flag = 0x40,
    Number = 0x80,
}

/// <summary>
///     Which columns of the table were read, and as what, so <see cref="DatColumnAudit"/> can ask the bytes for a second
///     opinion on each. A search trying every offset is not a read: it runs inside <see cref="Probe"/>.
/// </summary>
internal sealed partial class DatTable
{
    [ThreadStatic]
    private static int probing;

    private readonly byte[] reads;

    /// <summary>Until disposed, this thread's reads are a search's trials and are not counted as columns read.</summary>
    public static ProbeScope Probe()
    {
        probing++;
        return new ProbeScope(true);
    }

    /// <summary>Every column read outside a search, with how it was read.</summary>
    public IEnumerable<(int Offset, DatRead Read)> Reads()
    {
        for (var offset = 0; offset < this.reads.Length; offset++)
        {
            if (this.reads[offset] != 0)
            {
                yield return (offset, (DatRead)this.reads[offset]);
            }
        }
    }

    /// <summary>Counts the reads of another opening of the same table as this one's.</summary>
    public void MergeReads(DatTable other)
    {
        for (var offset = 0; offset < this.reads.Length && offset < other.reads.Length; offset++)
        {
            this.reads[offset] |= other.reads[offset];
        }
    }

    private void Note(int offset, DatRead read)
    {
        if (probing == 0 && (uint)offset < (uint)this.reads.Length)
        {
            this.reads[offset] |= (byte)read;
        }
    }

    internal readonly struct ProbeScope : IDisposable
    {
        private readonly bool entered;

        internal ProbeScope(bool entered) => this.entered = entered;

        public void Dispose()
        {
            if (this.entered)
            {
                probing--;
            }
        }
    }
}
