namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Whether an object under a minimap mark says, by its StateMachine, that what it was for is done. The Azak Bog's
    ///     central effigy keeps its quest icon after the Flameskin Ritual, only `burned=1` tells (dev `entities BurningMan`,
    ///     2026-11-19). Pure.
    /// </summary>
    public static class MarkDone
    {
        /// <summary>State names that, once set, mean the object was used: one at a time, as the game's files name them.</summary>
        private static readonly string[] DoneStates = { "burned" };

        public static bool Is(IEnumerable<(string Name, long Value)> states) =>
            states.Any(state => state.Value != 0 && Array.Exists(DoneStates, done => string.Equals(done, state.Name, StringComparison.OrdinalIgnoreCase)));
    }
}
