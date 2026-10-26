namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>A change in whether the player holds the shield up.</summary>
    public enum ShieldChange
    {
        None,
        Raised,
        Lowered,
    }

    /// <summary>
    ///     Whether the player holds the shield up (Raise Shield, the game's "active block"), from the player's animation
    ///     name: ActiveBlockChannel (or ...Moving) while held, ActiveBlockEnd (or ...Moving) as it comes down (game
    ///     Animation table rows 0x1DF-0x1E2). Pure.
    /// </summary>
    public sealed class ShieldStance
    {
        /// <summary>Another animation this long while held means the shield came down without its end (stunned, dodged).</summary>
        public const int LostMs = 300;

        private const string HeldPrefix = "ActiveBlockChannel";
        private const string EndPrefix = "ActiveBlockEnd";

        private bool raised;
        private long otherSince = -1;

        /// <summary>The player's animation now; a raise is told at once, a lowering at its end or once the shield is gone a moment.</summary>
        public ShieldChange Update(string? animation, long nowMs)
        {
            if (animation?.StartsWith(HeldPrefix, StringComparison.Ordinal) == true)
            {
                this.otherSince = -1;
                if (this.raised)
                {
                    return ShieldChange.None;
                }

                this.raised = true;
                return ShieldChange.Raised;
            }

            if (!this.raised)
            {
                return ShieldChange.None;
            }

            // A blocked hit's reaction flickers in while the shield stays up: only its end, or a lasting change, lowers it.
            this.otherSince = this.otherSince < 0 ? nowMs : this.otherSince;
            if (animation?.StartsWith(EndPrefix, StringComparison.Ordinal) != true && nowMs - this.otherSince < LostMs)
            {
                return ShieldChange.None;
            }

            this.Reset();
            return ShieldChange.Lowered;
        }

        /// <summary>A new area, death or another character: the shield is down and nothing is said for it.</summary>
        public void Reset()
        {
            this.raised = false;
            this.otherSince = -1;
        }
    }
}
