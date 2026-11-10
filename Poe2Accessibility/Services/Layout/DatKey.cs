namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     A dat foreign key in game memory, (row, file) (<see cref="GameLayout.SkillSlot"/>), and where the key keeps its file and
    ///     the file its name: tried where measured, and found again by the table name the caller expects when a patch moved them.
    /// </summary>
    public static class DatKey
    {
        /// <summary>The part's name in the layout report.</summary>
        public const string Part = "dat keys";

        // A key that does not read is searched at most this often until a key has read: a caller's wrong key costs little.
        private const int SearchEveryMs = 1000;

        private static readonly object Gate = new();
        private static (int File, int Name) current = Measured;
        private static (int File, int Name, string Table)? candidate;
        private static bool settled;
        private static int generation = -1;
        private static long nextSearchAt;

        /// <summary>Gets where the key keeps its file and the file its name in the build the mod was written for.</summary>
        public static (int File, int Name) Measured => (GameLayout.SkillSlot.KeyFileOffset, GameLayout.SkillSlot.FileNameOffset);

        /// <summary>Gets where a key keeps its file now.</summary>
        public static int FileOffset => Layout().File;

        /// <summary>Gets where a dat file keeps its name now.</summary>
        public static int NameOffset => Layout().Name;

        /// <summary>
        ///     The key's row, only when its file is the expected table. A key that does not read so, before any has, is searched:
        ///     the file pointer and its name nearest where measured that name <paramref name="table"/>. What it finds reads this key;
        ///     it becomes the layout of every key once a key of another table reads through it too.
        /// </summary>
        public static IntPtr? Row(HostMemoryReader reader, UiElementReader elements, IntPtr key, string table)
        {
            if (!reader.TryRead<IntPtr>(key, out var row) || row == IntPtr.Zero)
            {
                return null;
            }

            var layout = Layout();
            if (Names(reader, elements, key, layout, table))
            {
                Settle(layout);
                return row;
            }

            (int File, int Name, string Table)? found;
            lock (Gate)
            {
                found = candidate;
            }

            if (found is { } known && Names(reader, elements, key, (known.File, known.Name), table))
            {
                if (!string.Equals(known.Table, table, StringComparison.OrdinalIgnoreCase))
                {
                    Settle((known.File, known.Name));
                }

                return row;
            }

            return Search(reader, elements, key, table) ? row : null;
        }

        private static bool Names(HostMemoryReader reader, UiElementReader elements, IntPtr key, (int File, int Name) layout, string table) =>
            reader.TryRead<IntPtr>(key + layout.File, out var file) && file != IntPtr.Zero &&
            string.Equals(elements.ReadWStringStructAt(file + layout.Name), table, StringComparison.OrdinalIgnoreCase);

        /// <summary>The layout now; a new game process (the report reset) judges it again from where measured.</summary>
        private static (int File, int Name) Layout()
        {
            var now = CompatibilityReport.Current?.Generation ?? 0;
            lock (Gate)
            {
                if (now != generation)
                {
                    (generation, current, candidate, settled) = (now, Measured, null, false);
                }

                return current;
            }
        }

        private static void Settle((int File, int Name) layout)
        {
            lock (Gate)
            {
                if (settled && layout == current)
                {
                    return;
                }

                (current, candidate, settled) = (layout, null, true);
            }

            var measured = Measured;
            if (layout == measured)
            {
                CompatibilityReport.Current?.Found(Part);
            }
            else
            {
                var moved = layout.File != measured.File ? (measured.File, layout.File) : (measured.Name, layout.Name);
                CompatibilityReport.Current?.Moved(Part, moved.Item1, moved.Item2);
            }
        }

        private static bool Search(HostMemoryReader reader, UiElementReader elements, IntPtr key, string table)
        {
            lock (Gate)
            {
                if (settled || Environment.TickCount64 < nextSearchAt)
                {
                    return false;
                }

                nextSearchAt = Environment.TickCount64 + SearchEveryMs;
            }

            var layout = Layout();
            if (DatKeySearch.KeyLayout(reader.ReadBytes, elements.ReadWStringStructAt, key, layout, table) is not { } found || found == layout)
            {
                return false;
            }

            lock (Gate)
            {
                candidate = (found.File, found.Name, table);
            }

            return true;
        }
    }
}
