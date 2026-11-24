namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     One danger shape of an effect-driven skill, from its effect file (<c>telegraph-effects.txt</c>): when it hits, its
    ///     sizes and place in world units, and for a shape that sweeps its length (a wave) how long that takes (ms).
    /// </summary>
    public sealed record EffectDanger(string Shape, int HitMs, float Length, float Width, float AngleDegrees, Vector2 Offset, float OrientationDegrees, int SweepMs)
    {
        /// <summary>When the shape's last hit is over (ms from the effect's start).</summary>
        public int EndMs => this.HitMs + this.SweepMs;
    }

    /// <summary>The effects whose shapes the files give, by .ao path. Pure apart from <see cref="Load"/>.</summary>
    public sealed class EffectAreas
    {
        public const string File = "telegraph-effects.txt";

        private const int BasicFields = 5;
        private const int PlacedFields = 9;

        private readonly Dictionary<string, IReadOnlyList<EffectDanger>> effects = new(StringComparer.OrdinalIgnoreCase);

        public int Count => this.effects.Count;

        public static EffectAreas Load(string path)
        {
            var areas = new EffectAreas();
            areas.Add(AttackData.ReadLines(path));
            return areas;
        }

        /// <summary>Lines ".ao path|roles|shape:ms:length:width:angle[:x:y:orientation:sweep ms],…"; only timed shapes are kept.</summary>
        public void Add(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var columns = line.Split('|');
                if (line.Length == 0 || line[0] == '#' || columns.Length < 3)
                {
                    continue;
                }

                var shapes = columns[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Parse).OfType<EffectDanger>().ToList();
                if (shapes.Count > 0)
                {
                    this.effects[columns[0]] = shapes;
                }
            }
        }

        /// <summary>The shapes of an effect's .ao; null when the files give none.</summary>
        public IReadOnlyList<EffectDanger>? Of(string ao) => this.effects.GetValueOrDefault(ao);

        private static EffectDanger? Parse(string text)
        {
            var fields = text.Split(':');
            if (fields.Length is not (BasicFields or PlacedFields) || fields[1].Length == 0)
            {
                return null;
            }

            float Number(int k) => k < fields.Length && float.TryParse(fields[k], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
            var length = Number(2) * AttackAreas.WorldPerCell;
            return length <= 0f
                ? null
                : new EffectDanger(fields[0], (int)Number(1), length, Number(3) * AttackAreas.WorldPerCell, Number(4),
                    new Vector2(Number(5), Number(6)) * AttackAreas.WorldPerCell, Number(7), (int)Number(8));
        }
    }

    /// <summary>
    ///     An effect's shape where the effect stands in the world. An effect file's shapes lie in a frame half a turn from the
    ///     model's: forward (orientation 0) is its +y, orientation 90 its +x, and the entity's rotation turns the model
    ///     (docs/research/boss-mechanics.md, Diamora). Pure.
    /// </summary>
    public sealed class EffectZone
    {
        private readonly Vector2 origin;
        private readonly Vector2 direction;

        public EffectZone(EffectDanger shape, Vector2 at, float rotation)
        {
            this.Shape = shape;
            this.origin = at - Turned(shape.Offset, rotation);
            var (sin, cos) = MathF.SinCos(shape.OrientationDegrees * MathF.PI / 180f);
            this.direction = -Turned(new Vector2(sin, cos), rotation);
        }

        public EffectDanger Shape { get; }

        /// <summary>Whether it is a wave that runs along its length.</summary>
        private bool Sweeps => this.Shape.SweepMs > 0 && this.Shape.Shape == AttackAreas.Rectangle;

        /// <summary>Whether a body of this radius at a point is in the shape.</summary>
        public bool Catches(Vector2 point, float body)
        {
            var offset = point - this.origin;
            var along = Vector2.Dot(offset, this.direction);
            var across = MathF.Abs((this.direction.X * offset.Y) - (this.direction.Y * offset.X));
            switch (this.Shape.Shape)
            {
                case AttackAreas.Rectangle:
                    return along >= -body && along <= this.Shape.Length + body && across <= (this.Shape.Width / 2f) + body;
                case AttackAreas.Cone:
                    // The half angle's edge moved outward by the body.
                    var half = this.Shape.AngleDegrees / 2f * MathF.PI / 180f;
                    return offset.Length() <= this.Shape.Length + body && (half >= MathF.PI || offset.Length() <= body ||
                        MathF.Atan2(across, along) <= half + MathF.Asin(MathF.Min(1f, body / offset.Length())));
                case AttackAreas.Donut:
                    return offset.Length() <= this.Shape.Length + body && offset.Length() >= this.Shape.Width - body;
                default:
                    return offset.Length() <= this.Shape.Length + body;
            }
        }

        /// <summary>Ms from now until the shape strikes a point in it, <paramref name="elapsed"/> ms after its effect's start; negative once passed.</summary>
        public int StrikesIn(Vector2 point, int elapsed)
        {
            var along = this.Sweeps ? Math.Clamp(Vector2.Dot(point - this.origin, this.direction) / this.Shape.Length, 0f, 1f) : 0f;
            return this.Shape.HitMs + (int)MathF.Round(along * this.Shape.SweepMs) - elapsed;
        }

        /// <summary>The nearest point of a sweeping shape's front to a point, and how far ahead of the front the point is; null for a shape that does not sweep.</summary>
        public (Vector2 At, float Ahead)? Front(Vector2 point, int elapsed)
        {
            if (!this.Sweeps)
            {
                return null;
            }

            var front = Math.Clamp((elapsed - this.Shape.HitMs) / (float)this.Shape.SweepMs, 0f, 1f) * this.Shape.Length;
            var ahead = Vector2.Dot(point - this.origin, this.direction) - front;
            return (point - (this.direction * ahead), ahead);
        }

        private static Vector2 Turned(Vector2 v, float rotation)
        {
            var (sin, cos) = MathF.SinCos(rotation);
            return new Vector2((v.X * cos) - (v.Y * sin), (v.X * sin) + (v.Y * cos));
        }
    }
}
