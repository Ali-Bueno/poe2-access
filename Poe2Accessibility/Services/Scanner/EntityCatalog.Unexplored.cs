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

        private IReadOnlyList<ScanEntry> unexplored = Array.Empty<ScanEntry>();
        private long unexploredAt = long.MinValue / 2;

        /// <summary>
        ///     The longest a frontier search has taken, logged when it grows past this: the search walks every cell of the area
        ///     twice since 2026-11-12, measured only in a town (no frame held: it runs off the frame thread).
        /// </summary>
        private long slowestFrontierMs = 250;

        /// <summary>The frontier being worked out, with the map it reads and what its places need to become entries.</summary>
        private (Task<IReadOnlyList<FrontierPlace>> Places, ExplorationMap Map, float ToWorld, float Height)? frontierSearch;

        /// <summary>How much of the ground the player can reach has been seen, in percent; null outside gameplay or without a grid.</summary>
        public int? ExploredPercent()
        {
            return Seats.Followed.TryGetComponent<Render>(out var player) && this.TryLiveGrid(out var grid, out var row)
                ? this.explored.ExploredPercent(grid, row, new Vector2(player.GridPosition.X, player.GridPosition.Y))
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

            var places = this.Unexplored(area, player);
            var best = places.Count == 0 ? 0 : places.Min(entry => ExplorationMap.Rank(entry.Kind));
            return places.Where(entry => ExplorationMap.Rank(entry.Kind) == best).Select(entry => new Vector2(entry.WorldX, entry.WorldY) / toWorld).ToList();
        }

        /// <summary>What the line from <paramref name="from"/> along <paramref name="axis"/> (grid cells) soon leaves the seen ground onto, null for nothing: the radar's mark.</summary>
        public UnseenSize? UnseenToward(Vector2 from, Vector2 axis)
        {
            if (!Seats.Followed.TryGetComponent<Render>(out var player))
            {
                return null;
            }

            this.Unexplored(Core.States.InGameStateObject.CurrentAreaInstance, player); // keeps the reachable ground fresh
            return this.explored.UnseenAlong(from, axis, ExplorationMap.MarkReachCells);
        }

        /// <summary>Where unseen ground begins (<see cref="ExplorationMap"/>) as last worked out; a new search starts every UnexploredRefreshMs.</summary>
        private IReadOnlyList<ScanEntry> Unexplored(AreaInstance area, Render player)
        {
            var now = Environment.TickCount64;
            if (this.frontierSearch is { Places.IsCompleted: true } found)
            {
                this.frontierSearch = null;
                if (found.Places.IsFaulted)
                {
                    this.log.Info($"Unexplored ground not found: {found.Places.Exception?.GetBaseException().Message}");
                }
                else if (this.explored.Adopt(found.Map))
                {
                    this.unexplored = found.Places.Result
                        .Select(place => new ScanEntry(
                            UnexploredIdBase + (uint)(((int)place.Cell.Y / ExplorationMap.BlockCells) << 15) + (uint)((int)place.Cell.X / ExplorationMap.BlockCells),
                            ScanCategory.Unexplored,
                            Announcements.Unexplored(place.Size),
                            place.Cell.X * found.ToWorld,
                            place.Cell.Y * found.ToWorld,
                            found.Height,
                            place.Size == UnseenSize.Pocket ? ExplorationMap.PocketKind : place.Size == UnseenSize.Small ? ExplorationMap.SmallKind : string.Empty))
                        .ToList();
                }
            }

            if (this.frontierSearch != null || now - this.unexploredAt < UnexploredRefreshMs || !this.TryLiveGrid(out var grid, out var row))
            {
                return this.unexplored;
            }

            // Off the frame thread: every two seconds it held a frame for 30 ms, 140 ms on a Trial's floor (docs/research/performance.md).
            this.unexploredAt = now;
            var map = this.explored.Snapshot();
            var from = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            this.frontierSearch = (Task.Run(() => this.TimedFrontier(map, grid, row, from)), map, area.WorldToGridConvertor, player.WorldPosition.Z);
            return this.unexplored;
        }

        private IReadOnlyList<FrontierPlace> TimedFrontier(ExplorationMap map, byte[] grid, int row, Vector2 from)
        {
            var started = Environment.TickCount64;
            var places = map.Frontier(grid, row, from, MaxUnexplored);
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
