namespace Poe2Access
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using GameReader;

    /// <summary>What the frames cost, logged once a minute: the measure for running on small machines.</summary>
    internal sealed class FrameStats
    {
        private const double ReportSeconds = 60;
        private const double BudgetMs = 1000.0 / 60;

        /// <summary>The types read most often, named in the log: what a frame's reads are made of.</summary>
        private const int ReadTypesLogged = 6;

        private readonly Process self = Process.GetCurrentProcess();
        private readonly long reportTicks = (long)(Stopwatch.Frequency * ReportSeconds);
        private long since = Stopwatch.GetTimestamp();
        private TimeSpan cpuSince = Process.GetCurrentProcess().TotalProcessorTime;
        private Dictionary<string, long> readsSince = new(HostFrame.MemoryReadsByType);
        private int frames;
        private long total;
        private long slowest;
        private int overBudget;

        /// <summary>Gets the per-part ticks of the frames since the last report (filled by <see cref="HostFrame.Run"/>).</summary>
        public long[] Steps { get; } = new long[HostFrame.StepNames.Length];

        public void EndFrame(long ticks)
        {
            this.frames++;
            this.total += ticks;
            this.slowest = Math.Max(this.slowest, ticks);
            if (Ms(ticks) > BudgetMs)
            {
                this.overBudget++;
            }

            var now = Stopwatch.GetTimestamp();
            if (now - this.since >= this.reportTicks)
            {
                this.Report(now);
            }
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private void Report(long now)
        {
            this.self.Refresh();
            var cpu = this.self.TotalProcessorTime;
            var wall = (now - this.since) / (double)Stopwatch.Frequency;
            var reads = new Dictionary<string, long>(HostFrame.MemoryReadsByType);
            var readsPerFrame = reads.Select(pair => (pair.Key, Count: (pair.Value - this.readsSince.GetValueOrDefault(pair.Key)) / this.frames))
                .OrderByDescending(pair => pair.Count).ToList();
            var topReads = string.Join(", ", readsPerFrame.Take(ReadTypesLogged).Select(pair => $"{pair.Key} {pair.Count}"));
            var parts = string.Join(", ", HostFrame.StepNames.Select((name, i) => $"{name} {Ms(this.Steps[i]) / this.frames:0.00}"));
            HostLog.Line(
                $"Frames: {this.frames} in {wall:0} s, {Ms(this.total) / this.frames:0.00} ms each ({parts}), slowest {Ms(this.slowest):0.0} ms, " +
                $"{this.overBudget} over {BudgetMs:0.0} ms; {readsPerFrame.Sum(pair => pair.Count)} reads per frame ({topReads}); " +
                $"CPU {(cpu - this.cpuSince).TotalSeconds / wall * 100:0.0}% of one core, " +
                $"{this.self.WorkingSet64 / (1024 * 1024)} MB; {HostFrame.GameState}{(HostFrame.GameForeground ? string.Empty : ", game in background")}");
            this.since = now;
            this.cpuSince = cpu;
            this.readsSince = reads;
            this.frames = 0;
            this.total = 0;
            this.slowest = 0;
            this.overBudget = 0;
            Array.Clear(this.Steps);
        }
    }
}
