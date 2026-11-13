namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Holds back the interact prompt of a breakable that has the name of the one just said, so a row of pots says it once.</summary>
    public sealed class BreakablePrompts
    {
        /// <summary>
        ///     Walking along a row of pots takes the game's target from one to the next every second or so; a prompt for the same
        ///     name inside this window adds nothing and cut the pick-ups being said ("Tiesto, A", 90 times in 10 s windows).
        /// </summary>
        public const int SameNameMs = 5000;

        private string? lastName;
        private bool lastBreakable;
        private long lastAt;

        /// <summary>True when this prompt repeats the last one said; otherwise it is noted as the last said.</summary>
        public bool Repeats(bool breakable, string name, long now)
        {
            var repeat = breakable && this.lastBreakable && string.Equals(name, this.lastName, StringComparison.Ordinal) && now - this.lastAt < SameNameMs;
            if (!repeat)
            {
                this.lastName = name;
                this.lastBreakable = breakable;
                this.lastAt = now;
            }

            return repeat;
        }

        /// <summary>A new area: the first of its rows is said.</summary>
        public void Reset() => this.lastName = null;
    }
}
