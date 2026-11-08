namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>
    ///     Where the game's left stick moves the passive tree's cursor, as its own code does (stick step at RVA 0x9B9BA0,
    ///     build 26476662; docs/research/passive-tree.md): a linked node within 20 degrees of the push first, in edge order,
    ///     else the node of lowest (5 − 4 cos) × distance within 45 degrees, both among the nodes drawn inside the window.
    ///     Offsets are the tree's own units, y down; directions are octants counter-clockwise from right, as
    ///     <see cref="Announcements.StickDirections"/>. Pure.
    /// </summary>
    public static class PassiveStick
    {
        /// <summary>A linked node counts when the push is within acos 0.94 = 19.95 degrees of it (0x9B9E0F, kept when not below).</summary>
        private const float LinkedCosine = 0.94f;

        /// <summary>Linked score (1.5 − 8.333 (cos − 0.94)) × distance (0x9B9F01); 1 × distance straight ahead, 1.5 × at the edge.</summary>
        private const float LinkedBase = 1.5f;

        private const float LinkedSlope = 8.333333f;

        /// <summary>Any node whose cosine is above 0.7071 (0x3F350481 at 0x9CA139, acos 45.0005 degrees) is in the cone: one at exactly 45 degrees counts.</summary>
        private const float ConeCosine = 0.7071f;

        /// <summary>Cone score (5 − 4 cos) × distance (0x9CA080): 1 × distance straight ahead, 2.17 × at the edge.</summary>
        private const float ConeBase = 5f;

        private const float ConeSlope = 4f;

        private const int Octants = 8;

        /// <summary>The model fitted to 127 moves before the code was read (2026-10-31): distance × (1 + 1.5 tan off) within 45 degrees.</summary>
        private const double FittedConeDegrees = 45;

        private const double FittedSidePenalty = 1.5;

        /// <summary>
        ///     What the game sees: the cursor's node sits at <paramref name="Cursor"/> (window pixels), the window's client
        ///     area is <paramref name="Size"/>, and a tree unit is <paramref name="PixelsPerUnit"/> pixels. The game only lands
        ///     on nodes whose centre is inside the client rect (0x9BA8D0 against the globals at RVA 0x45A4C00).
        /// </summary>
        public readonly record struct Window(Vector2 Cursor, Vector2 Size, float PixelsPerUnit)
        {
            /// <summary>Whether a node this far from the cursor's node has its centre inside the window (0 ≤ x &lt; width, 0 ≤ y &lt; height).</summary>
            public bool Shows(Vector2 offset)
            {
                var at = this.Cursor + (offset * this.PixelsPerUnit);
                return at.X >= 0 && at.X < this.Size.X && at.Y >= 0 && at.Y < this.Size.Y;
            }
        }

        /// <summary>
        ///     Where the cursor's node sits once the view has settled: the game pans the canvas until the hovered node is at the
        ///     centre of the canvas's frame (1279.8,781.2 in a 2560×1440 client whose frame is 0,122 2560×1318, 2026-11-08).
        ///     Right after a push the view is still panning (the node drawn at 1307,699 a frame later), and a window placed
        ///     there let far nodes in: the finder said 3 pushes where the next push landed 7 away.
        /// </summary>
        public static Vector2 SettledCursor(Vector2 framePosition, Vector2 frameSize) => framePosition + (frameSize / 2);

        /// <summary>The unit vector of a push in the octant, y down as the game flips the stick's y (0x9B9BA0 entry).</summary>
        public static Vector2 Direction(int octant)
        {
            var angle = octant * MathF.PI / (Octants / 2);
            return new Vector2(MathF.Cos(angle), -MathF.Sin(angle));
        }

        /// <summary>
        ///     The candidate a push in the octant lands on, or -1 when the game would not move. <paramref name="linked"/> are
        ///     the candidates linked to the cursor's node in the game's edge order (empty when it skips the linked stage);
        ///     without a <paramref name="window"/> the fitted model stands in.
        /// </summary>
        public static int Pick(IReadOnlyList<Vector2> offsets, int octant, IReadOnlyList<int> linked, Window? window)
        {
            if (window is not { } seen)
            {
                return PickFitted(offsets, octant);
            }

            var direction = Direction(octant);
            var pick = -1;
            var best = float.MaxValue;
            foreach (var index in linked)
            {
                if (Aim(offsets, index, direction, seen) is not var (distance, cosine) || !(cosine >= LinkedCosine))
                {
                    continue;
                }

                var score = (LinkedBase - ((cosine - LinkedCosine) * LinkedSlope)) * distance;
                if (score < best)
                {
                    // The game keeps the cosine, not the score (0x9B9F26): no later link can beat the first one in the cone.
                    (pick, best) = (index, cosine);
                }
            }

            if (pick >= 0)
            {
                return pick;
            }

            for (var index = 0; index < offsets.Count; index++)
            {
                if (Aim(offsets, index, direction, seen) is not var (distance, cosine) || !(cosine > ConeCosine))
                {
                    continue;
                }

                var score = (ConeBase - (ConeSlope * cosine)) * distance;
                if (score < best)
                {
                    (pick, best) = (index, score);
                }
            }

            return pick;
        }

        /// <summary>The fitted model: the lowest distance × (1 + 1.5 tan off) within 45 degrees, -1 when none lies in the cone.</summary>
        public static int PickFitted(IReadOnlyList<Vector2> offsets, int octant)
        {
            var best = -1;
            var bestScore = double.MaxValue;
            for (var i = 0; i < offsets.Count; i++)
            {
                var distance = offsets[i].Length();
                var off = OffDegrees(offsets[i], octant);
                if (distance <= 0 || off > FittedConeDegrees)
                {
                    continue;
                }

                var score = distance * (1 + (FittedSidePenalty * Math.Tan(off * Math.PI / 180)));
                if (score < bestScore)
                {
                    (best, bestScore) = (i, score);
                }
            }

            return best;
        }

        /// <summary>The octant whose push lands on the target, the one nearest its angle when several do; null when none does.</summary>
        public static int? OctantTo(IReadOnlyList<Vector2> offsets, int target, IReadOnlyList<int> linked, Window? window)
        {
            int? found = null;
            for (var octant = 0; octant < Octants; octant++)
            {
                if (Pick(offsets, octant, linked, window) == target && (found is not { } previous || OffDegrees(offsets[target], octant) < OffDegrees(offsets[target], previous)))
                {
                    found = octant;
                }
            }

            return found;
        }

        /// <summary>The octant nearest the offset's angle (y down).</summary>
        public static int Nearest(Vector2 offset)
        {
            var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
            return (int)(((Math.Round(angle / 45) % Octants) + Octants) % Octants);
        }

        /// <summary>
        ///     Pushes that lead from the origin to the target, each the one landing nearest it, at most <paramref name="maxMoves"/>;
        ///     null when a push lands nowhere nearer or the moves run out. <paramref name="linksOf"/> gives each node's linked
        ///     nodes in edge order; the window stays the same, the game centring each node the cursor lands on.
        /// </summary>
        public static List<int>? Route(IReadOnlyList<Vector2> positions, int origin, int target, int maxMoves, Func<int, IReadOnlyList<int>> linksOf, Window? window)
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
                var linked = linksOf(here);
                for (var octant = 0; octant < Octants; octant++)
                {
                    var landed = Pick(offsets, octant, linked, window);
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

        /// <summary>A candidate's distance and the cosine of its angle off the push, null when the game skips it: at the cursor or outside the window.</summary>
        private static (float Distance, float Cosine)? Aim(IReadOnlyList<Vector2> offsets, int index, Vector2 direction, Window window)
        {
            var offset = offsets[index];
            var distance = offset.Length();
            if (distance <= 0 || !window.Shows(offset))
            {
                return null;
            }

            return (distance, Vector2.Dot(offset / distance, direction));
        }

        private static double OffDegrees(Vector2 offset, int octant)
        {
            var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
            return Math.Abs((((angle - (octant * 45) + 180) % 360) + 360) % 360 - 180);
        }
    }
}
