namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     How long after the first reading of an area what is read still counts as the baseline: buffs and stats fill in a
    ///     few frames after the area loads, and a first reading that was still empty made the real state news. Pure.
    /// </summary>
    public sealed class BaselineGrace
    {
        private readonly long graceMs;
        private long? until;

        public BaselineGrace(long graceMs) => this.graceMs = graceMs;

        /// <summary>True while a reading is baseline, not news; the first call after <see cref="Reset"/> starts the window.</summary>
        public bool Active(long now)
        {
            this.until ??= now + this.graceMs;
            return now < this.until;
        }

        /// <summary>A new area or character: the next reading starts a new window.</summary>
        public void Reset() => this.until = null;
    }
}
