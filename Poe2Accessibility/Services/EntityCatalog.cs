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
    ///     named as the game names them, nearest first. Only what a sighted player has seen (the minimap shows no
    ///     more): enemies while on screen, everything else once it has been on screen or on explored ground, and the
    ///     area's named tiles once their ground is explored. Exits, waypoints and NPCs then stay listed at their last
    ///     known place (the game unloads far entities), until the area changes.
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

        /// <summary>The place of the last death: one id between the tile landmarks and the unexplored blocks.</summary>
        private const uint DeathPlaceId = UnexploredIdBase - 1;

        /// <summary>
        ///     Where the player last died (D2R death place), with the area instance it belongs to: listed among the exits
        ///     whenever that instance is current, until the next death.
        /// </summary>
        private (string Area, ScanEntry Entry)? deathPlace;

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
        /// <summary>Entities and named tiles the player has seen in this area (see <see cref="Sighted"/>).</summary>
        private readonly HashSet<uint> sighted = new();
        /// <summary>
        ///     Title of each ground item seen in this area ("Pergamino de sabiduría") → its spoken name ("Pergamino de
        ///     sabiduría, 3"; empty for gold, which its pick-up says), kept past the pick-up: its HUD tooltip can come later.
        /// </summary>
        private readonly Dictionary<string, string> itemTitles = new(StringComparer.Ordinal);
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

        /// <summary>Monsters with the Targetable component this many, none flagged in play, mean the flag moved (game update).</summary>
        private const int TargetableJudgedOn = 20;

        /// <summary>
        ///     Living monsters, characters and players in one pass whose Render names judge where the names are (see
        ///     <see cref="LayoutProbe"/>). Gathered over time, the dead adds of a boss fight left freed renders that read
        ///     nothing: "the names of characters and monsters" were reported lost mid-fight (2026-10-24).
        /// </summary>
        private const int RendersToJudge = 15;

        /// <summary>
        ///     The Targetable in-play flag (<see cref="GameLayout.TargetableComponent"/>) is believed until a game update
        ///     visibly breaks it; then everything counts as in play rather than nothing.
        /// </summary>
        private static bool targetableTrusted = true;

        private readonly HashSet<uint> unflaggedMonsters = new();
        private bool targetableConfirmed;

        /// <summary>Checks the entity name field once enough named things are awake (set by the plugin).</summary>
        public LayoutProbe? Probe { get; set; }

        /// <summary>Where a broken in-play flag is reported (set by the plugin).</summary>
        public CompatibilityReport? Report { get; set; }

        /// <summary>The quest tracker's texts on screen ("Find Clearfell", "Slay the Bloated Miller"), for the Quest category.</summary>
        public Func<IReadOnlyList<string>> QuestTexts { get; set; }

        /// <summary>The current area's name in the client language (its name in an objective is not a destination).</summary>
        public Func<string> AreaName { get; set; } = () => string.Empty;

        /// <summary>The spoken name of the ground item whose tooltip holds these texts (empty for gold); null when none does.</summary>
        public string? ItemOfTooltip(IEnumerable<string> texts)
        {
            foreach (var text in texts)
            {
                if (this.itemTitles.TryGetValue(text, out var spoken))
                {
                    return spoken;
                }
            }

            return null;
        }

        /// <summary>A thing in the world, not a named place of the area's tiles.</summary>
        public static bool IsEntity(ScanEntry entry) => entry.Id < LandmarkIdBase;

        /// <summary>A new area: nothing seen yet.</summary>
        public void Forget()
        {
            this.remembered.Clear();
            this.sighted.Clear();
            this.itemTitles.Clear();
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
            // Known to the game but not yet to the player: only the quest marker may point at them.
            var unseen = new List<ScanEntry>();
            var renders = new List<IntPtr>();
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                this.CheckLayout(entity, render, renders);
                if (this.Classify(entity, render) is not { } entry)
                {
                    this.remembered.Remove(entity.Id); // a story variant left play (Una after the quest step)
                    continue;
                }

                if (!this.Sighted(entry, render.WorldPosition, area.WorldToGridConvertor, OnMinimap(entity)))
                {
                    unseen.Add(entry);
                    continue;
                }

                found[entry.Id] = entry;
                if (Landmarks.Contains(entry.Category) || Loot.Contains(entry.Category))
                {
                    this.remembered[entry.Id] = entry;
                }
            }

            if (this.Probe != null && renders.Count >= RendersToJudge)
            {
                this.Probe.ProbeRenderNames(renders);
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
                var place = new StdTuple3D<float> { X = landmark.WorldX, Y = landmark.WorldY, Z = landmark.WorldZ };
                if (this.Sighted(landmark, place, area.WorldToGridConvertor, false))
                {
                    found.TryAdd(landmark.Id, landmark);
                }
                else
                {
                    unseen.Add(landmark);
                }
            }

            if (this.deathPlace is { } death && death.Area == area.AreaHash)
            {
                found.TryAdd(death.Entry.Id, death.Entry);
            }

            foreach (var frontier in this.Unexplored(area, player))
            {
                found.TryAdd(frontier.Id, frontier);
            }

            var origin = player.WorldPosition;
            var sorted = found.Values.OrderBy(entry => Distance(origin, entry)).ToList();
            // The quest marker: what the objective names is found wherever the game knows it, seen or not (user,
            // 2026-10-24: the objective guided before the scanner kept to what is seen; docs/research/minimap.md).
            var questPool = sorted.Concat(unseen.Where(entry => !found.ContainsKey(entry.Id))).OrderBy(entry => Distance(origin, entry)).ToList();
            sorted.InsertRange(0, QuestMatcher.Targets(questPool, this.QuestTexts(), this.AreaName()));
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
        public string Describe(ScanEntry entry) => this.Describe(entry, out _);

        /// <inheritdoc cref="Describe(ScanEntry)"/>
        /// <param name="cut">The phrase says the way stops short or a closed barrier cuts it.</param>
        public string Describe(ScanEntry entry, out bool cut)
        {
            cut = false;
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
            cut = partial;
            if (legs.Count == 0)
            {
                // Standing at the cut (no leg left to walk): the straight line, still said as cut, not "no route".
                return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[DirectionOf(goal - start)], 0, null, route != null, partial, barrier);
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

        /// <summary>
        ///     A barrier just opened (<see cref="BarrierWatch"/>): the samples are taken only when something asks for the
        ///     grid, so without the guide locked the old closed ones would keep the gate shut for routes a while longer.
        /// </summary>
        public void BarrierOpened()
        {
            lock (this.samplesLock)
            {
                this.samples.Clear();
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

        /// <summary>
        ///     What a sighted player knows of: an enemy only while the camera shows it (the minimap draws no monsters),
        ///     other players always (the map draws the party), what the minimap draws an icon for (exits, waypoints,
        ///     checkpoints, NPCs, the stash, shrines) once the game has it, anything else from the moment it is on screen
        ///     or its ground has been explored.
        /// </summary>
        private bool Sighted(ScanEntry entry, StdTuple3D<float> place, float worldToGrid, bool onMinimap)
        {
            if (entry.Category == ScanCategory.Enemies && !onMinimap)
            {
                return ScreenProjection.IsOnScreen(place);
            }

            if (entry.Category == ScanCategory.Players || this.sighted.Contains(entry.Id))
            {
                return true;
            }

            if (!onMinimap && !ScreenProjection.IsOnScreen(place) && !this.explored.IsSeen(new Vector2(place.X, place.Y) / worldToGrid))
            {
                return false;
            }

            this.sighted.Add(entry.Id);
            return true;
        }

        /// <summary>The entity carries a MinimapIcons.dat icon (GameHelper's MinimapIcon component): the minimap draws it.</summary>
        private static bool OnMinimap(Entity entity) =>
            entity.TryGetComponent<MinimapIcon>(out var icon) && !string.IsNullOrEmpty(icon.IconName);

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
            var baseId = string.Empty;
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
                if (name.Split(',')[0].Trim() is { Length: > 0 } title)
                {
                    this.itemTitles[title] = kind == "gold" ? string.Empty : name; // "Oro, 2": the title before the amount
                }

                if (this.inventory.ItemBaseName(worldItem.ItemEntityAddress) is { Length: > 0 } baseName)
                {
                    this.itemTitles.TryAdd(baseName, kind == "gold" ? string.Empty : name); // a rare's tooltip names its base on a line of its own
                }
                baseId = this.inventory.ItemBaseId(worldItem.ItemEntityAddress);
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

            if (category == ScanCategory.Npcs && this.HasQuestMarker(entity))
            {
                // D2R's NPC "!": the game's quest indicator over the head (someone to talk to for the story).
                name = Announcements.NpcWithQuest(name);
                kind = "npc_quest";
            }

            var position = render.WorldPosition;
            return new ScanEntry(entity.Id, category.Value, name, position.X, position.Y, position.Z, kind, baseId);
        }

        /// <summary>
        ///     Feeds the layout checks a game update can defeat: the entity name field (judged once on enough monsters,
        ///     characters and players) and the in-play flag (no living monster flagged among many means it moved).
        /// </summary>
        private void CheckLayout(Entity entity, Render render, List<IntPtr> renders)
        {
            if ((entity.EntityType == EntityTypes.Monster && IsAliveEnemy(entity)) || entity.EntityType is EntityTypes.NPC or EntityTypes.Player)
            {
                renders.Add(render.Address);
            }

            if (this.targetableConfirmed || !targetableTrusted || entity.EntityType != EntityTypes.Monster || !IsAliveEnemy(entity) ||
                !entity.TryGetComponent<Targetable>(out var target))
            {
                return;
            }

            if (this.memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0)
            {
                this.targetableConfirmed = true; // one monster in play proves the flag
                this.Report?.Found("targetable flag");
            }
            else if (this.unflaggedMonsters.Add(entity.Id) && this.unflaggedMonsters.Count >= TargetableJudgedOn)
            {
                targetableTrusted = false;
                this.Report?.Missing("targetable flag");
            }
        }

        /// <summary>The NPC shows the quest indicator over its head (<see cref="GameLayout.NpcComponent"/>).</summary>
        private bool HasQuestMarker(Entity entity) =>
            entity.TryGetComponent<NPC>(out var npc) &&
            this.memory.TryRead<IntPtr>(npc.Address + GameLayout.NpcComponent.MarkerRowOffset, out var row) && row != IntPtr.Zero &&
            this.memory.TryRead<IntPtr>(row, out var id) && id != IntPtr.Zero &&
            (this.elements.ReadUtf16BufferAt(id, 32) ?? string.Empty).StartsWith(GameLayout.NpcComponent.QuestMarkerId, StringComparison.Ordinal);

        /// <summary>
        ///     What A uses now: the entity the game flags as the player's target (<see
        ///     cref="GameLayout.TargetableComponent.IsTargetedOffset"/>), named as the scanner names it; monsters are the
        ///     target bar's.
        /// </summary>
        public ScanEntry? InteractTarget()
        {
            foreach (var entity in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities.Values)
            {
                if (entity.IsValid && entity.TryGetComponent<Targetable>(out var target) &&
                    this.memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetedOffset, out var flag) && flag != 0 &&
                    entity.TryGetComponent<Render>(out var render) && this.Classify(entity, render) is { } entry &&
                    entry.Category is not ScanCategory.Enemies)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Notes where the player stands as the place of the death that just happened.</summary>
        public void RememberDeath()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.AreaHash.Length > 0 && area.Player.TryGetComponent<Render>(out var player))
            {
                var at = player.WorldPosition;
                this.deathPlace = (area.AreaHash, new ScanEntry(DeathPlaceId, ScanCategory.Exits, Announcements.DeathPlace, at.X, at.Y, at.Z));
            }
        }

        /// <summary>A living monster that is not the player's (minions are friendly).</summary>
        public static bool IsAliveEnemy(Entity entity) =>
            entity.TryGetComponent<Life>(out var life) && life.IsAlive &&
            !(entity.TryGetComponent<Positioned>(out var positioned) && positioned.IsFriendly);

        private bool InPlay(Entity entity) => IsInPlay(this.memory, entity);

        /// <summary>Targetable by its own flag (<see cref="GameLayout.TargetableComponent"/>); no component: in play.</summary>
        public static bool IsInPlay(HostMemoryReader memory, Entity entity) =>
            !targetableTrusted || !entity.TryGetComponent<Targetable>(out var target) ||
            (memory.TryRead<byte>(target.Address + GameLayout.TargetableComponent.IsTargetableOffset, out var flag) && flag != 0);
    }
}
