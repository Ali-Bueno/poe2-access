namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>
    ///     Which essences a crystal holds, the names a sighted player reads on it before deciding to break it: the rows of the
    ///     Essences table the server sent, kept in the crystal's component (<see cref="GameLayout.MonolithComponent"/>). A row is
    ///     believed only when its first key leads to an item whose file is under Metadata/Items/.
    /// </summary>
    public static class EssenceContents
    {
        private const string Items = "Metadata/Items/";
        private const int MaxIdChars = 128;

        /// <summary>No crystal holds more (the script's own count goes to a handful).</summary>
        private const int MostEssences = 12;

        /// <summary>The item ids of the essences held ("CurrencyEssenceFire1"), in the game's order; none when the component does not read.</summary>
        public static IReadOnlyList<string> Of(HostMemoryReader memory, UiElementReader elements, Entity crystal)
        {
            if (EntityComponents.AddressOf(crystal, GameLayout.MonolithComponent.Name) is not { } component ||
                !memory.TryRead<IntPtr>(component + GameLayout.MonolithComponent.EssencesOffset, out var first) ||
                !memory.TryRead<IntPtr>(component + GameLayout.MonolithComponent.EssencesOffset + IntPtr.Size, out var last) || !memory.IsValidAddress(first))
            {
                return Array.Empty<string>();
            }

            // A table row is kept as a pair, the row and its table, or alone: whichever stride makes every element an essence.
            var bytes = last.ToInt64() - first.ToInt64();
            foreach (var stride in new[] { 2 * IntPtr.Size, IntPtr.Size })
            {
                if (bytes <= 0 || bytes % stride != 0 || bytes / stride > MostEssences)
                {
                    continue;
                }

                var ids = new List<string>();
                for (var at = first; at.ToInt64() < last.ToInt64(); at += stride)
                {
                    if (ItemIdOf(memory, elements, at) is not { } id)
                    {
                        ids.Clear();
                        break;
                    }

                    ids.Add(id);
                }

                if (ids.Count > 0)
                {
                    return ids;
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>The last part of the item file an Essences row names; null when the pointers do not lead to one.</summary>
        private static string? ItemIdOf(HostMemoryReader memory, UiElementReader elements, IntPtr element) =>
            memory.TryRead<IntPtr>(element, out var row) && memory.IsValidAddress(row) &&
            memory.TryRead<IntPtr>(row, out var item) && memory.IsValidAddress(item) &&
            memory.TryRead<IntPtr>(item, out var text) && memory.IsValidAddress(text) &&
            elements.ReadUtf16BufferAt(text, MaxIdChars) is { } path && path.StartsWith(Items, StringComparison.Ordinal)
                ? path[(path.LastIndexOf('/') + 1)..]
                : null;
    }
}
