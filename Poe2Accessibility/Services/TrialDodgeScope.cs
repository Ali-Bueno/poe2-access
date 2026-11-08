namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     Whether every monster's attacks are warned of: the setting, or inside a trial while its own switch is on (every hit
    ///     costs the Sekhemas' honour). A trial is known by its HUD, the game's own lines: honour, or a Trial of Chaos round. Pure.
    /// </summary>
    public sealed class TrialDodgeScope
    {
        /// <summary>A HUD line missed for a moment (a panel over the HUD) does not leave the trial.</summary>
        public const int HoldMs = 3000;

        private long seenAt = long.MinValue / 2;

        /// <summary>For every monster: always, or in a trial when that switch is on; back to the setting alone on leaving.</summary>
        public static bool AllMonsters(bool always, bool inTrials, bool inTrial) => always || (inTrials && inTrial);

        /// <summary>One frame's look at the HUD.</summary>
        public void Saw(bool trialShown, long now)
        {
            if (trialShown)
            {
                this.seenAt = now;
            }
        }

        public bool InTrial(long now) => now - this.seenAt <= HoldMs;

        /// <summary>A new area: whatever trial it was is over.</summary>
        public void Reset() => this.seenAt = long.MinValue / 2;
    }
}
