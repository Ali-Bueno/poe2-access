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

        /// <summary>
        ///     The one place of a whole object that fits, for when nothing near where a field was measured does: every offset
        ///     of <paramref name="min"/>..<paramref name="limit"/> (exclusive) in step with <paramref name="expected"/>. Far from
        ///     where it was measured the nearest fit is no likelier than another, so two places that fit are not chosen between
        ///     (null), unless <paramref name="value"/> reads the same thing at each (two slots of one pointer): then the nearest.
        /// </summary>
        public static int? Only(int expected, int min, int limit, int step, Func<int, bool> fits, Func<int, long>? value = null)
        {
            int? found = null;
            var first = expected - ((expected - min) / step * step);
            for (var offset = first < min ? first + step : first; offset < limit; offset += step)
            {
                if (!fits(offset))
                {
                    continue;
                }

                if (found is not { } known)
                {
                    found = offset;
                }
                else if (value == null || value(known) != value(offset))
                {
                    return null;
                }
                else if (Math.Abs(offset - expected) < Math.Abs(known - expected))
                {
                    found = offset;
                }
            }

            return found;
        }

        /// <summary>
        ///     <see cref="First"/> near where the field was measured, then <see cref="Only"/> over the whole object with
        ///     <paramref name="wideFits"/>, the proof a far place needs (two facts that agree, never one).
        /// </summary>
        public static int? NearThenWide(int expected, int radius, int step, Func<int, bool> fits, int min, int limit, Func<int, bool> wideFits, Func<int, long>? value = null) =>
            First(expected, radius, step, fits, min) ?? Only(expected, min, limit, step, wideFits, value);
    }
}
