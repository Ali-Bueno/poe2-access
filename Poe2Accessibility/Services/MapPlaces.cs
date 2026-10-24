namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Pure geometry for naming the markers of the checkpoint travel map: which known checkpoint each marker stands
    ///     for, and what named place lies beside a point.
    /// </summary>
    public static class MapPlaces
    {
        /// <summary>
        ///     Worst fit accepted (residual over the markers' spread): 0.15 lets a lone marker sit 23° off its place's
        ///     direction; the map is drawn as the camera draws the world, only smaller.
        /// </summary>
        private const float MaxResidual = 0.15f;

        /// <summary>Above this many pairings the markers are matched one by one by direction.</summary>
        private const int MaxPairings = 50000;

        /// <summary>
        ///     For each marker (screen offset from the player's own marker), the index of the place (screen projection of
        ///     its world offset from the player's checkpoint) it stands for, or -1: the pairing whose single scale fits
        ///     best, none when even that fits badly.
        /// </summary>
        public static int[] Assign(IReadOnlyList<Vector2> markers, IReadOnlyList<Vector2> places)
        {
            var result = Enumerable.Repeat(-1, markers.Count).ToArray();
            if (markers.Count == 0 || places.Count == 0 || Pairings(Math.Max(markers.Count, places.Count), Math.Min(markers.Count, places.Count)) > MaxPairings)
            {
                return result;
            }

            var best = float.MaxValue;
            int[]? bestPairs = null;
            var pairs = new int[markers.Count];
            var used = new bool[places.Count];
            void Search(int marker, int matched)
            {
                if (marker == markers.Count)
                {
                    if (matched == Math.Min(markers.Count, places.Count) && Residual(markers, places, pairs) is var residual && residual < best)
                    {
                        (best, bestPairs) = (residual, (int[])pairs.Clone());
                    }

                    return;
                }

                if (markers.Count - marker > places.Count - matched)
                {
                    pairs[marker] = -1; // more markers than known places: some stay unnamed
                    Search(marker + 1, matched);
                }

                for (var place = 0; place < places.Count; place++)
                {
                    if (!used[place])
                    {
                        (used[place], pairs[marker]) = (true, place);
                        Search(marker + 1, matched + 1);
                        used[place] = false;
                    }
                }
            }

            Search(0, 0);
            return best <= MaxResidual && bestPairs != null ? bestPairs : result;
        }

        /// <summary>The named entry nearest <paramref name="point"/> within <paramref name="radius"/> (world units), or null.</summary>
        public static ScanEntry? NamedNear(Vector2 point, IEnumerable<ScanEntry> entries, float radius) =>
            entries.Where(entry => entry.Category is ScanCategory.Exits or ScanCategory.Waypoints or ScanCategory.Npcs or ScanCategory.Objects &&
                                   entry.Kind is not ("checkpoint" or "barrier"))
                .Select(entry => (Entry: entry, Distance: Vector2.Distance(point, new Vector2(entry.WorldX, entry.WorldY))))
                .Where(near => near.Distance <= radius)
                .OrderBy(near => near.Distance)
                .Select(near => near.Entry)
                .FirstOrDefault();

        /// <summary>How well one scale maps the paired places onto their markers; infinite for a mirrored or empty fit.</summary>
        private static float Residual(IReadOnlyList<Vector2> markers, IReadOnlyList<Vector2> places, int[] pairs)
        {
            float dot = 0, norm = 0, spread = 0;
            for (var i = 0; i < pairs.Length; i++)
            {
                if (pairs[i] >= 0)
                {
                    dot += Vector2.Dot(markers[i], places[pairs[i]]);
                    norm += places[pairs[i]].LengthSquared();
                    spread += markers[i].LengthSquared();
                }
            }

            if (norm <= 0 || spread <= 0 || dot <= 0)
            {
                return float.MaxValue;
            }

            var scale = dot / norm;
            var error = 0f;
            for (var i = 0; i < pairs.Length; i++)
            {
                if (pairs[i] >= 0)
                {
                    error += (markers[i] - (scale * places[pairs[i]])).LengthSquared();
                }
            }

            return error / spread;
        }

        private static double Pairings(int of, int taken)
        {
            var count = 1d;
            for (var i = 0; i < taken; i++)
            {
                count *= of - i;
            }

            return count;
        }
    }
}
