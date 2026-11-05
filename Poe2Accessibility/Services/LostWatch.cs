namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Time spent walking without seeing new ground, the sign of a lost player. Pure.</summary>
    public sealed class LostWatch
    {
        /// <summary>A longer gap between polls (a load, a menu) is not walking.</summary>
        private const long MaxStepMs = 1000;

        private int seenBlocks = -1;
        private long polledAt;
        private long walkedMs;

        /// <summary>A new area: nothing walked in it yet.</summary>
        public void Reset()
        {
            this.seenBlocks = -1;
            this.walkedMs = 0;
        }

        /// <summary>One poll; true when the player has walked this long without new ground, and the count starts again.</summary>
        /// <param name="walking">The player moved since the last poll, out of a fight.</param>
        public bool Update(long now, int seenBlocks, bool walking, int lostAfterMs)
        {
            var step = Math.Min(now - this.polledAt, MaxStepMs);
            this.polledAt = now;
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
