namespace Poe2Accessibility.Services
{
    /// <summary>A points label on the shop's bar (the account's, the guild's): said when the shop shows it and whenever it changes (a purchase).</summary>
    public sealed class ShopBalanceWatch
    {
        /// <summary>A bar gone this long is a closed shop; a page being rebuilt hides it for less.</summary>
        private const long ForgetMs = 3000;

        private readonly long settleMs;
        private string last = string.Empty;
        private string pending = string.Empty;
        private long pendingAt;
        private long seenAt;

        /// <param name="settleMs">How long a value must have shown before it is said: a label that comes with its page waits for the page's own line.</param>
        public ShopBalanceWatch(long settleMs = 0) => this.settleMs = settleMs;

        /// <returns>The points to say now, or null.</returns>
        /// <param name="shown">The points label's text, null while no shop page shows one.</param>
        /// <param name="now">Milliseconds on any steady clock.</param>
        public string? Heard(string? shown, long now)
        {
            if (shown == null || !ShopPurchase.IsAmount(shown))
            {
                this.pending = string.Empty;
                if (now - this.seenAt > ForgetMs)
                {
                    this.last = string.Empty;
                }

                return null;
            }

            this.seenAt = now;
            if (shown == this.last)
            {
                return null;
            }

            if (shown != this.pending)
            {
                (this.pending, this.pendingAt) = (shown, now);
            }

            if (now - this.pendingAt < this.settleMs)
            {
                return null;
            }

            this.last = shown;
            return shown;
        }
    }
}
