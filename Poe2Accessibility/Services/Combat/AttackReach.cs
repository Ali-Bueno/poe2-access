namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;

    /// <summary>A melee damage pattern's shape, numbered as the game's parser stores it (exe 0x1D47990).</summary>
    public enum PatternShape
    {
        Unknown = 0,
        Circle = 1,
        Cone = 2,
        Rectangle = 3,
    }

    /// <summary>
    ///     One damage pattern of a melee clip (an .ao's Melee <c>damage_patterns</c>, world units from the monster's centre):
    ///     its shape, length, width (rectangles), angle (cones, radians) and offset.
    /// </summary>
    public readonly record struct DamagePattern(PatternShape Shape, float Length, float Width, float Angle, Vector2 Offset);

    /// <summary>
    ///     Whether a blow can reach the player: a melee clip by its damage patterns, an area by its radius at the impact
    ///     point. Facing is left out (its convention is not measured), so a pattern counts all around the monster. Pure.
    /// </summary>
    public static class AttackReach
    {
        /// <summary>A pattern longer than this (world units, over two screens' height) is a misread one, not a blow.</summary>
        private const float MaxPatternWorld = 5000f;

        /// <summary>How far from the monster's centre a pattern can touch, whatever way the monster faces; null for a pattern not believed.</summary>
        public static float? Farthest(DamagePattern pattern)
        {
            if (!float.IsFinite(pattern.Length) || pattern.Length <= 0f || pattern.Length > MaxPatternWorld ||
                !float.IsFinite(pattern.Offset.X) || !float.IsFinite(pattern.Offset.Y))
            {
                return null;
            }

            var side = pattern.Shape == PatternShape.Rectangle && float.IsFinite(pattern.Width) && pattern.Width > 0f ? pattern.Width / 2f : 0f;
            return pattern.Offset.Length() + MathF.Sqrt((pattern.Length * pattern.Length) + (side * side));
        }

        /// <summary>The farthest any of a clip's patterns reaches; null when none is believed.</summary>
        public static float? Farthest(IEnumerable<DamagePattern> patterns)
        {
            float? farthest = null;
            foreach (var pattern in patterns)
            {
                if (Farthest(pattern) is { } reach && (farthest == null || reach > farthest))
                {
                    farthest = reach;
                }
            }

            return farthest;
        }

        /// <summary>Whether the player's body comes within a melee reach of the monster's centre, with slack for a step.</summary>
        public static bool MeleeReaches(float reach, Vector2 attacker, Vector2 player, float playerRadius, float slack) =>
            Vector2.Distance(attacker, player) - playerRadius <= reach + slack;

        /// <summary>Whether an area of <paramref name="radius"/> at <paramref name="impact"/> catches the player's body, with slack.</summary>
        public static bool AreaCatches(Vector2 impact, float radius, Vector2 player, float playerRadius, float slack) =>
            Vector2.Distance(impact, player) <= radius + playerRadius + slack;
    }
}
