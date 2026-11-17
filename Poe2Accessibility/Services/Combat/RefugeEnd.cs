namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     When a fight's shelter is no longer needed (Asala's barrier drops as Jamanra's sandstorm ends,
    ///     docs/research/boss-mechanics.md §13): the shelter seen up, then down for <see cref="GoneMs"/>, so one missed read
    ///     never sends the player out in the middle of the storm. Pure.
    /// </summary>
    public sealed class RefugeEnd
    {
        /// <summary>Down this long (ms), the shelter is over: two polls of <see cref="BossPlacesWatch"/>.</summary>
        public const int GoneMs = 600;

        private bool up;
        private long seenAt;

        public void Reset() => this.up = false;

        /// <summary>Whether the shelter has just ended, once per shelter.</summary>
        public bool Update(bool refugeUp, long now)
        {
            if (refugeUp)
            {
                this.up = true;
                this.seenAt = now;
                return false;
            }

            if (!this.up || now - this.seenAt < GoneMs)
            {
                return false;
            }

            this.up = false;
            return true;
        }
    }
}
