namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>Which part of a clone's way a marker is.</summary>
    public enum ClonePathRole
    {
        Start,
        Way,
        End,
    }

    /// <summary>A marker of the way a boss's clones walk (Yama's YamaFutureClonePath, with Start and End).</summary>
    public readonly record struct ClonePathMarker(uint Id, string Stem, ClonePathRole Role, Vector2 At);

    /// <summary>
    ///     Where a boss's clone will stop (docs/research/boss-mechanics.md, "Yama"): the game lays each clone's way as markers
    ///     with running ids (start, way…, end) and takes a way away as its clone sets off on it, so the ways are remembered
    ///     and the one that went as a clone came names its end. Pure.
    /// </summary>
    public sealed class ClonePaths
    {
        public const string StartSuffix = "Start";
        public const string EndSuffix = "End";

        /// <summary>A clone that moved no more than about a body's radius (world units) this long (ms, three polls) has stopped.</summary>
        public const int SettledMs = 450;
        private const float StillWorld = 20f;

        /// <summary>How far a clone may stray from its way (world units, about half a step), and how much better its way must fit than the next.</summary>
        private const float FitWorld = 60f;
        private const float ClearlyWorld = 60f;

        /// <summary>With no clone about, a way taken away this long ago (ms) belongs to a round that is over.</summary>
        private const int ForgetMs = 3000;

        private readonly Dictionary<uint, Way> ways = new();
        private readonly Dictionary<uint, Clone> clones = new();

        /// <summary>How many whole ways are known.</summary>
        public int Count => this.ways.Count;

        /// <summary>How many of them are laid now.</summary>
        public int Laid => this.ways.Values.Count(way => way.Laid);

        public void Reset()
        {
            this.ways.Clear();
            this.clones.Clear();
        }

        public void Update(IReadOnlyList<ClonePathMarker> markers, IReadOnlyList<(uint Id, Vector2 At)> seen, long now)
        {
            this.Lay(markers, now);
            foreach (var gone in this.clones.Keys.Where(id => seen.All(clone => clone.Id != id)).ToList())
            {
                this.clones.Remove(gone);
            }

            foreach (var (id, at) in seen)
            {
                if (!this.clones.TryGetValue(id, out var clone))
                {
                    this.clones[id] = new Clone(at, now);
                }
                else if (Vector2.Distance(at, clone.StillAt) > StillWorld)
                {
                    clone.Seen.Add(at);
                    (clone.StillAt, clone.StillSince) = (at, now);
                }
            }

            foreach (var (id, clone) in this.clones.OrderBy(pair => pair.Key))
            {
                if (clone.Way == null && this.Free(id).Where(way => !way.Laid).ToList() is { Count: 1 } taken)
                {
                    clone.Way = taken[0];
                }
            }

            if (this.clones.Count == 0)
            {
                foreach (var (start, _) in this.ways.Where(pair => !pair.Value.Laid && now - pair.Value.LaidAt >= ForgetMs).ToList())
                {
                    this.ways.Remove(start);
                }
            }
        }

        /// <summary>How long (ms) a clone has stood still; 0 for one not seen.</summary>
        public long StoppedMs(uint id, long now) => this.clones.TryGetValue(id, out var clone) ? now - clone.StillSince : 0;

        /// <summary>Whether a clone has been seen to set off.</summary>
        public bool Walked(uint id) => this.clones.TryGetValue(id, out var clone) && clone.Seen.Count > 1;

        /// <summary>Where a clone's way ends: of the way taken away as it came, or of the one that fits where it has been clearly better than the others.</summary>
        public Vector2? EndOf(uint id)
        {
            if (!this.clones.TryGetValue(id, out var clone))
            {
                return null;
            }

            if (clone.Way is { } own)
            {
                return own.Points[^1];
            }

            var free = this.Free(id).ToList();
            var fits = (free.Any(way => !way.Laid) ? free.Where(way => !way.Laid) : free)
                .Select(way => (Way: way, Off: clone.Seen.Max(at => DistanceTo(way.Points, at)))).OrderBy(fit => fit.Off).ToList();
            return fits.Count > 0 && fits[0].Off <= FitWorld && (fits.Count == 1 || fits[1].Off - fits[0].Off >= ClearlyWorld) ? fits[0].Way.Points[^1] : null;
        }

        /// <summary>The ways no other clone took.</summary>
        private IEnumerable<Way> Free(uint id) => this.ways.Values.Where(way => this.clones.All(other => other.Key == id || other.Value.Way != way));

        /// <summary>The ways of this poll's markers: in the order of their ids, from each start to its end.</summary>
        private void Lay(IReadOnlyList<ClonePathMarker> markers, long now)
        {
            foreach (var way in this.ways.Values)
            {
                way.Laid = false;
            }

            foreach (var stem in markers.GroupBy(marker => marker.Stem, StringComparer.OrdinalIgnoreCase))
            {
                uint start = 0;
                List<Vector2>? points = null;
                foreach (var marker in stem.OrderBy(marker => marker.Id))
                {
                    if (marker.Role == ClonePathRole.Start)
                    {
                        (start, points) = (marker.Id, new List<Vector2>());
                    }

                    points?.Add(marker.At);
                    if (marker.Role == ClonePathRole.End && points != null)
                    {
                        this.Keep(start, points, now);
                        points = null;
                    }
                }
            }
        }

        /// <summary>A way read whole this poll; one read with fewer markers than before keeps those it had.</summary>
        private void Keep(uint start, List<Vector2> points, long now)
        {
            if (!this.ways.TryGetValue(start, out var way) || way.Points[^1] != points[^1])
            {
                this.ways[start] = way = new Way(points);
            }
            else if (way.Points.Count < points.Count)
            {
                way.Points = points;
            }

            (way.Laid, way.LaidAt) = (true, now);
        }

        private static float DistanceTo(List<Vector2> way, Vector2 at)
        {
            var best = Vector2.Distance(way[0], at);
            for (var i = 1; i < way.Count; i++)
            {
                var leg = way[i] - way[i - 1];
                var share = leg.LengthSquared() > 0f ? Math.Clamp(Vector2.Dot(at - way[i - 1], leg) / leg.LengthSquared(), 0f, 1f) : 0f;
                best = Math.Min(best, Vector2.Distance(at, way[i - 1] + (leg * share)));
            }

            return best;
        }

        private sealed class Way
        {
            public Way(List<Vector2> points) => this.Points = points;

            public List<Vector2> Points { get; set; }

            public bool Laid { get; set; }

            public long LaidAt { get; set; }
        }

        private sealed class Clone
        {
            public Clone(Vector2 at, long now)
            {
                this.Seen.Add(at);
                (this.StillAt, this.StillSince) = (at, now);
            }

            public List<Vector2> Seen { get; } = new();

            public Vector2 StillAt { get; set; }

            public long StillSince { get; set; }

            public Way? Way { get; set; }
        }
    }
}
