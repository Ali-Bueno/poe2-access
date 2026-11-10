// <copyright file="AreaInstance.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.RemoteObjects.States.InGameStateObjects
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Components;
    using Coroutine;
    using CoroutineEvents;
    using GameReader.Cache;
    using GameReader.RemoteEnums.Entity;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;
    using Utils;

    /// <summary>
    ///     Points to the InGameState -> AreaInstanceData Object.
    /// </summary>
    public class AreaInstance : RemoteObjectBase
    {
        // Large composite areas such as the Trial of the Sekhemas foyer legitimately exceed the
        // old 50-million-cell ceiling (222 x 493 tiles requires 57,896,934 cells).
        // The exact TileDetails vector-shape check below remains the
        // primary guard against shifted metadata causing multi-gigabyte allocations.
        private const long MaxTerrainGridCells = 100_000_000;
        private const int TerrainTileStructureSize = 0x38;

        private static readonly EntityBackedBuffDefinition[] EntityBackedPlayerBuffs =
        {
            new(
                "Metadata/Effects/Spells/sandstorm_swipe/sandstorm_swipe_storm",
                "spear_sandstorm",
                rawStage => Math.Max(0, ((int)rawStage - 15) / 3)),
        };

        /// <summary>The terrain struct's remap the terrain was last read through (null: as compiled).</summary>
        private RemapSegment[]? terrainRemap;
        private readonly List<int> environments;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AreaInstance" /> class.
        /// </summary>
        /// <param name="address">address of the remote memory object.</param>
        internal AreaInstance(IntPtr address)
            : base(address)
        {
            this.environments = new();

            this.CurrentAreaLevel = 0;
            this.AreaHash = string.Empty;

            this.ServerDataObject = new(IntPtr.Zero);
            this.Player = new();
            this.AwakeEntities = new();
            this.EntityCaches = new()
            {
                new("/LeagueDelirium/", 1105, 1105, this.AwakeEntities), // always keep this at index 0.
                new("Breach", 1100, 1100, this.AwakeEntities),
            };

            this.TerrainMetadata = default;
            this.GridWalkableData = Array.Empty<byte>();
            this.TgtTilesLocations = new();

            Core.CoroutinesRegistrar.Add(CoroutineHandler.Start(
                this.OnPerFrame(), "[AreaInstance] Update Area Data", int.MaxValue - 4));
        }

        /// <summary>
        ///     Gets where this game build keeps the area's fields.
        /// </summary>
        public AreaInstanceLayout Layout { get; } = new();

        /// <summary>
        ///     Gets the level of the current Area.
        /// </summary>
        public int CurrentAreaLevel { get; private set; }

        /// <summary>
        ///     Gets the Hash of the current Area/Zone.
        ///     This value is sent to the client from the server.
        /// </summary>
        public string AreaHash { get; private set; }

        /// <summary>
        ///     Gets the data related to the player the user is playing.
        /// </summary>
        public ServerData ServerDataObject { get; }

        /// <summary>
        ///     Gets the player Entity.
        /// </summary>
        public Entity Player { get; }

        /// <summary>
        ///     Gets the Awake Entities of the current Area/Zone.
        ///     Awake Entities are the ones which player can interact with
        ///     e.g. Monsters, Players, NPC, Chests and etc. Sleeping entities
        ///     are opposite of awake entities e.g. Decorations, Effects, particles and etc.
        /// </summary>
        public ConcurrentDictionary<EntityNodeKey, Entity> AwakeEntities { get; }

        /// <summary>
        ///     Gets important environments entity caches. This only contain awake entities.
        /// </summary>
        public List<DisappearingEntity> EntityCaches { get; }

        /// <summary>
        ///     Gets the terrain metadata data of the current Area/Zone instance.
        /// </summary>
        public TerrainStruct TerrainMetadata { get; private set; }

        /// <summary>
        ///     Gets the terrain data of the current Area/Zone instance.
        /// </summary>
        public byte[] GridWalkableData { get; private set; }

        /// <summary>
        ///     Gets the Disctionary of Lists containing only the named tgt tiles locations.
        /// </summary>
        public Dictionary<string, List<Vector2>> TgtTilesLocations { get; private set; }

        /// <summary>
        ///     Gets a value that can convert World coordinate to Grid coordinate.
        /// </summary>
        public float WorldToGridConvertor => TileStructure.TileToWorldConversion / TileStructure.TileToGridConversion;

        /// <inheritdoc />
        protected override void CleanUpData()
        {
            this.Cleanup(false);
        }

        /// <inheritdoc />
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var data = this.Layout.Read(reader, this.Address, out var moved);

            // The code-field check places the terrain's fields seconds after the host attached in an area: an area read before
            // that kept a tile array read from where the field was compiled (the gap pass's simulation, 2026-11-08), so the
            // terrain is read again when its remap changes.
            var terrainRemap = StructRemap<TerrainStruct>.Segments;
            var remapped = !ReferenceEquals(terrainRemap, this.terrainRemap);
            this.terrainRemap = terrainRemap;
            if (hasAddressChanged || moved || remapped)
            {
                if (hasAddressChanged || moved)
                {
                    this.Cleanup(true);
                }

                this.TerrainMetadata = data.TerrainMetadata;
                this.CurrentAreaLevel = data.CurrentAreaLevel;
                this.AreaHash = $"{data.CurrentAreaHash:X}";
                if (this.TryGetTerrainDimensions(out _, out _, out _, out _))
                {
                    this.GridWalkableData = reader.ReadStdVector<byte>(
                        this.TerrainMetadata.GridWalkableData);
                    this.TgtTilesLocations = this.GetTgtFileData();
                }
                else
                {
                    this.GridWalkableData = Array.Empty<byte>();
                    this.TgtTilesLocations = new();
                    Console.WriteLine(
                        $"[AreaInstance] Rejected invalid terrain metadata at 0x{this.Address.ToInt64():X}: " +
                        $"TotalTiles={this.TerrainMetadata.TotalTiles}, " +
                        $"TileDetails={this.TerrainMetadata.TileDetailsPtr}. " +
                        $"Terrain must fit within {MaxTerrainGridCells:N0} grid cells and have a matching tile vector. " +
                        "The area may exceed the size limit, or the AreaInstance/TerrainMetadata offsets may have shifted.");
                }
            }

            this.UpdateEnvironmentAndCaches(data.Environments);
            this.ServerDataObject.Address = data.PlayerInfo.ServerDataPtr;
            this.Player.Address = data.PlayerInfo.LocalPlayerPtr;
            this.UpdateEntities(data.Entities.AwakeEntities, this.AwakeEntities, true);
            this.AddEntityBackedPlayerBuffs();
        }

        private void AddEntityBackedPlayerBuffs()
        {
            if (!this.Player.TryGetComponent<Buffs>(out var playerBuffs))
            {
                return;
            }

            foreach (var entity in this.AwakeEntities.Values)
            {
                if (!entity.IsValid)
                {
                    continue;
                }

                foreach (var definition in EntityBackedPlayerBuffs)
                {
                    if (!entity.Path.Contains(definition.EntityPathFragment, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Effect entities can be classified as useless and therefore stop refreshing
                    // cached components. Read a fresh component so changing stages remain live.
                    if (!entity.TryGetComponent<Buffs>(out var entityBuffs, false))
                    {
                        break;
                    }

                    foreach (var statusEffect in entityBuffs.StatusEffects)
                    {
                        if (!statusEffect.Key.Contains(definition.BuffNameFragment, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var syntheticEffect = statusEffect.Value;
                        syntheticEffect.Charges = (short)Math.Min(
                            short.MaxValue,
                            definition.StageToStacks(syntheticEffect.RawStage));
                        playerBuffs.AddSyntheticStatusEffect(statusEffect.Key, syntheticEffect);
                    }
                }
            }
        }

        private void UpdateEnvironmentAndCaches(StdVector environments)
        {
            this.environments.Clear();
            var reader = Core.Process.Handle;
            var envData = reader.ReadStdVector<EnvironmentStruct>(environments);
            for (var i = 0; i < envData.Length; i++)
            {
                this.environments.Add(envData[i].Key);
            }

            this.EntityCaches.ForEach((eCache) => eCache.UpdateState(this.environments));
        }

        private void AddToCacheParallel(EntityNodeKey key, string path)
        {
            for (var i = 0; i < this.EntityCaches.Count; i++)
            {
                if (this.EntityCaches[i].TryAddParallel(key, path))
                {
                    break;
                }
            }
        }

        private void UpdateEntities(
            StdMap ePtr,
            ConcurrentDictionary<EntityNodeKey, Entity> data,
            bool addToCache)
        {
            var reader = Core.Process.Handle;
            var staleCleanup = Core.GHSettings.EnableStaleEntityCleanup;
            var staleThreshold = Core.GHSettings.StaleEntityFrameThreshold;
            // No memory is read here: one thread is quicker than waking the pool's for every entity each frame.
            foreach (var kv in data)
            {
                if (kv.Value.IsValid)
                {
                    kv.Value.ResetInvalidFrames();
                }
                else
                {
                    kv.Value.IncrementInvalidFrames();

                    var shouldRemove = false;

                    if (kv.Value.EntityState == EntityStates.MonsterFriendly ||
                        (kv.Value.CanExplodeOrRemovedFromGame &&
                        this.Player.DistanceFrom(kv.Value) < AreaInstanceConstants.NETWORK_BUBBLE_RADIUS))
                    {
                        shouldRemove = true;
                    }

                    // Time-based cleanup: remove entities invalid for N consecutive frames.
                    if (!shouldRemove && staleCleanup &&
                        kv.Value.ConsecutiveInvalidFrames >= staleThreshold)
                    {
                        shouldRemove = true;
                    }

                    if (shouldRemove)
                    {
                        data.TryRemove(kv.Key, out _);
                    }
                }

                kv.Value.IsValid = false;
            }

            reader.ReadStdMap<EntityNodeKey, EntityNodeValue>(ePtr, 100000, false, (key, value) =>
            {
                if (!EntityFilter.IgnoreVisualsAndDecorations(key))
                {
                    return false;
                }

                // Drop torn-read entries whose pointer can't back a real entity.
                if (!SafeMemoryHandle.IsValidAddress(value.EntityPtr))
                {
                    return false;
                }

                if (data.TryGetValue(key, out var entity))
                {
                    entity.Address = value.EntityPtr;
                }
                else
                {
                    entity = new Entity(value.EntityPtr);
                    if (!string.IsNullOrEmpty(entity.Path))
                    {
                        data[key] = entity;
                        if (addToCache)
                        {
                            this.AddToCacheParallel(key, entity.Path);
                        }
                    }
                    else
                    {
                        entity = null;
                    }
                }

                return true;
            });
        }

        private Dictionary<string, List<Vector2>> GetTgtFileData()
        {
            if (!this.TryGetTerrainDimensions(out var tileCountX, out _, out _, out _))
            {
                return new();
            }

            var reader = Core.Process.Handle;
            var tileData = reader.ReadStdVector<TileStructure>(this.TerrainMetadata.TileDetailsPtr);
            var ret = new Dictionary<string, List<Vector2>>();
            object mylock = new();
            Parallel.For(
                0,
                tileData.Length,
                // happens on every thread, rather than every iteration.
                () => new Dictionary<string, List<Vector2>>(),
                // happens on every iteration.
                (tileNumber, _, localstate) =>
                {
                    var tile = tileData[tileNumber];
                    var tgtFile = reader.ReadMemory<TgtFileStruct>(tile.TgtFilePtr);
                    var tgtName = reader.ReadStdWString(tgtFile.TgtPath);
                    if (string.IsNullOrEmpty(tgtName))
                    {
                        return localstate;
                    }

                    if (tile.RotationSelector % 2 == 0)
                    {
                        tgtName += $"x:{tile.tileIdX}-y:{tile.tileIdY}";
                    }
                    else
                    {
                        tgtName += $"x:{tile.tileIdY}-y:{tile.tileIdX}";
                    }

                    var loc = new Vector2
                    {
                        Y = (tileNumber / tileCountX) * TileStructure.TileToGridConversion,
                        X = (tileNumber % tileCountX) * TileStructure.TileToGridConversion
                    };

                    if (localstate.ContainsKey(tgtName))
                    {
                        localstate[tgtName].Add(loc);
                    }
                    else
                    {
                        localstate[tgtName] = new() { loc };
                    }

                    return localstate;
                },
                finalresult => // happens on every thread, rather than every iteration.
                {
                    lock (mylock)
                    {
                        foreach (var kv in finalresult)
                        {
                            if (!ret.TryGetValue(kv.Key, out var value))
                            {
                                value = new();
                                ret[kv.Key] = value;
                            }

                            value.AddRange(kv.Value);
                        }
                    }
                });

            return ret;
        }

        private bool TryGetTerrainDimensions(
            out int tileCountX,
            out int tileCountY,
            out int gridSizeX,
            out int gridSizeY)
        {
            tileCountX = 0;
            tileCountY = 0;
            gridSizeX = 0;
            gridSizeY = 0;

            var totalTilesX = this.TerrainMetadata.TotalTiles.X;
            var totalTilesY = this.TerrainMetadata.TotalTiles.Y;
            var conversion = TileStructure.TileToGridConversion;
            if (conversion <= 0 ||
                totalTilesX <= 0 || totalTilesY <= 0 ||
                totalTilesX > int.MaxValue / conversion ||
                totalTilesY > int.MaxValue / conversion)
            {
                return false;
            }

            var candidateGridSizeX = totalTilesX * conversion;
            var candidateGridSizeY = totalTilesY * conversion;
            if (candidateGridSizeY > MaxTerrainGridCells / candidateGridSizeX)
            {
                return false;
            }

            var tileDetails = this.TerrainMetadata.TileDetailsPtr;
            var tileDetailsBytes = tileDetails.Last.ToInt64() - tileDetails.First.ToInt64();
            var expectedTileDetailsBytes = totalTilesX * totalTilesY * TerrainTileStructureSize;
            if (tileDetails.First == IntPtr.Zero || tileDetails.Last.ToInt64() < tileDetails.First.ToInt64() ||
                tileDetails.End.ToInt64() < tileDetails.Last.ToInt64() ||
                tileDetailsBytes != expectedTileDetailsBytes)
            {
                return false;
            }

            tileCountX = (int)totalTilesX;
            tileCountY = (int)totalTilesY;
            gridSizeX = (int)candidateGridSizeX;
            gridSizeY = (int)candidateGridSizeY;
            return true;
        }

        /// <summary>
        ///     knows how to clean up the <see cref="AreaInstance"/> class.
        /// </summary>
        /// <param name="isAreaChange">
        ///     true in case it's a cleanup due to area change otherwise false.
        /// </param>
        private void Cleanup(bool isAreaChange)
        {
            this.AwakeEntities.Clear();
            this.EntityCaches.ForEach((e) => e.Clear());

            if (!isAreaChange)
            {
                this.environments.Clear();
                this.CurrentAreaLevel = 0;
                this.AreaHash = string.Empty;
                this.ServerDataObject.Address = IntPtr.Zero;
                this.Player.Address = IntPtr.Zero;
                this.TerrainMetadata = default;
                this.GridWalkableData = Array.Empty<byte>();
                this.TgtTilesLocations.Clear();
            }
        }

        /// <summary>
        ///     Scans the game's SleepingEntities memory map (decorations / out-of-bubble objects)
        ///     and invokes <paramref name="onMatch"/> for each entity whose path satisfies
        ///     <paramref name="pathFilter"/>. Sleeping entities exist at a larger radius than awake
        ///     ones, so this can surface objects beyond the network bubble.
        ///     NOTE: the callback runs in parallel (see <see cref="SafeMemoryHandle.ReadStdMap"/>),
        ///     so <paramref name="onMatch"/> and anything it writes to must be thread-safe.
        /// </summary>
        /// <param name="pathFilter">Predicate on the entity path deciding which entities to keep.</param>
        /// <param name="onMatch">Invoked (possibly concurrently) for each matching entity.</param>
        /// <returns>Total number of sleeping entities scanned.</returns>
        public int ScanSleepingEntities(Func<string, bool> pathFilter, Action<EntityNodeKey, Entity> onMatch)
        {
            if (this.Address == IntPtr.Zero)
            {
                return 0;
            }

            var reader = Core.Process.Handle;
            var data = this.Layout.Read(reader, this.Address);
            return reader.ReadStdMap<EntityNodeKey, EntityNodeValue>(
                data.Entities.SleepingEntities,
                500000,
                true,
                (key, value) =>
                {
                    // Drop torn-read entries whose pointer can't back a real entity.
                    if (!SafeMemoryHandle.IsValidAddress(value.EntityPtr))
                    {
                        return false;
                    }

                    var entity = new Entity(value.EntityPtr);
                    if (!string.IsNullOrEmpty(entity.Path) && pathFilter(entity.Path))
                    {
                        onMatch(key, entity);
                    }

                    return true;
                });
        }

        private IEnumerator<Wait> OnPerFrame()
        {
            while (true)
            {
                yield return new Wait(GameHelperEvents.PerFrameDataUpdate);
                try
                {
                    if (this.Address != IntPtr.Zero)
                    {
                        this.UpdateData(false);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AreaInstance.OnPerFrame] {ex}");
                }
            }
        }

        private readonly record struct EntityBackedBuffDefinition(
            string EntityPathFragment,
            string BuffNameFragment,
            Func<uint, int> StageToStacks);
    }
}
