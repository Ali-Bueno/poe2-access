// <copyright file="Inventory.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using Coroutine;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Knows how to parse player, NPC, Crafting, stash inventories available in ServerData
    ///     and get the items available in them.
    /// </summary>
    public class Inventory : RemoteObjectBase
    {
        /// <summary>
        ///     This array stores items addresses in a given inventory.
        ///     Items addresses are in order w.r.t inventory slots. There might be duplicates or IntPtr.Zero
        ///     in case an item holds 2 slots or there is no item in the slot respectively.
        /// </summary>
        private IntPtr[] itemsToInventorySlotMapping = Array.Empty<IntPtr>();

        /// <summary>
        ///     The items are re-read this often (every item a block read plus one per cached component). GameHelper's
        ///     overlay wanted 50 Hz; the mod reads belts every 500 ms (FlaskWatch), so 10 Hz keeps them fresh.
        /// </summary>
        private const double UpdateSeconds = 0.1;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Inventory" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        /// <param name="name">name of the inventory for displaying purposes.</param>
        internal Inventory(IntPtr address, string name)
            : base(address)
        {
            CoroutineHandler.Start(
                this.OnTimeTick(), $"[Inventory] Update {name}", int.MaxValue - 4);
        }

        /// <summary>
        ///     Gets a value indicating total number of boxes in the inventory.
        /// </summary>
        public StdTuple2D<int> TotalBoxes { get; private set; }

        /// <summary>
        ///     Gets all the items in the inventory.
        /// </summary>
        public ConcurrentDictionary<IntPtr, Item> Items { get; } =
            new();

        /// <summary>
        ///     Gets the item at the specific slot in the inventory.
        ///     Always check if the returned item IsValid or not by comparing
        ///     Item Address with IntPtr.Zero.
        /// </summary>
        /// <param name="y">Inventory slot row, starting from 0.</param>
        /// <param name="x">Inventory slot column, starting from 0.</param>
        /// <returns>Item on the given slot.</returns>
        public Item this[int y, int x]
        {
            get
            {
                if (y >= this.TotalBoxes.Y || x >= this.TotalBoxes.X)
                {
                    return new Item(IntPtr.Zero);
                }

                var index = y * this.TotalBoxes.X + x;
                if (index >= this.itemsToInventorySlotMapping.Length)
                {
                    return new Item(IntPtr.Zero);
                }

                var itemAddr = this.itemsToInventorySlotMapping[index];
                if (itemAddr == IntPtr.Zero)
                {
                    return new Item(IntPtr.Zero);
                }

                if (this.Items.TryGetValue(itemAddr, out var item))
                {
                    return item;
                }

                return new Item(IntPtr.Zero);
            }
        }

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.TotalBoxes = default;
            this.itemsToInventorySlotMapping = Array.Empty<IntPtr>();
            this.Items.Clear();
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var invInfo = reader.ReadMemory<InventoryStruct>(this.Address);
            this.TotalBoxes = invInfo.TotalBoxes;
            this.itemsToInventorySlotMapping = reader.ReadStdVector<IntPtr>(invInfo.ItemList);
            if (hasAddressChanged)
            {
                this.Items.Clear();
            }

            foreach (var item in this.Items)
            {
                item.Value.IsValid = false;
            }

            // A belt or bag holds a few dozen items: one thread reads them quicker than the pool wakes for them.
            foreach (var invItemPtr in this.itemsToInventorySlotMapping.Distinct())
            {
                if (invItemPtr != IntPtr.Zero)
                {
                    // The list is torn while an area loads: a cell that cannot be read is skipped, without a log line each.
                    if (!reader.TryReadMemory<InventoryItemStruct>(invItemPtr, out var invItem))
                    {
                        continue;
                    }

                    if (this.Items.ContainsKey(invItemPtr))
                    {
                        this.Items[invItemPtr].Address = invItem.Item;
                    }
                    else
                    {
                        var item = new Item(invItem.Item);
                        if (!string.IsNullOrEmpty(item.Path))
                        {
                            this.Items.TryAdd(invItemPtr, item);
                        }
                    }
                }
            }

            foreach (var item in this.Items)
            {
                if (!item.Value.IsValid)
                {
                    this.Items.TryRemove(item.Key, out _);
                }
            }
        }

        private IEnumerable<Wait> OnTimeTick()
        {
            while (true)
            {
                yield return new Wait(UpdateSeconds);
                try
                {
                    if (this.Address != IntPtr.Zero)
                    {
                        this.UpdateData(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Inventory.OnTimeTick] {ex}");
                }
            }
        }
    }
}
