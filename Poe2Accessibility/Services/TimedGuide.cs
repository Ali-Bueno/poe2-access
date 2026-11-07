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
        /// <summary>Looks in a row with nothing left before the guide lets go: a page of the tracker can hide the objective for a moment.</summary>
        private const int LooksToRelease = 3;

        private uint? aimed;
        private int emptyLooks;

        /// <summary>
        ///     The entry to guide to now, if any, and whether to let go of the guide. Null leaves it as it is: while the one chosen
        ///     stands, even after arriving or the player's release, and while the player guides to something else. With the room
        ///     done nothing is left, and the guide left on the last crystal lets go (user 2026-11-07).
        /// </summary>
        /// <param name="nearestFirst">What the clock runs against, nearest first.</param>
        /// <param name="guided">What the guide is on, null for nothing.</param>
        public (uint? Next, bool Release) Look(IReadOnlyList<uint> nearestFirst, uint? guided)
        {
            if (nearestFirst.Count == 0)
            {
                if (this.aimed == null || ++this.emptyLooks < LooksToRelease)
                {
                    return (null, false);
                }

                var ours = guided == this.aimed;
                (this.aimed, this.emptyLooks) = (null, 0);
                return (null, ours);
            }

            this.emptyLooks = 0;
            if (this.aimed is { } standing && nearestFirst.Contains(standing))
            {
                return (null, false);
            }

            var free = guided == null || guided == this.aimed;
            this.aimed = free ? nearestFirst[0] : null;
            return (this.aimed, false);
        }
    }
}
