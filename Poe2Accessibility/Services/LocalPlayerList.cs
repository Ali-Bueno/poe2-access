namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>Reads the area's local player list (<see cref="GameLayout.LocalPlayers"/>). Pure.</summary>
    public static class LocalPlayerList
    {
        /// <summary>The second local player's entity address, trusted only when the first entry is GameHelper's player.</summary>
        public static IntPtr? Second(IReadOnlyList<IntPtr> entries, IntPtr self) =>
            entries.Count is >= 2 and <= GameLayout.LocalPlayers.MaxPlayers && self != IntPtr.Zero && entries[0] == self &&
            entries[1] != IntPtr.Zero && entries[1] != self
                ? entries[1]
                : null;
    }
}
