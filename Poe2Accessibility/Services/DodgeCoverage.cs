namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     How much of a monster's attacks the game's data describes (catalog/hit-times.txt): attacks with a kind from its
    ///     skills, attacks with a hit time, and attacks the data is mute about, which the mod reads from the animation's
    ///     name. Pure; the per-patch news lists it for each new monster (<see cref="GameNews"/>).
    /// </summary>
    public sealed class DodgeCoverage
    {
        /// <summary>The mark the game puts in the variety path of its unique monsters ("Metadata/Monsters/SwollenMiller/SwollenMillerBoss").</summary>
        public const string BossMark = "Boss";

        private const char Comment = '#';
        private const char KeySeparator = '|';
        private const char FlagSeparator = ';';
        private const char FlagListSeparator = ',';
        private const string Untimed = "-";

        private static readonly string[] KindTokens = { "melee", "ranged", "calm" };

        private readonly Dictionary<string, Counts> monsters = new(StringComparer.Ordinal);

        /// <summary>A monster's attacks in the file: how many, how many the data gives a kind, a hit time, or calls calm.</summary>
        public sealed record Counts(int Attacks, int Kinded, int Timed, int Calm)
        {
            /// <summary>Attacks whose kind only the animation's name gives.</summary>
            public int ByName => this.Attacks - this.Kinded - this.Calm;

            /// <summary>"7 attacks: 5 timed, 6 kinds from the data, 1 from the name" for the news file.</summary>
            public override string ToString() =>
                $"{this.Attacks} attacks: {this.Timed} timed, {this.Kinded} kinds from the data{(this.Calm > 0 ? $", {this.Calm} calm" : string.Empty)}{(this.ByName > 0 ? $", {this.ByName} from the name" : string.Empty)}";
        }

        public static bool IsBoss(string monsterPath) => monsterPath.Contains(BossMark, StringComparison.OrdinalIgnoreCase);

        /// <summary>From hit-times.txt lines ("monster|animation=hit/swing[/speed][;kind,flags]").</summary>
        public static DodgeCoverage Parse(IEnumerable<string> lines)
        {
            var coverage = new DodgeCoverage();
            foreach (var line in lines)
            {
                var keyEnd = line.IndexOf('=');
                var monsterEnd = line.IndexOf(KeySeparator);
                if (line.Length == 0 || line[0] == Comment || keyEnd <= 0 || monsterEnd <= 0 || monsterEnd > keyEnd)
                {
                    continue;
                }

                var flagsAt = line.IndexOf(FlagSeparator, keyEnd);
                var value = flagsAt > 0 ? line[(keyEnd + 1)..flagsAt] : line[(keyEnd + 1)..];
                var flags = flagsAt > 0 ? line[(flagsAt + 1)..].Split(FlagListSeparator) : Array.Empty<string>();
                var calm = Array.IndexOf(flags, KindTokens[2]) >= 0;
                var kinded = !calm && flags.Any(flag => Array.IndexOf(KindTokens, flag) >= 0);
                var monster = line[..monsterEnd];
                var before = coverage.monsters.GetValueOrDefault(monster) ?? new Counts(0, 0, 0, 0);
                coverage.monsters[monster] = new Counts(before.Attacks + 1, before.Kinded + (kinded ? 1 : 0), before.Timed + (value != Untimed ? 1 : 0), before.Calm + (calm ? 1 : 0));
            }

            return coverage;
        }

        /// <summary>The counts of a monster variety (its path without "@level"); null when the file has none of its attacks.</summary>
        public Counts? Of(string monsterPath)
        {
            var at = monsterPath.IndexOf('@');
            return this.monsters.GetValueOrDefault(at > 0 ? monsterPath[..at] : monsterPath);
        }

        public int Monsters => this.monsters.Count;
    }
}
