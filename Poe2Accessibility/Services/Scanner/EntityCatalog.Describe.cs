namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>The scanner's descriptions and routes: how far and which way a listed place lies, the lost hint and the spoken labels.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>One spoken step: about a quarter second of walking (0.5 s moved ~200 world units, 2026-10-23).</summary>
        private const float StepWorldUnits = 100f;

        /// <summary>How far (grid units) the grid's staircase may stray from a straight leg before it counts as a turn.</summary>
        private const float CornerToleranceGrid = 3f;

        /// <summary>Legs shorter than half a step are sidesteps the game's wall sliding takes care of.</summary>
        private const float MinLegGrid = 5f;

        /// <summary>A way this many degrees past the edge of the direction said last for the same target keeps that direction.</summary>
        private const double KeepDirectionDegrees = 10;

        /// <summary>
        ///     A closed barrier this near (grid cells) the line from a route's end to its target cuts the way: the beam
        ///     of TutorialBlocker3 ran 55 cells across its centre, the target snaps up to 60 cells to walkable ground.
        /// </summary>
        private const float BarrierReachCells = 30f;

        /// <summary>A route ending this near (grid cells) its target reached it, whatever stands beside.</summary>
        private const float MinBarrierGapCells = 8f;

        /// <summary>
        ///     The target described last (id and area instance), its whole route and the first direction said: asked again on
        ///     the way, the same way and direction are kept unless clearly changed (<see cref="RoutePlanner.Keep"/>).
        /// </summary>
        private (uint Id, string Area, List<Vector2>? Route, int? First) described;

        /// <summary>Closer than this a place has no side (ObjectSounds' centre distance).</summary>
        private const float HereWorld = 300f;

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

            return this.WayCell(new Vector2(goal.WorldX, goal.WorldY) / toWorld, here / toWorld);
        }

        /// <summary>The place a map hint leads to, seen or not: the nearest boss room, or an exit by its English label.</summary>
        public ScanEntry? HintGoal(string label)
        {
            if (label != MapHintTable.BossGoal)
            {
                var name = this.landmarks.Localize(label);
                return this.Entries().Concat(this.AreaLandmarks()).FirstOrDefault(entry => entry.Category == ScanCategory.Exits && string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            return Seats.Followed.TryGetComponent<Render>(out var player)
                ? this.AreaLandmarks().Where(entry => entry.Kind == MinimapMarks.BossRoomKind).OrderBy(entry => Distance(player.WorldPosition, entry)).FirstOrDefault()
                : null;
        }

        /// <summary>An English label as the game says it.</summary>
        public string Localized(string label) => this.landmarks.Localize(label);

        /// <summary>The game has shown this place on the map: the scanner lists it from now on. False when it was known.</summary>
        public bool Reveal(ScanEntry place) => this.sighted.Add(place.Id);

        /// <inheritdoc cref="SideToward(Vector2)"/>
        public string SideToward(ScanEntry place) =>
            Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor is > 0 and var toWorld
                ? this.SideToward(new Vector2(place.WorldX, place.WorldY) / toWorld)
                : string.Empty;

        /// <summary>
        ///     The side a place lies on by the walk, in words, not the straight line: empty with no player or when the player stands
        ///     there. A map hint's direction: "a la derecha".
        /// </summary>
        public string SideToward(Vector2 goal)
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return string.Empty;
            }

            var here = new Vector2(player.WorldPosition.X, player.WorldPosition.Y);
            var offset = (this.WayCell(goal, here / toWorld) * toWorld) - here;
            return offset.Length() < HereWorld ? string.Empty : Announcements.HintSides[ScreenProjection.Octant(player.WorldPosition, offset)];
        }

        /// <summary>Where the way to a cell leaves the seen ground, or the cell when no unseen ground leads there.</summary>
        private Vector2 WayCell(Vector2 goal, Vector2 from) =>
            this.TryLiveGrid(out var grid, out var row) ? this.explored.WayOn(grid, row, from, goal) ?? goal : goal;

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
            return Seats.Followed.TryGetComponent<Render>(out var player)
                ? Vector2.Distance(new Vector2(player.WorldPosition.X, player.WorldPosition.Y), new Vector2(entry.WorldX, entry.WorldY))
                : null;
        }

        /// <inheritdoc cref="LandmarkTable.HonourLabel"/>
        public string HonourLabel => this.landmarks.HonourLabel;

        /// <inheritdoc cref="LandmarkTable.WaterLabel"/>
        public string WaterLabel => this.landmarks.WaterLabel;

        /// <inheritdoc cref="LandmarkTable.UnallocatedLabel"/>
        public string UnallocatedLabel => this.landmarks.UnallocatedLabel;

        public string PinLevelLabel => this.landmarks.PinLevelLabel;

        public string PinTownLabel => this.landmarks.PinTownLabel;

        /// <summary>How far the walk to an entry is, in world units; infinite with no way there. A search on the frame thread: for a few entries.</summary>
        public float WalkTo(ScanEntry entry)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return float.PositiveInfinity;
            }

            var gridSize = area.WorldToGridConvertor;
            var start = new Vector2(player.WorldPosition.X, player.WorldPosition.Y) / gridSize;
            var route = RoutePlanner.FindRoute(this.WalkableGrid(area), area.TerrainMetadata.BytesPerRow, start, new Vector2(entry.WorldX, entry.WorldY) / gridSize);
            return route == null ? float.PositiveInfinity : RoutePlanner.Length(route) * gridSize;
        }

        /// <inheritdoc cref="Describe(ScanEntry)"/>
        /// <param name="cut">The phrase says the way stops short or a closed barrier cuts it.</param>
        public string Describe(ScanEntry entry, out bool cut) => this.Describe(entry, out cut, out _);

        /// <param name="longWay">The way is cut only because the search ran out of budget (<see cref="WayCuts"/>).</param>
        public string Describe(ScanEntry entry, out bool cut, out bool longWay)
        {
            cut = false;
            longWay = false;
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
            // One search for both: the whole way, or the ground as close as it goes.
            var route = RoutePlanner.FindRoute(grid, row, start, goal, true, out var whole, out var spent);
            var earlier = this.described.Id == entry.Id && this.described.Area == area.AreaHash ? this.described : default;
            if (route != null && whole)
            {
                route = RoutePlanner.Keep(earlier.Route, route, start, grid, row);
            }

            var partial = false;
            if (!whole)
            {
                // No way all the way (a gate the quest opens, a bridge out of the grid): the legs lead as close as the
                // ground goes, like the guide beacon; the distance stays the straight line.
                // Cut however near the cut the player stands; a part shorter than a step leaves no leg to walk.
                partial = route != null;
                route = route is { Count: > 1 } && RoutePlanner.Length(route) * gridSize >= StepWorldUnits ? route : null;
            }

            // A target inside a wall snaps to the nearest walkable cell, which can lie on this side of a closed barrier
            // (the Riverbank exit behind the boss's gate read as a full route): the way stops at the barrier.
            var barrier = route is { Count: > 0 } && entry.Kind != "barrier" && BarrierBetween(this.ClosedBarriers(), route[^1], goal);
            partial |= barrier;
            longWay = WayCuts.Of(partial, barrier, spent) == WayCut.Long;

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

            var (alongX, alongY) = ScreenProjection.Axes(origin);
            int DirectionOf(Vector2 delta) => StickDirection.Octant(alongX, alongY, delta.X, delta.Y, earlier.First, KeepDirectionDegrees);
            var legs = route == null
                ? new List<(int Direction, float Length)>()
                : RoutePlanner.Legs(RoutePlanner.Corners(route, CornerToleranceGrid), DirectionOf, MinLegGrid);
            this.described = (entry.Id, area.AreaHash, whole ? route : null, legs.Count > 0 ? legs[0].Direction : null);
            cut = partial;
            if (legs.Count == 0)
            {
                // Standing at the cut (no leg left to walk): the straight line, still said as cut, not "no route".
                return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[DirectionOf(goal - start)], 0, null, route != null, partial, barrier, longWay);
            }

            var legSteps = Math.Max(1, (int)Math.Round(legs[0].Length * gridSize / StepWorldUnits));
            var then = legs.Count > 1 ? Announcements.StickDirections[legs[1].Direction] : null;
            return Announcements.ScanEntry(entry.Name, steps, Announcements.StickDirections[legs[0].Direction], legSteps, then, true, partial, barrier, longWay);
        }

        /// <summary>The area's named tiles and boss arenas, seen or not (the "scan" dev command).</summary>
        public IReadOnlyList<ScanEntry> AreaLandmarks() =>
            Seats.Followed.TryGetComponent<Render>(out var player)
                ? this.TileLandmarks(Core.States.InGameStateObject.CurrentAreaInstance, player.WorldPosition.Z).ToList()
                : Array.Empty<ScanEntry>();
    }
}
