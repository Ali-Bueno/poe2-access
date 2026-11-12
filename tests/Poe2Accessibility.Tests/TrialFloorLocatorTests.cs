namespace Poe2Accessibility.Tests
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>
    ///     The Trial of the Sekhemas' links read from a floor built in a memory of its own: where they were measured, and
    ///     found again by shape once a patch has moved them.
    /// </summary>
    public static class TrialFloorLocatorTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheFloorReadsWhereItWasMeasured), TheFloorReadsWhereItWasMeasured),
            (nameof(AnotherMapsRoomsFitNoLinks), AnotherMapsRoomsFitNoLinks),
            (nameof(APointerMovedOnItsElementIsFoundWithTheFloorAsMeasured), APointerMovedOnItsElementIsFoundWithTheFloorAsMeasured),
            (nameof(AFloorMovedWholeIsFoundByItsShape), AFloorMovedWholeIsFoundByItsShape),
            (nameof(TwoFloorsOfOneShapeAreBelievedOnlyWhenTheyAgree), TwoFloorsOfOneShapeAreBelievedOnlyWhenTheyAgree),
            (nameof(MemoryThatCannotBeReadFindsNothing), MemoryThatCannotBeReadFindsNothing),
        };

        private static readonly TrialFloorShape Measured = new(
            GameLayout.TrialFloor.HolderOffset, GameLayout.TrialFloor.ColumnsOffset, GameLayout.TrialFloor.ColumnSize,
            GameLayout.TrialFloor.RoomsOffset, GameLayout.TrialFloor.RoomSize, GameLayout.TrialFloor.WaysOffset);

        // The user's floor of 2026-11-12 as the game drew it, and the same rooms linked another way.
        private static readonly int[] Rooms = { 1, 3, 3, 4, 5, 4, 6, 1 };

        private static readonly int[][][] Links =
        {
            new[] { new[] { 0, 1, 2 } },
            new[] { new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2 } },
            new[] { new[] { 0, 1, 2 }, new[] { 2 }, new[] { 2, 3 } },
            new[] { new[] { 0, 1, 2 }, new[] { 2 }, new[] { 2, 3, 4 }, new[] { 4 } },
            new[] { new[] { 0, 1 }, new[] { 1 }, new[] { 1, 2 }, new[] { 2 }, new[] { 2, 3 } },
            new[] { new[] { 0, 1, 2 }, new[] { 2, 3 }, new[] { 3, 4 }, new[] { 4, 5 } },
            new[] { new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 }, new[] { 0 } },
        };

        private static readonly int[][][] OtherLinks = Links.Select((column, index) => index == 1 ? new[] { new[] { 0 }, new[] { 1 }, new[] { 2 } } : column).ToArray();

        private static void TheFloorReadsWhereItWasMeasured()
        {
            var memory = new Memory();
            var floor = memory.Floor((Measured.ColumnsOffset, memory.Columns(Measured, Rooms, Links)));
            Check.Equal(Flat(Links), Flat(TrialFloorLocator.ReadLinks(memory.Read, floor, Measured, Rooms)), "the links of the floor");
        }

        private static void AnotherMapsRoomsFitNoLinks()
        {
            var memory = new Memory();
            var floor = memory.Floor((Measured.ColumnsOffset, memory.Columns(Measured, Rooms, Links)));
            var other = new[] { 1, 3, 4, 3, 5, 3, 6, 1 };
            Check.Equal(null, TrialFloorLocator.ReadLinks(memory.Read, floor, Measured, other), "another map's rooms");
            Check.Equal(true, TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(Measured.HolderOffset, floor) }, Measured, other) is null, "nor are they found by shape");
        }

        private static void APointerMovedOnItsElementIsFoundWithTheFloorAsMeasured()
        {
            // The floor as it was generated stands before the floor as it is shown; with the floor laid out as measured, only the shown one is read.
            var memory = new Memory();
            var floor = memory.Floor(
                (Measured.ColumnsOffset - 0x48, memory.Columns(Measured, Rooms, OtherLinks)),
                (Measured.ColumnsOffset, memory.Columns(Measured, Rooms, Links)));
            var moved = Measured.HolderOffset + 0x18;
            var found = TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(0x40, memory.Junk()), memory.Holder(moved, floor) }, Measured, Rooms);
            Check.Equal(Measured with { HolderOffset = moved }, found?.Shape, "the floor as measured, its pointer further on");
            Check.Equal(floor, found?.Floor ?? 0, "the floor");
            Check.Equal(Flat(Links), Flat(found?.Links), "the links the map shows");
        }

        private static void AFloorMovedWholeIsFoundByItsShape()
        {
            var moved = new TrialFloorShape(0x3C0, 0x210, 0x28, 0x8, 0x40, 0x8);
            var memory = new Memory();
            var floor = memory.Floor((moved.ColumnsOffset, memory.Columns(moved, Rooms, Links)));
            var found = TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(moved.HolderOffset, floor) }, Measured, Rooms);
            Check.Equal(moved, found?.Shape, "every offset and size, from the map on screen alone");
            Check.Equal(Flat(Links), Flat(found?.Links), "the links");
            Check.Equal(Flat(Links), Flat(TrialFloorLocator.ReadLinks(memory.Read, floor, moved, Rooms)), "and read again where they were found");
        }

        private static void TwoFloorsOfOneShapeAreBelievedOnlyWhenTheyAgree()
        {
            var moved = new TrialFloorShape(0x3C0, 0x210, 0x28, 0x0, 0x40, 0x0);
            var memory = new Memory();
            var same = memory.Floor((moved.ColumnsOffset - 0x50, memory.Columns(moved, Rooms, Links)), (moved.ColumnsOffset, memory.Columns(moved, Rooms, Links)));
            var found = TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(moved.HolderOffset, same) }, Measured, Rooms);
            Check.Equal(moved, found?.Shape, "two that agree: the later one, the floor as shown");

            var differ = memory.Floor((moved.ColumnsOffset - 0x50, memory.Columns(moved, Rooms, OtherLinks)), (moved.ColumnsOffset, memory.Columns(moved, Rooms, Links)));
            Check.Equal(true, TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(moved.HolderOffset, differ) }, Measured, Rooms) is null, "two that differ: which one the map shows is not known");
        }

        private static void MemoryThatCannotBeReadFindsNothing()
        {
            var memory = new Memory();
            Check.Equal(null, TrialFloorLocator.ReadLinks(memory.Read, 0x1234_5000, Measured, Rooms), "no floor there");
            Check.Equal(true, TrialFloorLocator.Find(memory.Read, new long[] { 0x1234_5000, 0 }, Measured, Rooms) is null, "no element there");
        }

        private static string Flat(IEnumerable<IEnumerable<IEnumerable<int>>>? links) =>
            links is null ? "none" : string.Join(" | ", links.Select(column => string.Join(" ", column.Select(ways => string.Join(",", ways)))));

        /// <summary>A process's memory of blocks a page apart: a read past a block's end returns what there is, elsewhere nothing.</summary>
        private sealed class Memory
        {
            private readonly SortedDictionary<long, byte[]> blocks = new();
            private long next = 0x2_0000_0000;

            public byte[] Read(long address, int count)
            {
                foreach (var (at, block) in this.blocks)
                {
                    if (address >= at && address < at + block.Length)
                    {
                        var from = (int)(address - at);
                        return block.AsSpan(from, Math.Min(count, block.Length - from)).ToArray();
                    }
                }

                return Array.Empty<byte>();
            }

            public long Add(byte[] block)
            {
                var at = this.next;
                this.blocks[at] = block;
                this.next += ((block.Length / 0x1000) + 2) * 0x1000;
                return at;
            }

            /// <summary>A block that is no floor: pointers on an element lead to many.</summary>
            public long Junk() => this.Add(Enumerable.Range(0, 0x300).Select(each => (byte)(each * 7)).ToArray());

            /// <summary>An element of the window holding a pointer.</summary>
            public long Holder(int at, long pointer)
            {
                var holder = new byte[TrialFloorLocator.HolderWindow];
                Put(holder, 0x10, this.Junk());
                Put(holder, at, pointer);
                return this.Add(holder);
            }

            /// <summary>A floor holding vectors of columns.</summary>
            public long Floor(params (int At, (long First, int Bytes) Columns)[] vectors)
            {
                var floor = new byte[TrialFloorLocator.FloorWindow];
                foreach (var (at, columns) in vectors)
                {
                    PutVector(floor, at, columns.First, columns.Bytes);
                }

                return this.Add(floor);
            }

            /// <summary>The columns of a floor laid out as a shape says, each with its rooms and their ways.</summary>
            public (long First, int Bytes) Columns(TrialFloorShape shape, int[] rooms, int[][][] links)
            {
                var columns = new byte[rooms.Length * shape.ColumnSize];
                for (var column = 0; column < rooms.Length; column++)
                {
                    var block = new byte[rooms[column] * shape.RoomSize];
                    for (var room = 0; room < rooms[column] && column < links.Length; room++)
                    {
                        var ways = links[column][room].Select(way => (byte)way).ToArray();
                        PutVector(block, (room * shape.RoomSize) + shape.WaysOffset, this.Add(ways), ways.Length);
                    }

                    PutVector(columns, (column * shape.ColumnSize) + shape.RoomsOffset, this.Add(block), block.Length);
                }

                return (this.Add(columns), columns.Length);
            }

            private static void Put(byte[] block, int at, long value) => BinaryPrimitives.WriteInt64LittleEndian(block.AsSpan(at), value);

            private static void PutVector(byte[] block, int at, long first, int bytes)
            {
                Put(block, at, first);
                Put(block, at + sizeof(long), first + bytes);
                Put(block, at + (2 * sizeof(long)), first + bytes);
            }
        }
    }
}
