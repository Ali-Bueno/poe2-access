namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     An answer read from the screen at some cost, kept while what it was read from looks the same (pure). It is read
    ///     again a few times after that changes, as a panel fills in after it shows, then once in a while.
    /// </summary>
    public sealed class SettlingMemo<T>
    {
        /// <summary>How long after a change the answer is read again: a panel's grid and labels come a moment after the panel.</summary>
        public static readonly int[] LooksMs = { 250, 500, 1000, 2000 };

        /// <summary>How old a kept answer may grow: what the key does not see (a label deep in a panel) is stale no longer.</summary>
        public const int RefreshMs = 5000;

        private bool has;
        private long key;
        private long changedAt;
        private long readAt;
        private int looks;
        private T answer = default!;

        /// <summary>The answer kept for <paramref name="key"/>; false when it must be read (again).</summary>
        public bool TryGet(long key, long now, out T answer)
        {
            answer = this.answer;
            return this.has && key == this.key && !this.LookDue(now) && now - this.readAt < RefreshMs;
        }

        /// <summary>Keeps the answer just read for <paramref name="key"/>.</summary>
        public void Set(long key, long now, T answer)
        {
            if (!this.has || key != this.key)
            {
                (this.has, this.key, this.changedAt, this.looks) = (true, key, now, 0);
            }
            else
            {
                while (this.LookDue(now))
                {
                    this.looks++;
                }
            }

            (this.readAt, this.answer) = (now, answer);
        }

        private bool LookDue(long now) => this.looks < LooksMs.Length && now - this.changedAt >= LooksMs[this.looks];
    }
}
