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

    /// <summary>The scanner's unexplored ground and the death place: the frontier places to walk to and where the player last died.</summary>
    public sealed partial class EntityCatalog
    {
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

        /// <summary>Two walks of the whole grid a goal: the nearest few tell where their ways go.</summary>
        private const int MaxKnownGoals = 4;

        private IReadOnlyList<ScanEntry> unexplored = Array.Empty<ScanEntry>();
        private long unexploredAt = long.MinValue / 2;

        /// <summary>
        ///     The longest a frontier search has taken, logged when it grows past this: the search walks every cell of the area
        ///     twice since 2026-11-12, measured only in a town (no frame held: it runs off the frame thread).
        /// </summary>
        private long slowestFrontierMs = 250;

        /// <summary>The frontier being worked out, with the map it reads and what its places need to become entries.</summary>
        private (Task<IReadOnlyList<FrontierPlace>> Places, ExplorationMap Map, float ToWorld, float Height, IReadOnlyList<string> Goals, long At)? frontierSearch;

        /// <summary>What the last frontier search read: the same grid, seen ground, player's block and goals again find the same places.</summary>
        private (byte[] Grid, int Seen, int X, int Y, string Goals, byte[]? Mask)? frontierFor;

        /// <summary>When what the unexplored places were worked out from was last current.</summary>
        private long unexploredAsOf;

        /// <summary>The walk to each unexplored place (cells), by its id.</summary>
        private Dictionary<uint, int> unexploredWalks = new();

        /// <summary>How much of the ground the player can reach has been seen, in percent; null outside gameplay or without a grid.</summary>
        public int? ExploredPercent()
        {
            return Seats.Followed.TryGetComponent<Render>(out var player) && this.TryLiveGrid(out var grid, out var row)
                ? this.explored.ExploredPercent(grid, row, new Vector2(player.GridPosition.X, player.GridPosition.Y), this.BarrierMask())
                : null;
        }

        /// <summary>Where unseen ground begins, in grid cells, nearest first: ground that goes on while any is left, then what soon ends, pockets last (the lost hint).</summary>
        public IReadOnlyList<Vector2> UnexploredCells()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var toWorld = area.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<Vector2>();
            }

            return this.BestUnexplored(area, player, false).Select(entry => new Vector2(entry.WorldX, entry.WorldY) / toWorld).ToList();
        }

        /// <summary>
        ///     "Zona sin explorar más cercana, 40 pasos, arriba 3, luego a la derecha": the nearest of the unseen ground
        ///     <see cref="UnexploredCells"/> points to, with its route; null when nothing is left unseen.
        /// </summary>
        public string? DescribeNearestUnexplored()
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.WorldToGridConvertor <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            var nearest = this.BestUnexplored(area, player, false).OrderBy(entry => Distance(player.WorldPosition, entry)).FirstOrDefault();
            return nearest is null ? null : this.Describe(nearest with { Name = Announcements.NearestUnexplored });
        }

        /// <summary>The unexplored ground is worked out again at once: the guide has seen the ground it led to.</summary>
        public void HurryUnexplored() => this.unexploredAt = long.MinValue / 2;

        /// <summary>
        ///     The unseen ground to go on to from <paramref name="last"/> (grid cells): the nearest to it within
        ///     <paramref name="beside"/> cells, else the nearest by the walk, among the best rank left, nooks left out. Known as of
        ///     <paramref name="since"/> or later, else Known is false and it is being worked out.
        /// </summary>
        public (bool Known, ScanEntry? Entry) NextUnexplored(long since, Vector2 last, float beside)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            var toWorld = area.WorldToGridConvertor;
            if (toWorld <= 0 || !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return (false, null);
            }

            this.Unexplored(area, player);
            if (this.unexploredAsOf < since)
            {
                return (false, null);
            }

            float FromLast(ScanEntry entry) => Vector2.Distance(new Vector2(entry.WorldX, entry.WorldY) / toWorld, last);
            var found = this.BestUnexplored(area, player).Where(entry => entry.Kind != ExplorationMap.PocketKind).ToList();
            // Ground behind a barrier stands at the gate's near side, seen ground: it is left out only once found shut.
            var left = BarrierGround.StillToTry(found, entry => this.IsSeen(new Vector2(entry.WorldX, entry.WorldY) / toWorld), this.tried);
            if (left.Count == 0 && found.Any(entry => entry.Kind != ExplorationMap.BarrierGroundKind))
            {
                this.HurryUnexplored(); // all of it seen since it was worked out: not the end, only old
                return (false, null);
            }

            var next = left.Where(entry => FromLast(entry) <= beside).OrderBy(FromLast).FirstOrDefault() ??
                       left.OrderBy(entry => this.unexploredWalks.GetValueOrDefault(entry.Id, int.MaxValue)).ThenBy(entry => Distance(player.WorldPosition, entry)).FirstOrDefault();
            return (true, next);
        }

        /// <summary>
        ///     The places the player knows of and has not reached (exits listed, boss rooms, what the quest asks), nearest first,
        ///     with how to name each as the end of a way; never what the scanner does not list (docs/radar.md "Ways to known places").
        /// </summary>
        public IReadOnlyList<(Vector2 Cell, string Name)> KnownGoals()
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            if (!this.NameKnownWays || toWorld <= 0 || this.listed is not { } last || last.Instance != this.instance ||
                !Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return Array.Empty<(Vector2, string)>();
            }

            var quest = last.List.Where(entry => entry.Category == ScanCategory.Quest).Select(entry => entry.Id).ToHashSet();
            return last.List
                .Where(entry => (entry.Category == ScanCategory.Exits && entry.Kind == QuestMatcher.ExitKind) ||
                                (entry.Kind == MinimapMarks.BossRoomKind && entry.Category != ScanCategory.Quest) ||
                                (entry.Category is ScanCategory.Objects or ScanCategory.Npcs or ScanCategory.PointsOfInterest && quest.Contains(entry.Id)))
                .Where(entry => !this.IsSeen(new Vector2(entry.WorldX, entry.WorldY) / toWorld))
                .DistinctBy(entry => entry.Name)
                .OrderBy(entry => Distance(player.WorldPosition, entry))
                .Take(MaxKnownGoals)
                .Select(entry => (new Vector2(entry.WorldX, entry.WorldY) / toWorld, Announcements.WayTo(entry.Kind == MinimapMarks.BossRoomKind ? Announcements.BossRoom(entry.Subject) : entry.Name, this.IsQuestGoal(entry, quest))))
                .ToList();
        }

        /// <summary>Whether unexplored ground and the forks' ways name the known places they lead to (setting).</summary>
        public bool NameKnownWays { get; set; } = true;

        /// <summary>
        ///     The unseen ground of the best rank left: ground that goes on while any is left, then what soon ends, ground behind
        ///     a barrier when <paramref name="withBarriers"/> (not for the lost hint, <see cref="BarrierGround.Best"/>), pockets last.
        /// </summary>
        private List<ScanEntry> BestUnexplored(AreaInstance area, Render player, bool withBarriers = true) =>
            BarrierGround.Best(this.Unexplored(area, player).Concat(this.UnopenedDoors(area, player)), withBarriers);

        /// <summary>The doors seen shut and not opened since, nearest first, as places still to explore (<see cref="ClosedDoors"/>).</summary>
        private IEnumerable<ScanEntry> UnopenedDoors(AreaInstance area, Render player)
        {
            var toWorld = area.WorldToGridConvertor;
            if (this.doors.Count == 0 || toWorld <= 0)
            {
                yield break;
            }

            this.doors.ForgetGone(id => area.AwakeEntities.Values.Any(entity => entity.Id == id), new Vector2(player.GridPosition.X, player.GridPosition.Y), toWorld);
            foreach (var (_, at) in this.doors.Nearest(new Vector2(player.WorldPosition.X, player.WorldPosition.Y)))
            {
                var block = (uint)(((int)(at.Y / toWorld) / ExplorationMap.BlockCells) << 15) + (uint)((int)(at.X / toWorld) / ExplorationMap.BlockCells);
                yield return new ScanEntry(UnexploredIdBase + block, ScanCategory.Unexplored, Announcements.UnopenedDoor, at.X, at.Y, at.Z, ClosedDoors.Kind);
            }
        }

        /// <summary>What the line from <paramref name="from"/> along <paramref name="axis"/> (grid cells) soon leaves the seen ground onto, null for nothing: the radar's mark.</summary>
        public UnseenSize? UnseenToward(Vector2 from, Vector2 axis)
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            this.Unexplored(Core.States.InGameStateObject.CurrentAreaInstance, player); // keeps the reachable ground fresh
            return this.TryLiveGrid(out var grid, out var row) ? this.explored.UnseenAlong(grid, row, from, axis, ExplorationMap.MarkReachCells) : null;
        }

        /// <summary>The edge of small unseen ground within <paramref name="radius"/> cells of a cell (<see cref="ExplorationMap.SmallUnseenNear"/>).</summary>
        public IReadOnlyList<Vector2> SmallUnseenNear(Vector2 from, float radius)
        {
            if (Seats.Followed.TryGetComponent<Render>(out var player))
            {
                this.Unexplored(Core.States.InGameStateObject.CurrentAreaInstance, player); // keeps the reachable ground fresh
            }

            return this.explored.SmallUnseenNear(from, radius);
        }

        /// <summary>Whether a grid cell's block has been seen.</summary>
        public bool IsSeen(Vector2 cell) => this.explored.IsSeen(cell);

        /// <summary>Where unseen ground begins (<see cref="ExplorationMap"/>) as last worked out; a new search starts every UnexploredRefreshMs.</summary>
        private IReadOnlyList<ScanEntry> Unexplored(AreaInstance area, Render player)
        {
            var now = Environment.TickCount64;
            if (this.frontierSearch is { Places.IsCompleted: true } found)
            {
                this.frontierSearch = null;
                if (found.Places.IsFaulted)
                {
                    this.frontierFor = null;
                    this.log.Info($"Unexplored ground not found: {found.Places.Exception?.GetBaseException().Message}");
                }
                else if (!this.explored.Adopt(found.Map))
                {
                    this.frontierFor = null;
                }
                else
                {
                    // A gate's entry goes by the barrier's own first cell, not its near side, which moves with the player.
                    uint IdOf(FrontierPlace place)
                    {
                        var at = place.BehindBarrier ? place.Barrier : place.Cell;
                        return UnexploredIdBase + (uint)(((int)at.Y / ExplorationMap.BlockCells) << 15) + (uint)((int)at.X / ExplorationMap.BlockCells);
                    }

                    // A barrier at a door the player opens is that door, listed as "Unopened door" already.
                    var doorCells = this.doors.Nearest(new Vector2(player.WorldPosition.X, player.WorldPosition.Y))
                        .Select(door => new Vector2(door.At.X, door.At.Y) / found.ToWorld).ToList();
                    var places = BarrierGround.AwayFromDoors(found.Places.Result, doorCells);
                    this.unexploredAsOf = found.At;
                    this.unexploredWalks = places.GroupBy(IdOf).ToDictionary(group => group.Key, group => group.Min(place => place.Steps));
                    this.unexplored = places
                        .Select(place => new ScanEntry(
                            IdOf(place),
                            ScanCategory.Unexplored,
                            place.BehindBarrier
                                ? Announcements.UnexploredBehindBarrier(place.Size)
                                : Announcements.Unexplored(place.Size, place.LeadsTo.Select(goal => found.Goals[goal]).ToList()),
                            place.Cell.X * found.ToWorld,
                            place.Cell.Y * found.ToWorld,
                            found.Height,
                            place.BehindBarrier ? ExplorationMap.BarrierGroundKind
                            : place.Size == UnseenSize.Pocket ? ExplorationMap.PocketKind : place.Size == UnseenSize.Small ? ExplorationMap.SmallKind : string.Empty))
                        .ToList();
                    this.tried.KeepOnly(this.unexplored.Where(entry => entry.Kind == ExplorationMap.BarrierGroundKind).Select(entry => entry.Id).ToList());
                }
            }

            if (this.frontierSearch != null || now - this.unexploredAt < UnexploredRefreshMs || !this.TryLiveGrid(out var grid, out var row))
            {
                return this.unexplored;
            }

            // Off the frame thread: every two seconds it held a frame for 30 ms, 140 ms on a Trial's floor (docs/research/performance.md).
            this.unexploredAt = now;
            var from = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var goals = this.KnownGoals();
            var goalCells = goals.Select(goal => goal.Cell).ToList();
            var goalNames = goals.Select(goal => goal.Name).ToList();
            var mask = this.BarrierMask();
            var exits = this.UnseenExitCells(from);
            (byte[] Grid, int Seen, int X, int Y, string Goals, byte[]? Mask) reads =
                (grid, this.explored.SeenBlocks, (int)(from.X / ExplorationMap.BlockCells), (int)(from.Y / ExplorationMap.BlockCells), string.Join("|", goalNames.Concat(exits.Select(cell => $"{cell.X:0},{cell.Y:0}"))), mask);
            if (this.frontierFor is { } last && ReferenceEquals(last.Grid, grid) && ReferenceEquals(last.Mask, mask) &&
                (last.Seen, last.X, last.Y, last.Goals) == (reads.Seen, reads.X, reads.Y, reads.Goals))
            {
                this.unexploredAsOf = now;
                return this.unexplored;
            }

            this.frontierFor = reads;
            var map = this.explored.Snapshot();
            this.frontierSearch = (Task.Run(() => this.TimedFrontier(map, grid, row, from, goalCells, mask, exits)), map, area.WorldToGridConvertor, player.WorldPosition.Z, goalNames, now);
            return this.unexplored;
        }

        /// <summary>
        ///     The exits the area's tiles place on ground not seen yet (grid cells), nearest first and at most
        ///     <see cref="MaxKnownGoals"/> (two walks of the whole grid each): the ground on the way to one is no nook, and
        ///     the exit is not named until seen (Freythorn's road, 2026-11-26).
        /// </summary>
        private IReadOnlyList<Vector2> UnseenExitCells(Vector2 from)
        {
            var toWorld = Core.States.InGameStateObject.CurrentAreaInstance.WorldToGridConvertor;
            return toWorld <= 0
                ? Array.Empty<Vector2>()
                : this.AreaLandmarks()
                    .Where(entry => entry.Category == ScanCategory.Exits && entry.Kind.Length == 0)
                    .Select(entry => new Vector2(entry.WorldX, entry.WorldY) / toWorld)
                    .Where(cell => !this.IsSeen(cell))
                    .OrderBy(cell => Vector2.DistanceSquared(cell, from))
                    .Take(MaxKnownGoals)
                    .ToList();
        }

        private IReadOnlyList<FrontierPlace> TimedFrontier(ExplorationMap map, byte[] grid, int row, Vector2 from, IReadOnlyList<Vector2> goals, byte[]? mask, IReadOnlyList<Vector2> exits)
        {
            var started = Environment.TickCount64;
            var places = map.Frontier(grid, row, from, MaxUnexplored, goals, mask, exits);
            var took = Environment.TickCount64 - started;
            if (took > this.slowestFrontierMs)
            {
                this.slowestFrontierMs = took;
                this.log.Info($"Unexplored ground worked out in {took} ms, the longest so far (grid of {grid.Length} bytes, {row} a row)");
            }

            return places;
        }

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
