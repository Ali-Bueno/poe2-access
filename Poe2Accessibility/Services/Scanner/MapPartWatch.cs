namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     Which part of the map the player is in, changing only once they are a look past the line between two parts: a walk
    ///     along the line names neither side again and again. A part is news once the player has stayed in it a moment. Pure.
    /// </summary>
    public sealed class MapPartWatch
    {
        /// <summary>A part must hold this long to be said: crossing where four parts meet names only the one walked into.</summary>
        public const int SettleMs = 1500;

        private (int Across, int Down)? part;
        private (int Across, int Down)? said;
        private long changedAt;

        /// <summary>Gets the part the player was last in; null before the first place of an area.</summary>
        public (int Across, int Down)? Current => this.part;

        /// <summary>A new area: its first place is where the player starts, not a part entered.</summary>
        public void Reset() => (this.part, this.said) = (null, null);

        /// <summary>The part entered, once held for <see cref="SettleMs"/>; null while it is the one said last, and for the first place after a reset.</summary>
        /// <param name="share">Where the player is on the map, 0 to 1 across and down (<see cref="MapFrame.Share"/>).</param>
        /// <param name="look">The share of the map one look covers (<see cref="MapFrame.Span"/> of the reveal radius).</param>
        public (int Across, int Down)? Update(Vector2 share, Vector2 look, long nowMs)
        {
            var now = (Step(this.part?.Across, share.X, look.X), Step(this.part?.Down, share.Y, look.Y));
            if (this.part != now)
            {
                if (this.part == null)
                {
                    this.said = now; // where the player starts
                }

                (this.part, this.changedAt) = (now, nowMs);
            }

            if (this.said == now || nowMs - this.changedAt < SettleMs)
            {
                return null;
            }

            this.said = now;
            return now;
        }

        /// <summary>The third along one way: the one held until the place is a look past its line, the middle one where a third is narrower than a look across.</summary>
        private static int Step(int? held, float share, float look)
        {
            if (2 * look > MapCorner.Third)
            {
                return 0;
            }

            var third = MapCorner.ThirdOf(share);
            if (held is not { } current || third == current)
            {
                return third;
            }

            var line = third > current
                ? current < 0 ? MapCorner.Third : 1f - MapCorner.Third
                : current > 0 ? 1f - MapCorner.Third : MapCorner.Third;
            return Math.Abs(share - line) >= look ? third : current;
        }
    }
}
