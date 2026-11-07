namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Keeps the guide on what a clock runs against, one after another: the sound of a Death Crystal goes through walls, the
    ///     route does not (user 2026-11-07). The player still walks; it only chooses the target. Pure.
    /// </summary>
    public sealed class TimedGuide
    {
        private uint? aimed;

        /// <summary>
        ///     The entry to guide to now, or null to leave the guide as it is: while the one chosen stands, even after arriving or
        ///     the player's release, and while the player guides to something else.
        /// </summary>
        /// <param name="nearestFirst">What the clock runs against, nearest first.</param>
        /// <param name="guided">What the guide is on, null for nothing.</param>
        public uint? Next(IReadOnlyList<uint> nearestFirst, uint? guided)
        {
            if (this.aimed is { } standing && nearestFirst.Contains(standing))
            {
                return null;
            }

            var free = guided == null || guided == this.aimed;
            this.aimed = free && nearestFirst.Count > 0 ? nearestFirst[0] : null;
            return this.aimed;
        }
    }
}
