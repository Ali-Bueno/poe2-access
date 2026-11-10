namespace GameReader.Utils
{
    using System;

    /// <summary>The stack count of a buff the game holds as several entries of one name. Pure.</summary>
    public static class BuffStacks
    {
        /// <summary>What one entry holds: its own count, or one when it keeps none.</summary>
        public static int Of(short charges) => charges > 0 ? charges : 1;

        /// <summary>Two entries of one buff as one: each counts for what it holds, the first one included.</summary>
        public static short Merged(short held, short incoming) => (short)Math.Min(short.MaxValue, Of(held) + Of(incoming));
    }
}
