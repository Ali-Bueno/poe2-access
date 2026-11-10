// <copyright file="AreaInstanceLayout.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    using System;
    using System.Buffers;
    using System.Runtime.InteropServices;
    using GameReader.Utils;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Reads an area instance through the offsets this game build really uses: judged once per area, searched again
    ///     while they do not hold what they must, so a patch that moves a field does not need a new GameHelper.
    /// </summary>
    public sealed class AreaInstanceLayout
    {
        // Past every field seen so far (0.5.3 reached 0xE90) and inside what the game allocates.
        internal const int BlockSize = 0x2000;
        private const long SearchEveryMs = 500;

        private IntPtr judged;
        private long nextSearchAt;

        /// <summary>Gets where the fields are read from now.</summary>
        public AreaFields Fields { get; private set; } = AreaFields.Measured;

        /// <summary>Gets the outcome of the last search; negative fields were not found.</summary>
        public AreaFields LastFound { get; private set; } = AreaFields.Measured;

        /// <summary>Gets a value indicating whether the current area's world fields were all found.</summary>
        public bool HasWorld { get; private set; }

        /// <summary>The tile record's size and the cells a tile spans this host was written for.</summary>
        public static (int Record, int CellsPerTile) MeasuredTerrainShape { get; } = (Marshal.SizeOf<TileStructure>(), TileStructure.MeasuredTileToGridConversion);

        /// <summary>Gets the tile record's size and the cells a tile spans as the terrain holds them; zero until a terrain was found.</summary>
        public (int Record, int CellsPerTile) TerrainShape { get; private set; }

        /// <summary>The area instance at <paramref name="address"/>; <paramref name="moved"/> says the fields changed.</summary>
        internal AreaInstanceOffsets Read(SafeMemoryHandle reader, IntPtr address, out bool moved)
        {
            return this.Read(reader, address, true, out moved);
        }

        /// <summary>The area instance at <paramref name="address"/> through the fields known now.</summary>
        internal AreaInstanceOffsets Read(SafeMemoryHandle reader, IntPtr address)
        {
            return this.Read(reader, address, false, out _);
        }

        /// <summary>
        ///     One read of the block into an array of the shared pool (every frame, 8 KB): the struct composed from it is a
        ///     copy, and the search, which takes an array, gets one of its own, so nothing holds the pooled one once this returns.
        /// </summary>
        private AreaInstanceOffsets Read(SafeMemoryHandle reader, IntPtr address, bool judge, out bool moved)
        {
            moved = false;
            var rented = ArrayPool<byte>.Shared.Rent(BlockSize);
            try
            {
                var block = rented.AsSpan(0, BlockSize);
                if (!reader.TryReadArray(address, block))
                {
                    return reader.ReadMemory<AreaInstanceOffsets>(address);
                }

                if (judge && address != this.judged && Environment.TickCount64 >= this.nextSearchAt)
                {
                    moved = this.Judge(reader, address, block.ToArray());
                }

                return Compose(block, this.Fields);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }

        private static AreaInstanceOffsets Compose(ReadOnlySpan<byte> block, AreaFields at)
        {
            var data = default(AreaInstanceOffsets);
            data.CurrentAreaLevel = block[at.Level];
            data.CurrentAreaHash = MemoryMarshal.Read<uint>(block[at.Hash..]);
            data.Environments = MemoryMarshal.Read<StdVector>(block[at.Environments..]);
            data.PlayerInfo.ServerDataPtr = MemoryMarshal.Read<IntPtr>(block[at.ServerData..]);
            data.PlayerInfo.LocalPlayerPtr = MemoryMarshal.Read<IntPtr>(block[at.LocalPlayer..]);
            data.Entities = MemoryMarshal.Read<EntityListStruct>(block[at.Entities..]);
            data.TerrainMetadata = ReadTerrain(block, at.Terrain);
            return data;
        }

        /// <summary>The terrain, read from the block: its fields move as the struct's remap says.</summary>
        private static TerrainStruct ReadTerrain(ReadOnlySpan<byte> block, int at)
        {
            var size = Marshal.SizeOf<TerrainStruct>();
            if (StructRemap<TerrainStruct>.Segments is not { } moved || at + StructRemapper.ReadSize(size, moved) > block.Length)
            {
                return MemoryMarshal.Read<TerrainStruct>(block[at..]);
            }

            Span<byte> target = stackalloc byte[size];
            StructRemapper.Apply(block[at..], target, moved);
            return MemoryMarshal.Read<TerrainStruct>(target);
        }

        private bool Judge(SafeMemoryHandle reader, IntPtr address, byte[] block)
        {
            var memory = new GameMemory(reader);
            var search = new AreaLayoutSearch(memory);
            var found = search.Find(block, this.Fields);
            if (!found.HasWorld && OffsetHealer.RepairEntityLayout(memory, search.EntitiesAt(block, found.Over(this.Fields))))
            {
                // Every check of the area asks entities for their path: with it moved, nothing read as a world.
                found = search.Find(block, this.Fields);
            }

            var fields = found.Over(this.Fields);
            if (found.Entities >= 0)
            {
                OffsetHealer.FollowEntityId(search.FindEntityId(block, found.Entities, OffsetHealer.MeasuredEntityId));
            }

            if (found.Terrain >= 0)
            {
                // Every grid reader converts tiles to cells through this: it follows what the terrain holds.
                this.TerrainShape = search.TerrainShape;
                TileStructure.TileToGridConversion = search.TerrainShape.CellsPerTile;
            }

            var moved = fields != this.Fields;
            this.LastFound = found;
            this.Fields = fields;
            this.HasWorld = found.HasWorld;
            if (found.HasWorld)
            {
                this.judged = address;
            }
            else
            {
                this.nextSearchAt = Environment.TickCount64 + SearchEveryMs;
            }

            return moved;
        }

    }

    /// <summary>The searches' view of the game's memory.</summary>
    internal sealed class GameMemory : IAreaMemory
    {
        private readonly SafeMemoryHandle reader;

        internal GameMemory(SafeMemoryHandle reader)
        {
            this.reader = reader;
        }

        /// <inheritdoc />
        public byte[] Read(long address, int size) => this.reader.ReadMemoryArray<byte>(new IntPtr(address), size);

        /// <inheritdoc />
        public string EntityPath(long entity)
        {
            return this.reader.TryReadMemory<ItemStruct>(new IntPtr(entity), out var item) &&
                   this.reader.TryReadMemory<EntityDetails>(item.EntityDetailsPtr, out var details)
                ? this.reader.ReadStdWString(details.name)
                : string.Empty;
        }

        /// <inheritdoc />
        public string WideText(long address) => this.reader.ReadUnicodeString(new IntPtr(address));
    }
}
