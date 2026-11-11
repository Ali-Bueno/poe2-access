namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Where a Perfect Timing channel's window opens, by its clip's own events: a release from "leeway" on was perfect,
    ///     one held to "release" was not (Perfect Strike, 2026-11-11; docs/research/perfect-timing.md). Pure.
    /// </summary>
    public static class PerfectTimingWindow
    {
        /// <summary>The clip event from which a release is perfect: a little before the last stage, the game's own slack.</summary>
        public const string Leeway = "leeway";

        /// <summary>The channel's last stage, for a clip without a leeway.</summary>
        public const string MaxStage = "max_stage";

        /// <summary>The clip time (seconds) the window opens at; null for a clip that marks none.</summary>
        public static float? Opens(IReadOnlyList<ClipEvent> events) => First(events, Leeway) ?? First(events, MaxStage);

        /// <summary>Whether the clip went past the opening between two readings of one channel: told as it happens, never late.</summary>
        public static bool Crossed(float? opens, float before, float now) => opens is { } at && before < at && now >= at;

        private static float? First(IReadOnlyList<ClipEvent> events, string name)
        {
            float? first = null;
            foreach (var clipEvent in events)
            {
                if (clipEvent.Name == name && (first == null || clipEvent.Time < first))
                {
                    first = clipEvent.Time;
                }
            }

            return first;
        }
    }
}
