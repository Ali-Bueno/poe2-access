namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;

    /// <summary>The live walkable grid and the barriers on it, for <see cref="EntityCatalog"/>'s routes.</summary>
    public sealed partial class EntityCatalog
    {
        /// <summary>The walkable grid as the game holds it now (see <see cref="WalkableGrid"/>); false outside an area.</summary>
        public bool TryLiveGrid(out byte[] grid, out int bytesPerRow)
        {
            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            bytesPerRow = area.TerrainMetadata.BytesPerRow;
            grid = bytesPerRow > 0 ? this.WalkableGrid(area) : Array.Empty<byte>();
            return grid.Length > 0;
        }

        /// <summary>
        ///     The walkable grid as the game holds it now. GameHelper copies it once per area, but the game edits it
        ///     in place when a tutorial barrier or a door opens or closes; the vector itself does not move.
        ///     The array returned is shared and must not be written to: the guide asks on every ping and a route is
        ///     searched on another thread, and a copy each (hundreds of kilobytes, so the large object heap) made the
        ///     collector run every few seconds (2026-11-10: 5 full collections a minute standing in town).
        /// </summary>
        private byte[] WalkableGrid(AreaInstance area)
        {
            var snapshot = area.GridWalkableData;
            lock (this.samplesLock)
            {
                // Between two samples the answer is the last one: the game's grid is read twice a second, not once a caller.
                var now = Environment.TickCount64;
                if (this.grid.Fresh(now, snapshot.Length) is { } recent)
                {
                    return recent;
                }

                if (this.ReadLiveGrid(area, snapshot.Length) is not { } live)
                {
                    return snapshot;
                }

                // What is kept of `live` and when the combined grid is made again: GridSamples.
                var closed = this.grid.ClosedCells;
                (int X, int Y)? at = Seats.Followed.TryGetComponent<Render>(out var player) ? ((int)player.GridPosition.X, (int)player.GridPosition.Y) : null;
                var stable = this.grid.Add(now, live, snapshot, area.TerrainMetadata.BytesPerRow, at);
                if (this.grid.ClosedCells != closed)
                {
                    this.log.Debug($"Barrier cells remembered closed: {this.grid.ClosedCells}");
                }

                if (this.log.Verbose && !live.AsSpan().SequenceEqual(snapshot))
                {
                    this.log.Debug($"Walkable grid changed since the area loaded: {Changed(snapshot, live)} cells");
                }

                return stable;
            }
        }

        /// <summary>
        ///     The game's grid now, read into one buffer kept for the area's size (hundreds of kilobytes twice a second, so
        ///     the large object heap otherwise); null when it cannot be read or is not <paramref name="cells"/> bytes long.
        ///     The buffer is valid until the next sample only and under the samples' lock: a sample <see cref="GridSamples"/>
        ///     keeps is a copy that owns its array.
        /// </summary>
        private byte[]? ReadLiveGrid(AreaInstance area, int cells)
        {
            if (cells == 0)
            {
                return Array.Empty<byte>(); // no grid loaded: as empty as the game's, whatever that holds
            }

            if (this.liveGrid.Length != cells)
            {
                this.liveGrid = new byte[cells];
            }

            return this.memory.ReadStdVector(area.TerrainMetadata.GridWalkableData, cells, ref this.liveGrid) == cells ? this.liveGrid : null;
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

        /// <summary>
        ///     Grid positions of the closed barriers (TriggerableBlockage, not a door the player opens): those awake now,
        ///     and those seen closed earlier in this area and not seen open since (past the bubble the game forgets them).
        ///     Frame thread only.
        /// </summary>
        public IReadOnlyList<Vector2> ClosedBarriers()
        {
            lock (this.samplesLock)
            {
                foreach (var (_, entity) in Core.States.InGameStateObject.CurrentAreaInstance.AwakeEntities)
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
                this.grid.Forget();
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
    }
}
