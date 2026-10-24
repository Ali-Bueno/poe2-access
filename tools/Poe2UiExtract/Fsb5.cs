using System.Text;

/// <summary>Reads the sample names of the FSB5 archive embedded in an FMOD Studio .bank (name table only).</summary>
internal static class Fsb5
{
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
