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
                if (this.stable is { } recent && recent.Grid.Length == snapshot.Length && now - this.lastSampleAt < SampleMs)
                {
                    return recent.Grid;
                }

                if (this.ReadLiveGrid(area, snapshot.Length) is not { } live)
                {
                    return snapshot;
                }

                // A cell is walkable for routes only if it was in every recent sample: a closed tutorial barrier
                // (TutorialBlocker3 by the Riverbank bridge, 2026-10-23) flickers in the live grid every few seconds,
                // which split the ground in two and made routes and the guide's "the way is closed" flap. A gate that
                // really opens is walkable again once the samples agree (about two seconds). Past the network bubble the
                // grid cannot be trusted at all: see BarrierMemory.
                if (this.samples.Count > 0 && this.samples.Peek().Length != live.Length)
                {
                    this.samples.Clear();
                    this.newestSample = null;
                    this.stable = null;
                }

                // A grid that did not change is the sample before it: one array stands for all of them. One that did is
                // kept as a copy: `live` is the buffer the next sample is read into.
                var newest = this.newestSample;
                var same = newest != null && newest.AsSpan().SequenceEqual(live);
                this.newestSample = same ? newest! : (byte[])live.Clone();
                this.samples.Enqueue(this.newestSample);
                this.lastSampleAt = now;
                var closed = this.barriers.ClosedCount;
                var changes = this.barriers.Changes;
                if (Seats.Followed.TryGetComponent<Render>(out var player))
                {
                    this.barriers.Observe(live, snapshot, area.TerrainMetadata.BytesPerRow, (int)player.GridPosition.X, (int)player.GridPosition.Y);
                    if (this.barriers.ClosedCount != closed)
                    {
                        this.log.Debug($"Barrier cells remembered closed: {this.barriers.ClosedCount}");
                    }
                }

                while (this.samples.Count > MaxSamples)
                {
                    this.samples.Dequeue();
                }

                if (this.log.Verbose && !live.AsSpan().SequenceEqual(snapshot))
                {
                    this.log.Debug($"Walkable grid changed since the area loaded: {Changed(snapshot, live)} cells");
                }

                // Every sample the same array and no barrier learnt or forgotten: what was combined before still holds.
                if (this.stable is { } kept && this.barriers.Changes == changes && this.samples.All(sample => ReferenceEquals(sample, kept.Live)))
                {
                    return kept.Grid;
                }

                var stable = (byte[])this.newestSample.Clone();
                foreach (var sample in this.samples)
                {
                    if (ReferenceEquals(sample, this.newestSample))
                    {
                        continue;
                    }

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
                this.stable = (this.newestSample, stable);
                return stable;
            }
        }

        /// <summary>
        ///     The game's grid now, read into one buffer kept for the area's size (hundreds of kilobytes twice a second, so
        ///     the large object heap otherwise); null when it cannot be read or is not <paramref name="cells"/> bytes long.
        ///     The buffer is valid until the next sample only and under the samples' lock: what is kept of it is copied.
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
                this.newestSample = null;
                this.stable = null;
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
