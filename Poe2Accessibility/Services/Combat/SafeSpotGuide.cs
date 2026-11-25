namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    public enum SafeSpotNewsKind
    {
        /// <summary>A true clone is marked: the way to it.</summary>
        First,

        /// <summary>The way again: the place is settled now, or it lies another way, or the player left it.</summary>
        Way,

        /// <summary>The player stands where the blow does not reach.</summary>
        Inside,
    }

    /// <summary>A word of the safe spot's guide: the way on screen and the steps left to where it is safe.</summary>
    public readonly record struct SafeSpotNews(SafeSpotNewsKind Kind, int Octant, int Steps);

    /// <summary>
    ///     Leads to where a fight's true clone stops (Yama's frozen time: the blow fills the arena but for a hole there): the
    ///     way and the steps, again once the place is settled and whenever it lies another way, and that the player is in it. Pure.
    /// </summary>
    public sealed class SafeSpotGuide
    {
        /// <summary>A clone that stopped this long ago (ms) is past its blow (Yama's came 2.3 to 3.2 s after).</summary>
        public const int StaleMs = 6000;

        /// <summary>The way is not said again sooner than this (ms), unless the place has just settled.</summary>
        private const int WordGapMs = 900;

        private uint? spotId;
        private bool settledSaid;
        private int saidOctant;
        private long saidAt;

        /// <summary>The player was in the safe place at the last update.</summary>
        public bool Inside { get; private set; }

        /// <summary>How wide the heartbeat takes the place to be: its "there" beat (a gap of the reach) means wholly in the hole.</summary>
        public static float BeatRadius(float hole, float playerRadius) => Math.Max(0f, SafeRadius(hole, playerRadius) - playerRadius - BossPlaces.AtPlaceWorld);

        public void Reset()
        {
            this.spotId = null;
            this.settledSaid = false;
            this.Inside = false;
        }

        /// <param name="settled">The place is final: the clone's way names its end, or it has stopped.</param>
        /// <param name="hole">The radius of the blow's hole (world units); null when the files draw none.</param>
        public SafeSpotNews? Update(uint id, Vector2 spot, bool settled, Vector2 player, float playerRadius, float? hole, Func<Vector2, int> octantOf, long now)
        {
            var first = this.spotId != id;
            if (first)
            {
                this.Reset();
                this.spotId = id;
            }

            var gap = Vector2.Distance(spot, player) - (hole is { } radius ? SafeRadius(radius, playerRadius) : playerRadius + BossPlaces.AtPlaceWorld);
            var octant = octantOf(spot - player);
            var was = this.Inside;
            this.Inside = settled && gap <= 0f;
            var kind = this.KindOf(first, settled, was, hole != null, octant, now);
            this.settledSaid |= settled;
            if (kind is not { } said)
            {
                return null;
            }

            (this.saidOctant, this.saidAt) = (octant, now);
            return new SafeSpotNews(said, octant, Math.Max(1, (int)MathF.Ceiling(gap / BlastGuide.StepWorldUnits)));
        }

        private SafeSpotNewsKind? KindOf(bool first, bool settled, bool was, bool holed, int octant, long now)
        {
            if (this.Inside)
            {
                return was || !holed ? null : SafeSpotNewsKind.Inside;
            }

            if (first)
            {
                return SafeSpotNewsKind.First;
            }

            var turned = (was || octant != this.saidOctant) && now - this.saidAt >= WordGapMs;
            return settled && (!this.settledSaid || turned) ? SafeSpotNewsKind.Way : null;
        }

        /// <summary>Within this of the hole's centre the whole body is out of the blow.</summary>
        private static float SafeRadius(float hole, float playerRadius) => Math.Max(0f, hole - playerRadius - AttackAim.MarginWorld);
    }
}
