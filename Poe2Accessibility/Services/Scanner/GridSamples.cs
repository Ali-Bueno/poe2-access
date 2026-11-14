namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     The recent samples of an area's live walkable grid and the grid routes are searched on. A cell is walkable for
    ///     routes only if it was in every sample kept: a closed tutorial barrier (TutorialBlocker3 by the Riverbank bridge,
    ///     2026-10-23) flickers in the live grid every few seconds, which split the ground in two and made routes and the
    ///     guide's "the way is closed" flap. A gate that really opens is walkable again once the samples agree (about two
    ///     seconds). Past the network bubble the grid cannot be trusted at all: see <see cref="BarrierMemory"/>.
    ///     Pure: the caller reads the game and holds the lock.
    /// </summary>
    internal sealed class GridSamples
    {
        private readonly int maxSamples;
        private readonly long sampleMs;
        private readonly Queue<byte[]> samples = new();
        private readonly BarrierMemory barriers = new();

        /// <summary>
        ///     The grid combined last, and the one sample it was made from: null unless every sample then was that array, so
        ///     a grid combined from samples that differ is never taken for what the newest alone says.
        /// </summary>
        private (byte[]? Only, byte[] Grid)? stable;
        private byte[]? newest;
        private long sampledAt;

        /// <summary>The sample the barrier memory read last: samples are never written to, so the next reads only what differs.</summary>
        private byte[]? observed;

        /// <param name="maxSamples">How many samples a cell must be walkable in.</param>
        /// <param name="sampleMs">How long one sample answers for.</param>
        public GridSamples(int maxSamples, long sampleMs)
        {
            this.maxSamples = maxSamples;
            this.sampleMs = sampleMs;
        }

        /// <summary>Gets how many cells the barrier memory holds closed.</summary>
        public int ClosedCells => this.barriers.ClosedCount;

        /// <summary>
        ///     The grid to answer with without reading the game: the one combined last, while it has <paramref name="cells"/>
        ///     bytes and its sample is younger than a sample's time; null when a sample is due.
        /// </summary>
        public byte[]? Fresh(long now, int cells) =>
            this.stable is { } kept && kept.Grid.Length == cells && now - this.sampledAt < this.sampleMs ? kept.Grid : null;

        /// <summary>
        ///     A barrier just opened: the samples that saw it closed are forgotten, so the next call samples and answers with
        ///     what the game holds then. What the barrier memory holds stays.
        /// </summary>
        public void Forget()
        {
            this.samples.Clear();
            this.newest = null;
            this.stable = null;
            this.observed = null;
        }

        /// <summary>Another area: no samples and no barrier memory.</summary>
        public void Reset()
        {
            this.Forget();
            this.barriers.Reset();
        }

        /// <summary>
        ///     Takes one sample and returns the grid for routes: shared, and not to be written to.
        /// </summary>
        /// <param name="live">The game's grid now. The caller's buffer, free to be read into again: a sample kept is a copy.</param>
        /// <param name="loaded">The grid as the area loaded, for the barrier memory.</param>
        /// <param name="player">The player's cell, when known: only near it is an open cell believed (<see cref="BarrierMemory"/>).</param>
        public byte[] Add(long now, byte[] live, byte[] loaded, int bytesPerRow, (int X, int Y)? player)
        {
            if (this.samples.Count > 0 && this.samples.Peek().Length != live.Length)
            {
                this.Forget();
            }

            // A grid that did not change is the sample before it: one array stands for all of them.
            var same = this.newest != null && this.newest.AsSpan().SequenceEqual(live);
            this.newest = same ? this.newest! : (byte[])live.Clone();
            this.samples.Enqueue(this.newest);
            this.sampledAt = now;
            var changes = this.barriers.Changes;
            if (player is { } at)
            {
                this.barriers.Observe(this.newest, loaded, bytesPerRow, at.X, at.Y, this.observed);
                this.observed = this.newest;
            }

            while (this.samples.Count > this.maxSamples)
            {
                this.samples.Dequeue();
            }

            // Every sample the one array the grid was made from, and no barrier learnt or forgotten: it still holds.
            if (this.stable is { Only: { } only } kept && this.barriers.Changes == changes && this.AllAre(only))
            {
                return kept.Grid;
            }

            var grid = this.Combine(bytesPerRow > 0 ? bytesPerRow : Math.Max(1, live.Length));
            this.barriers.Apply(grid);

            // Kept under the newest sample only when it was made from that sample alone: a combination of different
            // samples (a door that just opened still reads closed) must be made again once they all agree, or the
            // door would stay closed for routes.
            this.stable = (this.AllAre(this.newest) ? this.newest : null, grid);
            return grid;
        }

        private bool AllAre(byte[] sample)
        {
            foreach (var kept in this.samples)
            {
                if (!ReferenceEquals(kept, sample))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The newest sample with every cell closed that any other sample has closed (two cells a byte), read only in the rows where they differ.</summary>
        private byte[] Combine(int bytesPerRow)
        {
            var newest = this.newest!;
            var grid = (byte[])newest.Clone();
            foreach (var sample in this.samples)
            {
                if (ReferenceEquals(sample, newest))
                {
                    continue;
                }

                for (var start = 0; start < grid.Length; start += bytesPerRow)
                {
                    var end = Math.Min(start + bytesPerRow, grid.Length);
                    if (sample.AsSpan(start, end - start).SequenceEqual(newest.AsSpan(start, end - start)))
                    {
                        continue;
                    }

                    for (var i = start; i < end; i++)
                    {
                        var both = grid[i];
                        var other = sample[i];
                        if ((other & 0x0F) == 0)
                        {
                            both &= 0xF0;
                        }

                        if ((other & 0xF0) == 0)
                        {
                            both &= 0x0F;
                        }

                        grid[i] = both;
                    }
                }
            }

            return grid;
        }
    }
}
