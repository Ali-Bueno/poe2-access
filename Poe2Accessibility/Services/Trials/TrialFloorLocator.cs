namespace Poe2Accessibility.Services
{
    using System;
    using System.Buffers.Binary;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where the Trial of the Sekhemas' floor keeps its links: the floor's pointer on an element of the map window, its
    ///     vector of columns, each column's vector of rooms and each room's vector of ways. Measured
    ///     (<c>GameLayout.TrialFloor</c>) or found again by shape after a patch moved it (<see cref="TrialFloorLocator.Find"/>).
    /// </summary>
    /// <param name="HolderOffset">On the element holding the floor: its pointer.</param>
    /// <param name="ColumnsOffset">On the floor: the vector of columns.</param>
    /// <param name="ColumnSize">Bytes a column.</param>
    /// <param name="RoomsOffset">On a column: its vector of rooms.</param>
    /// <param name="RoomSize">Bytes a room.</param>
    /// <param name="WaysOffset">On a room: its vector of ways, a byte each, the row it leads to in the next column.</param>
    public sealed record TrialFloorShape(int HolderOffset, int ColumnsOffset, int ColumnSize, int RoomsOffset, int RoomSize, int WaysOffset);

    /// <summary>
    ///     Reads the floor's links out of the game's memory, and finds them again when a patch has moved them. Nothing is
    ///     guessed: the map on screen says how many columns there are and how many rooms each holds, so a vector of that
    ///     many columns, each of that many rooms, whose ways fit the map (<see cref="TrialMap.Fits"/>) is the floor wherever
    ///     it stands, and the sizes of a column and a room are those vectors' spans over the counts on screen. Pure: the
    ///     memory is a function of an address and a count that returns fewer bytes (none) when it cannot be read.
    /// </summary>
    public static class TrialFloorLocator
    {
        /// <summary>How much of an element of the map window is searched for the floor's pointer.</summary>
        public const int HolderWindow = 0x800;

        /// <summary>How much of a floor is searched for its vector of columns.</summary>
        public const int FloorWindow = 0x600;

        /// <summary>The most ways read of a room; a room has no more than the next column has rooms (six, the widest seen).</summary>
        public const int MaxWays = 8;

        // A std::vector: begin, end and capacity, a pointer each. A column and a room hold one, so neither is smaller.
        private const int VectorSize = 3 * sizeof(long);
        private const int MaxColumnSize = 0x100;
        private const int MaxRoomSize = 0x200;

        // Windows maps nothing below 64 KiB nor, for a user process, above 0x7FFF'FFFFFFFF; memory is mapped a 4 KiB page at a time.
        private const long LowestAddress = 0x10000;
        private const long HighestAddress = 0x7FFF_FFFF_FFFF;
        private const long PageSize = 0x1000;

        /// <summary>
        ///     The links of a floor laid out as <paramref name="shape"/> says: for each column but the boss's, each room's
        ///     ways into the next. Null unless they fit the map on screen.
        /// </summary>
        /// <param name="rooms">The rooms of each column, as the window shows them.</param>
        public static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? ReadLinks(Func<long, int, byte[]> read, long floor, TrialFloorShape shape, IReadOnlyList<int> rooms)
        {
            if (!TryVector(read(floor + shape.ColumnsOffset, VectorSize), 0, out var first, out var span) || span != (long)shape.ColumnSize * rooms.Count)
            {
                return null;
            }

            var columns = read(first, (int)span);
            return columns.Length == span && RoomBlocks(read, columns, shape, rooms) is { } blocks ? LinksOf(read, blocks, shape, rooms) : null;
        }

        /// <summary>
        ///     Finds the floor from the elements of the map window when it is not where <paramref name="measured"/> says.
        ///     First with the floor laid out as measured and only its pointer moved on the element (what a patch that grows
        ///     the interface's elements does); then by shape alone. By shape, a floor that holds two vectors of links that
        ///     differ is not believed: the game keeps the floor as it was generated beside the floor as it shows it.
        /// </summary>
        /// <param name="holders">The elements to search for the floor's pointer.</param>
        /// <param name="rooms">The rooms of each column, as the window shows them.</param>
        public static (TrialFloorShape Shape, long Floor, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links)? Find(
            Func<long, int, byte[]> read, IReadOnlyList<long> holders, TrialFloorShape measured, IReadOnlyList<int> rooms)
        {
            var pointers = new List<(int At, long Floor)>();
            var seen = new HashSet<long>();
            foreach (var holder in holders)
            {
                var element = Window(read, holder, HolderWindow);
                for (var at = 0; at + sizeof(long) <= element.Length; at += sizeof(long))
                {
                    var floor = Word(element, at);
                    if (IsAddress(floor) && floor % sizeof(long) == 0 && seen.Add(floor))
                    {
                        pointers.Add((at, floor));
                    }
                }
            }

            foreach (var (at, floor) in pointers)
            {
                if (ReadLinks(read, floor, measured, rooms) is { } links)
                {
                    return (measured with { HolderOffset = at }, floor, links);
                }
            }

            foreach (var (at, floor) in pointers)
            {
                if (FindIn(read, floor, rooms) is { } found)
                {
                    return (found.Shape with { HolderOffset = at }, floor, found.Links);
                }
            }

            return null;
        }

        /// <summary>The floor's links by shape: the last vector of columns in the floor that fits the map, when every one that fits says the same.</summary>
        private static (TrialFloorShape Shape, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links)? FindIn(Func<long, int, byte[]> read, long floor, IReadOnlyList<int> rooms)
        {
            (TrialFloorShape Shape, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links)? found = null;
            var body = Window(read, floor, FloorWindow);
            for (var at = 0; at + VectorSize <= body.Length; at += sizeof(long))
            {
                if (!TryVector(body, at, out var first, out var span) || span == 0 || Word(body, at + (2 * sizeof(long))) < first + span ||
                    span % rooms.Count != 0 || span / rooms.Count is < VectorSize or > MaxColumnSize || span / rooms.Count % sizeof(long) != 0)
                {
                    continue;
                }

                var columns = read(first, (int)span);
                if (columns.Length == span && ShapeOf(read, columns, at, (int)(span / rooms.Count), rooms) is { } here)
                {
                    if (found is { } before && !Same(before.Links, here.Links))
                    {
                        return null;
                    }

                    found = here;
                }
            }

            return found;
        }

        /// <summary>Where the rooms and their ways stand in columns of a known size: the first places whose links fit the map.</summary>
        private static (TrialFloorShape Shape, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> Links)? ShapeOf(
            Func<long, int, byte[]> read, byte[] columns, int columnsOffset, int columnSize, IReadOnlyList<int> rooms)
        {
            for (var roomsAt = 0; roomsAt + VectorSize <= columnSize; roomsAt += sizeof(long))
            {
                // A room's size is its column's span over the rooms the window shows there.
                if (!TryVector(columns, roomsAt, out _, out var span) || span == 0 || span % rooms[0] != 0 ||
                    span / rooms[0] is < VectorSize or > MaxRoomSize || span / rooms[0] % sizeof(long) != 0)
                {
                    continue;
                }

                var roomSize = (int)(span / rooms[0]);
                if (RoomBlocks(read, columns, new TrialFloorShape(0, columnsOffset, columnSize, roomsAt, roomSize, 0), rooms) is not { } blocks)
                {
                    continue;
                }

                for (var waysAt = 0; waysAt + (2 * sizeof(long)) <= roomSize; waysAt += sizeof(long))
                {
                    var shape = new TrialFloorShape(0, columnsOffset, columnSize, roomsAt, roomSize, waysAt);
                    if (LinksOf(read, blocks, shape, rooms) is { } links)
                    {
                        return (shape, links);
                    }
                }
            }

            return null;
        }

        /// <summary>Each column's rooms, as many as the window shows there; null when a column holds another count.</summary>
        private static byte[][]? RoomBlocks(Func<long, int, byte[]> read, byte[] columns, TrialFloorShape shape, IReadOnlyList<int> rooms)
        {
            var blocks = new byte[rooms.Count][];
            for (var column = 0; column < rooms.Count; column++)
            {
                if (!TryVector(columns, (column * shape.ColumnSize) + shape.RoomsOffset, out var first, out var span) || span != (long)shape.RoomSize * rooms[column])
                {
                    return null;
                }

                blocks[column] = read(first, (int)span);
                if (blocks[column].Length != span)
                {
                    return null;
                }
            }

            return blocks;
        }

        private static IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>>? LinksOf(Func<long, int, byte[]> read, byte[][] blocks, TrialFloorShape shape, IReadOnlyList<int> rooms)
        {
            var links = new List<IReadOnlyList<IReadOnlyList<int>>>();
            for (var column = 0; column < rooms.Count; column++)
            {
                var ways = new List<IReadOnlyList<int>>();
                for (var room = 0; room < rooms[column]; room++)
                {
                    // A room that leads nowhere (the boss's) may hold no pointer at all: only the span is asked of them.
                    if (!TryVector(blocks[column], (room * shape.RoomSize) + shape.WaysOffset, out var begin, out var count, anyBegin: true) || count > MaxWays)
                    {
                        return null;
                    }

                    var rows = count == 0 ? Array.Empty<byte>() : read(begin, (int)count);
                    // The game keeps a room's ways in the order it draws them, top to bottom.
                    if (rows.Length != count || rows.Zip(rows.Skip(1), (one, next) => one < next).Contains(false))
                    {
                        return null;
                    }

                    ways.Add(rows.Select(each => (int)each).ToList());
                }

                links.Add(ways);
            }

            // The boss's room leads nowhere; the links are those of the columns before it.
            if (links[^1].Any(ways => ways.Count > 0))
            {
                return null;
            }

            links.RemoveAt(links.Count - 1);
            return TrialMap.Fits(rooms, links) ? links : null;
        }

        /// <summary>A vector's begin and the bytes it spans, read from a buffer; false when it is no vector (end before begin, no address).</summary>
        /// <param name="anyBegin">The span alone is asked: an empty vector's begin may be anything.</param>
        private static bool TryVector(byte[] buffer, int at, out long begin, out long span, bool anyBegin = false)
        {
            begin = 0;
            span = 0;
            if (at < 0 || at + (2 * sizeof(long)) > buffer.Length)
            {
                return false;
            }

            begin = Word(buffer, at);
            span = Word(buffer, at + sizeof(long)) - begin;
            return span >= 0 && span <= int.MaxValue && (IsAddress(begin) || (anyBegin && span == 0));
        }

        private static bool Same(IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> one, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> other) =>
            one.Count == other.Count && one.Zip(other, (left, right) =>
                left.Count == right.Count && left.Zip(right, (ways, others) => ways.SequenceEqual(others)).All(same => same)).All(same => same);

        /// <summary>As much of a window as can be read: all of it, or what stands before the end of its first page.</summary>
        public static byte[] Window(Func<long, int, byte[]> read, long address, int size)
        {
            var all = read(address, size);
            var toPageEnd = (int)(PageSize - (address & (PageSize - 1)));
            return all.Length == size || toPageEnd >= size ? all : read(address, toPageEnd);
        }

        private static long Word(byte[] buffer, int at) => BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(at, sizeof(long)));

        private static bool IsAddress(long value) => value is >= LowestAddress and <= HighestAddress;
    }
}
