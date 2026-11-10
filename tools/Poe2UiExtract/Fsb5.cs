using System;
using System.Collections.Generic;
using System.Text;

/// <summary>Reads the sample names of the FSB5 archive embedded in an FMOD Studio .bank (name table only).</summary>
internal static class Fsb5
{
    private const int HeaderBytes = 0x40;

    /// <summary>
    ///     Why a bank is not the container this reader knows (a RIFF "FEV " file holding an FSB5 archive whose header is
    ///     version 0 or 1 and whose sizes fit the bank; 0.5.x, 2026-11-10); null when it is.
    /// </summary>
    public static string? Problem(byte[] bank)
    {
        if (bank.Length < HeaderBytes || !bank.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bank.AsSpan(8, 4).SequenceEqual("FEV "u8))
        {
            return "it does not start as a RIFF \"FEV \" file";
        }

        var at = bank.AsSpan().IndexOf("FSB5"u8);
        if (at < 0 || at + HeaderBytes > bank.Length)
        {
            return "it holds no FSB5 archive";
        }

        var version = BitConverter.ToUInt32(bank, at + 4);
        if (version > 1)
        {
            return $"its FSB5 header is version {version}; this reader knows 0 and 1";
        }

        long count = BitConverter.ToUInt32(bank, at + 8), headers = BitConverter.ToUInt32(bank, at + 12), names = BitConverter.ToUInt32(bank, at + 16), data = BitConverter.ToUInt32(bank, at + 20);
        return count > 0 && at + (version == 1 ? 0x3C : 0x40) + headers + names + data <= bank.Length
            ? null
            : $"its FSB5 header promises {count} samples in {headers + names + data} bytes, more than the bank holds";
    }

    public static List<string> Names(byte[] bank)
    {
        var names = new List<string>();
        for (var at = bank.AsSpan().IndexOf("FSB5"u8); at >= 0;)
        {
            // Header: magic, version, sample count, sample-header size, name-table size, data size, mode.
            var version = BitConverter.ToUInt32(bank, at + 4);
            var count = BitConverter.ToInt32(bank, at + 8);
            var headerSize = BitConverter.ToInt32(bank, at + 12);
            var table = at + (version == 1 ? 0x3C : 0x40) + headerSize;
            for (var i = 0; i < count; i++)
            {
                var start = table + BitConverter.ToInt32(bank, table + (i * 4));
                var end = Array.IndexOf(bank, (byte)0, start);
                names.Add(Encoding.ASCII.GetString(bank, start, end - start));
            }

            var next = bank.AsSpan(at + 4).IndexOf("FSB5"u8);
            at = next < 0 ? -1 : at + 4 + next;
        }

        return names;
    }
}
