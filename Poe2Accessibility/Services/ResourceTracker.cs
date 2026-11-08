namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>How a counted thing is told: every settled count (charges, stacks) or only its edges (a pool that moves with every hit).</summary>
    public enum ResourceMode
    {
        Counted,
        Edges,
    }

    /// <summary>What a reading of one resource became.</summary>
    public enum ResourceChange
    {
        /// <summary>From nothing to one held, a buff that begins.</summary>
        Begun,

        /// <summary>A count other than one, settled.</summary>
        Count,

        /// <summary>Back to nothing.</summary>
        Ended,

        /// <summary>A pool reaching its maximum.</summary>
        Full,

        /// <summary>A pool running out.</summary>
        Empty,
    }

    /// <summary>
    ///     Diff-gates the player's counted resources (<see cref="ResourceWatch"/>): counted ones are said once they have held
    ///     <see cref="SettleMs"/> (a chain of kills is one count), pools only as they fill and run out, and the first reading
    ///     after a reset is only remembered. Pure.
    /// </summary>
    public sealed class ResourceTracker
    {
        /// <summary>A count is said once it has held this long: stacks come in bursts.</summary>
        public const int SettleMs = 400;

        /// <summary>A maximum up to this is counted; above it only the edges are news (Rage-like pools).</summary>
        public const int CountedMax = 12;

        /// <summary>After a reset, readings this long are still the baseline: what an area gives on entry (Grace Period) is not news.</summary>
        public const int BaselineGraceMs = 2000;

        private readonly Dictionary<string, (int Count, int Max, ResourceMode Mode)> held = new();
        private readonly List<string> unsaid = new();
        private long changedAt;
        private long baselineAt = long.MinValue;
        private bool baseline = true;

        /// <summary>One reading of a resource: its key, the count and maximum (0 unknown) now, and how to tell it.</summary>
        public readonly record struct Reading(string Key, int Count, int Max, ResourceMode Mode);

        /// <summary>What to say about a resource.</summary>
        public readonly record struct News(string Key, ResourceChange Change, int Count, int Max);

        /// <summary>Whether the resource was read with a count the last time.</summary>
        public bool IsHeld(string key) => this.held.GetValueOrDefault(key).Count > 0;

        /// <summary>What is held now, by key, for a report.</summary>
        public IEnumerable<(string Key, int Count, int Max)> Held => this.held.Where(entry => entry.Value.Count > 0).Select(entry => (entry.Key, entry.Value.Count, entry.Value.Max));

        /// <summary>The mode a maximum calls for when the reader has no better rule.</summary>
        public static ResourceMode ModeFor(int max) => max > CountedMax ? ResourceMode.Edges : ResourceMode.Counted;

        /// <summary>A new area or character: what is held now is not news.</summary>
        public void Reset()
        {
            this.held.Clear();
            this.unsaid.Clear();
            this.baseline = true;
            this.baselineAt = long.MinValue;
        }

        /// <summary>Takes the readings of this moment; a resource read before and missing now counts as 0.</summary>
        public List<News> Update(IReadOnlyList<Reading> readings, long nowMs)
        {
            var news = new List<News>();
            var seen = new HashSet<string>();
            if (this.baselineAt == long.MinValue)
            {
                this.baselineAt = nowMs;
            }

            this.baseline = nowMs - this.baselineAt < BaselineGraceMs;
            foreach (var reading in Merged(readings))
            {
                seen.Add(reading.Key);
                this.Take(reading, nowMs, news);
            }

            foreach (var key in this.held.Keys.Where(key => !seen.Contains(key) && this.held[key].Count > 0).ToList())
            {
                this.Take(new Reading(key, 0, this.held[key].Max, this.held[key].Mode), nowMs, news);
            }

            if (this.unsaid.Count > 0 && nowMs - this.changedAt >= SettleMs)
            {
                foreach (var key in this.unsaid)
                {
                    var (count, max, _) = this.held[key];
                    news.Add(new News(key, count == 0 ? ResourceChange.Ended : count == 1 ? ResourceChange.Begun : ResourceChange.Count, count, max));
                }

                this.unsaid.Clear();
            }

            return news;
        }

        /// <summary>One reading per key: a buff two skills grant comes twice under one key, its stacks added as the host adds them.</summary>
        private static IEnumerable<Reading> Merged(IReadOnlyList<Reading> readings) =>
            readings.GroupBy(reading => reading.Key)
                .Select(same => same.Skip(1).Any() ? same.First() with { Count = same.Sum(reading => reading.Count) } : same.First());

        private void Take(Reading reading, long nowMs, List<News> news)
        {
            var before = this.held.GetValueOrDefault(reading.Key);
            this.held[reading.Key] = (reading.Count, reading.Max, reading.Mode);
            if (this.baseline || reading.Count == before.Count)
            {
                return;
            }

            if (reading.Mode == ResourceMode.Counted)
            {
                this.changedAt = nowMs;
                if (!this.unsaid.Contains(reading.Key))
                {
                    this.unsaid.Add(reading.Key);
                }
            }
            else if (reading.Max > 0 && reading.Count >= reading.Max && before.Count < reading.Max)
            {
                news.Add(new News(reading.Key, ResourceChange.Full, reading.Count, reading.Max));
            }
            else if (reading.Count == 0 && before.Count > 0)
            {
                news.Add(new News(reading.Key, ResourceChange.Empty, 0, reading.Max));
            }
        }
    }
}
