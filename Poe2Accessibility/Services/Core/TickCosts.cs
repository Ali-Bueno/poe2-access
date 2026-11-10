namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>Time each part of the plugin's frame takes, summed between reports (the dev command `costs`), and its slow runs.</summary>
    public sealed class TickCosts
    {
        /// <summary>A run longer than this is counted as slow: half a frame at 60 Hz.</summary>
        public const double SlowRunMs = 8;

        /// <summary>A run longer than this (a frame and a half at 60 Hz) is a hitch, logged as it happens.</summary>
        public const double HitchMs = 25;

        /// <summary>A part's hitches are logged at most this often, so a part that stays slow cannot fill the log.</summary>
        public const long HitchLogIntervalMs = 10_000;

        private readonly Dictionary<string, Part> parts = new();
        private long since = Stopwatch.GetTimestamp();

        /// <summary>Adds one run of a part; true when it was a hitch to log now (at most once per <see cref="HitchLogIntervalMs"/> a part).</summary>
        public bool Add(string part, long ticks, long nowMs)
        {
            if (!this.parts.TryGetValue(part, out var cost))
            {
                cost = new Part();
                this.parts[part] = cost;
            }

            cost.Ticks += ticks;
            cost.Calls++;
            cost.Slowest = Math.Max(cost.Slowest, ticks);
            var ms = Ms(ticks);
            if (ms > SlowRunMs)
            {
                cost.SlowRuns++;
            }

            if (ms <= HitchMs || (cost.HitchLoggedAt is { } logged && nowMs - logged < HitchLogIntervalMs))
            {
                return false;
            }

            cost.HitchLoggedAt = nowMs;
            return true;
        }

        /// <summary>The costliest parts since the last report, in ms per second of play, then any other with slow runs; starts over.</summary>
        public string Report(int top)
        {
            var now = Stopwatch.GetTimestamp();
            var seconds = (now - this.since) / (double)Stopwatch.Frequency;
            var lines = this.parts
                .Where(part => part.Value.Calls > 0)
                .OrderByDescending(part => part.Value.Ticks)
                .Where((part, rank) => rank < top || part.Value.SlowRuns > 0)
                .Select(part => Line(part.Key, part.Value, seconds))
                .ToList();
            var total = Ms(this.parts.Values.Sum(part => part.Ticks)) / seconds;
            foreach (var part in this.parts.Values)
            {
                part.StartOver();
            }

            this.since = now;
            return $"Plugin costs over {seconds:0} s, {total:0.0} ms/s in parts: " + string.Join("; ", lines);
        }

        private static string Line(string name, Part part, double seconds) =>
            $"{name} {Ms(part.Ticks) / seconds:0.0} ms/s ({part.Calls / seconds:0}/s, slowest {Ms(part.Slowest):0.0} ms" +
            (part.SlowRuns > 0 ? $", {part.SlowRuns} over {SlowRunMs:0} ms)" : ")");

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private sealed class Part
        {
            public long Ticks;
            public long Calls;
            public long Slowest;
            public int SlowRuns;

            /// <summary>When a hitch of the part was last logged: kept across reports.</summary>
            public long? HitchLoggedAt;

            public void StartOver() => (this.Ticks, this.Calls, this.Slowest, this.SlowRuns) = (0, 0, 0, 0);
        }
    }
}
