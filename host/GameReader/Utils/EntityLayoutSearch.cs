// <copyright file="EntityLayoutSearch.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;
    using static GameReader.Utils.StructShapes;

    /// <summary>
    ///     Where an entity keeps what leads to its path and components: the details pointer, the path in the details, the
    ///     component vector, the lookup pointer in the details and the name map in the lookup; negative when not found.
    /// </summary>
    public readonly record struct EntityLayout(int Details, int Path, int Components, int LookUp, int NameMap)
    {
        /// <summary>The offsets the host was written for.</summary>
        public static EntityLayout Measured { get; } = new(
            OffsetOf<ItemStruct>(nameof(ItemStruct.EntityDetailsPtr)),
            OffsetOf<EntityDetails>(nameof(EntityDetails.name)),
            OffsetOf<ItemStruct>(nameof(ItemStruct.ComponentListPtr)),
            OffsetOf<EntityDetails>(nameof(EntityDetails.ComponentLookUpPtr)),
            OffsetOf<ComponentLookUpStruct>(nameof(ComponentLookUpStruct.ComponentsNameAndIndex)));

        /// <summary>Gets how an entry of the name map is laid out; as measured while the map is not found.</summary>
        public NameEntryLayout Entry { get; init; } = NameEntryLayout.Measured;

        private static int OffsetOf<T>(string field) => Marshal.OffsetOf<T>(field).ToInt32();
    }

    /// <summary>An entry of an entity's component name map: its size, where it keeps the name and where the component's index.</summary>
    public readonly record struct NameEntryLayout(int Stride, int Name, int Index)
    {
        /// <summary>The layout the host was written for.</summary>
        public static NameEntryLayout Measured { get; } = new(
            Marshal.SizeOf<ComponentNameAndIndexStruct>(),
            Marshal.OffsetOf<ComponentNameAndIndexStruct>(nameof(ComponentNameAndIndexStruct.NamePtr)).ToInt32(),
            Marshal.OffsetOf<ComponentNameAndIndexStruct>(nameof(ComponentNameAndIndexStruct.Index)).ToInt32());
    }

    /// <summary>
    ///     Finds an entity's path and component fields by what they hold, nearest to where they were measured: the details
    ///     lead to a wide string under <c>Metadata/</c>, every component points back at its entity, and the lookup's map
    ///     names components such as <c>Render</c> and <c>Positioned</c> (docs/compatibility.md in the accessibility mod).
    /// </summary>
    public sealed class EntityLayoutSearch
    {
        /// <summary>Bytes searched in an entity, its details and its lookup: every field seen so far sits in the first 0x30.</summary>
        public const int BlockBytes = 0x100;

        private const int Pointer = 8;
        private const int VectorBytes = 3 * Pointer;
        private const string PathRoot = "Metadata/";

        // The game's own bound on an entity's components (Entity.MaxComponentsInAnEntity) and on a path's length (ReadStdWString).
        private const int MaxComponents = 50;
        private const int MaxPathLength = 1000;
        private const int MaxNameBytes = 64;

        // A path never fits the string's inline buffer (8 UTF-16 characters), so the buffer is a pointer.
        private const int InlineCapacity = 8;

        /// <summary>Components nearly every world entity has; the map must name at least one of them.</summary>
        private static readonly HashSet<string> KnownComponents = new(StringComparer.Ordinal)
        {
            "Positioned", "Render", "Life", "Actor", "Player", "Targetable", "WorldItem", "Chest", "Stats", "Buffs", "Animated",
        };

        private static readonly int StringLength = Marshal.OffsetOf<StdWString>(nameof(StdWString.Length)).ToInt32();
        private static readonly int StringCapacity = Marshal.OffsetOf<StdWString>(nameof(StdWString.Capacity)).ToInt32();
        private static readonly int StringBytes = Marshal.SizeOf<StdWString>();
        private static readonly int Owner = Marshal.OffsetOf<ComponentHeader>(nameof(ComponentHeader.EntityPtr)).ToInt32();
        // An entry holds at least a name pointer and an index, so 0x10 bytes once aligned; a patch may grow it a little.
        private const int MinEntry = 0x10;
        private const int MaxEntry = 0x40;

        // A layout other than the measured one must name this many components over the entities judged: a single name read
        // through an entry as long as the whole map proves nothing.
        private const int MinNamesToMove = 4;

        private readonly IAreaMemory memory;
        private readonly Dictionary<long, byte[]> blocks = new();
        private readonly Dictionary<long, string> names = new();

        /// <summary>Initializes a new instance of the <see cref="EntityLayoutSearch"/> class.</summary>
        public EntityLayoutSearch(IAreaMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>
        ///     The fields that every one of <paramref name="entities"/> that can be read agrees on, nearest <paramref name="near"/>;
        ///     a part none of them holds anywhere is negative. The components are judged on the entities that hold their own.
        /// </summary>
        public EntityLayout Find(IReadOnlyList<long> entities, EntityLayout near)
        {
            var bodies = entities.Where(IsPointer).Distinct().Select(entity => (Address: entity, Body: this.Block(entity))).Where(entity => entity.Body.Length > 0).ToList();
            var (details, path) = Pair(near.Details, near.Path, (d, p) => bodies.All(entity => this.IsPath(entity.Body, d, p)));
            // An entity whose components point back at it nowhere says nothing of where the vector is, which a patch moves in
            // every entity: three corpses of the Vastiri Outskirts listed one another's and the part was called lost (2026-11-11).
            var sound = bodies.Where(entity => Nearest(near.Components, BlockBytes - VectorBytes, at => this.Components(entity.Body, at, entity.Address) > 0) >= 0).ToList();
            var components = sound.Count == 0 ? -1
                : Nearest(near.Components, BlockBytes - VectorBytes, at => sound.All(entity => this.Components(entity.Body, at, entity.Address) > 0));
            var counts = components < 0 ? null : bodies.Select(entity => this.Components(entity.Body, components, entity.Address)).ToList();
            var entry = NameEntryLayout.Measured;
            bool IsMap(int l, int m)
            {
                var maps = bodies.Select((entity, index) => (this.Block(Long(entity.Body, details)), counts?[index] is > 0 and var count ? count : MaxComponents)).ToList();
                if (this.NameEntry(maps, l, m, near.Entry) is not { } fits)
                {
                    return false;
                }

                entry = fits;
                return true;
            }

            var (lookUp, map) = details < 0 ? (-1, -1) : Pair(near.LookUp, near.NameMap, IsMap);
            return bodies.Count == 0 ? new EntityLayout(-1, -1, -1, -1, -1)
                : new EntityLayout(details, path, components, lookUp, map) { Entry = map < 0 ? NameEntryLayout.Measured : entry };
        }

        /// <summary>
        ///     How the entries of the name map at <paramref name="map"/> of each lookup are laid out, when every map reads as one
        ///     (<see cref="Named"/>); null otherwise. The layout <paramref name="near"/> is tried first, then the smallest size
        ///     that fits (a multiple of the true size fits too, reading every other entry), each field nearest where measured.
        /// </summary>
        private NameEntryLayout? NameEntry(IReadOnlyList<(byte[] Details, int Components)> maps, int lookUp, int map, NameEntryLayout near)
        {
            var entries = new List<(byte[] Bytes, int Components)>(maps.Count);
            foreach (var (details, components) in maps)
            {
                var block = this.Block(Long(details, lookUp));
                if (!IsVector(block, map, out var first, out var bytes) || bytes > MaxComponents * MaxEntry)
                {
                    return null;
                }

                var read = this.memory.Read(first, (int)bytes);
                if (read.Length < bytes)
                {
                    return null;
                }

                entries.Add((read, components));
            }

            if (entries.All(entry => this.Named(entry.Bytes, near, entry.Components) > 0))
            {
                return near;
            }

            for (var stride = MinEntry; stride <= MaxEntry; stride += Pointer)
            {
                var size = stride;
                if (entries.Any(entry => entry.Bytes.Length % size != 0))
                {
                    continue;
                }

                var found = (NameEntryLayout?)null;
                Nearest(near.Name, size - Pointer, name => StructShapes.Nearest(near.Index, sizeof(int), size - sizeof(int), index =>
                {
                    var layout = new NameEntryLayout(size, name, index);
                    var apart = index + sizeof(int) <= name || index >= name + Pointer;
                    var named = apart && layout != near ? entries.Select(entry => this.Named(entry.Bytes, layout, entry.Components)).ToList() : null;
                    found = named is not null && named.All(count => count > 0) && named.Sum() >= MinNamesToMove ? layout : null;
                    return found is not null;
                }) >= 0);
                if (found is { } layout)
                {
                    return layout;
                }
            }

            return null;
        }

        /// <summary>Whether the entity at <paramref name="entity"/> leads to its path through <paramref name="layout"/>.</summary>
        public bool ReadsPath(long entity, EntityLayout layout) =>
            IsPointer(entity) && this.Block(entity) is { Length: > 0 } body && this.IsPath(body, layout.Details, layout.Path);

        private static (int First, int Second) Pair(int nearFirst, int nearSecond, Func<int, int, bool> fits)
        {
            var second = -1;
            var first = Nearest(nearFirst, BlockBytes - Pointer, at => (second = Nearest(nearSecond, BlockBytes - StringBytes, inner => fits(at, inner))) >= 0);
            return first < 0 ? (-1, -1) : (first, second);
        }

        private static int Nearest(int start, int limit, Func<int, bool> fits) => StructShapes.Nearest(start, Pointer, limit, fits);

        /// <summary>A vector that holds something.</summary>
        private static bool IsVector(byte[] block, int at, out long first, out long bytes) => StructShapes.IsVector(block, at, out first, out bytes) && bytes > 0;

        /// <summary>A component's name as the map holds it: ASCII letters, as "Render" or "ObjectMagicProperties".</summary>
        private static bool IsComponentName(string name) => name.Length >= 2 && char.IsUpper(name[0]) && name.All(char.IsAsciiLetterOrDigit);

        private byte[] Block(long address)
        {
            if (!IsPointer(address))
            {
                return Array.Empty<byte>();
            }

            if (!this.blocks.TryGetValue(address, out var block))
            {
                block = this.memory.Read(address, BlockBytes);
                this.blocks[address] = block;
            }

            return block;
        }

        /// <summary>The pointer at <paramref name="details"/> leads to a string at <paramref name="path"/> under <c>Metadata/</c>.</summary>
        private bool IsPath(byte[] entity, int details, int path)
        {
            var block = this.Block(Long(entity, details));
            if (path + StringBytes > block.Length)
            {
                return false;
            }

            var length = BitConverter.ToInt32(block, path + StringLength);
            var capacity = BitConverter.ToInt32(block, path + StringCapacity);
            return length > PathRoot.Length && length <= MaxPathLength && capacity > InlineCapacity && capacity >= length &&
                   IsPointer(Long(block, path)) && this.memory.WideText(Long(block, path)).StartsWith(PathRoot, StringComparison.Ordinal);
        }

        /// <summary>How many components the vector at <paramref name="at"/> holds when the first and the last point back at the entity; else 0.</summary>
        private int Components(byte[] entity, int at, long address)
        {
            if (!IsVector(entity, at, out var first, out var bytes) || bytes % Pointer != 0 || bytes / Pointer > MaxComponents)
            {
                return 0;
            }

            var pointers = this.memory.Read(first, (int)bytes);
            if (pointers.Length < bytes)
            {
                return 0;
            }

            var ends = new[] { Long(pointers, 0), Long(pointers, (int)bytes - Pointer) };
            return ends.All(component => Long(this.memory.Read(component + Owner, Pointer), 0) == address) ? (int)(bytes / Pointer) : 0;
        }

        /// <summary>
        ///     How many components <paramref name="entries"/>, a map's bytes, name when they are name and index entries laid out
        ///     as <paramref name="layout"/>: most named as components, at least one a component nearly every entity has, every
        ///     index inside the component vector and no two alike (each component has one name); 0 when they are not.
        /// </summary>
        private int Named(byte[] entries, NameEntryLayout layout, int components)
        {
            if (entries.Length == 0 || entries.Length % layout.Stride != 0 || entries.Length / layout.Stride > MaxComponents)
            {
                return 0;
            }

            var named = new List<string>();
            var indexes = new HashSet<int>();
            for (var at = 0; at < entries.Length; at += layout.Stride)
            {
                var name = this.AsciiName(Long(entries, at + layout.Name));
                if (IsComponentName(name))
                {
                    var index = Int(entries, at + layout.Index);
                    if (index < 0 || index >= components || !indexes.Add(index))
                    {
                        return 0;
                    }

                    named.Add(name);
                }
            }

            return named.Count * 2 >= entries.Length / layout.Stride && named.Any(KnownComponents.Contains) ? named.Count : 0;
        }

        private string AsciiName(long address)
        {
            if (!IsPointer(address))
            {
                return string.Empty;
            }

            if (!this.names.TryGetValue(address, out var name))
            {
                var bytes = this.memory.Read(address, MaxNameBytes);
                var end = Array.IndexOf(bytes, (byte)0);
                name = end > 0 ? Encoding.ASCII.GetString(bytes, 0, end) : string.Empty;
                this.names[address] = name;
            }

            return name;
        }
    }
}
