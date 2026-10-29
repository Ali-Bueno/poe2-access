namespace Poe2Accessibility.Services
{
    using System;
    using GameHelper.RemoteObjects.Components;

    /// <summary>Reads a minimap icon's label from its MinimapIcons.dat row in game memory (<see cref="GameLayout.MinimapIconRow"/>).</summary>
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

        private static string LabelAt(HostMemoryReader memory, UiElementReader reader, IntPtr row) =>
            memory.TryRead<IntPtr>(row + GameLayout.MinimapIconRow.LabelOffset, out var text) && text != IntPtr.Zero
                ? reader.ReadUtf16BufferAt(text, 80)?.Trim() ?? string.Empty
                : string.Empty;
    }
}
