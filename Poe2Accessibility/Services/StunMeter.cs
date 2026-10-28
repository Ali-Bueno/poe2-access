namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;

    /// <summary>
    ///     The player's heavy stun bar, which fills only while blocking with a raised shield, parrying or riding (KeywordPopups
    ///     "HeavyStun"): each step is said once on the way up and re-armed once the bar has fallen back below it by a
    ///     margin. Pure: a <see cref="VitalAlarm"/> on what is left before the stun.
    /// </summary>
    public sealed class StunMeter
    {
        private const int Full = 100;

        /// <summary>A bar more than this many times its threshold is a misread, not a buildup.</summary>
        private const int MaxOverfill = 4;

        private readonly VitalAlarm left;

        public StunMeter(int rearmMargin, params int[] steps) =>
            this.left = new VitalAlarm(rearmMargin, steps.Select(step => Full - step).ToArray());

        /// <summary>The buildup as a percent of the threshold (at most 100), or null for a reading that cannot be one.</summary>
        public static int? Percent(int buildup, int threshold) =>
            threshold > 0 && buildup >= 0 && buildup <= (long)threshold * MaxOverfill ? (int)Math.Min(Full, buildup * 100L / threshold) : null;

        /// <summary>The highest step the bar newly rose past (one call even for a big blow), or null.</summary>
        public int? Update(int percent) => Full - this.left.Update(Full - percent);

        /// <summary>A new area or character: every step is armed again.</summary>
        public void Reset() => this.left.Update(Full);
    }
}
