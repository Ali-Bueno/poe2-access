namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Whether the game world is in front: shut the moment a menu covers it, open again once nothing has for
    ///     <see cref="ReturnMs"/>, since a panel without focus reaches the screen snapshots a moment after the focus left. Pure.
    /// </summary>
    public sealed class WorldGate
    {
        /// <summary>Longer than two screen snapshots (ScreenWatcher, 150 ms each).</summary>
        public const int ReturnMs = 400;

        private long coveredAt = long.MinValue / 2;

        /// <summary>Notes whether a menu covers the world now; true when the world is in front.</summary>
        public bool InFront(bool covered, long now)
        {
            if (covered)
            {
                this.coveredAt = now;
            }

            return now - this.coveredAt >= ReturnMs;
        }
    }
}
