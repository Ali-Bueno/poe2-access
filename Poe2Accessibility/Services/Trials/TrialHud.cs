namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>What a Trial's run holds, as its HUD counts it: sacred water ("39(+7)"), then the bronze, silver and gold keys.</summary>
    public sealed record TrialCounters(int Water, int WaterToCome, int Bronze, int Silver, int Gold);

    /// <summary>The numbers of a Trial's HUD (dump 2026-11-07). Pure.</summary>
    public static class TrialHud
    {
        private const int CounterCount = 4;

        /// <summary>WorldAreas ids of a run's floors ("Sanctum_1_Foyer_1"), not the hub before them.</summary>
        private const string RunAreaPrefix = "Sanctum_";

        private static readonly Regex Water = new(@"^(\d+)(?:\(\+(\d+)\))?$", RegexOptions.Compiled);
        private static readonly Regex Honour = new(@"(\d+)/(\d+)$", RegexOptions.Compiled);

        /// <summary>Whether an area is a floor of a run.</summary>
        public static bool IsRunArea(string areaId) => areaId.StartsWith(RunAreaPrefix, System.StringComparison.Ordinal);

        /// <summary>The honour line's amount and most, null without them.</summary>
        public static (int Now, int Most)? HonourOf(string line) =>
            Honour.Match(line) is { Success: true } amount ? (Number(amount.Groups[1].Value), Number(amount.Groups[2].Value)) : null;

        /// <summary>The counters beside the honour line, in the HUD's order; null when they are not all there.</summary>
        public static TrialCounters? Parse(IReadOnlyList<string> counts)
        {
            if (counts.Count != CounterCount || Water.Match(counts[0]) is not { Success: true } water)
            {
                return null;
            }

            return int.TryParse(counts[1], out var bronze) && int.TryParse(counts[2], out var silver) && int.TryParse(counts[3], out var gold)
                ? new TrialCounters(Number(water.Groups[1].Value), water.Groups[2].Success ? Number(water.Groups[2].Value) : 0, bronze, silver, gold)
                : null;
        }

        private static int Number(string digits) => int.Parse(digits, CultureInfo.InvariantCulture);
    }

    /// <summary>What changed in a run's counters: keys at once, water once it has settled, as drops are picked up in a row. Pure.</summary>
    public sealed class TrialCounterWatch
    {
        private const int WaterSettleMs = 1500;

        private TrialCounters? last;
        private int waterSaid = -1;
        private long waterChangedAt;

        /// <summary>A new area or run: the first reading is no news.</summary>
        public void Reset()
        {
            this.last = null;
            this.waterSaid = -1;
        }

        /// <summary>Keys gained since the last reading, and water to say now, if any.</summary>
        public (int Bronze, int Silver, int Gold, int? Water) Heard(TrialCounters now, long atMs)
        {
            var before = this.last;
            this.last = now;
            if (before == null)
            {
                this.waterSaid = now.Water;
                return default;
            }

            if (now.Water != before.Water)
            {
                this.waterChangedAt = atMs;
            }

            int? water = null;
            if (now.Water > this.waterSaid && atMs - this.waterChangedAt >= WaterSettleMs)
            {
                water = now.Water;
            }

            if (atMs - this.waterChangedAt >= WaterSettleMs)
            {
                this.waterSaid = now.Water; // spent at a fountain: nothing to say, the next gain counts from here
            }

            return (now.Bronze > before.Bronze ? now.Bronze : 0, now.Silver > before.Silver ? now.Silver : 0, now.Gold > before.Gold ? now.Gold : 0, water);
        }
    }
}
