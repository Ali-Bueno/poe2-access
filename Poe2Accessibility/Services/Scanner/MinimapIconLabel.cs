namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Concurrent;
    using GameReader.RemoteObjects.Components;

    /// <summary>Reads a minimap icon's label and arrow reach from its MinimapIcons.dat row in game memory (<see cref="GameLayout.MinimapIconRow"/>).</summary>
    public static class MinimapIconLabel
    {
        /// <summary>
        ///     The icon's label in the client's language; for a spent "…Inactive" icon, the label of its "…Active" row
        ///     beside it (the table keeps each pair together), so a slain boss's arena keeps its name.
        /// </summary>
        public static string Of(HostMemoryReader memory, UiElementReader reader, MinimapIcon icon)
        {
            if (!memory.TryRead<IntPtr>(icon.Address + GameLayout.MinimapIconRow.ComponentRowOffset, out var row) || row == IntPtr.Zero)
            {
                return string.Empty;
            }

            var own = LabelAt(memory, reader, row);
            if (own.Length == 0 && EndgameCatalog.Shared.DeviceName(icon.IconName ?? string.Empty, Localization.Language) is { Length: > 0 } objectiveWord)
            {
                return objectiveWord; // the Breach icon has no label: "Fisura", the word its map objective links
            }

            if (own.Length > 0 || icon.IconName is not { } name || !MinimapMarks.IsSpent(name))
            {
                return own;
            }

            var active = name.Replace("InActive", "Active", StringComparison.Ordinal).Replace("Inactive", "Active", StringComparison.Ordinal);
            foreach (var step in new[] { -1, 1 })
            {
                var other = row + (step * GameLayout.MinimapIconRow.RowSize);
                if (memory.TryRead<IntPtr>(other, out var id) && id != IntPtr.Zero && reader.ReadUtf16BufferAt(id, 80) == active)
                {
                    return LabelAt(memory, reader, other);
                }
            }

            return string.Empty;
        }

        /// <summary>The table's rows never change while the game runs: each icon's reach is read once.</summary>
        private static readonly ConcurrentDictionary<string, int> Reaches = new(StringComparer.Ordinal);

        /// <summary>How far, in grid cells, the minimap's edge arrow points to the icon; 0 when the row cannot be read.</summary>
        public static int PointerCells(HostMemoryReader memory, MinimapIcon icon)
        {
            var name = icon.IconName ?? string.Empty;
            if (Reaches.TryGetValue(name, out var known))
            {
                return known;
            }

            if (!memory.TryRead<IntPtr>(icon.Address + GameLayout.MinimapIconRow.ComponentRowOffset, out var row) || row == IntPtr.Zero ||
                !memory.TryRead<int>(row + GameLayout.MinimapIconRow.PointerDistanceOffset, out var cells))
            {
                return 0;
            }

            cells = Math.Max(0, cells);
            if (name.Length > 0)
            {
                Reaches[name] = cells;
            }

            return cells;
        }

        private static string LabelAt(HostMemoryReader memory, UiElementReader reader, IntPtr row) =>
            memory.TryRead<IntPtr>(row + GameLayout.MinimapIconRow.LabelOffset, out var text) && text != IntPtr.Zero
                ? reader.ReadUtf16BufferAt(text, 80)?.Trim() ?? string.Empty
                : string.Empty;
    }
}
