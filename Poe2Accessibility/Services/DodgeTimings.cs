namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;

    /// <summary>
    ///     When each monster attack lands, learnt from the hits the player takes: the time from the attack animation's
    ///     start to the player's life falling (to a projectile's release, its flight taken off), per monster kind and
    ///     animation (a rare uses its kind's animations), where the playing clip's own events do not time it
    ///     (<see cref="AttackClock"/>). A blow far from the files' time is not believed: it was another monster's, or damage
    ///     over time (docs/research/dodge-timing.md). Pure apart from <see cref="Load"/> and <see cref="Save"/>.
    /// </summary>
    public sealed class DodgeTimings
    {
        /// <summary>Delays kept per attack; their median is the answer, so one blow from another monster does not move it.</summary>
        public const int Samples = 5;

        /// <summary>A hit later than this after the start is not that attack's.</summary>
        public const int MaxDelayMs = 4000;

        private const int MinDelayMs = 50;

        /// <summary>
        ///     How far a learnt blow may fall from the files' time and still be believed: the files and blows felt in play part by a
        ///     median 86-156 ms (docs/research/dodge-timing.md, "Kinds from the data"), so twice the larger, or a quarter of a long hit.
        /// </summary>
        private const int LearnToleranceMs = 300;

        private const float LearnToleranceShare = 0.25f;

        private const float MsPerSecond = 1000f;

        private readonly Dictionary<string, List<int>> delays = new(StringComparer.Ordinal);

        /// <summary>Learnt since the last save.</summary>
        public bool Changed { get; private set; }

        /// <summary>
        ///     From the game's animation data (catalog/hit-times.txt): when each attack lands (a projectile's release), how
        ///     long one swing lasts and how fast its projectile flies (0: no projectile), for an attack no blow has timed yet
        ///     (docs/research/dodge-timing.md).
        /// </summary>
        private readonly Dictionary<string, (int Hit, int Swing, int Speed)> fromFiles = new(StringComparer.Ordinal);

        /// <summary>Attacks the game marks as impossible to block (the red glow on a boss's wind-up), and those only impossible to parry.</summary>
        private readonly HashSet<string> unblockable = new(StringComparer.Ordinal);
        private readonly HashSet<string> unparryable = new(StringComparer.Ordinal);

        /// <summary>What the monster's skills make of each attack (a blow, a hit from afar, or nothing), where the tables say.</summary>
        private readonly Dictionary<string, DodgeThreat> threats = new(StringComparer.Ordinal);

        private const string NoBlockFlag = "noblock";
        private const string NoParryFlag = "noparry";
        private const string MeleeFlag = "melee";
        private const string RangedFlag = "ranged";
        private const string CalmFlag = "calm";

        /// <summary>"Metadata/Monsters/MudGolem/MudGolemWet1|13": the monster kind without its "@level" suffix.</summary>
        public static string Key(string monsterPath, int animation)
        {
            var at = monsterPath.IndexOf('@');
            return $"{(at > 0 ? monsterPath[..at] : monsterPath)}|{animation}";
        }

        /// <summary>When to warn for an attack that began at <paramref name="start"/>: the lead before its learnt hit, or at once.</summary>
        public static long WarnAt(long start, int? delayMs, int leadMs) =>
            delayMs is { } delay && delay - leadMs > 0 ? start + delay - leadMs : start;

        /// <summary>
        ///     How long a projectile takes to cross <paramref name="gapWorld"/> world units at <paramref name="speed"/> world
        ///     units a second (the files' ProjectileSpeed); 0 for an attack without a projectile. Its blow lands this long
        ///     after the release the files time.
        /// </summary>
        public static int TravelMs(float gapWorld, int? speed) =>
            speed is > 0 && gapWorld > 0 ? (int)Math.Round(gapWorld * MsPerSecond / speed.Value) : 0;

        /// <summary>
        ///     Whether an attack can still land: its animation runs (<paramref name="endedAt"/> 0), or it ended after the
        ///     swing's release (<paramref name="releaseAt"/>) let a projectile go that is still flying.
        /// </summary>
        public static bool CanStillHit(long endedAt, long? releaseAt, int travelMs, long now) =>
            endedAt == 0 || (travelMs > 0 && releaseAt is { } release && release <= endedAt && now - endedAt <= travelMs);

        /// <summary>Whether a learnt delay is believed: inside the learnable range, and near the files' time when they give one (a projectile of unknown speed may land later, never sooner).</summary>
        public bool Believable(string key, int delayMs)
        {
            if (delayMs is < MinDelayMs or > MaxDelayMs)
            {
                return false;
            }

            if (!this.fromFiles.TryGetValue(key, out var timed))
            {
                return true;
            }

            var tolerance = Math.Max(LearnToleranceMs, (int)(timed.Hit * LearnToleranceShare));
            var flightUnknown = timed.Speed == 0 && this.Threat(key) == DodgeThreat.Ranged;
            return delayMs >= timed.Hit - tolerance && (flightUnknown || delayMs <= timed.Hit + tolerance);
        }

        /// <summary>Keeps a blow's delay; false when it is not believed (<see cref="Believable"/>) and nothing was learnt.</summary>
        public bool Learn(string key, int delayMs)
        {
            if (!this.Believable(key, delayMs))
            {
                return false;
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
            return true;
        }

        /// <summary>
        ///     The median of the believed learnt delays (blows learnt before the files were loaded, or before this rule, are
        ///     judged again here); else the one the game's files give; null when neither knows it.
        /// </summary>
        public int? Delay(string key)
        {
            var believed = this.delays.TryGetValue(key, out var list) ? list.Where(value => this.Believable(key, value)).OrderBy(value => value).ToList() : new List<int>();
            if (believed.Count == 0)
            {
                return this.fromFiles.TryGetValue(key, out var timed) ? timed.Hit : null;
            }

            return believed[believed.Count / 2];
        }

        /// <summary>The files' own time of the attack's blow (a projectile's release), learnt blows aside; null when they do not time it.</summary>
        public int? FileHit(string key) => this.fromFiles.TryGetValue(key, out var timed) ? timed.Hit : null;

        /// <summary>How many attacks the game's files time.</summary>
        public int FileTimes => this.fromFiles.Count;

        /// <summary>How long one swing of the attack lasts (it repeats while the animation holds), when the files say.</summary>
        public int? Swing(string key) => this.fromFiles.TryGetValue(key, out var timed) && timed.Swing > 0 ? timed.Swing : null;

        /// <summary>How fast the attack's projectile flies (world units a second), when the files say it fires one.</summary>
        public int? ProjectileSpeed(string key) => this.fromFiles.TryGetValue(key, out var timed) && timed.Speed > 0 ? timed.Speed : null;

        /// <summary>Whether the player can block the attack: false when the game's files say it cannot be blocked.</summary>
        public bool CanBlock(string key) => !this.unblockable.Contains(key);

        /// <summary>Whether the player can parry the attack: a parry is a block, and some attacks only refuse the parry.</summary>
        public bool CanParry(string key) => this.CanBlock(key) && !this.unparryable.Contains(key);

        /// <summary>The kind of threat the game's tables give the attack; null where they are mute and the animation's name must say.</summary>
        public DodgeThreat? Threat(string key) => this.threats.TryGetValue(key, out var threat) ? threat : null;

        /// <summary>How many attacks the tables give a kind.</summary>
        public int FileKinds => this.threats.Count;

        /// <summary>
        ///     Lines "key=hit/swing[/speed][;kind,flags]" (catalog/hit-times.txt; "-" for an untimed attack with a kind or
        ///     flags); "#" lines are comments. A hit later than a blow is learnt is still believed within its own swing (long
        ///     boss attacks).
        /// </summary>
        public void AddHitTimes(IEnumerable<string> lines)
        {
            foreach (var entry in lines)
            {
                var flagsAt = entry.IndexOf(';');
                var line = flagsAt > 0 ? entry[..flagsAt] : entry;
                var split = line.LastIndexOf('=');
                var flags = flagsAt > 0 && split > 0 ? entry[(flagsAt + 1)..].Split(',') : Array.Empty<string>();
                if (flags.Contains(NoBlockFlag))
                {
                    this.unblockable.Add(line[..split]);
                }

                if (flags.Contains(NoParryFlag))
                {
                    this.unparryable.Add(line[..split]);
                }

                if (flags.Contains(RangedFlag))
                {
                    this.threats[line[..split]] = DodgeThreat.Ranged;
                }
                else if (flags.Contains(MeleeFlag))
                {
                    this.threats[line[..split]] = DodgeThreat.Melee;
                }
                else if (flags.Contains(CalmFlag))
                {
                    this.threats[line[..split]] = DodgeThreat.None;
                }

                var parts = split > 0 ? line[(split + 1)..].Split('/') : Array.Empty<string>();
                if (line.StartsWith('#') || parts.Length is < 2 or > 3 ||
                    !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hit) ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var swing) ||
                    hit < MinDelayMs || hit > Math.Max(MaxDelayMs, swing))
                {
                    continue;
                }

                var speed = 0;
                if (parts.Length == 3 && !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out speed))
                {
                    continue;
                }

                this.fromFiles[line[..split]] = (hit, swing, speed);
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
