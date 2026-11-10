namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;

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

        /// <summary>The token a timings line carries for an attack that moves its user (older readers skip it: not a number).</summary>
        private const string MovesToken = "moves";

        /// <summary>Attacks seen this many times carrying their user to the place they aim at are leaps (<see cref="Moves"/>).</summary>
        public const int MoveSamples = 2;

        /// <summary>A monster that moved less than this (world units) during its attack stayed where it struck from.</summary>
        public const float MoveMinWorld = 80f;

        /// <summary>
        ///     Attacks that carry their user to the place they aim at, and how often each was seen doing it. A leap is judged
        ///     by its reach from that place, not from where the monster stands: the urchins' Puncture (melee_launch_01,
        ///     UrchinLeapGeometryAttack) read "out of reach" until it had landed, then "blow in 64 ms" (Clearfell, 2026-11-08).
        ///     No table marks such skills (a leap is a plain geometry_attack: docs/research/dodge-timing.md), so the monsters' own
        ///     moves teach them.
        /// </summary>
        private readonly Dictionary<string, int> moves = new(StringComparer.Ordinal);

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
        ///     Notes an attack of <paramref name="key"/> that took its user from <paramref name="start"/> to <paramref name="end"/>
        ///     while it aimed at <paramref name="target"/>; true when that made it a leap (<see cref="Moves"/>).
        /// </summary>
        public bool LearnMove(string key, Vector2 start, Vector2 end, Vector2 target)
        {
            if (!MovedToward(start, end, target) || this.Moves(key))
            {
                return false;
            }

            this.moves[key] = this.moves.GetValueOrDefault(key) + 1;
            this.Changed |= this.Moves(key);
            return this.Moves(key);
        }

        /// <summary>The attack carries its user to the place it aims at: judge its reach from there.</summary>
        public bool Moves(string key) => this.moves.GetValueOrDefault(key) >= MoveSamples;

        /// <summary>
        ///     The monster went at least <see cref="MoveMinWorld"/> and most of the way it went brought it nearer the aimed place:
        ///     a leap or a charge, not a step aside or a knock-back.
        /// </summary>
        public static bool MovedToward(Vector2 start, Vector2 end, Vector2 target)
        {
            var went = Vector2.Distance(start, end);
            return went >= MoveMinWorld && Vector2.Distance(start, target) - Vector2.Distance(end, target) >= went / 2f;
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

        /// <summary>One line per attack: "key=delay,delay,…", with "moves" among the values of a leap.</summary>
        public IEnumerable<string> ToLines() =>
            this.delays.Keys.Concat(this.moves.Keys.Where(this.Moves)).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => $"{key}={string.Join(',', this.delays.GetValueOrDefault(key, new List<int>()).Select(value => value.ToString(CultureInfo.InvariantCulture)).Concat(this.Moves(key) ? new[] { MovesToken } : Array.Empty<string>()))}");

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
                    else if (part == MovesToken)
                    {
                        timings.moves[line[..split]] = MoveSamples;
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
        public static DodgeTimings Load(string seedPath, string path) => FromLines(AttackData.ReadLines(seedPath).Concat(AttackData.ReadLines(path)));

        /// <summary>As <see cref="Load(string, string)"/>, with the game files' hit times beneath what was measured.</summary>
        public static DodgeTimings Load(string hitTimesPath, string seedPath, string path)
        {
            var timings = Load(seedPath, path);
            timings.AddHitTimes(AttackData.ReadLines(hitTimesPath));
            return timings;
        }

        /// <summary>Writes the learnt timings when they changed; the reason when that failed (they stay in memory for the next try), else null.</summary>
        public string? Save(string path)
        {
            if (!this.Changed)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllLines(path, this.ToLines());
                this.Changed = false;
                return null;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return exception.Message;
            }
        }
    }
}
