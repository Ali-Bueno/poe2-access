namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteEnums;
    using GameHelper.RemoteEnums.Entity;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;

    /// <summary>
    ///     The scanner's view of the area: GameHelper's awake entities sorted into <see cref="ScanCategory"/>,
    ///     named as the game names them, nearest first. Exits, waypoints and NPCs stay listed at their last known
    ///     place once seen (the game unloads far entities), until the area changes.
    /// </summary>
    public sealed class EntityCatalog
    {
        /// <summary>One spoken step: about a quarter second of walking (0.5 s moved ~200 world units, 2026-10-23).</summary>
        private const float StepWorldUnits = 100f;

        /// <summary>How far (grid units) the grid's staircase may stray from a straight leg before it counts as a turn.</summary>
        private const float CornerToleranceGrid = 3f;

        /// <summary>Legs shorter than half a step are sidesteps the game's wall sliding takes care of.</summary>
        private const float MinLegGrid = 5f;

        /// <summary>World offset used to measure how the camera draws the world axes.</summary>
        private const float ProbeWorldUnits = 100f;

        private static readonly ScanCategory[] Landmarks = { ScanCategory.Exits, ScanCategory.Waypoints, ScanCategory.Npcs };

        /// <summary>
        ///     Remembered while out of the game's range too, but dropped once the player comes back near the place and
        ///     the game no longer lists them (chest opened, item picked up).
        /// </summary>
        private static readonly ScanCategory[] Loot = { ScanCategory.Chests, ScanCategory.Items };

        /// <summary>The game keeps entities this close to the player awake; a loot entry missing inside it is gone.</summary>
        private const float AwakeWorld = 1000f;

        /// <summary>Landmark ids live above entity ids so the two never collide.</summary>
        private const uint LandmarkIdBase = 0x80000000;

        /// <summary>Unexplored entries: this base plus their block (row in the high half), above the tile landmarks.</summary>
        private const uint UnexploredIdBase = 0xC0000000;

        private const int MaxUnexplored = 8;

        /// <summary>The frontier scans the whole grid: recomputed at most this often (the many Entries() callers share it).</summary>
        private const int UnexploredRefreshMs = 2000;

        private const int TrackMs = 250;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly InventoryReader inventory;
        private readonly LandmarkTable landmarks;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly Dictionary<uint, ScanEntry> remembered = new();
        private readonly ExplorationMap explored = new();
        private IReadOnlyList<ScanEntry> unexplored = Array.Empty<ScanEntry>();
        private long unexploredAt = long.MinValue / 2;
        private long trackedAt = long.MinValue / 2;

        public EntityCatalog(HostMemoryReader memory, UiElementReader elements, InventoryReader inventory, LandmarkTable landmarks, Func<string> areaId, PluginLog log)
        {
            this.QuestTexts = () => Array.Empty<string>();
            this.log = log;
            this.memory = memory;
            this.elements = elements;
            this.inventory = inventory;
            this.landmarks = landmarks;
            this.areaId = areaId;
        }

        /// <summary>The quest tracker's texts on screen ("Find Clearfell", "Slay the Bloated Miller"), for the Quest category.</summary>
        public Func<IReadOnlyList<string>> QuestTexts { get; set; }

        /// <summary>A thing in the world, not a named place of the area's tiles.</summary>
        public static bool IsEntity(ScanEntry entry) => entry.Id < LandmarkIdBase;

        /// <summary>A new area: nothing seen yet.</summary>
        public void Forget()
        {
            this.remembered.Clear();
            this.explored.Reset();
            this.unexplored = Array.Empty<ScanEntry>();
            this.unexploredAt = long.MinValue / 2;
        }

        /// <summary>Per frame while playing: the ground around the player counts as seen (unexplored category).</summary>
        public void Track()
        {
            var now = Environment.TickCount64;
            if (now - this.trackedAt < TrackMs)
            {
                return;
            }

            this.trackedAt = now;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var row = area.TerrainMetadata.BytesPerRow;
            if (row > 0 && area.Player.TryGetComponent<Render>(out var player))
            {
                this.explored.Visit(new Vector2(player.GridPosition.X, player.GridPosition.Y), row * 2, area.GridWalkableData.Length / row);
            }
        }

        /// <summary>Everything the scanner can name, nearest first.</summary>
        public IReadOnlyList<ScanEntry> Entries()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!area.Player.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<ScanEntry>();
            }

            var found = new Dictionary<uint, ScanEntry>();
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                if (this.Classify(entity, render) is not { } entry)
                {
                    this.remembered.Remove(entity.Id); // a story variant left play (Una after the quest step)
                    continue;
                }

                found[entry.Id] = entry;
                if (Landmarks.Contains(entry.Category) || Loot.Contains(entry.Category))
                {
                    this.remembered[entry.Id] = entry;
                }
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            foreach (var landmark in this.remembered.Values.ToList())
            {
                if (found.ContainsKey(landmark.Id))
                {
                    continue;
                }

                if (Loot.Contains(landmark.Category) && Vector2.Distance(here, new Vector2(landmark.WorldX, landmark.WorldY)) < AwakeWorld)
                {
                    this.remembered.Remove(landmark.Id); // near its place and no longer listed: opened or picked up
                    continue;
                }

                found.Add(landmark.Id, landmark);
            }

            foreach (var landmark in this.TileLandmarks(area, player.WorldPosition.Z))
            {
                found.TryAdd(landmark.Id, landmark);
            }

            foreach (var frontier in this.Unexplored(area, player))
            {
                found.TryAdd(frontier.Id, frontier);
            }

            var origin = player.WorldPosition;
            var sorted = found.Values.OrderBy(entry => Distance(origin, entry)).ToList();
            sorted.InsertRange(0, QuestMatcher.Targets(sorted, this.QuestTexts()));
            return sorted;
        }

        /// <summary>The walkable grid as the game holds it now (see <see cref="WalkableGrid"/>); false outside an area.</summary>
        public bool TryLiveGrid(out byte[] grid, out int bytesPerRow)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            bytesPerRow = area.TerrainMetadata.BytesPerRow;
            grid = bytesPerRow > 0 ? this.WalkableGrid(area) : Array.Empty<byte>();
            return grid.Length > 0;
        }

        /// <summary>
        ///     "Clearfell Encampment, 116 steps, down 3, then left": the length of the walkable route, the leg to walk
        ///     now and where it turns next, so the player knows when to turn instead of walking past the corner (a
        ///     wall between the player and the target is walked around, not into); the straight line, marked "no
        ///     route", when the grid has no way there.
        /// </summary>
        public string Describe(ScanEntry entry)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!area.Player.TryGetComponent<Render>(out var player))
            {
                return entry.Name;
            }

            var origin = player.WorldPosition;
            var gridSize = area.WorldToGridConvertor;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var grid = this.WalkableGrid(area);
            var row = area.TerrainMetadata.BytesPerRow;
            var start = new Vector2(origin.X, origin.Y) / gridSize;
            var goal = new Vector2(entry.WorldX, entry.WorldY) / gridSize;
            var route = RoutePlanner.FindRoute(grid, row, start, goal);
            this.log.Debug($"Route to {entry.Name}: {(route == null ? "none" : $"{route.Count} cells")} in {timer.ElapsedMilliseconds} ms");
            if (route == null)
            {
                this.log.Debug($"No route to {entry.Name}: grid {grid.Length} bytes, {row} per row; start {start} walkable={RoutePlanner.IsWalkable(grid, row, (int)start.X, (int)start.Y)}, goal {goal} walkable={RoutePlanner.IsWalkable(grid, row, (int)goal.X, (int)goal.Y)}");
            }

            var length = route == null ? Distance(origin, entry) : RoutePlanner.Length(route) * gridSize;
            var steps = (int)Math.Round(length / StepWorldUnits);
            if (steps < 1)
            {
                return Announcements.ScanEntry(entry.Name, 0, string.Empty, 0, null, true);
            }

            var world = Core.States.InGameStateObject.CurrentWorldInstance;
            var here = world.WorldToScreen(origin);
            var alongX = world.WorldToScreen(Offset(origin, ProbeWorldUnits, 0)) - here;
            var alongY = world.WorldToScreen(Offset(origin, 0, ProbeWorldUnits)) - here;
            int DirectionOf(Vector2 delta) => StickDirection.Octant(alongX, alongY, delta.X, delta.Y);
            var legs = route == null
                ? new List<(int Direction, float Length)>()
                : RoutePlanner.Legs(RoutePlanner.Corners(route, CornerToleranceGrid), DirectionOf, MinLegGrid);
            if (legs.Count == 0)
            {
                return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[DirectionOf(goal - start)], 0, null, route != null);
            }

            var legSteps = Math.Max(1, (int)Math.Round(legs[0].Length * gridSize / StepWorldUnits));
            var then = legs.Count > 1 ? Announcements.StickDirections[legs[1].Direction] : null;
            return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[legs[0].Direction], legSteps, then, true);
        }

        /// <summary>
        ///     The walkable grid as the game holds it now. GameHelper copies it once per area, but the game edits it
        ///     in place when a tutorial barrier or a door opens or closes; the vector itself does not move.
        /// </summary>
        private byte[] WalkableGrid(AreaInstance area)
        {
            var snapshot = area.GridWalkableData;
            var live = this.memory.ReadStdVector<byte>(area.TerrainMetadata.GridWalkableData, snapshot.Length);
            if (live.Length != snapshot.Length)
            {
                return snapshot;
            }

            if (!live.AsSpan().SequenceEqual(snapshot))
            {
                this.log.Debug($"Walkable grid changed since the area loaded: {Changed(snapshot, live)} cells");
            }

            return live;
        }

        private static int Changed(byte[] before, byte[] after)
        {
            var count = 0;
            for (var i = 0; i < before.Length; i++)
            {
                count += ((before[i] & 0xF) == 0) != ((after[i] & 0xF) == 0) ? 1 : 0;
                count += ((before[i] >> 4) == 0) != ((after[i] >> 4) == 0) ? 1 : 0;
            }

            return count;
        }

        /// <summary>Named tiles of this area (<see cref="LandmarkTable"/>), placed from the area's tile grid.</summary>
        private IEnumerable<ScanEntry> TileLandmarks(AreaInstance area, float height)
        {
            var tiles = area.TgtTilesLocations;
            var toWorld = area.WorldToGridConvertor;
            var index = 0u;
            foreach (var (key, label) in this.landmarks.For(this.areaId()))
            {
                index++;
                var spots = key.Contains(':', StringComparison.Ordinal)
                    ? tiles.TryGetValue(key, out var exact) ? exact : new List<Vector2>()
                    : tiles.Where(tile => tile.Key.StartsWith(key + ".", StringComparison.Ordinal)).SelectMany(tile => tile.Value).ToList();
                if (spots.Count == 0)
                {
                    continue;
                }

                var centre = new Vector2(spots.Average(spot => spot.X), spots.Average(spot => spot.Y)) * toWorld;
                yield return new ScanEntry(LandmarkIdBase + index, ScanCategory.Exits, label, centre.X, centre.Y, height);
            }
        }

        /// <summary>Where unseen ground begins (<see cref="ExplorationMap"/>), refreshed every UnexploredRefreshMs.</summary>
        private IReadOnlyList<ScanEntry> Unexplored(AreaInstance area, Render player)
        {
            var now = Environment.TickCount64;
            if (now - this.unexploredAt < UnexploredRefreshMs || !this.TryLiveGrid(out var grid, out var row))
            {
                return this.unexplored;
            }

            this.unexploredAt = now;
            var toWorld = area.WorldToGridConvertor;
            var from = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            this.unexplored = this.explored.Frontier(grid, row, from, MaxUnexplored)
                .Select(cell => new ScanEntry(
                    UnexploredIdBase + (uint)(((int)cell.Y / ExplorationMap.BlockCells) << 15) + (uint)((int)cell.X / ExplorationMap.BlockCells),
                    ScanCategory.Unexplored,
                    Announcements.UnexploredArea,
                    cell.X * toWorld,
                    cell.Y * toWorld,
                    player.WorldPosition.Z))
                .ToList();
            return this.unexplored;
        }

        private static float Distance(StdTuple3D<float> origin, ScanEntry entry) =>
            new Vector2(entry.WorldX - origin.X, entry.WorldY - origin.Y).Length();

        private static StdTuple3D<float> Offset(StdTuple3D<float> origin, float dx, float dy) =>
            new() { X = origin.X + dx, Y = origin.Y + dy, Z = origin.Z };

        /// <summary>
        ///     Category and name, or null for what the scanner leaves out (self, dead, opened, unnamed, and what is
        ///     not in play: the town holds every story variant of an NPC, only one of them targetable).
        /// </summary>
        private ScanEntry? Classify(Entity entity, Render render)
        {
            if (!this.InPlay(entity))
            {
                return null;
            }

            var name = this.elements.ReadWStringStructAt(render.Address + GameLayout.RenderComponent.NameOffset) ?? string.Empty;
            var path = entity.Path ?? string.Empty;
            var kind = string.Empty;
            ScanCategory? category;
            if (entity.EntityType == EntityTypes.Player)
            {
                category = entity.EntitySubtype == EntitySubtypes.PlayerSelf ? null : ScanCategory.Players;
                if (entity.TryGetComponent<Player>(out var other) && other.Name.Length > 0)
                {
                    name = Announcements.OtherPlayer(other.Name, name); // the render name is the class
                }
            }
            else if (path.Contains("AreaTransition", StringComparison.Ordinal))
            {
                category = ScanCategory.Exits;
                kind = "exit";
            }
            else if (path.Contains("Waypoint", StringComparison.Ordinal) || path.Contains("Checkpoint", StringComparison.Ordinal) ||
                     path.Contains("Portal", StringComparison.Ordinal))
            {
                category = ScanCategory.Waypoints;
                kind = path.Contains("Checkpoint", StringComparison.Ordinal) ? "checkpoint" : path.Contains("Waypoint", StringComparison.Ordinal) ? "waypoint" : "portal";
            }
            else if (entity.TryGetComponent<WorldItem>(out var worldItem))
            {
                category = ScanCategory.Items;
                name = this.inventory.DescribeItemEntity(worldItem.ItemEntityAddress, name) ?? name;
                kind = this.inventory.ItemKind(worldItem.ItemEntityAddress);
            }
            else if (entity.EntityType == EntityTypes.Chest)
            {
                category = entity.TryGetComponent<Chest>(out var chest) && chest.IsOpened ? null : ScanCategory.Chests;
                kind = "chest";
            }
            else if (entity.EntityType == EntityTypes.Shrine)
            {
                category = entity.TryGetComponent<Shrine>(out var shrine) && shrine.IsUsed ? null : ScanCategory.Chests;
                kind = "shrine";
            }
            else if (entity.EntityType == EntityTypes.Monster)
            {
                // A friendly monster with the NPC component is a character (Mortimer by the Miller); minions have none.
                category = IsAliveEnemy(entity) ? ScanCategory.Enemies : entity.TryGetComponent<NPC>(out _) ? ScanCategory.Npcs : null;
                if (category == ScanCategory.Enemies && entity.TryGetComponent<ObjectMagicProperties>(out var magic) && magic.Rarity != Rarity.Normal)
                {
                    name = Announcements.EnemyWithRarity(name, magic.Rarity);
                }
            }
            else if (entity.EntityType == EntityTypes.NPC || entity.TryGetComponent<NPC>(out _))
            {
                category = ScanCategory.Npcs;
            }
            else
            {
                category = entity.TryGetComponent<Targetable>(out _) ? ScanCategory.Objects : null; // stash, bench, well
            }

            if (category == null || name.Length == 0)
            {
                return null; // unnamed placeholders ("TutorialBlocker1") are not things a player can find
            }

            var position = render.WorldPosition;
            return new ScanEntry(entity.Id, category.Value, name, position.X, position.Y, position.Z, kind);
        }

        /// <summary>A living monster that is not the player's (minions are friendly).</summary>
        public static bool IsAliveEnemy(Entity entity) =>
            entity.TryGetComponent<Life>(out var life) && life.IsAlive &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly);

        private bool InPlay(Entity entity) => IsInPlay(this.memory, entity);

        /// <summary>Targetable by its own flag (<see cref="GameLayout.TargetableComponent"/>); no component: in play.</summary>
        public static bool IsInPlay(HostMemoryReader memory, Entity entity) =>
            !entity.TryGetComponent<Targetable>(out var target) ||
            (memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0);
    }
}
