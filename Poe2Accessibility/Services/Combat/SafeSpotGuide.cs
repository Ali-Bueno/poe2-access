namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    public enum SafeSpotNewsKind
    {
        /// <summary>A true clone is marked: the way to it.</summary>
        First,

        /// <summary>The way again: the place is settled now, or it lies another way.</summary>
        Way,

        /// <summary>The player is, or at their pace is about to be, where the blow does not reach: stop there.</summary>
        Inside,

        /// <summary>The player left that place, or ran past it: the way back.</summary>
        Out,
    }

    /// <summary>A word of the safe spot's guide: the way on screen and the steps left to where it is safe.</summary>
    public readonly record struct SafeSpotNews(SafeSpotNewsKind Kind, int Octant, int Steps);

    /// <summary>
    ///     Leads to where a fight's true clone stops (Yama's frozen time: the blow fills the arena but for a hole there): the
    ///     way and the steps, again once the place is settled and whenever it lies another way, to stop as the player comes
    ///     into it, and at once when they are out of it again. Asked each frame: a running player crosses it in 0.6 s. Pure.
    /// </summary>
    public sealed class SafeSpotGuide
    {
        /// <summary>A clone that stopped this long ago (ms) is past its blow (Yama's came 2.2 to 3.3 s after).</summary>
        public const int StaleMs = 6000;

        /// <summary>The word to stop comes this long (ms) before a running player is inside: about what the voice takes to start and the hand to let go.</summary>
        public const int AheadMs = 250;

        /// <summary>The player's pace is taken over at least this long (ms), so one frame's jitter is none.</summary>
        private const int PaceMs = 80;

        /// <summary>The way is not said again sooner than this (ms), unless the place has just settled or the player is out of it.</summary>
        private const int WordGapMs = 900;

        private uint? spotId;
        private bool settledSaid;
        private bool stopSaid;
        private bool wasIn;
        private int saidOctant;
        private long saidAt;
        private (Vector2 At, long When)? paceFrom;
        private Vector2 pace;

        /// <summary>The player was in the safe place at the last update.</summary>
        public bool Inside { get; private set; }

        /// <summary>How wide the lead takes the place to be: no gap left to the body means it is wholly out of the blow.</summary>
        public static float LeadRadius(float hole, float playerRadius) => Math.Max(0f, SafeRadius(hole, playerRadius) - playerRadius);

        public void Reset()
        {
            this.spotId = null;
            this.settledSaid = this.stopSaid = this.wasIn = false;
            this.paceFrom = null;
            this.pace = Vector2.Zero;
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

            this.NotePace(player, now);
            var reach = hole is { } radius ? SafeRadius(radius, playerRadius) : playerRadius + BossPlaces.AtPlaceWorld;
            var gap = Vector2.Distance(spot, player) - reach;
            this.Inside = settled && gap <= 0f;
            var arriving = settled && hole != null && Vector2.Distance(spot, player + (this.pace * AheadMs)) <= reach;
            var octant = octantOf(spot - player);
            var kind = this.KindOf(first, settled, hole != null, arriving, octant, now);
            this.settledSaid |= settled;
            if (kind is not { } said)
            {
                return null;
            }

            this.stopSaid = said == SafeSpotNewsKind.Inside;
            (this.saidOctant, this.saidAt) = (octant, now);
            return new SafeSpotNews(said, octant, Math.Max(1, (int)MathF.Ceiling(gap / BlastGuide.StepWorldUnits)));
        }

        private SafeSpotNewsKind? KindOf(bool first, bool settled, bool holed, bool arriving, int octant, long now)
        {
            if (this.Inside)
            {
                this.wasIn = true;
                return this.stopSaid || !holed ? null : SafeSpotNewsKind.Inside;
            }

            var left = this.wasIn || (!arriving && now - this.saidAt > AheadMs);
            this.wasIn = false;
            if (this.stopSaid)
            {
                return left ? SafeSpotNewsKind.Out : null;
            }

            if (arriving)
            {
                return SafeSpotNewsKind.Inside;
            }

            if (first)
            {
                return SafeSpotNewsKind.First;
            }

            var turned = octant != this.saidOctant && now - this.saidAt >= WordGapMs;
            return settled && (!this.settledSaid || turned) ? SafeSpotNewsKind.Way : null;
        }

        private void NotePace(Vector2 player, long now)
        {
            if (this.paceFrom is not { } from)
            {
                this.paceFrom = (player, now);
            }
            else if (now - from.When >= PaceMs)
            {
                this.pace = (player - from.At) / (now - from.When);
                this.paceFrom = (player, now);
            }
        }

        /// <summary>Within this of the hole's centre the whole body is out of the blow.</summary>
        private static float SafeRadius(float hole, float playerRadius) => Math.Max(0f, hole - playerRadius - AttackAim.MarginWorld);
    }
}
