namespace Poe2Accessibility.Services
{
    /// <summary>What the stat key check has found so far.</summary>
    public enum StatKeyVerdict
    {
        /// <summary>Nothing to judge on yet, or one reading that missed: looked at again.</summary>
        Pending,

        /// <summary>The <c>level</c> stat's key reads the character's level: a stat's key is its row plus one.</summary>
        Holds,

        /// <summary>Two readings running read something else there: the keys the catalog gives are not the game's.</summary>
        Lost,
    }

    /// <summary>
    ///     Checks the one rule no file can: a stat's key in memory is its row in the Stats table plus one (GameIds,
    ///     `Poe2UiExtract game-ids`). Stats row 0 is <c>level</c>, and the character's level is known from its Player component,
    ///     so the key 1 must read it. Judged once per game process; said lost only on two readings in a row. Pure.
    /// </summary>
    public sealed class StatKeyCheck
    {
        /// <summary>The <c>level</c> stat's key under the rule: Stats row 0 (the extractor refuses a table that starts otherwise) plus one.</summary>
        public const int LevelKey = 1;

        /// <summary>A character lists hundreds of stats (250 on a level 22 Mercenary): under this many, the list is not read yet.</summary>
        public const int MinStats = 8;

        /// <summary>No character is past this level; a larger number is no level to compare with.</summary>
        public const int MaxLevel = 100;

        /// <summary>A new character's level: its list holds no <c>level</c> stat yet (74 stats, the attributes at their keys, 2026-11-11).</summary>
        public const int FirstLevel = 1;

        private const int MissesToLose = 2;

        private int misses;

        /// <summary>Gets the verdict; once it holds or is lost nothing more is looked at.</summary>
        public StatKeyVerdict Verdict { get; private set; } = StatKeyVerdict.Pending;

        /// <summary>
        ///     One reading: the character's <paramref name="level"/>, what the <c>level</c> key reads among its stats (0 when
        ///     the list holds no such key) and how many stats the list holds. True when the verdict was reached by it.
        /// </summary>
        public bool Observe(int level, int read, int stats)
        {
            if (this.Verdict != StatKeyVerdict.Pending || level is <= 0 or > MaxLevel || stats < MinStats || (level == FirstLevel && read == 0))
            {
                return false;
            }

            if (read == level)
            {
                this.Verdict = StatKeyVerdict.Holds;
                return true;
            }

            if (++this.misses < MissesToLose)
            {
                return false;
            }

            this.Verdict = StatKeyVerdict.Lost;
            return true;
        }

        /// <summary>A new game process: judged again.</summary>
        public void Reset() => (this.Verdict, this.misses) = (StatKeyVerdict.Pending, 0);
    }
}
