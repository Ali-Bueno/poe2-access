namespace Poe2Accessibility.Services
{
    /// <summary>How long after attach no game state read means the states are lost. Pure.</summary>
    public static class StatesWait
    {
        /// <summary>
        ///     Attached this long without ever reading a game state: GameHelper's byte patterns or state offsets no longer
        ///     fit the game (a patch); the game reaches its login screen well within it, even compiling shaders.
        /// </summary>
        public const int UnreadMs = 120_000;

        /// <summary>
        ///     While the game has made no state object at all (the host knows where it will be and reads nothing there) the two
        ///     minutes prove nothing: a client that patches itself before its login screen takes longer. Said all the same after
        ///     this long, in case the place is the wrong one and holds nothing for good.
        /// </summary>
        public const int AwaitedMs = 600_000;

        /// <summary>Whether no state read for <paramref name="elapsedMs"/> since attach means the states are lost (see the two limits).</summary>
        public static bool Overdue(long elapsedMs, bool awaitsStateObject) => elapsedMs > (awaitsStateObject ? AwaitedMs : UnreadMs);
    }
}
