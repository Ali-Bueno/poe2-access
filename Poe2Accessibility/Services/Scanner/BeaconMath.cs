namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Geometry and timing of the guide beacon (D2R beacon.md): where along the route the ping points, how far is
    ///     left and how often it sounds. Pure: grid cells in, numbers out.
    /// </summary>
    public static class BeaconMath
    {
        /// <summary>D2R: 160 ms when close, 1200 ms when far, linear in the route left.</summary>
        public const int NearIntervalMs = 160;
        public const int FarIntervalMs = 1200;

        /// <summary>Route left (world units) at which the ping is fastest / slowest: one step, thirty steps.</summary>
        public const float NearWorld = 100f;
        public const float FarWorld = 3000f;

        /// <summary>Index of the route cell nearest to <paramref name="player"/> (grid units).</summary>
        public static int NearestIndex(IReadOnlyList<Vector2> route, Vector2 player)
        {
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < route.Count; i++)
            {
                var distance = Vector2.DistanceSquared(route[i], player);
                if (distance < bestDistance)
                {
                    (best, bestDistance) = (i, distance);
                }
            }

            return best;
        }

        /// <summary>Grid length still to walk: to the nearest route cell, then along the route to its end.</summary>
        public static float Remaining(IReadOnlyList<Vector2> route, int from, Vector2 player)
        {
            if (route.Count == 0)
            {
                return 0f;
            }

            var length = Vector2.Distance(player, route[from]);
            for (var i = from + 1; i < route.Count; i++)
            {
                length += Vector2.Distance(route[i - 1], route[i]);
            }

            return length;
        }

        /// <summary>
        ///     The point the ping points at: the farthest route cell within <paramref name="lookAhead"/> grid units of
        ///     route that the player can walk to in a straight line, so the ping turns where the corridor turns and
        ///     never points into a wall; the next cell when nothing further is in clear view.
        /// </summary>
        public static Vector2 AimPoint(byte[] grid, int bytesPerRow, IReadOnlyList<Vector2> route, int from, Vector2 player, float lookAhead)
        {
            var aim = route[Math.Min(from + 1, route.Count - 1)];
            var travelled = 0f;
            for (var i = from + 1; i < route.Count; i++)
            {
                travelled += Vector2.Distance(route[i - 1], route[i]);
                if (travelled > lookAhead)
                {
                    break;
                }

                if (LineWalkable(grid, bytesPerRow, player, route[i]))
                {
                    aim = route[i];
                }
            }

            return aim;
        }

        /// <summary>Every grid cell on the straight segment is walkable (sampled at half-cell steps).</summary>
        public static bool LineWalkable(byte[] grid, int bytesPerRow, Vector2 a, Vector2 b)
        {
            var samples = (int)MathF.Ceiling(Vector2.Distance(a, b) * 2f);
            for (var s = 0; s <= samples; s++)
            {
                var point = samples == 0 ? a : Vector2.Lerp(a, b, (float)s / samples);
                if (!RoutePlanner.IsWalkable(grid, bytesPerRow, (int)MathF.Round(point.X), (int)MathF.Round(point.Y)))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Time between pings for the route left, in world units.</summary>
        public static int IntervalMs(float remainingWorld)
        {
            var t = Math.Clamp((remainingWorld - NearWorld) / (FarWorld - NearWorld), 0f, 1f);
            return (int)MathF.Round(NearIntervalMs + (t * (FarIntervalMs - NearIntervalMs)));
        }

        /// <summary>
        ///     Stereo and pitch of a cue for a screen direction (x right, y down): left/right by pan, up higher and down
        ///     lower, since stereo alone cannot tell up from down (about four semitones each way: the D2R steps, 1.12 and
        ///     0.9, were too subtle to hear on loot, user 2026-10-24).
        /// </summary>
        private const float UpPitch = 0.26f;

        private const float DownPitch = 0.2f;

        public static (float Pan, float Pitch) Voice(Vector2 screenDirection)
        {
            if (screenDirection.LengthSquared() < float.Epsilon)
            {
                return (0f, 1f);
            }

            var unit = Vector2.Normalize(screenDirection);
            var up = -unit.Y;
            return (unit.X, up >= 0 ? 1f + (UpPitch * up) : 1f + (DownPitch * up));
        }
    }
}
