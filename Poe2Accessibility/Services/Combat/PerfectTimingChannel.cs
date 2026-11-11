namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>What a frame's animation says of a Perfect Timing channel.</summary>
    public enum PerfectTimingChange
    {
        None,
        Began,

        /// <summary>Released inside the window (the animation's perfect end).</summary>
        Perfect,

        /// <summary>Released outside it (the plain end).</summary>
        Plain,

        /// <summary>The channel ended without either end (stunned, dodged, the skill let go at once).</summary>
        Dropped,
    }

    /// <summary>
    ///     A Perfect Timing channel by the player's animation name alone: the channel's own animation while held, then its
    ///     plain or its perfect end (game Animation table: PerfectStrike / PerfectStrikeEnd / PerfectStrikePerfectEnd,
    ///     SnipeChannel / SnipeChannelEnd / SnipeChannelEndPerfect, GatheringStorm / GatheringStormEnd /
    ///     GatheringStormPerfectEnd). Pure.
    /// </summary>
    public sealed class PerfectTimingChannel
    {
        /// <summary>The channels' animations; public for the data step's check that a patch kept them (<see cref="GameNames"/>).</summary>
        public static readonly string[] Channels = { "PerfectStrike", "SnipeChannel", "GatheringStorm" };

        private const string End = "End";
        private const string Perfect = "Perfect";

        private string? held;

        /// <summary>When the channel being held began.</summary>
        public long Since { get; private set; }

        /// <summary>When the channel the last change ended had begun.</summary>
        public long EndedSince { get; private set; }

        public static bool IsChannel(string? animation) => animation != null && Array.IndexOf(Channels, animation) >= 0;

        public void Reset() => this.held = null;

        public PerfectTimingChange Update(string? animation, long nowMs)
        {
            if (animation == this.held)
            {
                return PerfectTimingChange.None;
            }

            var change = PerfectTimingChange.None;
            if (this.held is { } channel)
            {
                // "…PerfectEnd" or "…EndPerfect": the perfect end holds both words, the plain one only "End".
                var rest = animation?.StartsWith(channel, StringComparison.Ordinal) == true ? animation[channel.Length..] : null;
                change = rest == null ? PerfectTimingChange.Dropped
                    : rest.Contains(Perfect, StringComparison.Ordinal) && rest.Contains(End, StringComparison.Ordinal) ? PerfectTimingChange.Perfect
                    : rest == End ? PerfectTimingChange.Plain
                    : PerfectTimingChange.Dropped;
                this.EndedSince = this.Since;
                this.held = null;
            }

            if (IsChannel(animation))
            {
                this.held = animation;
                this.Since = nowMs;
                return change == PerfectTimingChange.None ? PerfectTimingChange.Began : change; // a channel started straight after an end
            }

            return change;
        }
    }
}
