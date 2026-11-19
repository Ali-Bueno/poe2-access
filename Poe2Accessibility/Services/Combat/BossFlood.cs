namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     A boss arena's flood (Mektul's molten gold): an arena object plays one long clip whose front bone runs down the hall
    ///     and whose edge bones draw the tongue behind it ([boss mechanics](../../../docs/research/boss-mechanics.md#mektuls-lava)).
    ///     <paramref name="Track"/> is the front at even steps of the clip and <paramref name="Anchors"/> other room objects, both
    ///     as offsets from the object in its room's frame (world units); <paramref name="Outline"/> is the tongue's half width
    ///     (Y) at a distance behind the front (X).
    /// </summary>
    public sealed record FloodRule(string Boss, string Object, float ClipSeconds, IReadOnlyList<Vector2> Track, IReadOnlyList<Vector2> Outline,
        IReadOnlyList<(string Path, Vector2 Room)> Anchors)
    {
        /// <summary>Seconds of clip between two track samples.</summary>
        public float PeriodSeconds => this.Track.Count > 1 ? this.ClipSeconds / (this.Track.Count - 1) : this.ClipSeconds;

        /// <summary>A catalog line "flood|boss|object|clip ms|b:w;…|x:y;…|path@x:y;…"; null when malformed.</summary>
        public static FloodRule? Parse(string[] c)
        {
            if (c.Length < 7 || !float.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms) || ms <= 0f)
            {
                return null;
            }

            var outline = Pairs(c[4]);
            var track = Pairs(c[5]);
            var anchors = c[6].Split(';', StringSplitOptions.RemoveEmptyEntries).Select(part => part.Split('@'))
                .Where(part => part.Length == 2 && Pair(part[1]) != null).Select(part => (part[0], Pair(part[1])!.Value)).ToList();
            return outline is { Count: > 0 } && track is { Count: > 1 } && anchors.Count > 0
                ? new FloodRule(c[1], c[2], ms / 1000f, track, outline.OrderBy(point => point.X).ToList(), anchors)
                : null;
        }

        private static List<Vector2>? Pairs(string text)
        {
            var pairs = text.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(Pair).ToList();
            return pairs.All(pair => pair != null) ? pairs.Select(pair => pair!.Value).ToList() : null;
        }

        private static Vector2? Pair(string text) =>
            text.Split(':') is [var x, var y] && float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) &&
            float.TryParse(y, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
                ? new Vector2(a, b)
                : null;
    }

    /// <summary>
    ///     How a room's frame lies in the world: tiles are placed turned by quarter turns or mirrored, so the frame maps to the
    ///     world by one of eight signed axis swaps. <c>world = (Xx·x + Xy·y, Yx·x + Yy·y)</c>.
    /// </summary>
    public readonly record struct RoomFit(int Xx, int Xy, int Yx, int Yy)
    {
        /// <summary>A room's positions are whole grid cells: an anchor may sit this far (world units) from where its cell says.</summary>
        private const float CellSlack = 3f * 250f / 23f;

        /// <summary>…or this share of its distance, for a long way to the anchor.</summary>
        private const float ShareSlack = 0.05f;

        private static readonly RoomFit[] Candidates =
        {
            new(1, 0, 0, 1), new(-1, 0, 0, 1), new(1, 0, 0, -1), new(-1, 0, 0, -1),
            new(0, 1, 1, 0), new(0, -1, 1, 0), new(0, 1, -1, 0), new(0, -1, -1, 0),
        };

        /// <summary>The fit that takes an anchor's room offset to its world offset; null when none does or the anchor tells two apart.</summary>
        public static RoomFit? Of(Vector2 room, Vector2 world)
        {
            var slack = Math.Max(CellSlack, ShareSlack * room.Length());
            var fitting = Candidates.Where(fit => Vector2.Distance(fit.ToWorld(room), world) <= slack).ToList();
            return fitting.Count == 1 ? fitting[0] : null;
        }

        public Vector2 ToWorld(Vector2 room) => new((this.Xx * room.X) + (this.Xy * room.Y), (this.Yx * room.X) + (this.Yy * room.Y));

        /// <summary>The inverse: a signed axis swap's inverse is its transpose.</summary>
        public Vector2 ToRoom(Vector2 world) => new((this.Xx * world.X) + (this.Yx * world.Y), (this.Xy * world.X) + (this.Yy * world.Y));
    }

    /// <summary>Where a flood stands at a time of its clip, and how far a point is from it. Pure.</summary>
    public static class FloodGeometry
    {
        /// <summary>The flood's outline at a time of its clip (seconds), in the room's frame: the tongue's left edge from the front back to the source, then its right edge.</summary>
        public static IReadOnlyList<Vector2> Outline(FloodRule rule, float seconds)
        {
            var track = rule.Track;
            var axis = Vector2.Normalize(track[^1] - track[0]);
            var side = new Vector2(-axis.Y, axis.X);
            var along = track.Select(point => Vector2.Dot(point - track[0], axis)).ToList();
            var front = Along(along, Math.Clamp(seconds / rule.PeriodSeconds, 0f, track.Count - 1));
            var behind = rule.Outline.Select(point => point.X).Where(b => b <= front).Concat(along.Where(s => s < front).Select(s => front - s)).Append(front)
                .Distinct().OrderBy(b => b).ToList();
            var left = new List<Vector2>();
            var right = new List<Vector2>();
            foreach (var b in behind)
            {
                var centre = Centre(track, along, front - b);
                var half = Half(rule.Outline, b);
                left.Add(centre + (side * half));
                right.Add(centre - (side * half));
            }

            right.Reverse();
            return left.Concat(right).ToList();
        }

        /// <summary>Whether a point lies in the outline, how far it is from its edge and the nearest point of that edge.</summary>
        public static (bool Inside, float Distance, Vector2 Nearest) Measure(IReadOnlyList<Vector2> outline, Vector2 point)
        {
            var inside = false;
            var best = float.MaxValue;
            var nearest = point;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                var (a, b) = (outline[j], outline[i]);
                if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < a.X + ((point.Y - a.Y) / (b.Y - a.Y) * (b.X - a.X)))
                {
                    inside = !inside;
                }

                var edge = b - a;
                var t = edge.LengthSquared() > 0f ? Math.Clamp(Vector2.Dot(point - a, edge) / edge.LengthSquared(), 0f, 1f) : 0f;
                var on = a + (edge * t);
                var distance = Vector2.Distance(on, point);
                if (distance < best)
                {
                    (best, nearest) = (distance, on);
                }
            }

            return (inside, best, nearest);
        }

        private static float Along(IReadOnlyList<float> along, float index)
        {
            var low = (int)Math.Floor(index);
            var high = Math.Min(low + 1, along.Count - 1);
            return along[low] + ((along[high] - along[low]) * (index - low));
        }

        /// <summary>The track's point that far along its axis, between the samples around it.</summary>
        private static Vector2 Centre(IReadOnlyList<Vector2> track, IReadOnlyList<float> along, float s)
        {
            for (var k = 1; k < track.Count; k++)
            {
                if (along[k] >= s)
                {
                    var span = along[k] - along[k - 1];
                    return span > 0f ? Vector2.Lerp(track[k - 1], track[k], (s - along[k - 1]) / span) : track[k];
                }
            }

            return track[^1];
        }

        /// <summary>The tongue's half width that far behind its front: the outline between its points, its widest past the last.</summary>
        private static float Half(IReadOnlyList<Vector2> outline, float behind)
        {
            if (behind <= outline[0].X)
            {
                return outline[0].Y;
            }

            for (var k = 1; k < outline.Count; k++)
            {
                if (outline[k].X >= behind)
                {
                    var span = outline[k].X - outline[k - 1].X;
                    return span > 0f ? outline[k - 1].Y + ((outline[k].Y - outline[k - 1].Y) * (behind - outline[k - 1].X) / span) : outline[k].Y;
                }
            }

            return outline.Max(point => point.Y);
        }
    }

    /// <summary>What a flood's warnings tell (<see cref="FloodWarnings"/>).</summary>
    public enum FloodNewsKind
    {
        /// <summary>The flood came within a closer band of steps.</summary>
        Near,

        /// <summary>The player stands in it.</summary>
        Inside,
    }

    /// <summary>
    ///     A flood's warnings as it nears: once per closer band of steps, once on stepping in; a band is told again only after
    ///     the player got clear of it. Pure.
    /// </summary>
    public sealed class FloodWarnings
    {
        /// <summary>Steps at which a nearing flood is told: room to walk away, then to dodge.</summary>
        public static readonly int[] Bands = { 10, 5, 2 };

        /// <summary>A band is armed again once the flood is this many steps beyond it, so standing on its edge tells it once.</summary>
        private const int Rearm = 2;

        private int? band;
        private bool inside;

        public void Reset() => (this.band, this.inside) = (null, false);

        /// <summary>One reading: the news to tell, or null.</summary>
        public FloodNewsKind? Update(bool inside, int steps)
        {
            if (inside)
            {
                var entered = !this.inside;
                (this.inside, this.band) = (true, 0);
                return entered ? FloodNewsKind.Inside : null;
            }

            this.inside = false;
            var tightest = Bands.Where(b => steps <= b).DefaultIfEmpty(int.MaxValue).Min();
            if (tightest != int.MaxValue && (this.band == null || tightest < this.band))
            {
                this.band = tightest;
                return FloodNewsKind.Near;
            }

            if (this.band != null && steps > this.band + Rearm)
            {
                this.band = tightest == int.MaxValue ? null : tightest;
            }

            return null;
        }
    }
}
