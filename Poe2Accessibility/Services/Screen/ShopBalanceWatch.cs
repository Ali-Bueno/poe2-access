namespace Poe2Accessibility.Services
{
    /// <summary>The account's points on the shop's bar: said when the shop shows them and whenever they change (a purchase).</summary>
    public sealed class ShopBalanceWatch
    {
        /// <summary>A bar gone this long is a closed shop; a page being rebuilt hides it for less.</summary>
        private const long ForgetMs = 3000;

        private string last = string.Empty;
        private long seenAt;

        /// <returns>The points to say now, or null.</returns>
        /// <param name="shown">The points label's text, null while no shop page shows one.</param>
        /// <param name="now">Milliseconds on any steady clock.</param>
        public string? Heard(string? shown, long now)
        {
            if (shown == null || !ShopPurchase.IsAmount(shown))
            {
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

            this.last = shown;
            return shown;
        }
    }
}
