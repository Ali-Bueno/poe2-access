namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Where the game's left stick moves the passive tree's cursor: not along the links but to the node it scores best
    ///     within 45 degrees of the push, distance × (1 + 1.5 × tan of the angle off the push). Fitted to 127 recorded moves
    ///     at three zoom levels, 120 right (docs/research/passive-tree.md). Offsets are screen pixels, y down; directions
    ///     are octants counter-clockwise from right, as <see cref="Announcements.StickDirections"/>. Pure.
    /// </summary>
    public static class PassiveStick
    {
        private const double ConeDegrees = 45;

        private const double SidePenalty = 1.5;

        private const int Octants = 8;

        /// <summary>The candidate a push in the octant lands on, or -1 when none lies within its cone.</summary>
        public static int Pick(IReadOnlyList<Vector2> offsets, int octant)
        {
            var best = -1;
            var bestScore = double.MaxValue;
            for (var i = 0; i < offsets.Count; i++)
            {
                var distance = offsets[i].Length();
                var off = OffDegrees(offsets[i], octant);
                if (distance <= 0 || off > ConeDegrees)
                {
                    continue;
                }

                var score = distance * (1 + (SidePenalty * Math.Tan(off * Math.PI / 180)));
                if (score < bestScore)
                {
                    (best, bestScore) = (i, score);
                }
            }

            return best;
        }

        /// <summary>The octant whose push lands on the target, the one nearest its angle when several do; null when none does.</summary>
        public static int? OctantTo(IReadOnlyList<Vector2> offsets, int target)
        {
            int? found = null;
            for (var octant = 0; octant < Octants; octant++)
            {
                if (Pick(offsets, octant) == target && (found is not { } previous || OffDegrees(offsets[target], octant) < OffDegrees(offsets[target], previous)))
                {
                    found = octant;
                }
            }

            return found;
        }

        /// <summary>The octant nearest the offset's angle (screen y down).</summary>
        public static int Nearest(Vector2 offset)
        {
            var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
            return (int)(((Math.Round(angle / 45) % Octants) + Octants) % Octants);
        }

        /// <summary>
        ///     Pushes that lead from the origin to the target, each the one landing nearest it, at most <paramref name="maxMoves"/>;
        ///     null when a push lands nowhere nearer or the moves run out. Positions in any one frame's pixels.
        /// </summary>
        public static List<int>? Route(IReadOnlyList<Vector2> positions, int origin, int target, int maxMoves)
        {
            var route = new List<int>();
            var here = origin;
            while (here != target)
            {
                if (route.Count == maxMoves)
                {
                    return null;
                }

                var offsets = new Vector2[positions.Count];
                for (var i = 0; i < positions.Count; i++)
                {
                    offsets[i] = i == here ? Vector2.Zero : positions[i] - positions[here];
                }

                var (move, next) = (-1, -1);
                var left = Vector2.Distance(positions[here], positions[target]);
                for (var octant = 0; octant < Octants; octant++)
                {
                    var landed = Pick(offsets, octant);
                    if (landed >= 0 && Vector2.Distance(positions[landed], positions[target]) < left)
                    {
                        (move, next, left) = (octant, landed, Vector2.Distance(positions[landed], positions[target]));
                    }
                }

                if (move < 0)
                {
                    return null;
                }

                route.Add(move);
                here = next;
            }

            return route;
        }

        private static double OffDegrees(Vector2 offset, int octant)
        {
            var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
            return Math.Abs((((angle - (octant * 45) + 180) % 360) + 360) % 360 - 180);
        }
    }
}
