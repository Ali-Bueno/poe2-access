namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    /// <summary>Time each part of the plugin's frame takes, summed between reports (the dev command `costs`).</summary>
    public sealed class TickCosts
    {
        private readonly Dictionary<string, (long Ticks, long Calls)> parts = new();
        private long since = Stopwatch.GetTimestamp();

        public void Add(string part, long ticks)
        {
            this.parts.TryGetValue(part, out var cost);
            this.parts[part] = (cost.Ticks + ticks, cost.Calls + 1);
        }

        /// <summary>The costliest parts since the last report, in ms per second of play, and starts over.</summary>
        public string Report(int top)
        {
            var now = Stopwatch.GetTimestamp();
            var seconds = (now - this.since) / (double)Stopwatch.Frequency;
            var lines = this.parts
                .OrderByDescending(part => part.Value.Ticks)
                .Take(top)
                .Select(part => $"{part.Key} {Ms(part.Value.Ticks) / seconds:0.0} ms/s ({part.Value.Calls / seconds:0}/s)")
                .ToList();
            var total = Ms(this.parts.Values.Sum(part => part.Ticks)) / seconds;
            this.parts.Clear();
            this.since = now;
            return $"Plugin costs over {seconds:0} s, {total:0.0} ms/s in parts: " + string.Join("; ", lines);
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
    }
}
