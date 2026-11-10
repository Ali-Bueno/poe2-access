namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Where a dat key keeps its file and the file its name, and where a struct keeps a key, found by the table the file
    ///     names (<see cref="DatKey"/>, <see cref="DatKeySite"/>): pure, so the tests run it on synthetic memory.
    /// </summary>
    public static class DatKeySearch
    {
        /// <summary>How far into a key and into its file, and either side of a struct's measured key, things are looked for.</summary>
        public const int Reach = 0x40;

        // An MSVC wide string (pointer or inline text, size, capacity) as the file holds its name.
        private const int StringBytes = 0x20;

        /// <summary>
        ///     Where the key at <paramref name="key"/> keeps its file and the file its name, nearest <paramref name="near"/>, when
        ///     they name <paramref name="table"/>; null when none does. The row stays first: a key begins with it.
        /// </summary>
        public static (int File, int Name)? KeyLayout(Func<IntPtr, int, byte[]> read, Func<IntPtr, string?> text, IntPtr key, (int File, int Name) near, string table)
        {
            var block = read(key, Reach);
            if (block.Length < Reach)
            {
                return null;
            }

            (int File, int Name)? found = null;
            OffsetSearch.First(near.File, Reach, IntPtr.Size, at =>
            {
                var file = at + IntPtr.Size <= Reach ? new IntPtr(BitConverter.ToInt64(block, at)) : IntPtr.Zero;
                if (file == IntPtr.Zero)
                {
                    return false;
                }

                var name = OffsetSearch.First(near.Name, Reach, IntPtr.Size, place =>
                    place + StringBytes <= Reach && string.Equals(text(file + place), table, StringComparison.OrdinalIgnoreCase));
                found = name is { } place ? (at, place) : null;
                return found is not null;
            }, IntPtr.Size);
            return found;
        }

        /// <summary>
        ///     The place nearest <paramref name="near"/>, within <see cref="Reach"/> and not before 0, other than it, where
        ///     <paramref name="reads"/> finds a key of the table; null when none does.
        /// </summary>
        public static int? Site(int near, Func<int, bool> reads) => OffsetSearch.First(near, Reach, IntPtr.Size, at => at != near && reads(at));
    }
}
