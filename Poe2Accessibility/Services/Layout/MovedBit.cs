namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     When a flag bit found elsewhere than measured is believed: one round of elements can all be of one odd kind that
    ///     carries another bit (twelve passive tree nodes carried 0x12 and not the navigable 0xE, and the bit was moved on a
    ///     healthy game, 2026-11-10), so a second round of other elements must name the same bit. Pure.
    /// </summary>
    public sealed class MovedBit
    {
        private int? candidate;
        private long namedBy;

        /// <summary>
        ///     <paramref name="witness"/> (a round, a screen) found <paramref name="bit"/> in place of the measured one; true once
        ///     a second, different witness has named the same bit.
        /// </summary>
        public bool Confirms(int bit, long witness)
        {
            if (this.candidate == bit && witness != this.namedBy)
            {
                return true;
            }

            (this.candidate, this.namedBy) = (bit, witness);
            return false;
        }

        /// <summary>The measured bit fitted after all, or a new game process began: what was named no longer counts.</summary>
        public void Clear() => this.candidate = null;
    }
}
