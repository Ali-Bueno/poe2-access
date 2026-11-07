namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     How loud something nearby sounds by distance: full on it, falling to a far gain at the edge of its range, then
    ///     dying away to nothing past it, so walking off is a fade and not a step down to silence. Pure.
    /// </summary>
    public static class ProximityFade
    {
        /// <summary>How far past its range a sound dies away: about a second and a half of walking.</summary>
        public const float TailWorld = 600f;

        /// <summary>Squared, as a straight line in level is heard to hold on and then drop at the very end.</summary>
        public static float Gain(float distance, float range, float farGain)
        {
            if (distance <= range)
            {
                return farGain + ((1f - farGain) * (1f - (Math.Max(distance, 0f) / range)));
            }

            var left = 1f - Math.Min((distance - range) / TailWorld, 1f);
            return farGain * left * left;
        }
    }
}
