// <copyright file="AreaLayoutSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameReader.Utils;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>Where the fields of <see cref="AreaInstanceOffsets"/> sit in an area instance; negative when not found.</summary>
    public readonly record struct AreaFields(int Level, int Hash, int Environments, int ServerData, int LocalPlayer, int Entities, int Terrain)
    {
        /// <summary>The offsets this build of GameHelper was written for.</summary>
        public static AreaFields Measured { get; } = new(
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.CurrentAreaLevel)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.CurrentAreaHash)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.Environments)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.PlayerInfo)) + OffsetOf<LocalPlayerStruct>(nameof(LocalPlayerStruct.ServerDataPtr)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.PlayerInfo)) + OffsetOf<LocalPlayerStruct>(nameof(LocalPlayerStruct.LocalPlayerPtr)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.Entities)),
            OffsetOf<AreaInstanceOffsets>(nameof(AreaInstanceOffsets.TerrainMetadata)));

        /// <summary>Gets a value indicating whether the fields everything else hangs from were all found.</summary>
        public bool HasWorld => this.LocalPlayer >= 0 && this.Entities >= 0 && this.Terrain >= 0;

        /// <summary>These fields, keeping <paramref name="known"/>'s where one was not found.</summary>
        public AreaFields Over(AreaFields known) => new(
            this.Level >= 0 ? this.Level : known.Level,
            this.Hash >= 0 ? this.Hash : known.Hash,
            this.Environments >= 0 ? this.Environments : known.Environments,
            this.ServerData >= 0 ? this.ServerData : known.ServerData,
            this.LocalPlayer >= 0 ? this.LocalPlayer : known.LocalPlayer,
            this.Entities >= 0 ? this.Entities : known.Entities,
            this.Terrain >= 0 ? this.Terrain : known.Terrain);

        private static int OffsetOf<T>(string field) => Marshal.OffsetOf<T>(field).ToInt32();
    }

    /// <summary>What <see cref="AreaLayoutSearch"/> needs to read from the game.</summary>
    public interface IAreaMemory
    {
        /// <summary>The bytes at <paramref name="address"/>; empty when they cannot be read.</summary>
        byte[] Read(long address, int size);

        /// <summary>The metadata path of the entity at <paramref name="entity"/>; empty when it is not one.</summary>
        string EntityPath(long entity);

        /// <summary>The zero-ended UTF-16 text at <paramref name="address"/>; empty when it cannot be read.</summary>
        string WideText(long address);
    }

    /// <summary>
    ///     Finds an area instance's fields by what they hold, nearest to where they were last first, so a game patch that
    ///     inserts a member does not cost the mod its world (docs/compatibility.md in the accessibility mod).
    /// </summary>
    public sealed class AreaLayoutSearch
    {
        private const long MinPointer = 0x10000;
        private const long MaxPointer = 0x7FFFFFFFFFFF;
        private const int PointerStep = 8;
        private const int Int32Step = 4;

        // MSVC std::map: a sentinel node (nil flag set) whose parent is the root; the key and value follow the links.
        private const int NodeParent = 0x08;
        private const int NodeLeft = 0x00;
        private const int NodeIsNil = 0x19;
        private const int NodeKey = 0x20;
        private const int NodeValue = 0x28;
        private const int NodeSize = 0x30;
        private const int MaxEntities = 500000;
        private const int EntityBytes = 0x100;

        private const int MaxTiles = 10000;

        // The tile record and the cells a tile spans are read from the terrain's own sizes (0.5.5f: 0x38 bytes, 23 cells);
        // these only bound them. The walkable grid keeps two cells to a byte (RoutePlanner.IsWalkable).
        private const int MinTileRecord = 0x10;
        private const int MaxTileRecord = 0x200;
        private const int MinCellsPerTile = 4;
        private const int MaxCellsPerTile = 128;
        private const int CellsPerByte = 2;

        private const int MaxLocalPlayers = 4;
        private const int MaxAreaLevel = 100;
        private const int MaxEnvironments = 1024;
        private const int NearWindow = 0x100;

        // The level is one byte in a 4-byte slot: the next can hold a flag (0x0101 on a new character's Riverbank, 0.5.5f).
        // Then 8 bytes that hold flags and three vectors, any of them empty (0.5.5f: a mine, a town, a cave).
        private const int LevelGap = 8;
        private const int HeaderVectors = 3;
        private const int VectorSize = 0x18;

        // A real hash is random: a value this small is the top half of a pointer.
        private const uint MinHash = 0x10000;

        private static readonly int TotalTiles = Marshal.OffsetOf<TerrainStruct>(nameof(TerrainStruct.TotalTiles)).ToInt32();
        private static readonly int TileDetails = Marshal.OffsetOf<TerrainStruct>(nameof(TerrainStruct.TileDetailsPtr)).ToInt32();
        private static readonly int Walkable = Marshal.OffsetOf<TerrainStruct>(nameof(TerrainStruct.GridWalkableData)).ToInt32();
        private static readonly int BytesPerRow = Marshal.OffsetOf<TerrainStruct>(nameof(TerrainStruct.BytesPerRow)).ToInt32();
        private static readonly int PlayerList = Marshal.OffsetOf<ServerDataOffsets>(nameof(ServerDataOffsets.PlayerServerDataPtr)).ToInt32();
        private static readonly int Inventories = Marshal.OffsetOf<ServerDataStructure>(nameof(ServerDataStructure.PlayerInventories)).ToInt32();
        private static readonly int InventoryEntry = Marshal.SizeOf<InventoryArrayStruct>();
        private static readonly int EntityMapSize = Marshal.SizeOf<StdMap>();

        private readonly IAreaMemory memory;

        /// <summary>Initializes a new instance of the <see cref="AreaLayoutSearch"/> class.</summary>
        public AreaLayoutSearch(IAreaMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>Gets the tile record's size and the cells a tile spans, as the last terrain found holds them; zero before.</summary>
        public (int Record, int CellsPerTile) TerrainShape { get; private set; }

        /// <summary>The fields found in <paramref name="block"/>, the start of an area instance, nearest to <paramref name="near"/>.</summary>
        public AreaFields Find(byte[] block, AreaFields near)
        {
            var player = Nearest(near.LocalPlayer, PointerStep, block.Length - PointerStep, at => this.IsLocalPlayer(block, at));
            var server = player < 0 ? -1 : this.FindServerData(block, player, near);
            var level = Nearest(near.Level, Int32Step, block.Length - HeaderSize, at => IsHeader(block, at, near.Hash - near.Level));
            return new AreaFields(
                level,
                level < 0 ? -1 : level + near.Hash - near.Level,
                NearestIn(near.Environments + (player < 0 ? 0 : player - near.LocalPlayer), block, at => IsIntVector(block, at)),
                server,
                player,
                Nearest(near.Entities, PointerStep, block.Length - (2 * EntityMapSize), at => this.IsEntityLists(block, at)),
                Nearest(near.Terrain, PointerStep, block.Length - BytesPerRow - sizeof(int), at => this.IsTerrain(block, at)));
        }

        /// <summary>
        ///     Where an entity keeps its id, nearest <paramref name="near"/>: the lists' key is that id, so the root and its
        ///     left child must agree. Negative when they cannot be read or no place fits.
        /// </summary>
        public int FindEntityId(byte[] block, int entities, int near)
        {
            var sentinel = this.memory.Read(Int64At(block, entities), NodeSize);
            if (sentinel.Length < NodeSize)
            {
                return -1;
            }

            var root = this.memory.Read(Int64At(sentinel, NodeParent), NodeSize);
            if (root.Length < NodeSize)
            {
                return -1;
            }

            var left = this.memory.Read(Int64At(root, NodeLeft), NodeSize);
            var samples = left.Length == NodeSize && left[NodeIsNil] == 0 ? new[] { root, left } : new[] { root };
            var bodies = samples.Select(node => (Key: BitConverter.ToUInt32(node, NodeKey), Body: this.memory.Read(Int64At(node, NodeValue), EntityBytes))).ToList();
            if (bodies.Any(entity => entity.Body.Length < EntityBytes))
            {
                return -1;
            }

            return Nearest(near, Int32Step, EntityBytes - sizeof(uint), at => bodies.All(entity => BitConverter.ToUInt32(entity.Body, at) == entity.Key));
        }

        /// <summary>
        ///     What reads as entities through <paramref name="at"/> without asking them anything: the local player and the
        ///     values of the awake list's root and its left child. For judging the entity layout when no world can be read.
        /// </summary>
        public long[] EntitiesAt(byte[] block, AreaFields at)
        {
            var found = new System.Collections.Generic.List<long>();
            if (at.LocalPlayer >= 0 && at.LocalPlayer + PointerStep <= block.Length)
            {
                found.Add(Int64At(block, at.LocalPlayer));
            }

            var sentinel = at.Entities >= 0 && at.Entities + PointerStep <= block.Length ? this.memory.Read(Int64At(block, at.Entities), NodeSize) : Array.Empty<byte>();
            var root = sentinel.Length == NodeSize ? this.memory.Read(Int64At(sentinel, NodeParent), NodeSize) : Array.Empty<byte>();
            if (root.Length == NodeSize && root[NodeIsNil] == 0)
            {
                found.Add(Int64At(root, NodeValue));
                var left = this.memory.Read(Int64At(root, NodeLeft), NodeSize);
                if (left.Length == NodeSize && left[NodeIsNil] == 0)
                {
                    found.Add(Int64At(left, NodeValue));
                }
            }

            return found.Where(IsPointer).ToArray();
        }

        private static int HeaderSize => sizeof(uint) + LevelGap + (HeaderVectors * VectorSize);

        private static int Nearest(int start, int step, int limit, Func<int, bool> fits)
        {
            for (var distance = 0; distance <= limit; distance += step)
            {
                if (start + distance >= 0 && start + distance <= limit && fits(start + distance))
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

        private static int NearestIn(int start, byte[] block, Func<int, bool> fits)
        {
            var found = Nearest(start, PointerStep, block.Length - VectorSize, fits);
            return Math.Abs(found - start) <= NearWindow ? found : -1;
        }

        private static long Int64At(byte[] bytes, int at) => BitConverter.ToInt64(bytes, at);

        private static bool IsPointer(long value) => value > MinPointer && value < MaxPointer;

        private static bool IsVector(byte[] block, int at)
        {
            var first = Int64At(block, at);
            var last = Int64At(block, at + PointerStep);
            var end = Int64At(block, at + (2 * PointerStep));
            return first == 0 ? last == 0 && end == 0 : IsPointer(first) && IsPointer(end) && first <= last && last <= end;
        }

        private static bool IsIntVector(byte[] block, int at)
        {
            var size = Int64At(block, at + PointerStep) - Int64At(block, at);
            return IsVector(block, at) && Int64At(block, at) != 0 && size % sizeof(int) == 0 && size / sizeof(int) <= MaxEnvironments;
        }

        private static bool IsHeader(byte[] block, int level, int hashDistance)
        {
            var value = block[level];
            if (value is 0 or > MaxAreaLevel || level + hashDistance < 0 || level + hashDistance + sizeof(uint) > block.Length ||
                BitConverter.ToUInt32(block, level + hashDistance) < MinHash)
            {
                return false;
            }

            var vectors = level + sizeof(uint) + LevelGap;
            if (vectors % PointerStep != 0)
            {
                return false;
            }

            for (var vector = 0; vector < HeaderVectors; vector++)
            {
                if (!IsVector(block, vectors + (vector * VectorSize)))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsLocalPlayer(byte[] block, int at) =>
            IsPointer(Int64At(block, at)) &&
            this.memory.EntityPath(Int64At(block, at)).StartsWith("Metadata/Characters/", StringComparison.Ordinal);

        private int FindServerData(byte[] block, int player, AreaFields near)
        {
            var gap = near.LocalPlayer - near.ServerData;
            var found = Nearest(player - gap, PointerStep, player, at => this.IsServerData(Int64At(block, at)));
            return found >= 0 ? found : player - gap;
        }

        private bool IsServerData(long pointer)
        {
            if (!IsPointer(pointer))
            {
                return false;
            }

            var list = this.memory.Read(pointer + StructRemapper.Follow(StructRemap<ServerDataOffsets>.Segments, PlayerList), VectorSize);
            if (list.Length < VectorSize || !IsVector(list, 0))
            {
                return false;
            }

            var bytes = Int64At(list, PointerStep) - Int64At(list, 0);
            if (bytes <= 0 || bytes % PointerStep != 0 || bytes / PointerStep > MaxLocalPlayers)
            {
                return false;
            }

            var entry = this.memory.Read(Int64At(list, 0), PointerStep);
            if (entry.Length < PointerStep || !IsPointer(Int64At(entry, 0)))
            {
                return false;
            }

            var inventories = this.memory.Read(Int64At(entry, 0) + StructRemapper.Follow(StructRemap<ServerDataStructure>.Segments, Inventories), VectorSize);
            if (inventories.Length < VectorSize || !IsVector(inventories, 0))
            {
                return false;
            }

            var size = Int64At(inventories, PointerStep) - Int64At(inventories, 0);
            return size > 0 && size % InventoryEntry == 0;
        }

        private bool IsEntityLists(byte[] block, int at) =>
            this.IsEntityMap(block, at, false) && this.IsEntityMap(block, at + EntityMapSize, true);

        private bool IsEntityMap(byte[] block, int at, bool mayBeEmpty)
        {
            var head = Int64At(block, at);
            var size = Int64At(block, at + PointerStep);
            if (!IsPointer(head) || size < (mayBeEmpty ? 0 : 1) || size > MaxEntities)
            {
                return false;
            }

            var sentinel = this.memory.Read(head, NodeSize);
            if (sentinel.Length < NodeSize || sentinel[NodeIsNil] == 0)
            {
                return false;
            }

            var root = Int64At(sentinel, NodeParent);
            if (size == 0)
            {
                return root == head;
            }

            var node = IsPointer(root) ? this.memory.Read(root, NodeSize + PointerStep) : Array.Empty<byte>();
            return node.Length > NodeValue + PointerStep - 1 && node[NodeIsNil] == 0 && Int64At(node, NodeParent) == head &&
                   this.memory.EntityPath(Int64At(node, NodeValue)).StartsWith("Metadata/", StringComparison.Ordinal);
        }

        private bool IsTerrain(byte[] block, int at)
        {
            var moved = StructRemap<TerrainStruct>.Segments;
            var (total, details, walkable, row) = (at + StructRemapper.Follow(moved, TotalTiles), at + StructRemapper.Follow(moved, TileDetails),
                at + StructRemapper.Follow(moved, Walkable), at + StructRemapper.Follow(moved, BytesPerRow));
            var tilesX = Int64At(block, total);
            var tilesY = Int64At(block, total + PointerStep);
            if (tilesX is <= 0 or > MaxTiles || tilesY is <= 0 or > MaxTiles || !IsVector(block, details) ||
                Int64At(block, details) == 0 || !IsVector(block, walkable))
            {
                return false;
            }

            var tileBytes = Int64At(block, details + PointerStep) - Int64At(block, details);
            var walkableBytes = Int64At(block, walkable + PointerStep) - Int64At(block, walkable);
            var bytesPerRow = BitConverter.ToInt32(block, row);
            if (tileBytes % (tilesX * tilesY) != 0 || bytesPerRow <= 0 || walkableBytes % (bytesPerRow * tilesY) != 0)
            {
                return false;
            }

            // Each tile a record of the same size; each row of tiles as many grid rows as a tile spans cells, a row two cells to a byte.
            var record = tileBytes / (tilesX * tilesY);
            var cells = walkableBytes / (bytesPerRow * tilesY);
            if (record % PointerStep != 0 || record is < MinTileRecord or > MaxTileRecord || cells is < MinCellsPerTile or > MaxCellsPerTile ||
                bytesPerRow != ((tilesX * cells) + CellsPerByte - 1) / CellsPerByte)
            {
                return false;
            }

            this.TerrainShape = ((int)record, (int)cells);
            return true;

        }
    }
}
