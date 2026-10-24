namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     Where to look for a field a game update may have moved: where it was measured first, then outward, one step
    ///     either side at a time (classes grow or shrink by a few fields, so the nearest fit is the likeliest). Pure.
    /// </summary>
    public static class OffsetSearch
    {
        /// <summary><paramref name="expected"/>, then expected ± step, ± 2·step… up to <paramref name="radius"/>, never below <paramref name="min"/>.</summary>
        public static IEnumerable<int> Outward(int expected, int radius, int step, int min = 0)
        {
            if (expected >= min)
            {
                yield return expected;
            }

            for (var distance = step; distance <= radius; distance += step)
            {
                yield return expected + distance;
                if (expected - distance >= min)
                {
                    yield return expected - distance;
                }
            }
        }

        /// <summary>The first offset, in <see cref="Outward"/> order, that <paramref name="fits"/>; null when none does.</summary>
        public static int? First(int expected, int radius, int step, Func<int, bool> fits, int min = 0)
        {
            foreach (var offset in Outward(expected, radius, step, min))
            {
                if (fits(offset))
                {
                    return offset;
                }
            }

            return null;
        }
    }
}
