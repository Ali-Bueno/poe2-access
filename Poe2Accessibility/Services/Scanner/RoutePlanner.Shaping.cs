namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>Shaping a found route in <see cref="RoutePlanner"/>: keeping the earlier way, its length, its corners and the legs to walk.</summary>
    public static partial class RoutePlanner
    {
        /// <summary><see cref="Keep"/>: the player within this many cells of the earlier route is still on it.</summary>
        private const float KeepNearCells = 6f;

        /// <summary><see cref="Keep"/>: two routes ending this close (cells) lead to the same place.</summary>
        private const float KeepEndCells = 3f;

        /// <summary><see cref="Keep"/>: the earlier way is kept unless the new one is shorter by this share of it...</summary>
        private const float KeepSlack = 0.1f;

        /// <summary>...plus this many cells (a step or two of walking: noise near the start).</summary>
        private const float KeepCells = 8f;

        /// <summary>
        ///     The way to follow when a route is searched again on the way to the same goal: the earlier route from the player's
        ///     place on it, unless the new one is clearly shorter. Of two ways of nearly equal length round a block the shorter
        ///     swaps as the player walks, and each fresh search sent them back toward the other side: Clearfell, 2026-11-08,
        ///     the guide's ping turned the player round four times in a corridor between two log piles (318 and 347 cells round
        ///     each side). The earlier way is kept while the player stands within <see cref="KeepNearCells"/> of it, it is still
        ///     walkable and ends where the new one does; a way that opens (a gate) and is shorter by more than
        ///     <see cref="KeepSlack"/> plus <see cref="KeepCells"/> replaces it.
        /// </summary>
        public static List<Vector2> Keep(IReadOnlyList<Vector2>? previous, List<Vector2> fresh, Vector2 player, byte[] grid, int bytesPerRow)
        {
            if (previous is not { Count: >= 2 } || fresh.Count < 2 || Vector2.Distance(previous[^1], fresh[^1]) > KeepEndCells)
            {
                return fresh;
            }

            var nearest = 0;
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < previous.Count; i++)
            {
                var distance = Vector2.DistanceSquared(previous[i], player);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }
            }

            nearestDistance = MathF.Sqrt(nearestDistance);
            if (nearestDistance > KeepNearCells)
            {
                return fresh; // the player left that way: the new search is where they are
            }

            var rest = new List<Vector2>(previous.Count - nearest);
            for (var i = nearest; i < previous.Count; i++)
            {
                if (!IsWalkable(grid, bytesPerRow, (int)MathF.Round(previous[i].X), (int)MathF.Round(previous[i].Y)))
                {
                    return fresh; // closed since (a barrier): only the new way is real
                }

                rest.Add(previous[i]);
            }

            var kept = nearestDistance + Length(rest);
            return kept <= (Length(fresh) * (1f + KeepSlack)) + KeepCells ? rest : fresh;
        }

        /// <summary>Length of a route in grid units.</summary>
        public static float Length(IReadOnlyList<Vector2> route)
        {
            var length = 0f;
            for (var i = 1; i < route.Count; i++)
            {
                length += Vector2.Distance(route[i - 1], route[i]);
            }

            return length;
        }

        /// <summary>
        ///     The route's real turns (Ramer–Douglas–Peucker): the staircase an 8-neighbour grid draws for a slanted
        ///     line stays within <paramref name="tolerance"/> grid units of it and falls away.
        /// </summary>
        public static List<Vector2> Corners(IReadOnlyList<Vector2> route, float tolerance)
        {
            if (route.Count < 3)
            {
                return new List<Vector2>(route);
            }

            var keep = new bool[route.Count];
            keep[0] = keep[^1] = true;
            var spans = new Stack<(int First, int Last)>();
            spans.Push((0, route.Count - 1));
            while (spans.Count > 0)
            {
                var (first, last) = spans.Pop();
                var farthest = -1;
                var worst = tolerance;
                for (var i = first + 1; i < last; i++)
                {
                    var off = DistanceToSegment(route[i], route[first], route[last]);
                    if (off > worst)
                    {
                        worst = off;
                        farthest = i;
                    }
                }

                if (farthest >= 0)
                {
                    keep[farthest] = true;
                    spans.Push((first, farthest));
                    spans.Push((farthest, last));
                }
            }

            var corners = new List<Vector2>();
            for (var i = 0; i < route.Count; i++)
            {
                if (keep[i])
                {
                    corners.Add(route[i]);
                }
            }

            return corners;
        }

        /// <summary>
        ///     The route as legs to walk: one stick direction each (<paramref name="directionOf"/> bins a grid
        ///     vector) and its length in grid units. Legs shorter than <paramref name="minLeg"/> (a sidestep round a
        ///     rock; the game slides the player along walls anyway) are dropped, then neighbours that point the same
        ///     way are one leg.
        /// </summary>
        public static List<(int Direction, float Length)> Legs(IReadOnlyList<Vector2> corners, Func<Vector2, int> directionOf, float minLeg)
        {
            var segments = new List<(int Direction, float Length)>();
            for (var i = 1; i < corners.Count; i++)
            {
                var delta = corners[i] - corners[i - 1];
                if (delta.Length() > 0f)
                {
                    segments.Add((directionOf(delta), delta.Length()));
                }
            }

            var kept = segments.FindAll(segment => segment.Length >= minLeg);
            if (kept.Count == 0 && segments.Count > 0)
            {
                kept.Add(segments.MaxBy(segment => segment.Length)); // a short route: its main way
            }

            var legs = new List<(int Direction, float Length)>();
            foreach (var segment in kept)
            {
                if (legs.Count > 0 && legs[^1].Direction == segment.Direction)
                {
                    legs[^1] = (segment.Direction, legs[^1].Length + segment.Length);
                }
                else
                {
                    legs.Add(segment);
                }
            }

            return legs;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var along = to - from;
            var lengthSquared = along.LengthSquared();
            if (lengthSquared == 0f)
            {
                return Vector2.Distance(point, from);
            }

            var t = Math.Clamp(Vector2.Dot(point - from, along) / lengthSquared, 0f, 1f);
            return Vector2.Distance(point, from + (along * t));
        }
    }
}
