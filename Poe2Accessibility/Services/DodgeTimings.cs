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

        /// <summary>
        ///     From the game's animation data (catalog/hit-times.txt): when each attack lands and how long one swing
        ///     lasts, for an attack no blow has timed yet (docs/research/dodge-timing.md).
        /// </summary>
        private readonly Dictionary<string, (int Hit, int Swing)> fromFiles = new(StringComparer.Ordinal);

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

        /// <summary>The median learnt delay; before any blow, the one the game's files give; null when neither knows it.</summary>
        public int? Delay(string key)
        {
            if (!this.delays.TryGetValue(key, out var list) || list.Count == 0)
            {
                return this.fromFiles.TryGetValue(key, out var timed) ? timed.Hit : null;
            }

            var sorted = list.OrderBy(value => value).ToList();
            return sorted[sorted.Count / 2];
        }

        /// <summary>How many attacks the game's files time.</summary>
        public int FileTimes => this.fromFiles.Count;

        /// <summary>How long one swing of the attack lasts (it repeats while the animation holds), when the files say.</summary>
        public int? Swing(string key) => this.fromFiles.TryGetValue(key, out var timed) && timed.Swing > 0 ? timed.Swing : null;

        /// <summary>Lines "key=hit/swing" (catalog/hit-times.txt); "#" lines are comments.</summary>
        public void AddHitTimes(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var split = line.LastIndexOf('=');
                var slash = line.LastIndexOf('/');
                if (line.StartsWith('#') || split <= 0 || slash < split ||
                    !int.TryParse(line[(split + 1)..slash], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hit) ||
                    !int.TryParse(line[(slash + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var swing) ||
                    hit is < MinDelayMs or > MaxDelayMs)
                {
                    continue;
                }

                this.fromFiles[line[..split]] = (hit, swing);
            }
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

        /// <summary>
        ///     The shipped timings measured in development (<paramref name="seedPath"/>), then the player's own: their
        ///     blows come later in each list, so they push the measured ones out.
        /// </summary>
        public static DodgeTimings Load(string seedPath, string path) => FromLines(ReadLines(seedPath).Concat(ReadLines(path)));

        /// <summary>As <see cref="Load(string, string)"/>, with the game files' hit times beneath what was measured.</summary>
        public static DodgeTimings Load(string hitTimesPath, string seedPath, string path)
        {
            var timings = Load(seedPath, path);
            timings.AddHitTimes(ReadLines(hitTimesPath));
            return timings;
        }

        private static IEnumerable<string> ReadLines(string path)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<string>();
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
