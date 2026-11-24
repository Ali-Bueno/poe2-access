namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>What is left to spend, by a window's own label (the shop's points, the guild's, a ritual's tribute): said when the window shows it and whenever it changes.</summary>
    public sealed class ShopBalanceWatch
    {
        /// <summary>A bar gone this long is a closed shop; a page being rebuilt hides it for less.</summary>
        private const long ForgetMs = 3000;

        private readonly long settleMs;
        private readonly long changeMs;
        private readonly long forgetMs;
        private readonly Func<string, bool> isBalance;
        private string last = string.Empty;
        private string pending = string.Empty;
        private long pendingAt;
        private long seenAt;

        /// <param name="settleMs">How long a value must have shown before it is said: a label that comes with its page waits for the page's own line.</param>
        /// <param name="changeMs">The same for a value that replaces one already said; as the first when not given.</param>
        /// <param name="forgetMs">How long the label must be gone for the window to count as closed.</param>
        /// <param name="isBalance">Whether a text of the label is a balance; a bare amount when not given.</param>
        public ShopBalanceWatch(long settleMs = 0, long? changeMs = null, long forgetMs = ForgetMs, Func<string, bool>? isBalance = null)
        {
            this.settleMs = settleMs;
            this.changeMs = changeMs ?? settleMs;
            this.forgetMs = forgetMs;
            this.isBalance = isBalance ?? ShopPurchase.IsAmount;
        }

        /// <summary>The balance was said elsewhere (with the focus that came with the window): only a change of it is news.</summary>
        public void Told(string shown, long now) => (this.last, this.pending, this.seenAt) = (shown, shown, now);

        /// <returns>The points to say now, or null.</returns>
        /// <param name="shown">The points label's text, null while no shop page shows one.</param>
        /// <param name="now">Milliseconds on any steady clock.</param>
        public string? Heard(string? shown, long now)
        {
            if (shown == null || !this.isBalance(shown))
            {
                this.pending = string.Empty;
                if (now - this.seenAt > this.forgetMs)
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

            if (now - this.pendingAt < (this.last.Length == 0 ? this.settleMs : this.changeMs))
            {
                return null;
            }

            this.last = shown;
            return shown;
        }
    }
}
