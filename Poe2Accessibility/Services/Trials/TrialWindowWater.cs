namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     The sacred water a Trial window over the game shows (the merchant's "269" behind the water icon, the map's counters):
    ///     said once a shop-like window has opened, and whenever it changes while the window stays (a purchase). The HUD's
    ///     counters are hidden under such a window, so a purchase was said by nothing (the owner's run, 2026-11-16). Pure.
    /// </summary>
    public sealed class TrialWindowWater
    {
        /// <summary>The opening's amount waits this long, so the window's first focus is said before it.</summary>
        public const long OpeningDelayMs = 700;

        private IntPtr layer;
        private int water;
        private long seenAt;
        private bool opened;

        /// <summary>No window shows the water: the next one is a new opening.</summary>
        public void Reset() => this.layer = IntPtr.Zero;

        /// <summary>
        ///     The water a window shows now; returns what to say (the amount, and the change since the last look, 0 at
        ///     the opening) or null. <paramref name="sayOpening"/>: the window shows the water alone (the merchant), not
        ///     beside the keys (the map, where the rooms are what the player came for).
        /// </summary>
        public (int Now, int Change)? Heard(IntPtr window, int now, bool sayOpening, long at)
        {
            if (window != this.layer)
            {
                (this.layer, this.water, this.seenAt, this.opened) = (window, now, at, !sayOpening);
                return null;
            }

            if (!this.opened && at - this.seenAt >= OpeningDelayMs)
            {
                (this.opened, this.water) = (true, now);
                return (now, 0);
            }

            if (now == this.water)
            {
                return null;
            }

            var change = now - this.water;
            (this.water, this.opened) = (now, true);
            return (now, change);
        }
    }
}
