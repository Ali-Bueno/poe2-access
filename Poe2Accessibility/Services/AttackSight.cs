namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>
    ///     What a sighted player sees of an attack on the 16:9 screen <see cref="CameraView"/> judges: the attacker's body,
    ///     or, from an attacker off screen, the moment its projectile comes into view. Places are drawn offsets in screen
    ///     heights from the screen's centre (<c>ScreenProjection.Draw</c>). Pure.
    /// </summary>
    public static class AttackSight
    {
        /// <summary>Halvings of the path when looking for where it enters the screen: under a thousandth of the way.</summary>
        private const int EntrySteps = 10;

        /// <summary>Whether any of the body shows: its centre, or its edge left, right, above, below or toward the screen's centre.</summary>
        public static bool BodySeen(Vector2 centre, float radius)
        {
            if (CameraView.InView(centre))
            {
                return true;
            }

            if (!(radius > 0f) || !float.IsFinite(radius))
            {
                return false;
            }

            var inward = centre.LengthSquared() > 0f ? -Vector2.Normalize(centre) * radius : Vector2.Zero;
            return CameraView.InView(centre + inward) || CameraView.InView(centre + new Vector2(radius, 0f)) ||
                   CameraView.InView(centre - new Vector2(radius, 0f)) || CameraView.InView(centre + new Vector2(0f, radius)) ||
                   CameraView.InView(centre - new Vector2(0f, radius));
        }

        /// <summary>
        ///     How far along the way from <paramref name="from"/> to <paramref name="to"/> (0 to 1) something flying it comes
        ///     into view: 0 when it starts in view, null when the way never shows (the player's end is on screen while the
        ///     camera follows the player, so null means a bad reading).
        /// </summary>
        public static float? EntryFraction(Vector2 from, Vector2 to)
        {
            if (CameraView.InView(from))
            {
                return 0f;
            }

            if (!CameraView.InView(to))
            {
                return null;
            }

            // The screen is a rectangle, so the way leaves the outside once: halve toward that crossing.
            float outside = 0f, inside = 1f;
            for (var step = 0; step < EntrySteps; step++)
            {
                var middle = (outside + inside) / 2f;
                if (CameraView.InView(Vector2.Lerp(from, to, middle)))
                {
                    inside = middle;
                }
                else
                {
                    outside = middle;
                }
            }

            return inside;
        }

        /// <summary>When a projectile released at <paramref name="releaseAt"/> that lands <paramref name="flightMs"/> later comes into view.</summary>
        public static long SeenAt(long releaseAt, int flightMs, float entryFraction) =>
            releaseAt + (long)Math.Round(Math.Clamp(entryFraction, 0f, 1f) * Math.Max(0, flightMs));
    }
}
