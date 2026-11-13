namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>What a long walk through an area calls for (<see cref="LostWatch.Explored"/>).</summary>
    public enum LostNews
    {
        None,

        /// <summary>The hint's sound, on the side the way on lies.</summary>
        Sound,

        /// <summary>The way on said in words, with its route.</summary>
        Spoken,
    }

    /// <summary>
    ///     Time spent walking without seeing new ground, the sign of a lost player; and time spent moving through the area at
    ///     all: a dead end is new ground too, so a player trying one after another was never "lost" and no hint came in an
    ///     hour (user 2026-11-12, La ciudad perdida). Pure.
    /// </summary>
    public sealed class LostWatch
    {
        /// <summary>The moving between one hint by the area's clock and the next of its kind.</summary>
        public const int AreaRepeatMs = 180000;

        /// <summary>A longer gap between polls (a load, a menu) is not walking.</summary>
        private const long MaxStepMs = 1000;

        private int seenBlocks = -1;
        private long polledAt;
        private long walkedMs;
        private long areaMovedMs;
        private long soundAt = -1;
        private long spokenAt = -1;

        /// <summary>A new area: nothing walked in it yet.</summary>
        public void Reset()
        {
            this.seenBlocks = -1;
            this.walkedMs = 0;
            this.areaMovedMs = 0;
            this.soundAt = this.spokenAt = -1;
        }

        /// <summary>
        ///     After <see cref="Update"/>: what the moving done in this area calls for. The sound once it passes
        ///     <paramref name="soundAfterMs"/>, the words once it passes <paramref name="spokenAfterMs"/> (0: never), each again
        ///     every <see cref="AreaRepeatMs"/> of moving. The caller says nothing once the way on is in sight.
        /// </summary>
        public LostNews Explored(int soundAfterMs, int spokenAfterMs)
        {
            if (this.Due(spokenAfterMs, ref this.spokenAt))
            {
                this.soundAt = this.areaMovedMs; // the words come with the sound
                return LostNews.Spoken;
            }

            return this.Due(soundAfterMs, ref this.soundAt) ? LostNews.Sound : LostNews.None;
        }

        private bool Due(int afterMs, ref long last)
        {
            if (afterMs <= 0 || this.areaMovedMs < afterMs || (last >= 0 && this.areaMovedMs - last < AreaRepeatMs))
            {
                return false;
            }

            last = this.areaMovedMs;
            return true;
        }

        /// <summary>One poll; true when the player has walked this long without new ground, and the count starts again.</summary>
        /// <param name="walking">The player moved since the last poll, out of a fight.</param>
        /// <param name="moving">The player moved since the last poll, fighting or not: the area's clock (null: as <paramref name="walking"/>).</param>
        public bool Update(long now, int seenBlocks, bool walking, int lostAfterMs, bool? moving = null)
        {
            var step = Math.Min(now - this.polledAt, MaxStepMs);
            this.polledAt = now;
            this.areaMovedMs += moving ?? walking ? step : 0;
            if (seenBlocks != this.seenBlocks)
            {
                this.seenBlocks = seenBlocks;
                this.walkedMs = 0;
                return false;
            }

            this.walkedMs += walking ? step : 0;
            if (this.walkedMs < lostAfterMs)
            {
                return false;
            }

            this.walkedMs = 0;
            return true;
        }
    }
}
