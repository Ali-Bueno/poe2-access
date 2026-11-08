// <copyright file="InventoryLayoutSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.States.InGameState;
    using static GameReader.Utils.StructShapes;

    /// <summary>
    ///     Where the server data keeps the local players, a player its inventories, and an inventory its size and its cells;
    ///     negative when not found.
    /// </summary>
    public readonly record struct InventoryFields(int Players, int Inventories, int Boxes, int Cells)
    {
        /// <summary>The offsets the host was written for.</summary>
        public static InventoryFields Measured { get; } = new(
            Marshal.OffsetOf<ServerDataOffsets>(nameof(ServerDataOffsets.PlayerServerDataPtr)).ToInt32(),
            Marshal.OffsetOf<ServerDataStructure>(nameof(ServerDataStructure.PlayerInventories)).ToInt32(),
            Marshal.OffsetOf<InventoryStruct>(nameof(InventoryStruct.TotalBoxes)).ToInt32(),
            Marshal.OffsetOf<InventoryStruct>(nameof(InventoryStruct.ItemList)).ToInt32());

        /// <summary>Gets how an inventory list's entry and an inventory's cell are laid out; as measured while not found.</summary>
        public InventoryEntries Entries { get; init; } = InventoryEntries.Measured;
    }

    /// <summary>
    ///     An entry of a player's inventory list (its size, where it keeps the inventory's id and where the inventory), and where
    ///     a cell of an inventory keeps its item.
    /// </summary>
    public readonly record struct InventoryEntries(int Stride, int Id, int Inventory, int Item)
    {
        /// <summary>The layout the host was written for.</summary>
        public static InventoryEntries Measured { get; } = new(
            Marshal.SizeOf<InventoryArrayStruct>(),
            Marshal.OffsetOf<InventoryArrayStruct>(nameof(InventoryArrayStruct.InventoryId)).ToInt32(),
            Marshal.OffsetOf<InventoryArrayStruct>(nameof(InventoryArrayStruct.InventoryPtr0)).ToInt32(),
            Marshal.OffsetOf<InventoryItemStruct>(nameof(InventoryItemStruct.Item)).ToInt32());
    }

    /// <summary>
    ///     Finds the player's inventories by what they hold: a short list of players, the first with a list of inventory
    ///     entries (distinct ids, the main inventory's among them, and a pointer to each inventory), each inventory X by Y cells
    ///     with one pointer per cell, and the items in those cells entities under <c>Metadata/Items/</c>
    ///     (docs/compatibility.md in the accessibility mod).
    /// </summary>
    public sealed class InventoryLayoutSearch
    {
        /// <summary>Bytes searched around each measured field.</summary>
        public const int Reach = 0x200;

        /// <summary>Bytes of a cell searched for its item: the cell is an item pointer and two slot pairs, 0x20 bytes in 0.5.5f.</summary>
        public const int CellBytes = 0x40;

        private const int Pointer = 8;
        private const int VectorBytes = 3 * Pointer;
        private const int MaxPlayers = 4;
        private const int MaxInventories = 128;
        private const int MaxSide = 64;
        private const int ItemsChecked = 4;
        private const string ItemFolder = "Metadata/Items/";

        // An entry holds an id and a pointer, so 0x10 bytes once aligned; a patch may grow it a little.
        private const int MinEntry = 0x10;
        private const int MaxEntry = 0x40;

        // Far above any inventory id the game has (the host's list ends below 100); the halves of a pointer are not.
        private const int MaxInventoryId = 0x400;

        // The entry's second pointer leads 0x10 bytes before the inventory (GameHelper's InventoryPtr1): the same inventory,
        // read from where the UI does not hold it, so it is never taken for the inventory.
        private const int InventoryBase = 0x10;

        private readonly IAreaMemory memory;
        private readonly Dictionary<(long, int), byte[]> blocks = new();

        /// <summary>Initializes a new instance of the <see cref="InventoryLayoutSearch"/> class.</summary>
        public InventoryLayoutSearch(IAreaMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>The fields from the server data at <paramref name="serverData"/>, nearest <paramref name="near"/>; all negative when no player holds an item.</summary>
        public InventoryFields Find(long serverData, InventoryFields near)
        {
            var server = this.Block(serverData, near.Players + Reach);
            var (inventories, boxes, cells, entries) = (-1, -1, -1, near.Entries);
            var players = Nearest(near.Players, server.Length - VectorBytes, at =>
            {
                var list = this.Pointers(server, at, MaxPlayers);
                if (list.Length == 0)
                {
                    return false;
                }

                var player = this.Block(list[0], near.Inventories + Reach);
                inventories = Nearest(near.Inventories, player.Length - VectorBytes, place =>
                {
                    var owned = this.Owned(player, place, near);
                    (boxes, cells, entries) = owned ?? (-1, -1, near.Entries);
                    return owned is not null;
                });
                return inventories >= 0;
            });
            return players < 0 ? new InventoryFields(-1, -1, -1, -1) : new InventoryFields(players, inventories, boxes, cells) { Entries = entries };
        }

        private static int Nearest(int start, int limit, Func<int, bool> fits) => StructShapes.Nearest(start, Pointer, limit, fits);

        private static bool IsSize(byte[] body, int at) => Int(body, at) is > 0 and <= MaxSide && Int(body, at + sizeof(int)) is > 0 and <= MaxSide;

        /// <summary>Distinct ids at <paramref name="id"/> of every entry, the main inventory's among them.</summary>
        private static bool IsIdList(byte[] entries, int stride, int id)
        {
            var ids = Enumerable.Range(0, entries.Length / stride).Select(index => Int(entries, (index * stride) + id)).ToList();
            return ids.All(value => value is > 0 and < MaxInventoryId) && ids.Distinct().Count() == ids.Count && ids.Contains((int)InventoryName.MainInventory1);
        }

        private byte[] Block(long address, int size)
        {
            if (!IsPointer(address) || size <= 0)
            {
                return Array.Empty<byte>();
            }

            if (!this.blocks.TryGetValue((address, size), out var block))
            {
                block = this.memory.Read(address, size);
                this.blocks[(address, size)] = block;
            }

            return block;
        }

        /// <summary>The entries of the vector at <paramref name="at"/>, of <paramref name="size"/> bytes each; empty when it is no such vector.</summary>
        private byte[] Vector(byte[] block, int at, int size, int max)
        {
            return !IsVector(block, at, out var first, out var bytes) || bytes <= 0 || bytes % size != 0 || bytes / size > max ? Array.Empty<byte>() : this.Block(first, (int)bytes);
        }

        private long[] Pointers(byte[] block, int at, int max)
        {
            var bytes = this.Vector(block, at, Pointer, max);
            var pointers = Enumerable.Range(0, bytes.Length / Pointer).Select(index => Long(bytes, index * Pointer)).ToArray();
            return pointers.Length > 0 && pointers.All(IsPointer) ? pointers : Array.Empty<long>();
        }

        /// <summary>
        ///     The size and cell fields and the entry layout under which the player's list at <paramref name="at"/> leads to
        ///     inventories that hold items; null when none does. The measured entry is tried first, then the smallest size that
        ///     fits (a multiple of the true size fits too, reading every other entry), each field nearest where measured.
        /// </summary>
        private (int Boxes, int Cells, InventoryEntries Entries)? Owned(byte[] player, int at, InventoryFields near)
        {
            var bytes = this.Vector(player, at, 1, MaxInventories * MaxEntry);
            if (bytes.Length == 0)
            {
                return null;
            }

            (int, int, InventoryEntries)? Try(InventoryEntries layout)
            {
                var owned = this.Inventories(bytes, layout);
                return owned.Length > 0 && this.Cells(owned, near, layout.Item) is { } found ? (found.Boxes, found.Cells, layout with { Item = found.Item }) : null;
            }

            if (Try(near.Entries) is { } measured)
            {
                return measured;
            }

            for (var stride = MinEntry; stride <= MaxEntry; stride += Pointer)
            {
                var size = stride;
                if (bytes.Length % size != 0)
                {
                    continue;
                }

                (int, int, InventoryEntries)? found = null;
                StructShapes.Nearest(near.Entries.Id, sizeof(int), size - sizeof(int), id => IsIdList(bytes, size, id) &&
                    Nearest(near.Entries.Inventory, size - Pointer, inventory =>
                    {
                        var layout = near.Entries with { Stride = size, Id = id, Inventory = inventory };
                        var apart = id + sizeof(int) <= inventory || id >= inventory + Pointer;
                        found = apart && layout != near.Entries ? Try(layout) : null;
                        return found is not null;
                    }) >= 0);
                if (found is { } layout)
                {
                    return layout;
                }
            }

            return null;
        }

        /// <summary>The inventories of a player's list laid out as <paramref name="layout"/>; empty when the list is no such list.</summary>
        private long[] Inventories(byte[] entries, InventoryEntries layout)
        {
            var count = entries.Length / layout.Stride;
            if (entries.Length % layout.Stride != 0 || count > MaxInventories || layout.Inventory + Pointer > layout.Stride || !IsIdList(entries, layout.Stride, layout.Id))
            {
                return Array.Empty<long>();
            }

            var held = Enumerable.Range(0, count).Select(index => index * layout.Stride).Where(entry => IsPointer(Long(entries, entry + layout.Inventory))).ToList();
            var inventories = held.Select(entry => Long(entries, entry + layout.Inventory)).ToArray();

            // A pointer that every entry also holds 0x10 higher in another slot is the inventory's base, not the inventory.
            var isBase = Enumerable.Range(0, layout.Stride / Pointer).Select(slot => slot * Pointer).Where(slot => slot != layout.Inventory).Any(slot =>
                held.All(entry => Long(entries, entry + slot) == Long(entries, entry + layout.Inventory) + InventoryBase));
            return inventories.Length > 0 && inventories.Length * 2 >= count && !isBase ? inventories : Array.Empty<long>();
        }

        /// <summary>
        ///     The size and cell fields nearest <paramref name="near"/> under which most inventories (three in four: a few are
        ///     never laid out) are X by Y cells with one pointer per cell, and their items, the first few checked, entities under
        ///     <c>Metadata/Items/</c>, with where a cell keeps its item (nearest <paramref name="item"/>); at least one item must be there.
        /// </summary>
        private (int Boxes, int Cells, int Item)? Cells(long[] inventories, InventoryFields near, int item)
        {
            var bodies = inventories.Select(inventory => this.Block(inventory, near.Cells + Reach)).Where(body => body.Length > 0).ToList();
            var (cells, found) = (-1, -1);
            var boxes = bodies.Count == 0 ? -1 : Nearest(near.Boxes, bodies.Min(body => body.Length) - Pointer, box =>
                bodies.Count(body => IsSize(body, box)) * 4 >= bodies.Count * 3 &&
                (cells = Nearest(near.Cells, bodies.Min(body => body.Length) - VectorBytes, list => list != box && (found = this.ItemField(bodies, box, list, item)) >= 0)) >= 0);
            return boxes < 0 ? null : (boxes, cells, found);
        }

        /// <summary>
        ///     Where the cells of the list at <paramref name="list"/> keep their item, nearest <paramref name="item"/>, when most
        ///     inventories are laid out as X by Y cells there; negative otherwise.
        /// </summary>
        private int ItemField(List<byte[]> bodies, int boxes, int list, int item)
        {
            var laidOut = bodies.Where(body => IsSize(body, boxes) && Long(body, list + Pointer) - Long(body, list) == Int(body, boxes) * Int(body, boxes + sizeof(int)) * Pointer).ToList();
            if (laidOut.Count * 4 < bodies.Count * 3)
            {
                return -1;
            }

            var items = new List<long>();
            foreach (var body in laidOut.TakeWhile(_ => items.Count < ItemsChecked))
            {
                var slots = this.Vector(body, list, Pointer, MaxSide * MaxSide);
                items.AddRange(Enumerable.Range(0, slots.Length / Pointer).Select(index => Long(slots, index * Pointer)).Where(IsPointer).Distinct().Take(ItemsChecked - items.Count));
            }

            bool LeadToItems(int at, int size) => items.All(cell => this.memory.EntityPath(Long(this.Block(cell, size), at)).StartsWith(ItemFolder, StringComparison.Ordinal));
            if (items.Count == 0)
            {
                return -1;
            }

            // The measured place is read with the measured size alone: a cell near the end of what the game allocated may not
            // have CellBytes behind it.
            return LeadToItems(item, item + Pointer) ? item : Nearest(item, CellBytes - Pointer, at => at != item && LeadToItems(at, CellBytes));
        }
    }
}
