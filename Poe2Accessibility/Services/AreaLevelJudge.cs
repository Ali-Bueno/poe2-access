namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>
    ///     Whether the area level's field is really lost. One area proves nothing: it can be read before the game has
    ///     filled it, so only a second area missing it counts, and a level found once settles the matter.
    /// </summary>
    public sealed class AreaLevelJudge
    {
        private IntPtr firstMiss;

        /// <summary>Gets a value indicating whether the level has been found in this game process.</summary>
        public bool Seen { get; private set; }

        /// <summary>True once two different areas have missed the level and none has held it.</summary>
        public bool Lost(IntPtr area, bool found)
        {
            this.Seen |= found;
            if (this.Seen)
            {
                return false;
            }

            if (this.firstMiss == IntPtr.Zero)
            {
                this.firstMiss = area;
            }

            return area != this.firstMiss;
        }
    }
}
