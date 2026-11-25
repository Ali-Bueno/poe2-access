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
    ///     with running ids (start, way…, end) before it walks, so the way that fits where it has been names its end. Pure.
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

        private readonly List<List<Vector2>> ways = new();
        private readonly Dictionary<uint, (List<Vector2> Seen, Vector2 StillAt, long StillSince)> clones = new();

        /// <summary>How many whole ways are laid.</summary>
        public int Count => this.ways.Count;

        public void Reset()
        {
            this.ways.Clear();
            this.clones.Clear();
        }

        public void Update(IReadOnlyList<ClonePathMarker> markers, IReadOnlyList<(uint Id, Vector2 At)> seen, long now)
        {
            this.Lay(markers);
            foreach (var gone in this.clones.Keys.Where(id => seen.All(clone => clone.Id != id)).ToList())
            {
                this.clones.Remove(gone);
            }

            foreach (var (id, at) in seen)
            {
                if (!this.clones.TryGetValue(id, out var clone))
                {
                    this.clones[id] = (new List<Vector2> { at }, at, now);
                    continue;
                }

                if (Vector2.Distance(at, clone.StillAt) > StillWorld)
                {
                    clone.Seen.Add(at);
                    this.clones[id] = (clone.Seen, at, now);
                }
            }
        }

        /// <summary>How long (ms) a clone has stood still; 0 for one not seen.</summary>
        public long StoppedMs(uint id, long now) => this.clones.TryGetValue(id, out var clone) ? now - clone.StillSince : 0;

        /// <summary>Where a clone's way ends, when one way fits where it has been clearly better than the others.</summary>
        public Vector2? EndOf(uint id)
        {
            if (this.ways.Count == 0 || !this.clones.TryGetValue(id, out var clone))
            {
                return null;
            }

            var fits = this.ways.Select(way => (Way: way, Off: clone.Seen.Max(at => DistanceTo(way, at)))).OrderBy(fit => fit.Off).ToList();
            return fits[0].Off <= FitWorld && (fits.Count == 1 || fits[1].Off - fits[0].Off >= ClearlyWorld) ? fits[0].Way[^1] : null;
        }

        /// <summary>The ways of this poll's markers: in the order of their ids, from each start to its end.</summary>
        private void Lay(IReadOnlyList<ClonePathMarker> markers)
        {
            this.ways.Clear();
            foreach (var stem in markers.GroupBy(marker => marker.Stem, StringComparer.OrdinalIgnoreCase))
            {
                List<Vector2>? way = null;
                foreach (var marker in stem.OrderBy(marker => marker.Id))
                {
                    if (marker.Role == ClonePathRole.Start)
                    {
                        way = new List<Vector2>();
                    }

                    way?.Add(marker.At);
                    if (marker.Role == ClonePathRole.End && way != null)
                    {
                        this.ways.Add(way);
                        way = null;
                    }
                }
            }
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
    }
}
