namespace Poe2Accessibility.Services
{
    using System;
    using System.Globalization;
    using System.Numerics;

    /// <summary>
    ///     The danger area a monster skill's table describes (<c>telegraph-skills.txt</c>: GeometryAttack and GeometryTrigger
    ///     rows, docs/research/attack-telegraphs.md): its shape, length or radius, width, angle in degrees, range and delay. Sizes
    ///     are taken as world units, as the study's cross-check says; the live protocol measures them.
    /// </summary>
    public readonly record struct AttackArea(string Shape, float Length, float Width, float AngleDegrees, float Range, int DelayMs);

    /// <summary>Reads an area from its catalog line and tells whether it catches the player. Pure.</summary>
    public static class AttackAreas
    {
        public const string Circle = "circle";
        public const string Cone = "cone";
        public const string Rectangle = "rectangle";
        public const string Donut = "donut";
        public const string Triangle = "triangle";

        private const char Pair = ';';
        private const char KeyValue = '=';

        /// <summary>
        ///     The area of a <c>geometry</c> line ("shape=cone;length=10;angle=100;delay=33;range=85") or a <c>trigger</c> line
        ///     ("count=1;range=15;radius=10;angle=360": a circle); null for other kinds or an area with no size.
        /// </summary>
        public static AttackArea? Parse(string kind, string area)
        {
            string? shape = null;
            float length = 0f, width = 0f, angle = 0f, range = 0f;
            var delay = 0;
            foreach (var part in area.Split(Pair, StringSplitOptions.RemoveEmptyEntries))
            {
                var at = part.IndexOf(KeyValue);
                if (at <= 0)
                {
                    continue;
                }

                var (key, text) = (part[..at], part[(at + 1)..]);
                if (key == "shape")
                {
                    shape = text;
                    continue;
                }

                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !float.IsFinite(value))
                {
                    continue;
                }

                switch (key)
                {
                    case "length" or "radius":
                        length = value;
                        break;
                    case "width":
                        width = value;
                        break;
                    case "angle":
                        angle = value;
                        break;
                    case "range":
                        range = value;
                        break;
                    case "delay":
                        delay = (int)value;
                        break;
                }
            }

            shape = kind == "trigger" ? Circle : kind == "geometry" ? shape : null;
            return shape != null && length > 0f ? new AttackArea(shape, length, width, angle, range, delay) : null;
        }

        /// <summary>
        ///     Whether the player's body (radius plus slack) is inside the area: a circle or donut centred on the target place
        ///     (at most its range from the monster), a cone, triangle or rectangle reaching from the monster toward it.
        /// </summary>
        public static bool Catches(AttackArea area, Vector2 monster, Vector2 target, Vector2 player, float playerRadius, float slack)
        {
            var body = playerRadius + slack;
            var toward = target - monster;
            var distance = toward.Length();
            var direction = distance > 0f ? toward / distance : Vector2.UnitX;
            switch (area.Shape)
            {
                case Circle or Donut:
                    var centre = area.Range > 0f && distance > area.Range ? monster + (direction * area.Range) : target;
                    return Vector2.Distance(centre, player) <= area.Length + body;
                case Rectangle:
                    var offset = player - monster;
                    var along = Vector2.Dot(offset, direction);
                    var across = MathF.Abs((direction.X * offset.Y) - (direction.Y * offset.X));
                    return along >= -body && along <= area.Length + body && across <= (area.Width / 2f) + body;
                case Cone or Triangle:
                    var reach = Vector2.Distance(monster, player);
                    if (reach > area.Length + body)
                    {
                        return false;
                    }

                    if (reach <= body || area.AngleDegrees >= 360f)
                    {
                        return true;
                    }

                    var half = (area.AngleDegrees / 2f) + (MathF.Asin(Math.Clamp(body / reach, 0f, 1f)) * 180f / MathF.PI);
                    var cosine = Vector2.Dot(Vector2.Normalize(player - monster), direction);
                    return MathF.Acos(Math.Clamp(cosine, -1f, 1f)) * 180f / MathF.PI <= half;
                default:
                    return false;
            }
        }
    }
}
