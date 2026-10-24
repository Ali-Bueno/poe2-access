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

        /// <summary>
        ///     A closed barrier this near (grid cells) the line from a route's end to its target cuts the way: the beam
        ///     of TutorialBlocker3 ran 55 cells across its centre, the target snaps up to 60 cells to walkable ground.
        /// </summary>
        private const float BarrierReachCells = 30f;

        /// <summary>A route ending this near (grid cells) its target reached it, whatever stands beside.</summary>
        private const float MinBarrierGapCells = 8f;

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

        /// <summary>Live walkable grids kept for routing, one per SampleMs: a cell must be walkable in all of them.</summary>
        private const int MaxSamples = 4;

        private const int SampleMs = 500;

        private readonly HostMemoryReader memory;
        private readonly UiElementReader elements;
        private readonly InventoryReader inventory;
        private readonly LandmarkTable landmarks;
        private readonly Func<string> areaId;
        private readonly PluginLog log;
        private readonly Dictionary<uint, ScanEntry> remembered = new();
        private readonly ExplorationMap explored = new();
        /// <summary>Recent live walkable grids (GameLayout-free: whole copies), oldest first.</summary>
        private readonly Queue<byte[]> samples = new();
        private readonly BarrierMemory barriers = new();
        /// <summary>Closed barriers seen in this area by entity id (grid position), kept past the network bubble.</summary>
        private readonly Dictionary<uint, Vector2> closedBarriers = new();
        private readonly object samplesLock = new();
        private long lastSampleAt;
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
            lock (this.samplesLock)
            {
                this.samples.Clear();
                this.barriers.Reset();
                this.closedBarriers.Clear();
            }
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
            var partial = false;
            if (route == null && RoutePlanner.FindRoute(grid, row, start, goal, true, out _) is { Count: > 1 } nearest &&
                RoutePlanner.Length(nearest) * gridSize >= StepWorldUnits)
            {
                // No way all the way (a gate the quest opens, a bridge out of the grid): the legs lead as close as the
                // ground goes, like the guide beacon; the distance stays the straight line.
                (route, partial) = (nearest, true);
            }

            // A target inside a wall snaps to the nearest walkable cell, which can lie on this side of a closed barrier
            // (the Riverbank exit behind the boss's gate read as a full route): the way stops at the barrier.
            var barrier = route is { Count: > 0 } && entry.Kind != "barrier" && BarrierBetween(this.ClosedBarriers(), route[^1], goal);
            partial |= barrier;

            this.log.Debug($"Route to {entry.Name}: {(route == null ? "none" : $"{route.Count} cells")} in {timer.ElapsedMilliseconds} ms from cell ({start.X:F0},{start.Y:F0}) to ({goal.X:F0},{goal.Y:F0})");
            if (route == null)
            {
                var loaded = area.GridWalkableData;
                var fromLoaded = loaded.Length == grid.Length ? RoutePlanner.FindRoute(loaded, row, start, goal) : null;
                this.log.Debug($"No route to {entry.Name}: grid {grid.Length} bytes, {row} per row; start {start} walkable={RoutePlanner.IsWalkable(grid, row, (int)start.X, (int)start.Y)}, goal {goal} walkable={RoutePlanner.IsWalkable(grid, row, (int)goal.X, (int)goal.Y)}; the grid as loaded {(fromLoaded == null ? "has none either" : $"has one of {fromLoaded.Count} cells")}");
            }

            var length = route == null || partial ? Distance(origin, entry) : RoutePlanner.Length(route) * gridSize;
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
                return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[DirectionOf(goal - start)], 0, null, route != null && !partial);
            }

            var legSteps = Math.Max(1, (int)Math.Round(legs[0].Length * gridSize / StepWorldUnits));
            var then = legs.Count > 1 ? Announcements.StickDirections[legs[1].Direction] : null;
            return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[legs[0].Direction], legSteps, then, true, partial, barrier);
        }

        /// <summary>
        ///     Grid positions of the closed barriers (TriggerableBlockage, not a door the player opens): those awake now,
        ///     and those seen closed earlier in this area and not seen open since (past the bubble the game forgets them).
        ///     Frame thread only.
        /// </summary>
        public IReadOnlyList<Vector2> ClosedBarriers()
        {
            lock (this.samplesLock)
            {
                foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
                {
                    if (!entity.IsValid || !entity.TryGetComponent<TriggerableBlockage>(out var blockage) ||
                        entity.TryGetComponent<Targetable>(out _) || !entity.TryGetComponent<Render>(out var render))
                    {
                        continue;
                    }

                    if (blockage.IsBlocked)
                    {
                        this.closedBarriers[entity.Id] = new Vector2(render.GridPosition.X, render.GridPosition.Y);
                    }
                    else
                    {
                        this.closedBarriers.Remove(entity.Id);
                    }
                }

                return this.closedBarriers.Values.ToList();
            }
        }

        /// <summary>A closed barrier lies within <see cref="BarrierReachCells"/> of the line from a route's end to its target.</summary>
        public static bool BarrierBetween(IReadOnlyList<Vector2> barriers, Vector2 routeEnd, Vector2 target)
        {
            var line = target - routeEnd;
            var lengthSquared = line.LengthSquared();
            if (lengthSquared < MinBarrierGapCells * MinBarrierGapCells)
            {
                return false; // the route reaches the target (the boss stands by his arena's gate)
            }

            foreach (var barrier in barriers)
            {
                var along = lengthSquared == 0 ? 0 : Math.Clamp(Vector2.Dot(barrier - routeEnd, line) / lengthSquared, 0f, 1f);
                if (Vector2.Distance(barrier, routeEnd + (line * along)) <= BarrierReachCells)
                {
                    return true;
                }
            }

            return false;
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

            // A cell is walkable for routes only if it was in every recent sample: a closed tutorial barrier
            // (TutorialBlocker3 by the Riverbank bridge, 2026-10-23) flickers in the live grid every few seconds,
            // which split the ground in two and made routes and the guide's "the way is closed" flap. A gate that
            // really opens is walkable again once the samples agree (about two seconds). Past the network bubble the
            // grid cannot be trusted at all: see BarrierMemory.
            lock (this.samplesLock)
            {
                var now = Environment.TickCount64;
                if (this.samples.Count > 0 && this.samples.Peek().Length != live.Length)
                {
                    this.samples.Clear();
                }

                if (this.samples.Count == 0 || now - this.lastSampleAt >= SampleMs)
                {
                    this.samples.Enqueue(live);
                    this.lastSampleAt = now;
                    if (area.Player.TryGetComponent<Render>(out var player))
                    {
                        var before = this.barriers.ClosedCount;
                        this.barriers.Observe(live, snapshot, area.TerrainMetadata.BytesPerRow, (int)player.GridPosition.X, (int)player.GridPosition.Y);
                        if (this.barriers.ClosedCount != before)
                        {
                            this.log.Debug($"Barrier cells remembered closed: {this.barriers.ClosedCount}");
                        }
                    }
                    while (this.samples.Count > MaxSamples)
                    {
                        this.samples.Dequeue();
                    }
                }

                if (!live.AsSpan().SequenceEqual(snapshot))
                {
                    this.log.Debug($"Walkable grid changed since the area loaded: {Changed(snapshot, live)} cells");
                }

                var stable = (byte[])live.Clone();
                foreach (var sample in this.samples)
                {
                    for (var i = 0; i < stable.Length; i++)
                    {
                        var both = stable[i];
                        var other = sample[i];
                        if ((other & 0x0F) == 0)
                        {
                            both &= 0xF0;
                        }

                        if ((other & 0xF0) == 0)
                        {
                            both &= 0x0F;
                        }

                        stable[i] = both;
                    }
                }

                this.barriers.Apply(stable);
                return stable;
            }
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
            else if (entity.TryGetComponent<TriggerableBlockage>(out var blockage))
            {
                // Tutorial beams and gates: where the way is shut, while it is (BarrierWatch says when one opens).
                category = blockage.IsBlocked ? ScanCategory.Objects : null;
                name = name.Length > 0 ? name : Announcements.ClosedBarrier;
                kind = "barrier";
            }
            else
            {
                category = entity.TryGetComponent<Targetable>(out _) ? ScanCategory.Objects : null; // stash, bench, well
            }

            if (category == null || name.Length == 0)
            {
                return null; // unnamed placeholders are not things a player can find
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
