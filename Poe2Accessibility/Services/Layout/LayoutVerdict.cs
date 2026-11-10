namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     When a layout check's "not found" is a verdict and when it only came too early. The game's first UI root, in its
    ///     first frames (PreGameState), has no children yet: the element tree and flags read "lost" on it, and the player heard
    ///     "the interface's controls were not found, menus will stay silent" at a cold start while every menu then read
    ///     (2026-11-08, 13:27). A lost judgment waits for the next roots; only one still lost after a while is reported.
    /// </summary>
    public static class LayoutVerdict
    {
        /// <summary>How long a part may read lost, judged again on every new root, before it is reported missing.</summary>
        public const int LostWaitMs = 30000;

        /// <summary>The judgment can be kept: nothing lost, or still lost <see cref="LostWaitMs"/> after it first was.</summary>
        public static bool Settled(bool anyLost, long firstLostAt, long now) => !anyLost || now - firstLostAt >= LostWaitMs;
    }
}
