namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     When each monster attack lands, learnt from the hits the player takes: the time from the attack animation's
    ///     start to the player's life falling, per monster kind and animation (a rare uses its kind's animations). The
    ///     game keeps no attack progress readable from the Actor (docs/research/dodge-timing.md), so the warning is timed
    ///     from these. Pure apart from <see cref="Load"/> and <see cref="Save"/>.
    /// </summary>
    public sealed class DodgeTimings
    {
        /// <summary>Delays kept per attack; their median is the answer, so one blow from another monster does not move it.</summary>
        public const int Samples = 5;

        /// <summary>A hit later than this after the start is not that attack's.</summary>
        public const int MaxDelayMs = 4000;

        private const int MinDelayMs = 50;

        private readonly Dictionary<string, List<int>> delays = new(StringComparer.Ordinal);

        /// <summary>Learnt since the last save.</summary>
        public bool Changed { get; private set; }

        /// <summary>"Metadata/Monsters/MudGolem/MudGolemWet1|13": the monster kind without its "@level" suffix.</summary>
        public static string Key(string monsterPath, int animation)
        {
            var at = monsterPath.IndexOf('@');
            return $"{(at > 0 ? monsterPath[..at] : monsterPath)}|{animation}";
        }

        /// <summary>When to warn for an attack that began at <paramref name="start"/>: the lead before its learnt hit, or at once.</summary>
        public static long WarnAt(long start, int? delayMs, int leadMs) =>
            delayMs is { } delay && delay - leadMs > 0 ? start + delay - leadMs : start;

        public void Learn(string key, int delayMs)
        {
            if (delayMs is < MinDelayMs or > MaxDelayMs)
            {
                return;
            }

            if (!this.delays.TryGetValue(key, out var list))
            {
                list = new List<int>();
                this.delays[key] = list;
            }

            list.Add(delayMs);
            if (list.Count > Samples)
            {
                list.RemoveAt(0);
            }

            this.Changed = true;
        }

        /// <summary>The median learnt delay, null when the attack has never hit.</summary>
        public int? Delay(string key)
        {
            if (!this.delays.TryGetValue(key, out var list) || list.Count == 0)
            {
                return null;
            }

            var sorted = list.OrderBy(value => value).ToList();
            return sorted[sorted.Count / 2];
        }

        /// <summary>One line per attack: "key=delay,delay,…".</summary>
        public IEnumerable<string> ToLines() =>
            this.delays.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={string.Join(',', pair.Value.Select(value => value.ToString(CultureInfo.InvariantCulture)))}");

        public static DodgeTimings FromLines(IEnumerable<string> lines)
        {
            var timings = new DodgeTimings();
            foreach (var line in lines)
            {
                var split = line.LastIndexOf('=');
                if (split <= 0)
                {
                    continue;
                }

                foreach (var part in line[(split + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delay))
                    {
                        timings.Learn(line[..split], delay);
                    }
                }
            }

            timings.Changed = false;
            return timings;
        }

        public static DodgeTimings Load(string path)
        {
            try
            {
                return File.Exists(path) ? FromLines(File.ReadAllLines(path)) : new DodgeTimings();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new DodgeTimings();
            }
        }

        public void Save(string path)
        {
            if (!this.Changed)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, this.ToLines());
                this.Changed = false;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // kept in memory; saved with the next change
            }
        }
    }
}
