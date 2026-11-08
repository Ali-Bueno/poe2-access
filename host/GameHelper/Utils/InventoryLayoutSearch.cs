// <copyright file="InventoryLayoutSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.States.InGameState;

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
    }

    /// <summary>
    ///     Finds the player's inventories by what they hold: a short list of players, the first with a list of inventory
    ///     entries, each inventory X by Y cells with one pointer per cell, and the items in those cells entities under
    ///     <c>Metadata/Items/</c> (docs/compatibility.md in the accessibility mod).
    /// </summary>
    public sealed class InventoryLayoutSearch
    {
        /// <summary>Bytes searched around each measured field.</summary>
        public const int Reach = 0x200;

        private const long MinPointer = 0x10000;
        private const long MaxPointer = 0x7FFFFFFFFFFF;
        private const int Pointer = 8;
        private const int VectorBytes = 3 * Pointer;
        private const int MaxPlayers = 4;
        private const int MaxInventories = 128;
        private const int MaxSide = 64;
        private const int ItemsChecked = 4;
        private const string ItemFolder = "Metadata/Items/";

        private static readonly int Entry = Marshal.SizeOf<InventoryArrayStruct>();
        private static readonly int EntryInventory = Marshal.OffsetOf<InventoryArrayStruct>(nameof(InventoryArrayStruct.InventoryPtr0)).ToInt32();
        private static readonly int CellItem = Marshal.OffsetOf<InventoryItemStruct>(nameof(InventoryItemStruct.Item)).ToInt32();

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
            var (inventories, boxes, cells) = (-1, -1, -1);
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
                    var owned = this.Inventories(player, place);
                    (boxes, cells) = owned.Length == 0 ? (-1, -1) : this.Cells(owned, near);
                    return boxes >= 0;
                });
                return inventories >= 0;
            });
            return players < 0 ? new InventoryFields(-1, -1, -1, -1) : new InventoryFields(players, inventories, boxes, cells);
        }

        private static int Nearest(int start, int limit, Func<int, bool> fits)
        {
            for (var distance = 0; distance <= Reach; distance += Pointer)
            {
                if (start + distance <= limit && fits(start + distance))
                {
                    return start + distance;
                }

                if (distance > 0 && start - distance >= 0 && start - distance <= limit && fits(start - distance))
                {
                    return start - distance;
                }
            }

            return -1;
        }

        private static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        private static long Long(byte[] bytes, int at) => at >= 0 && at + Pointer <= bytes.Length ? BitConverter.ToInt64(bytes, at) : 0;

        private static int Int(byte[] bytes, int at) => at >= 0 && at + sizeof(int) <= bytes.Length ? BitConverter.ToInt32(bytes, at) : 0;

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
            var (first, last, end) = (Long(block, at), Long(block, at + Pointer), Long(block, at + (2 * Pointer)));
            var bytes = last - first;
            return !IsPointer(first) || bytes <= 0 || bytes % size != 0 || bytes / size > max || end < last ? Array.Empty<byte>() : this.Block(first, (int)bytes);
        }

        private long[] Pointers(byte[] block, int at, int max)
        {
            var bytes = this.Vector(block, at, Pointer, max);
            var pointers = Enumerable.Range(0, bytes.Length / Pointer).Select(index => Long(bytes, index * Pointer)).ToArray();
            return pointers.Length > 0 && pointers.All(IsPointer) ? pointers : Array.Empty<long>();
        }

        /// <summary>The inventories a player's list at <paramref name="at"/> leads to (an id and a pointer per entry).</summary>
        private long[] Inventories(byte[] player, int at)
        {
            var bytes = this.Vector(player, at, Entry, MaxInventories);
            var inventories = Enumerable.Range(0, bytes.Length / Entry).Select(index => Long(bytes, (index * Entry) + EntryInventory)).Where(IsPointer).ToArray();
            return inventories.Length * 2 >= bytes.Length / Entry ? inventories : Array.Empty<long>();
        }

        /// <summary>
        ///     The size and cell fields nearest <paramref name="near"/> under which most inventories (three in four: a few are
        ///     never laid out) are X by Y cells with one pointer per cell, and their items, the first few checked, entities under
        ///     <c>Metadata/Items/</c>; at least one item must be there.
        /// </summary>
        private (int Boxes, int Cells) Cells(long[] inventories, InventoryFields near)
        {
            var bodies = inventories.Select(inventory => this.Block(inventory, near.Cells + Reach)).Where(body => body.Length > 0).ToList();
            var cells = -1;
            var boxes = bodies.Count == 0 ? -1 : Nearest(near.Boxes, bodies.Min(body => body.Length) - Pointer, box =>
                bodies.Count(body => IsSize(body, box)) * 4 >= bodies.Count * 3 &&
                (cells = Nearest(near.Cells, bodies.Min(body => body.Length) - VectorBytes, list => list != box && this.HoldsItems(bodies, box, list))) >= 0);
            return boxes < 0 ? (-1, -1) : (boxes, cells);
        }

        private static bool IsSize(byte[] body, int at) => Int(body, at) is > 0 and <= MaxSide && Int(body, at + sizeof(int)) is > 0 and <= MaxSide;

        private bool HoldsItems(List<byte[]> bodies, int boxes, int list)
        {
            var laidOut = bodies.Where(body => IsSize(body, boxes) && Long(body, list + Pointer) - Long(body, list) == Int(body, boxes) * Int(body, boxes + sizeof(int)) * Pointer).ToList();
            if (laidOut.Count * 4 < bodies.Count * 3)
            {
                return false;
            }

            var items = new List<long>();
            foreach (var body in laidOut.TakeWhile(_ => items.Count < ItemsChecked))
            {
                var slots = this.Vector(body, list, Pointer, MaxSide * MaxSide);
                items.AddRange(Enumerable.Range(0, slots.Length / Pointer).Select(index => Long(slots, index * Pointer)).Where(IsPointer).Distinct().Take(ItemsChecked - items.Count));
            }

            return items.Count > 0 && items.All(cell => this.memory.EntityPath(Long(this.Block(cell, Pointer), CellItem)).StartsWith(ItemFolder, StringComparison.Ordinal));
        }
    }
}
