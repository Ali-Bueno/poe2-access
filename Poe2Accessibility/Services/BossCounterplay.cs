namespace Poe2Accessibility.Services
{
    using System;
    using System.Numerics;

    /// <summary>What to do inside a boss's danger area: leave it (any way, or one way), go to its safe centre, get behind the boss, or keep moving off the markers.</summary>
    public enum CounterplayKind
    {
        /// <summary>Out of a circle centred on the player: any way is as short.</summary>
        Out,

        /// <summary>Into a donut's hole, toward its centre.</summary>
        Centre,

        /// <summary>Behind the boss: out of a cone of half a turn or more.</summary>
        Behind,

        /// <summary>Off the ground markers a trigger scatters around the player.</summary>
        Move,

        /// <summary>One way out (<see cref="Counterplay.Way"/>): off a line's side or end, a cone's edge, an off-centre circle's rim.</summary>
        Step,
    }

    /// <summary>The answer to one area, and for <see cref="CounterplayKind.Step"/> the world direction to take.</summary>
    public readonly record struct Counterplay(CounterplayKind Kind, Vector2 Way);

    /// <summary>
    ///     The shortest way out of a skill's danger area (<see cref="AttackArea"/>, oriented from the monster toward its target
    ///     as <see cref="AttackAreas.Catches"/> is) for a player standing in it; null when the player is outside it or in a
    ///     donut's hole. Pure (docs/research/boss-mechanics.md, "Counterplay").
    /// </summary>
    public static class BossCounterplay
    {
        /// <summary>A player this near a circle's centre (share of its radius) can leave any way: no direction is said.</summary>
        public const float CentredShare = 1f / 3f;

        /// <summary>A cone this wide (degrees) or wider is escaped behind its caster.</summary>
        public const float BehindAngleDegrees = 180f;

        /// <summary>A cone this wide is a circle around its caster.</summary>
        private const float FullTurnDegrees = 360f;

        public static Counterplay? Of(AttackArea area, Vector2 monster, Vector2 target, Vector2 player, float playerRadius, float slack)
        {
            var body = playerRadius + slack;
            if (area.Count > 1)
            {
                // Markers scatter within the trigger's spread around its target (telegraph-skills "range" for a trigger).
                return Vector2.Distance(player, target) <= area.Range + area.Length + body ? new Counterplay(CounterplayKind.Move, Vector2.Zero) : null;
            }

            if (!AttackAreas.Catches(area, monster, target, player, playerRadius, slack))
            {
                return null;
            }

            var toward = target - monster;
            var direction = toward.LengthSquared() > 0f ? Vector2.Normalize(toward) : Vector2.UnitX;
            switch (area.Shape)
            {
                case AttackAreas.Donut:
                    var hub = Centre(area, monster, target, direction);
                    var fromHub = Vector2.Distance(player, hub);
                    if (area.Width > 0f && fromHub + playerRadius < area.Width)
                    {
                        return null; // in the hole: safe
                    }

                    return area.Width > 0f && fromHub - area.Width <= area.Length - fromHub
                        ? new Counterplay(CounterplayKind.Centre, Away(player, hub, -1f))
                        : Leave(player, hub, area.Length);
                case AttackAreas.Rectangle:
                    return Sidestep(area, monster, direction, player, body);
                case AttackAreas.Cone or AttackAreas.Triangle:
                    return area.AngleDegrees >= FullTurnDegrees ? Leave(player, monster, area.Length) : OutOfCone(area, monster, direction, player, body);
                default:
                    return Leave(player, Centre(area, monster, target, direction), area.Length);
            }
        }

        /// <summary>A circle's or donut's centre: its target, at most its range from the monster (as <see cref="AttackAreas.Catches"/>).</summary>
        private static Vector2 Centre(AttackArea area, Vector2 monster, Vector2 target, Vector2 direction) =>
            area.Range > 0f && Vector2.Distance(monster, target) > area.Range ? monster + (direction * area.Range) : target;

        /// <summary>Out of a circle: any way from near its centre, else straight away from it.</summary>
        private static Counterplay Leave(Vector2 player, Vector2 centre, float radius) =>
            Vector2.Distance(player, centre) <= radius * CentredShare
                ? new Counterplay(CounterplayKind.Out, Vector2.Zero)
                : new Counterplay(CounterplayKind.Step, Away(player, centre, 1f));

        /// <summary>A line: off the nearer long side, or off its far end when that is nearer.</summary>
        private static Counterplay Sidestep(AttackArea area, Vector2 monster, Vector2 direction, Vector2 player, float body)
        {
            var offset = player - monster;
            var along = Vector2.Dot(offset, direction);
            var across = Cross(direction, offset);
            var side = (area.Width / 2f) + body - MathF.Abs(across);
            var end = area.Length + body - along;
            if (end < side)
            {
                return new Counterplay(CounterplayKind.Step, direction);
            }

            var left = new Vector2(-direction.Y, direction.X);
            return new Counterplay(CounterplayKind.Step, across >= 0f ? left : -left);
        }

        /// <summary>A cone: behind its caster when it covers half a turn and that is nearer, else across the nearer edge or out past its reach.</summary>
        private static Counterplay OutOfCone(AttackArea area, Vector2 monster, Vector2 direction, Vector2 player, float body)
        {
            var offset = player - monster;
            var reach = offset.Length();
            var outward = area.Length + body - reach;
            var away = reach > 0f ? offset / reach : direction;
            if (area.AngleDegrees >= BehindAngleDegrees)
            {
                var behind = Vector2.Dot(offset, direction) + body;
                return behind < outward ? new Counterplay(CounterplayKind.Behind, -direction) : new Counterplay(CounterplayKind.Step, away);
            }

            var sign = Cross(direction, offset) >= 0f ? 1f : -1f;
            var half = area.AngleDegrees / 2f;
            var off = MathF.Acos(Math.Clamp(Vector2.Dot(away, direction), -1f, 1f)) * 180f / MathF.PI;
            var sideways = (reach * MathF.Sin(MathF.Max(0f, half - off) * MathF.PI / 180f)) + body;
            if (outward <= sideways)
            {
                return new Counterplay(CounterplayKind.Step, away);
            }

            // Square off the nearer edge: the edge's own direction turned a quarter outward.
            var edge = Rotate(direction, sign * half);
            return new Counterplay(CounterplayKind.Step, Rotate(edge, sign * 90f));
        }

        private static Vector2 Away(Vector2 player, Vector2 centre, float sign)
        {
            var from = player - centre;
            return from.LengthSquared() > 0f ? Vector2.Normalize(from) * sign : Vector2.UnitX;
        }

        private static float Cross(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            var radians = degrees * MathF.PI / 180f;
            var (sin, cos) = MathF.SinCos(radians);
            return new Vector2((v.X * cos) - (v.Y * sin), (v.X * sin) + (v.Y * cos));
        }
    }
}
