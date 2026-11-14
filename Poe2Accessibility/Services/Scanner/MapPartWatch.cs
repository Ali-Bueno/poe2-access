namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     Which part of the map the player is in, changing only once they are a look past the line between two parts: a walk
    ///     along the line names neither side again and again. Pure.
    /// </summary>
    public sealed class MapPartWatch
    {
        private (int Across, int Down)? part;

        /// <summary>Gets the part the player was last in; null before the first place of an area.</summary>
        public (int Across, int Down)? Current => this.part;

        /// <summary>A new area: its first place is where the player starts, not a part entered.</summary>
        public void Reset() => this.part = null;

        /// <summary>The part entered now; null while it is the same one, and for the first place after a reset.</summary>
        /// <param name="share">Where the player is on the map, 0 to 1 across and down (<see cref="MapFrame.Share"/>).</param>
        /// <param name="look">The share of the map one look covers (<see cref="MapFrame.Span"/> of the reveal radius).</param>
        public (int Across, int Down)? Update(Vector2 share, Vector2 look)
        {
            var now = (Step(this.part?.Across, share.X, look.X), Step(this.part?.Down, share.Y, look.Y));
            var entered = this.part is { } last && last != now;
            this.part = now;
            return entered ? now : null;
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
