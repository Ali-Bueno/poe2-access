namespace GameReader.Utils
{
    using System;
    using GameOffsets.Objects.Components;

    /// <summary>One stat looked for in a stat list of the game, without a dictionary of the whole list.</summary>
    public static class StatPairs
    {
        /// <summary>
        ///     The value of <paramref name="key"/> as a dictionary filled from <paramref name="pairs"/> in order would hold it:
        ///     the last pair with that key; false when no pair has it.
        /// </summary>
        public static bool TryFind(ReadOnlySpan<StatArrayStruct> pairs, int key, out int value)
        {
            value = 0;
            var found = false;
            foreach (var pair in pairs)
            {
                if (pair.key == key)
                {
                    value = pair.value;
                    found = true;
                }
            }

            return found;
        }
    }
}
