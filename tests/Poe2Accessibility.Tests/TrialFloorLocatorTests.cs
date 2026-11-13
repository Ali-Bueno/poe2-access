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
            (nameof(TheLinesTheMapDrawsSayItsLinks), TheLinesTheMapDrawsSayItsLinks),
            (nameof(LinesThatDrawNoMapOfTheseRoomsSayNone), LinesThatDrawNoMapOfTheseRoomsSayNone),
            (nameof(TheLinesAreFoundOnTheCanvasByTheirShape), TheLinesAreFoundOnTheCanvasByTheirShape),
            (nameof(TheLinesDrawnTellWhichFloorIsShown), TheLinesDrawnTellWhichFloorIsShown),
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

        // The 45 lines the game drew on that map (measured 2026-11-12, as whole numbers): each ends short of its rooms' centres.
        private static readonly TrialMapLines.Line[] Drawn =
        {
            new(176, 442, 296, 356), new(187, 479, 284, 478), new(176, 516, 296, 600), new(412, 318, 487, 318), new(399, 358, 501, 438),
            new(412, 478, 487, 478), new(399, 517, 501, 597), new(412, 636, 487, 636), new(611, 295, 695, 262), new(611, 342, 695, 374),
            new(593, 367, 713, 508), new(611, 501, 695, 533), new(611, 613, 695, 580), new(611, 660, 695, 692), new(814, 215, 898, 183),
            new(814, 262, 898, 295), new(796, 287, 916, 429), new(814, 421, 898, 454), new(814, 533, 898, 501), new(814, 580, 898, 613),
            new(796, 605, 916, 747), new(814, 739, 898, 772), new(1017, 183, 1101, 215), new(999, 208, 1119, 349), new(1017, 342, 1101, 374),
            new(1017, 454, 1101, 421), new(1017, 501, 1101, 533), new(1017, 613, 1101, 580), new(999, 747, 1119, 605), new(1017, 772, 1101, 739),
            new(1211, 199, 1313, 119), new(1224, 238, 1299, 238), new(1211, 278, 1313, 358), new(1224, 398, 1299, 398), new(1211, 437, 1313, 517),
            new(1224, 556, 1299, 556), new(1211, 596, 1313, 676), new(1224, 716, 1299, 716), new(1211, 755, 1313, 835), new(1395, 135, 1557, 424),
            new(1407, 285, 1545, 433), new(1424, 419, 1528, 457), new(1424, 536, 1528, 500), new(1408, 669, 1544, 526), new(1395, 819, 1557, 535),
        };

        private static void TheLinesTheMapDrawsSayItsLinks()
        {
            Check.Equal(Flat(Links), Flat(TrialMapLines.Links(Drawn, Rooms)), "the links read from the floor that day, out of the lines alone");
            Check.Equal(Flat(Links), Flat(TrialMapLines.Links(Drawn.Reverse().ToArray(), Rooms)), "in whatever order the lines are kept");
            var moved = Drawn.Select(line => new TrialMapLines.Line((line.X1 * 1.6f) + 300, (line.Y1 * 1.6f) - 40, (line.X2 * 1.6f) + 300, (line.Y2 * 1.6f) - 40)).ToArray();
            Check.Equal(Flat(Links), Flat(TrialMapLines.Links(moved, Rooms)), "and wherever and however large the map is drawn");
        }

        private static void LinesThatDrawNoMapOfTheseRoomsSayNone()
        {
            Check.Equal("none", Flat(TrialMapLines.Links(Drawn, new[] { 1, 3, 4, 3, 5, 3, 6, 1 })), "another map's rooms");
            Check.Equal("none", Flat(TrialMapLines.Links(Drawn.Skip(1).ToArray(), Rooms)), "a line missing: a room no line leads to");
            var noise = Enumerable.Range(0, 45).Select(each => new TrialMapLines.Line(each * 37 % 101, each * 53 % 89, 200 + (each * 71 % 97), each * 29 % 83)).ToArray();
            Check.Equal("none", Flat(TrialMapLines.Links(noise, Rooms)), "numbers that draw nothing");
            Check.Equal("none", Flat(TrialMapLines.Links(Drawn.Select(line => line with { Y1 = float.NaN }).ToArray(), Rooms)), "nor what is no number");
        }

        private static void TheLinesAreFoundOnTheCanvasByTheirShape()
        {
            var memory = new Memory();
            var measured = memory.Canvas(GameLayout.TrialFloor.LinesOffset, GameLayout.TrialFloor.LineSize, Drawn);
            Check.Equal(Flat(Links), Flat(TrialMapLines.Read(memory.Read, measured, GameLayout.TrialFloor.LinesOffset, GameLayout.TrialFloor.LineSize, Rooms)), "where they were measured");
            var moved = memory.Canvas(0x2E0, 0x20, Drawn);
            Check.Equal("none", Flat(TrialMapLines.Read(memory.Read, moved, GameLayout.TrialFloor.LinesOffset, GameLayout.TrialFloor.LineSize, Rooms)), "moved, they are not there");
            var found = TrialMapLines.Find(memory.Read, moved, Rooms);
            Check.Equal((0x2E0, 0x20), (found?.Offset ?? 0, found?.LineSize ?? 0), "found by shape, with a line's new size");
            Check.Equal(Flat(Links), Flat(found?.Links), "and the links");
            Check.Equal(true, TrialMapLines.Find(memory.Read, memory.Junk(), Rooms) is null, "an element with no lines");
        }

        private static void TheLinesDrawnTellWhichFloorIsShown()
        {
            // Two floors that differ, and the lines on screen: the floor that says what they draw is the one shown, whichever comes first.
            var moved = new TrialFloorShape(0x3C0, 0x210, 0x28, 0x0, 0x40, 0x0);
            var memory = new Memory();
            var drawn = TrialMapLines.Links(Drawn, Rooms);
            var shownLast = memory.Floor((moved.ColumnsOffset - 0x50, memory.Columns(moved, Rooms, OtherLinks)), (moved.ColumnsOffset, memory.Columns(moved, Rooms, Links)));
            var found = TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(moved.HolderOffset, shownLast) }, Measured, Rooms, drawn);
            Check.Equal(moved, found?.Shape, "the later floor says the lines");
            var shownFirst = memory.Floor((moved.ColumnsOffset - 0x50, memory.Columns(moved, Rooms, Links)), (moved.ColumnsOffset, memory.Columns(moved, Rooms, OtherLinks)));
            found = TrialFloorLocator.Find(memory.Read, new[] { memory.Holder(moved.HolderOffset, shownFirst) }, Measured, Rooms, drawn);
            Check.Equal(moved with { ColumnsOffset = moved.ColumnsOffset - 0x50 }, found?.Shape, "the earlier floor says the lines");
            Check.Equal(Flat(Links), Flat(found?.Links), "and its links are the ones said");
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

            /// <summary>The map's canvas: an element holding the vector of the lines it draws, each two points then how it is painted.</summary>
            public long Canvas(int at, int lineSize, IReadOnlyList<TrialMapLines.Line> lines)
            {
                var block = new byte[lines.Count * lineSize];
                for (var each = 0; each < lines.Count; each++)
                {
                    var floats = new[] { lines[each].X1, lines[each].Y1, lines[each].X2, lines[each].Y2 };
                    Buffer.BlockCopy(floats, 0, block, each * lineSize, floats.Length * sizeof(float));
                    block[(each * lineSize) + (floats.Length * sizeof(float))] = (byte)(each % 4);
                }

                var canvas = new byte[TrialMapLines.Window];
                Put(canvas, 0x10, this.Junk());
                PutVector(canvas, at, this.Add(block), block.Length);
                return this.Add(canvas);
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
