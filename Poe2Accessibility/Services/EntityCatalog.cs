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
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     The scanner's view of the area: GameHelper's awake entities sorted into <see cref="ScanCategory"/>,
    ///     named as the game names them, nearest first. Only what a sighted player has seen (the minimap shows no
    ///     more): enemies while on screen, everything else once it has been on screen or on explored ground, and the
    ///     area's named tiles once their ground is explored. Exits, waypoints and NPCs then stay listed at their last
    ///     known place (the game unloads far entities), for as long as the area instance lives (<see cref="AreaMemory"/>).
    /// </summary>
    public sealed partial class EntityCatalog
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

        private static readonly ScanCategory[] Landmarks = { ScanCategory.Exits, ScanCategory.PointsOfInterest, ScanCategory.Waypoints, ScanCategory.Npcs };

        /// <summary>
        ///     Remembered while out of the game's range too, but dropped once the player comes back near the place and
        ///     the game no longer lists them (chest opened, item picked up).
        /// </summary>
        private static readonly ScanCategory[] Loot = { ScanCategory.Chests, ScanCategory.Items };

        /// <summary>The game keeps entities this close to the player awake; a loot entry missing inside it is gone.</summary>
        private const float AwakeWorld = 1000f;

        /// <summary>Landmark ids live above entity ids so the two never collide.</summary>
        private const uint LandmarkIdBase = 0x80000000;

        /// <summary>Boss arenas the landmark table does not name, between the named tiles and the mod's places.</summary>
        private const uint ArenaIdBase = LandmarkIdBase + 0x10000000;

        /// <summary>Pieces of one arena tile this many tiles apart are one arena.</summary>
        private const int ArenaReachTiles = 2;

        /// <summary>Unexplored entries: this base plus their block (row in the high half), above the tile landmarks.</summary>
        private const uint UnexploredIdBase = 0xC0000000;

        /// <summary>The place of the last death: one id between the tile landmarks and the unexplored blocks.</summary>
        private const uint DeathPlaceId = UnexploredIdBase - 1;

        /// <summary>
        ///     Exits and checkpoints brought from another instance of the same map (<see cref="AreaMemory.Inherit"/>): ids
        ///     above the mod's places, below the death place, until the game's own entity there is seen.
        /// </summary>
        private const uint InheritedIdBase = 0xB0000000;

        /// <summary>The game's entity this near an inherited place (world units) is that place in this instance.</summary>
        private const float InheritedMatchWorld = 400f;

        /// <summary>An inherited place the player stands this near with no such entity awake is not in this instance.</summary>
        private const float InheritedGoneWorld = 500f;

        /// <summary>
        ///     A named exit tile this near (grid cells) the game's exit of the same name is that exit, said once: Clearfell's
        ///     forest transition is a metatile nine 23-cell pieces wide, its exit entity 65 cells from the named piece.
        /// </summary>
        private const float TileExitMatchCells = 250f;

        /// <summary>
        ///     Where the player last died (D2R death place), with the area instance it belongs to: listed among the exits
        ///     whenever that instance is current, until the next death.
        /// </summary>
        private (string Area, ScanEntry Entry)? deathPlace;

        /// <summary>The unnamed boss arenas of the last tile layout read (<see cref="UnnamedArenas"/>).</summary>
        private (Dictionary<string, List<Vector2>>? Tiles, int Count, string Area, IReadOnlyList<ScanEntry> Entries) arenas = (null, 0, string.Empty, Array.Empty<ScanEntry>());

        private const int MaxUnexplored = 8;

        /// <summary>The frontier scans the whole grid: recomputed at most this often (the many Entries() callers share it).</summary>
        private const int UnexploredRefreshMs = 2000;

        private const int TrackMs = 250;

        /// <summary>The explored ground is written this often while playing, so a host restart loses little.</summary>
        private const int SaveExploredMs = 15000;

        /// <summary>Live walkable grids kept for routing, one per SampleMs: a cell must be walkable in all of them.</summary>
        private const int MaxSamples = 4;

        private const int SampleMs = 500;

        /// <summary>An area's name this soon after its instance was bound unnamed belongs to that instance (its loading screen lagged).</summary>
        private const long LateNameMs = 15000;

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

        /// <summary>What the objective names, seen or not, as of the last <see cref="Entries"/>: the lost hint's goal.</summary>
        private IReadOnlyList<ScanEntry> questTargets = Array.Empty<ScanEntry>();
        /// <summary>Recent live walkable grids (GameLayout-free: whole copies), oldest first.</summary>
        private readonly Queue<byte[]> samples = new();
        private readonly BarrierMemory barriers = new();
        /// <summary>Closed barriers seen in this area by entity id (grid position), kept past the network bubble.</summary>
        private readonly Dictionary<uint, Vector2> closedBarriers = new();
        private readonly object samplesLock = new();

        /// <summary>The last combined grid and the live grid it was made from (see WalkableGrid).</summary>
        private (byte[] Live, byte[] Grid)? stable;
        private long lastSampleAt;
        private IReadOnlyList<ScanEntry> unexplored = Array.Empty<ScanEntry>();
        private long unexploredAt = long.MinValue / 2;
        private long trackedAt = long.MinValue / 2;
        private long savedAt;

        /// <summary>The area instance being read (AreaInstance.AreaHash), its map (<see cref="AreaMemory.Layout"/>) and its name.</summary>
        private string instance = string.Empty;
        private ulong layout;

        /// <summary>Its exits' frame (<see cref="AreaMemory.Frame"/>): another instance with the same one is the same map.</summary>
        private ulong frame;
        private string areaName = string.Empty;

        /// <summary>The tile table the layout was last computed from: a new one is a reloaded area.</summary>
        private object? layoutTiles;

        /// <summary>An area entered before the game had its instance loaded; that instance takes the name.</summary>
        private string? pendingName;
        private long unnamedAt = long.MinValue / 2;

        /// <summary>The area this instance was first entered from: the exit back there is not the way on.</summary>
        private string cameFrom = string.Empty;

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

        /// <summary>
        ///     Living monsters, characters and players in one pass whose Render names judge where the names are (see
        ///     <see cref="LayoutProbe"/>). Gathered over time, the dead adds of a boss fight left freed renders that read
        ///     nothing: "the names of characters and monsters" were reported lost mid-fight (2026-10-24).
        /// </summary>
        private const int RendersToJudge = 15;

        /// <summary>Checks the entity name field once enough named things are awake (set by the plugin).</summary>
        public LayoutProbe? Probe { get; set; }

        /// <summary>Finds the in-play flag and the quest marker again after a game update (set by the plugin).</summary>
        public EntityFields? Fields { get; set; }

        /// <summary>The quest tracker's texts on screen ("Find Clearfell", "Slay the Bloated Miller"), for the Quest category.</summary>
        public Func<IReadOnlyList<string>> QuestTexts { get; set; }

        /// <summary>The area's named places (<see cref="PlaceNarrator.Entries"/>), set by the plugin.</summary>
        public Func<float, IEnumerable<ScanEntry>>? PlaceEntries { get; set; }

        /// <summary>The current area's name in the client language (its name in an objective is not a destination).</summary>
        public Func<string> AreaName { get; set; } = () => string.Empty;

        /// <summary>How much ground has been seen in this area: it grows while the player finds new ground.</summary>
        public int SeenBlocks => this.explored.SeenBlocks;

        /// <summary>What each area instance left held, to restore it on return (set by the plugin).</summary>
        public AreaMemory? Memory { get; set; }

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

        /// <summary>
        ///     The game entered the area <paramref name="name"/> (its loading screen): bound to the instance the game holds
        ///     once that instance is the new one, which may come later than the name.
        /// </summary>
        public void AreaEntered(string name)
        {
            if (this.Rebind(name))
            {
                return;
            }

            if (Environment.TickCount64 - this.unnamedAt < LateNameMs)
            {
                this.areaName = name;
                this.unnamedAt = long.MinValue / 2;
            }
            else
            {
                this.pendingName = name;
            }
        }

        /// <summary>
        ///     Binds the scanner to the instance the game holds when it is another (hash or map): what the old one held is
        ///     kept, and what was known of the new one returns if its map is the same; true when it switched.
        /// </summary>
        private bool Rebind(string? name)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var hash = area.AreaHash;
            var tiles = area.TgtTilesLocations;
            if (hash.Length == 0 || (hash == this.instance && ReferenceEquals(tiles, this.layoutTiles)))
            {
                return false;
            }

            var layout = AreaMemory.Layout(tiles, area.TerrainMetadata.BytesPerRow, area.GridWalkableData.Length);
            this.layoutTiles = tiles;
            if (hash == this.instance && layout == this.layout)
            {
                return false;
            }

            if (this.Memory != null && this.instance.Length > 0)
            {
                this.Memory.Keep(this.instance, this.Snapshot());
            }

            var leaving = this.areaName;
            this.Clear();
            name ??= this.pendingName;
            var frame = AreaMemory.Frame(tiles, area.TerrainMetadata.BytesPerRow, area.GridWalkableData.Length);
            var known = this.Memory?.Recall(hash, layout);
            var inherited = known == null ? this.Memory?.Inherit(frame, hash) : null;
            if (inherited is { } same)
            {
                // The same campaign map in a new instance (Clearfell: exits and checkpoints at the same cells, other
                // tiles varied; testers heard its exits forgotten, 2026-11-02): the exits and checkpoints seen there, at
                // their places, until the game's own are seen again. Not the explored ground: Beira's arena stood
                // elsewhere in the next instance and was announced "on the map" before the player saw it.
                var index = 0u;
                foreach (var entry in AreaMemory.Framed(same.Snapshot))
                {
                    var id = InheritedIdBase + index++;
                    this.remembered[id] = entry with { Id = id };
                }
            }

            if (known != null)
            {
                this.explored.Load(known.Explored);
                foreach (var (id, entry) in known.Remembered)
                {
                    this.remembered[id] = entry;
                }

                this.sighted.UnionWith(known.Sighted);
                lock (this.samplesLock)
                {
                    foreach (var (id, at) in known.ClosedBarriers)
                    {
                        this.closedBarriers[id] = at;
                    }
                }
            }

            this.cameFrom = known?.CameFrom ?? leaving;
            (this.instance, this.layout, this.frame, this.areaName) = (hash, layout, frame, name ?? leaving);
            this.unnamedAt = name == null ? Environment.TickCount64 : long.MinValue / 2;
            this.pendingName = null;
            this.savedAt = Environment.TickCount64;
            this.log.Info(known != null
                ? $"Instance {hash} ({this.areaName}): same map, {known.Remembered.Count} places and its explored ground restored"
                : this.remembered.Count > 0 || inherited != null
                    ? $"Instance {hash} ({this.areaName}): new instance of the map of {inherited?.Instance} (frame {frame:X16}), its {this.remembered.Count} exits and checkpoints kept"
                    : $"Instance {hash} ({this.areaName}): new map {layout:X16}, frame {frame:X16}, unexplored");
            return true;
        }

        /// <summary>A copy of what is known of this instance, for <see cref="AreaMemory"/>.</summary>
        private AreaMemory.Snapshot Snapshot()
        {
            lock (this.samplesLock)
            {
                return new AreaMemory.Snapshot(this.layout, this.explored.Save(), this.cameFrom, new(this.remembered), new(this.sighted), new(this.closedBarriers), this.frame);
            }
        }

        private void Clear()
        {
            this.remembered.Clear();
            this.sighted.Clear();
            this.itemTitles.Clear();
            this.explored.Reset();
            this.questTargets = Array.Empty<ScanEntry>();
            lock (this.samplesLock)
            {
                this.samples.Clear();
                this.stable = null;
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
            this.Rebind(null);
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.AreaHash != this.instance)
            {
                return;
            }

            var row = area.TerrainMetadata.BytesPerRow;
            if (row > 0 && Seats.Followed.TryGetComponent<Render>(out var player))
            {
                this.explored.Visit(new Vector2(player.GridPosition.X, player.GridPosition.Y), row * 2, area.GridWalkableData.Length / row);
            }

            if (this.Memory != null && this.instance.Length > 0 && now - this.savedAt >= SaveExploredMs)
            {
                this.savedAt = now;
                this.Memory.Save(this.instance, this.Snapshot());
            }
        }

        /// <summary>Everything the scanner can name, nearest first.</summary>
        public IReadOnlyList<ScanEntry> Entries()
        {
            this.Rebind(null);
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<ScanEntry>();
            }

            var found = new Dictionary<uint, ScanEntry>();
            // Known to the game but not yet to the player: only the quest marker may point at them.
            var unseen = new List<ScanEntry>();
            var renders = new List<IntPtr>();
            var sampling = this.Fields?.StartPass() ?? false;
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || !entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                this.CheckLayout(entity, render, renders);
                if (sampling)
                {
                    this.Fields!.Sample(entity, render);
                }

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

            if (sampling)
            {
                this.Fields!.EndPass();
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

                if (landmark.Id is >= InheritedIdBase and < DeathPlaceId && !KeepInherited(landmark, here, found.Values, unseen))
                {
                    this.remembered.Remove(landmark.Id); // the game's own is listed now, or this instance has none there
                    continue;
                }

                found.Add(landmark.Id, landmark);
            }

            var toCells = area.WorldToGridConvertor;
            foreach (var landmark in this.TileLandmarks(area, player.WorldPosition.Z))
            {
                if (landmark.Category == ScanCategory.Exits && found.Values.Any(exit => exit.Category == ScanCategory.Exits && SameExit(exit, landmark, toCells)))
                {
                    continue; // the exit's own tile beside the game's exit: one place, said once (two "El bosque singular", 2026-11-02)
                }

                var place = new StdTuple3D<float> { X = landmark.WorldX, Y = landmark.WorldY, Z = landmark.WorldZ };
                if (this.Pointed(landmark, player, area.WorldToGridConvertor) || this.Sighted(landmark, place, area.WorldToGridConvertor, false))
                {
                    found.TryAdd(landmark.Id, landmark);
                }
                else if (landmark.Kind != MinimapMarks.BossRoomKind)
                {
                    // A boss room the minimap neither shows nor points to is unknown to the quest marker too.
                    unseen.Add(landmark);
                }
            }

            foreach (var place in this.PlaceEntries?.Invoke(player.WorldPosition.Z) ?? Array.Empty<ScanEntry>())
            {
                var at = new StdTuple3D<float> { X = place.WorldX, Y = place.WorldY, Z = place.WorldZ };
                if (this.Sighted(place, at, area.WorldToGridConvertor, false))
                {
                    found.TryAdd(place.Id, place);
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
            var sorted = found.Values.OrderBy(entry => entry.Kind == ExplorationMap.PocketKind).ThenBy(entry => Distance(origin, entry)).ToList();
            // The objective is known wherever the game knows it, but said only once seen: the rest is for the player to find,
            // with the lost hint for a last resort (user 2026-11-05, over the marker of 2026-10-24; docs/radar.md).
            var questPool = sorted.Concat(unseen.Where(entry => !found.ContainsKey(entry.Id))).OrderBy(entry => Distance(origin, entry)).ToList();
            this.questTargets = QuestMatcher.Targets(questPool, this.QuestTexts(), this.AreaName(), this.cameFrom);
            sorted.InsertRange(0, this.questTargets.Where(target => target.QuestMarked || found.ContainsKey(target.Id)));
            return sorted;
        }

        /// <summary>
        ///     Where a lost player's hint comes from, in grid cells: where the way to what the objective names leaves the seen
        ///     ground, or to a boss room not found yet (the way on often lies through it), else the nearest ground that goes on.
        /// </summary>
        public Vector2? LostHintCell()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var toWorld = area.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            this.Entries();
            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            float Away(ScanEntry entry) => Vector2.Distance(here, new Vector2(entry.WorldX, entry.WorldY));
            var goal = this.questTargets.FirstOrDefault() ??
                       this.AreaLandmarks().Where(entry => entry.Kind == MinimapMarks.BossRoomKind && !this.sighted.Contains(entry.Id)).OrderBy(Away).FirstOrDefault();
            if (goal == null)
            {
                return this.UnexploredCells() is { Count: > 0 } onward ? onward[0] : null;
            }

            var cell = new Vector2(goal.WorldX, goal.WorldY) / toWorld;
            return this.TryLiveGrid(out var grid, out var row) ? this.explored.WayOn(grid, row, here / toWorld, cell) ?? cell : cell;
        }

        /// <summary>
        ///     "Clearfell Encampment, 116 steps, down 3, then left": the length of the walkable route, the leg to walk
        ///     now and where it turns next, so the player knows when to turn instead of walking past the corner (a
        ///     wall between the player and the target is walked around, not into); the straight line, marked "no
        ///     route", when the grid has no way there.
        /// </summary>
        public string Describe(ScanEntry entry) => this.Describe(entry, out _);

        /// <summary>Straight-line world distance from the player to the entry; null without a player position.</summary>
        public float? DistanceTo(ScanEntry entry)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            return Seats.Followed.TryGetComponent<Render>(out var player)
                ? Vector2.Distance(new Vector2(player.WorldPosition.X, player.WorldPosition.Y), new Vector2(entry.WorldX, entry.WorldY))
                : null;
        }

        /// <inheritdoc cref="Describe(ScanEntry)"/>
        /// <param name="cut">The phrase says the way stops short or a closed barrier cuts it.</param>
        public string Describe(ScanEntry entry, out bool cut)
        {
            cut = false;
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
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
            if (route == null && this.log.Verbose)
            {
                // Research only: a third search over the grid as loaded (up to ~300 ms on the frame thread).
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

        /// <summary>The area's named tiles and boss arenas, seen or not (the "scan" dev command).</summary>
        public IReadOnlyList<ScanEntry> AreaLandmarks() =>
            Seats.Followed.TryGetComponent<Render>(out var player)
                ? this.TileLandmarks(Core.States.InGameStateObject.CurrentAreaInstance, player.WorldPosition.Z).ToList()
                : Array.Empty<ScanEntry>();

        /// <summary>
        ///     An exit or checkpoint inherited from another instance of this map stays until the game's own entity of that
        ///     name is listed beside it, or the player stands by its place and the game has none there.
        /// </summary>
        private static bool KeepInherited(ScanEntry inherited, Vector2 here, IEnumerable<ScanEntry> listed, IEnumerable<ScanEntry> unseen)
        {
            var at = new Vector2(inherited.WorldX, inherited.WorldY);
            bool Same(ScanEntry entry) => IsEntity(entry) && entry.Category == inherited.Category &&
                                          string.Equals(entry.Name, inherited.Name, StringComparison.OrdinalIgnoreCase) &&
                                          Vector2.Distance(at, new Vector2(entry.WorldX, entry.WorldY)) <= InheritedMatchWorld;
            return !listed.Any(Same) && (unseen.Any(Same) || Vector2.Distance(here, at) > InheritedGoneWorld);
        }

        /// <summary>The game's exit and a named tile of the same name this near it are one exit.</summary>
        private static bool SameExit(ScanEntry exit, ScanEntry tile, float worldPerCell) =>
            string.Equals(exit.Name, tile.Name, StringComparison.OrdinalIgnoreCase) &&
            Vector2.Distance(new Vector2(exit.WorldX, exit.WorldY), new Vector2(tile.WorldX, tile.WorldY)) <= TileExitMatchCells * worldPerCell;

        /// <summary>
        ///     Named tiles of this area (<see cref="LandmarkTable"/>), placed from the area's tile grid, and the boss arenas
        ///     among its tiles: a named one as "Boss room: Asinia", one the table lacks by its file alone.
        /// </summary>
        private IEnumerable<ScanEntry> TileLandmarks(AreaInstance area, float height)
        {
            var tiles = area.TgtTilesLocations;
            var toWorld = area.WorldToGridConvertor;
            var source = this.landmarks.Source(this.areaId());
            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var namedArenas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

                named.Add(MinimapMarks.TileFile(key));
                var centre = new Vector2(spots.Average(spot => spot.X), spots.Average(spot => spot.Y)) * toWorld;
                var english = source.TryGetValue(key, out var written) ? written : label;
                if (!this.landmarks.IsAreaName(english) && MinimapMarks.ArenaBoss(key, english) is { } boss)
                {
                    // The Tomb's and the Mausoleum's arenas were "Asinia" and "MausoleumBoss_ArenaFloor" among the
                    // exits, a maze's boss not found by that (user 2026-10-29); the boss's name lets the quest find it.
                    var name = boss.Length == 0 ? string.Empty : boss == english.Trim() ? label : this.landmarks.Localize(boss);
                    namedArenas.Add(MinimapMarks.TileFile(key));
                    yield return new ScanEntry(LandmarkIdBase + index, ScanCategory.PointsOfInterest, Announcements.BossRoom(name),
                                               centre.X, centre.Y, height, MinimapMarks.BossRoomKind, Subject: MinimapMarks.WithoutNote(name));
                    continue;
                }

                yield return new ScanEntry(LandmarkIdBase + index, ScanCategory.Exits, label, centre.X, centre.Y, height,
                                           this.landmarks.IsAreaName(label) ? string.Empty : QuestMatcher.TileNote);
            }

            foreach (var arena in this.UnnamedArenas(tiles, named, namedArenas))
            {
                yield return arena with { Name = Announcements.BossRoom(string.Empty), WorldX = arena.WorldX * toWorld, WorldY = arena.WorldY * toWorld, WorldZ = height };
            }
        }

        /// <summary>
        ///     Boss arenas among the area's tiles that the landmark table does not name, at their grid cells: the pieces of
        ///     arena tiles side by side are one arena, left out when the table names a piece of it (an arena's floor named,
        ///     its walls not). Worked out once per area layout.
        /// </summary>
        private IReadOnlyList<ScanEntry> UnnamedArenas(Dictionary<string, List<Vector2>> tiles, HashSet<string> named, HashSet<string> namedArenas)
        {
            // GameHelper empties the same dictionary when the area is left, so its size is part of the key; the area's id
            // too, in case the first look at a layout came before the id did (its named arenas would pass for unnamed).
            var area = this.areaId();
            if (ReferenceEquals(this.arenas.Tiles, tiles) && this.arenas.Count == tiles.Count && this.arenas.Area == area)
            {
                return this.arenas.Entries;
            }

            var pieces = new List<(Vector2 Spot, bool Named)>();
            foreach (var (key, spots) in tiles)
            {
                var file = MinimapMarks.TileFile(key);
                var isNamed = namedArenas.Contains(file);
                if (isNamed || (!named.Contains(file) && MinimapMarks.IsArenaTile(key)))
                {
                    pieces.AddRange(spots.Select(spot => (spot, isNamed)));
                }
            }

            var entries = new List<ScanEntry>();
            foreach (var group in MinimapMarks.Groups(pieces.Select(piece => piece.Spot).ToList(), ArenaReachTiles * TileStructure.TileToGridConversion))
            {
                if (group.Any(member => pieces[member].Named))
                {
                    continue;
                }

                var centre = group.Aggregate(Vector2.Zero, (sum, member) => sum + pieces[member].Spot) / group.Count;
                entries.Add(new ScanEntry(ArenaIdBase + (uint)entries.Count, ScanCategory.PointsOfInterest, string.Empty,
                                          centre.X, centre.Y, 0, MinimapMarks.BossRoomKind));
            }

            if (entries.Count > 0)
            {
                this.log.Info($"Boss arenas without a landmark name: {entries.Count}");
            }

            this.arenas = (tiles, tiles.Count, area, entries);
            return entries;
        }

        /// <summary>How much of the ground the player can reach has been seen, in percent; null outside gameplay or without a grid.</summary>
        public int? ExploredPercent()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            return Seats.Followed.TryGetComponent<Render>(out var player) && this.TryLiveGrid(out var grid, out var row)
                ? this.explored.ExploredPercent(grid, row, new Vector2(player.GridPosition.X, player.GridPosition.Y))
                : null;
        }

        /// <summary>Where unseen ground begins, in grid cells, nearest first; pockets only once nothing else is left (the radar's mark, the loop).</summary>
        public IReadOnlyList<Vector2> UnexploredCells()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var toWorld = area.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<Vector2>();
            }

            var places = this.Unexplored(area, player);
            var onward = places.Where(entry => entry.Kind != ExplorationMap.PocketKind).ToList();
            return (onward.Count > 0 ? onward : places).Select(entry => new Vector2(entry.WorldX, entry.WorldY) / toWorld).ToList();
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
                .Select(place => new ScanEntry(
                    UnexploredIdBase + (uint)(((int)place.Cell.Y / ExplorationMap.BlockCells) << 15) + (uint)((int)place.Cell.X / ExplorationMap.BlockCells),
                    ScanCategory.Unexplored,
                    place.Pocket ? Announcements.UnexploredPocket : Announcements.UnexploredArea,
                    place.Cell.X * toWorld,
                    place.Cell.Y * toWorld,
                    player.WorldPosition.Z,
                    place.Pocket ? ExplorationMap.PocketKind : string.Empty))
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

        /// <summary>
        ///     A boss room the minimap points to before it is seen: an arrow at the minimap's edge while the player is within the
        ///     icon's pointer distance (<see cref="MinimapMarks.CampaignBossPointerCells"/>, <see cref="MinimapMarks.MapBossPointerCells"/>).
        ///     A sighted player follows it to the boss of a maze (user 2026-10-29), so the scanner lists the room from then on.
        /// </summary>
        private bool Pointed(ScanEntry entry, Render player, float worldToGrid)
        {
            var reach = entry.Id >= ArenaIdBase ? MinimapMarks.MapBossPointerCells : MinimapMarks.CampaignBossPointerCells;
            if (entry.Kind != MinimapMarks.BossRoomKind ||
                Vector2.Distance(new Vector2(player.GridPosition.X, player.GridPosition.Y), new Vector2(entry.WorldX, entry.WorldY) / worldToGrid) > reach)
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

        /// <summary>Notes where the player stands as the place of the death that just happened.</summary>
        public void RememberDeath()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.AreaHash.Length > 0 && Seats.Followed.TryGetComponent<Render>(out var player))
            {
                var at = player.WorldPosition;
                this.deathPlace = (area.AreaHash, new ScanEntry(DeathPlaceId, ScanCategory.Exits, Announcements.DeathPlace, at.X, at.Y, at.Z));
            }
        }

    }
}
