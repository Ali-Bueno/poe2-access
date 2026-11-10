namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The plugin's per-part costs (the dev command `costs`): slow runs counted, hitches logged sparingly.</summary>
    public static class TickCostsTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AHitchIsLoggedOnceInTenSecondsPerPart), AHitchIsLoggedOnceInTenSecondsPerPart),
            (nameof(TheReportNamesSlowestRunsAndKeepsSlowPartsPastTheTop), TheReportNamesSlowestRunsAndKeepsSlowPartsPastTheTop),
        };

        private static void AHitchIsLoggedOnceInTenSecondsPerPart()
        {
            var costs = new TickCosts();
            Expect(true, costs.Add("sounds", Ticks(30), 1_000), "a first hitch");
            Expect(false, costs.Add("sounds", Ticks(40), 6_000), "another within ten seconds");
            Expect(true, costs.Add("screen", Ticks(30), 6_000), "another part's hitch");
            Expect(false, costs.Add("sounds", Ticks(20), 12_000), "a slow run that is no hitch");
            Expect(true, costs.Add("sounds", Ticks(30), 11_000), "ten seconds after the first");
            costs.Report(10);
            Expect(false, costs.Add("sounds", Ticks(30), 12_000), "a report does not reset the interval");
        }

        private static void TheReportNamesSlowestRunsAndKeepsSlowPartsPastTheTop()
        {
            var costs = new TickCosts();
            for (var run = 0; run < 10; run++)
            {
                costs.Add("radar", Ticks(5), 0);
            }

            costs.Add("doors", Ticks(1), 0);
            costs.Add("doors", Ticks(12), 0);
            costs.Add("quiet", Ticks(1), 0);
            var report = costs.Report(1);
            Expect(true, report.Contains("radar ", StringComparison.Ordinal), $"the costliest part: {report}");
            Expect(true, report.Contains("slowest 12", StringComparison.Ordinal) && report.Contains("1 over 8 ms", StringComparison.Ordinal), $"the slow part past the top: {report}");
            Expect(false, report.Contains("quiet", StringComparison.Ordinal), $"a cheap part past the top: {report}");
            Expect(false, costs.Report(10).Contains("radar", StringComparison.Ordinal), "the next report starts over");
        }

        private static long Ticks(double ms) => (long)(ms * Stopwatch.Frequency / 1000.0);
    }
}
