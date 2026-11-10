namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Whether the controls' enabled bit can be believed, judged screen by screen: nothing tells the bit apart, but on a
    ///     screen of the game most controls are enabled. One screen with few of them is no verdict (a login that is still
    ///     connecting greys every button): two different screens must agree before the bit counts as lost, and any screen
    ///     where it reads as it should gives it back. Pure.
    /// </summary>
    public sealed class EnabledBitCheck
    {
        /// <summary>Below this share of enabled controls a screen speaks against the bit.</summary>
        public const double LostShare = 0.2;

        /// <summary>Different screens that must speak against the bit before it is lost.</summary>
        public const int ScreensToLose = 2;

        private readonly HashSet<long> against = new();

        /// <summary>Gets a value indicating whether the bit is believed: until two screens speak against it, and again once one speaks for it.</summary>
        public bool Trusted { get; private set; } = true;

        /// <summary>Gets a value indicating whether a screen has shown the bit working: nothing more needs judging.</summary>
        public bool Proved { get; private set; }

        /// <summary>
        ///     One screen (<paramref name="screen"/> tells it from the others) with <paramref name="share"/> of its controls
        ///     enabled; true when the verdict changed or was first reached.
        /// </summary>
        public bool Observe(long screen, double share)
        {
            if (share >= LostShare)
            {
                var news = !this.Proved || !this.Trusted;
                this.against.Clear();
                (this.Trusted, this.Proved) = (true, true);
                return news;
            }

            if (this.Proved || !this.against.Add(screen) || this.against.Count < ScreensToLose || !this.Trusted)
            {
                return false;
            }

            this.Trusted = false;
            return true;
        }

        public void Reset()
        {
            this.against.Clear();
            (this.Trusted, this.Proved) = (true, false);
        }
    }
}
