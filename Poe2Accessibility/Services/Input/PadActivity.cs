namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Whether the player is driving the game with the controller behind another window. The game takes the pad there: a
    ///     tester went through the support engraving window's categories with R1, apparently with a notes window in front
    ///     (the host ran at its 20 frames a second for most of those minutes), heard each choice and none of the categories,
    ///     which came as screen changes the mod leaves unsaid behind another window (2026-11-26).
    /// </summary>
    public sealed class PadActivity
    {
        /// <summary>A press counts this long as the player's: the page it brings settles well within it (300 ms to a second).</summary>
        public const int DrivesMs = 3000;

        private (uint Buttons, bool Direction) last;
        private long pressedAt = long.MinValue / 2;

        /// <summary>Per frame, the pad as the game received it: a button going down or a direction taken is a press.</summary>
        public void Observe(uint buttons, bool direction, long nowMs)
        {
            if ((buttons & ~this.last.Buttons) != 0 || (direction && !this.last.Direction))
            {
                this.pressedAt = nowMs;
            }

            this.last = (buttons, direction);
        }

        /// <summary>
        ///     A press came after <paramref name="sinceMs"/> (when the game went behind another window) and within
        ///     <see cref="DrivesMs"/> of now. A press from before is play the window was taken from: what the game redraws
        ///     as it loses the front (a notification over a fight) stays unsaid.
        /// </summary>
        public bool Driving(long sinceMs, long nowMs) => this.pressedAt > sinceMs && nowMs - this.pressedAt < DrivesMs;
    }
}
