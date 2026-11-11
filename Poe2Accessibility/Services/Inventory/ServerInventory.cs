namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     One server inventory read on demand through the host's own layout (<see cref="InventoryStruct"/>,
    ///     <see cref="InventoryItemStruct"/>): no reader object kept per inventory, for the ones looked at now and then.
    /// </summary>
    public static class ServerInventory
    {
        /// <summary>The stash's largest tab holds 24 x 24 cells; more slots than this is not an inventory.</summary>
        private const int MaxSlots = 4096;

        /// <summary>Each item's entity and the cell its top left corner is in.</summary>
        public static IReadOnlyList<(IntPtr Entity, int X, int Y)> Items(HostMemoryReader reader, IntPtr inventory)
        {
            var items = new List<(IntPtr Entity, int X, int Y)>();
            if (inventory == IntPtr.Zero || !reader.TryRead<InventoryStruct>(inventory, out var info))
            {
                return items;
            }

            var seen = new HashSet<IntPtr>();
            foreach (var slot in reader.ReadStdVector<IntPtr>(info.ItemList, MaxSlots))
            {
                if (slot != IntPtr.Zero && seen.Add(slot) && reader.IsValidAddress(slot) &&
                    reader.TryRead<InventoryItemStruct>(slot, out var item) && item.Item != IntPtr.Zero)
                {
                    items.Add((item.Item, item.SlotStart.X, item.SlotStart.Y));
                }
            }

            return items;
        }

        /// <summary>
        ///     How many cells the first item of an inventory spans, columns and rows; (0, 0) when it holds none. For the
        ///     cursor's inventory, the item in the hand. Source: the host's <see cref="InventoryItemStruct"/>, SlotEnd (+0x10)
        ///     less SlotStart (+0x08), GameHelper's own layout; validated live 2026-11-11 with a short bow in the hand, which
        ///     read 2 x 4 as the frame the game draws around its place.
        /// </summary>
        public static (int Columns, int Rows) FirstSpan(HostMemoryReader reader, IntPtr inventory)
        {
            if (inventory == IntPtr.Zero || !reader.TryRead<InventoryStruct>(inventory, out var info))
            {
                return (0, 0);
            }

            foreach (var slot in reader.ReadStdVector<IntPtr>(info.ItemList, MaxSlots))
            {
                if (slot != IntPtr.Zero && reader.IsValidAddress(slot) && reader.TryRead<InventoryItemStruct>(slot, out var item) && item.Item != IntPtr.Zero)
                {
                    var (columns, rows) = (item.SlotEnd.X - item.SlotStart.X, item.SlotEnd.Y - item.SlotStart.Y);
                    return columns is > 0 and <= MaxSpan && rows is > 0 and <= MaxSpan ? (columns, rows) : (0, 0);
                }
            }

            return (0, 0);
        }

        /// <summary>No item is wider or taller than this many cells (a two-handed weapon is 2 x 4): more is a misread.</summary>
        private const int MaxSpan = 8;
    }
}
