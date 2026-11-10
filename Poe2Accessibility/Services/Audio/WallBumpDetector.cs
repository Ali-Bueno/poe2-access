namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>Distances in world units, times in ms (see docs/radar.md, wall bump).</summary>
    public sealed record WallBumpLimits(float MinPush, long StallMs, float StallWorld, float RearmWorld, float RearmTurnDegrees, long RearmIdleMs);

    /// <summary>
    ///     The wall bump (LeAccess / D2R): the stick pushes, the character has not moved for a moment, and the terrain
    ///     blocks just ahead along the push — one thud per contact. It re-arms after walking on, turning the push, or a
    ///     moment without pushing into anything.
    /// </summary>
    public sealed class WallBumpDetector
    {
        private readonly WallBumpLimits limits;
        private Vector2 anchor;
        private long anchorAt;
        private bool anchored;
        private bool armed = true;
        private Vector2 bumpPosition;
        private Vector2 bumpPush;
        private long lastRefusalAt;

        public WallBumpDetector(WallBumpLimits limits) => this.limits = limits;

        /// <param name="push">Left stick on screen (x right, y down), magnitude 0..1.</param>
        /// <param name="position">Player position in world units.</param>
        /// <param name="blockedAhead">The terrain blocks within reach along the push.</param>
        /// <returns>True when the thud should play now.</returns>
        public bool Update(long now, Vector2 push, Vector2 position, bool blockedAhead)
        {
            var pushing = push.Length() >= this.limits.MinPush;
            if (!pushing || !this.anchored || Vector2.Distance(position, this.anchor) >= this.limits.StallWorld)
            {
                this.anchor = position;
                this.anchorAt = now;
                this.anchored = pushing;
            }

            var refused = pushing && blockedAhead && this.anchored && now - this.anchorAt >= this.limits.StallMs;
            if (!this.armed && (Vector2.Distance(position, this.bumpPosition) >= this.limits.RearmWorld ||
                                (pushing && Angle(push, this.bumpPush) > this.limits.RearmTurnDegrees) ||
                                (!refused && now - this.lastRefusalAt >= this.limits.RearmIdleMs)))
            {
                this.armed = true;
            }

            if (!refused)
            {
                return false;
            }

            this.lastRefusalAt = now;
            if (!this.armed)
            {
                return false;
            }

            this.armed = false;
            this.bumpPosition = position;
            this.bumpPush = push;
            return true;
        }

        private static float Angle(Vector2 a, Vector2 b) =>
            MathF.Acos(Math.Clamp(Vector2.Dot(Vector2.Normalize(a), Vector2.Normalize(b)), -1f, 1f)) * 180f / MathF.PI;
    }
}
