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
    }
}
