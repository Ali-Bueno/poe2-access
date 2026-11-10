namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>
    ///     Sets what a file catalog says beside what the game's memory reads for the same things, on the first few different
    ///     ones read both ways, and gives one verdict when enough were compared: the two sources back each other, so a
    ///     disagreement is worth one log line, not one per reading. Pure.
    /// </summary>
    public sealed class CatalogCrossCheck
    {
        private readonly int needed;
        private readonly HashSet<string> seen = new(System.StringComparer.Ordinal);
        private int disagreeing;
        private string first = string.Empty;

        /// <param name="needed">How many different things are compared before the verdict.</param>
        public CatalogCrossCheck(int needed) => this.needed = needed;

        /// <summary>Gets a value indicating whether the verdict was given: nothing more is compared.</summary>
        public bool Done { get; private set; }

        /// <summary>Whether this thing is still to be compared: not once the verdict is given, and each thing once.</summary>
        public bool Wants(string key) => !this.Done && !this.seen.Contains(key);

        /// <summary>
        ///     One thing read both ways; <paramref name="difference"/> says how they differ, null when they agree. The verdict
        ///     on the comparison that completes it, null before and after.
        /// </summary>
        public Verdict? Note(string key, string? difference)
        {
            if (this.Done || !this.seen.Add(key))
            {
                return null;
            }

            if (difference != null)
            {
                this.disagreeing++;
                this.first = this.first.Length > 0 ? this.first : $"{key}: {difference}";
            }

            if (this.seen.Count < this.needed)
            {
                return null;
            }

            this.Done = true;
            return new Verdict(this.seen.Count, this.disagreeing, this.first);
        }

        /// <summary>A new game process: compared again.</summary>
        public void Reset()
        {
            this.seen.Clear();
            (this.disagreeing, this.first, this.Done) = (0, string.Empty, false);
        }

        /// <summary>How many were compared, how many differed, and the first that did.</summary>
        public readonly record struct Verdict(int Compared, int Disagreeing, string First)
        {
            /// <summary>Gets a value indicating whether most differ: one source reads something else than the other, not a renamed thing or two.</summary>
            public bool MostDisagree => this.Disagreeing * 2 > this.Compared;
        }
    }
}
