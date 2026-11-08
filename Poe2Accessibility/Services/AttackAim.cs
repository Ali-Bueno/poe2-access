namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>Where a monster's current action is aimed, for the dodge warning.</summary>
    public enum AimKind
    {
        /// <summary>The action holds no target the mod could read: judged as before, by reach alone.</summary>
        Unknown,

        /// <summary>At the player's body.</summary>
        Player,

        /// <summary>At the ground or another body close enough for its area or path to catch the player.</summary>
        PlayerArea,

        /// <summary>At a minion, an ally, a totem or ground away from the player.</summary>
        Elsewhere,
    }

    /// <summary>
    ///     What the current action of a monster aims at (Actor +0x238: target entity +0x148, target location +0x150), in
    ///     world units on the ground plane. Pure (docs/research/dodge-timing.md).
    /// </summary>
    /// <param name="TargetId">The target entity's id, 0 for none.</param>
    /// <param name="TargetPoint">The target location in world units, null for none.</param>
    public readonly record struct ActionAim(uint TargetId, Vector2? TargetPoint);

    /// <summary>Tells an attack on the player from one aimed elsewhere. Pure.</summary>
    public static class AttackAim
    {
        /// <summary>
        ///     Slack around the player's body for an area or a path (world units): a tenth of a second of walking (about 400
        ///     units a second, the radar's measure), as the player moves while the action's aim was taken.
        /// </summary>
        public const float MarginWorld = 40f;

        /// <param name="aim">The action's target, as read.</param>
        /// <param name="playerId">The followed player's entity id.</param>
        /// <param name="player">The player's place, world units.</param>
        /// <param name="playerRadius">The player's body radius (Render bounds), world units.</param>
        /// <param name="attacker">The monster's place, world units.</param>
        /// <param name="areaRadius">The area the blow covers at its target (world units), 0 when unknown or none.</param>
        /// <param name="projectile">The attack flies from the monster through its target place (a projectile's width is in no file the mod reads: the slack around the body covers it).</param>
        public static AimKind Of(ActionAim aim, uint playerId, Vector2 player, float playerRadius, Vector2 attacker, float areaRadius, bool projectile)
        {
            if (aim.TargetId != 0 && aim.TargetId == playerId)
            {
                return AimKind.Player;
            }

            // Another body, or a place: the player is caught only by the area there or the path toward it.
            var point = aim.TargetPoint;
            if (point is not { } at)
            {
                return aim.TargetId != 0 ? AimKind.Elsewhere : AimKind.Unknown;
            }

            var reach = playerRadius + MarginWorld;
            if (Vector2.Distance(at, player) <= areaRadius + reach)
            {
                return AimKind.PlayerArea;
            }

            return projectile && OnPath(attacker, at, player, reach) ? AimKind.PlayerArea : AimKind.Elsewhere;
        }

        /// <summary>Whether a point lies within <paramref name="halfWidth"/> of the ray from <paramref name="from"/> through <paramref name="toward"/>, ahead of its start.</summary>
        public static bool OnPath(Vector2 from, Vector2 toward, Vector2 point, float halfWidth)
        {
            var direction = toward - from;
            var length = direction.Length();
            if (!(length > 0f))
            {
                return Vector2.Distance(from, point) <= halfWidth;
            }

            direction /= length;
            var along = Vector2.Dot(point - from, direction);
            if (along < -halfWidth)
            {
                return false;
            }

            var across = MathF.Abs((direction.X * (point.Y - from.Y)) - (direction.Y * (point.X - from.X)));
            return across <= halfWidth;
        }
    }
}
