namespace Poe2Accessibility.Services
{
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>What the shop's item page shows on and around its buy button; empty where a label is hidden.</summary>
    /// <param name="Item">The page's title.</param>
    /// <param name="Caption">The button's own text: the price, or the game's "Confirmar" once pressed.</param>
    /// <param name="ConfirmTip">The line over the pressed button ("Confirma tu compra de 150 puntos").</param>
    /// <param name="PointsTip">The button's line for too few points.</param>
    /// <param name="OldPrice">The struck-through price of a discounted item.</param>
    /// <param name="Owned">The item card's "- Ya lo tienes -".</param>
    /// <param name="Balance">The account's points.</param>
    public readonly record struct ShopOffer(string Item, string Caption, string ConfirmTip, string PointsTip, string OldPrice, string Owned, string Balance);

    /// <summary>The words for the shop's buy button, a card's flags and the account's points (docs/research/shop.md). Pure.</summary>
    public static class ShopPurchase
    {
        /// <summary>Points as the shop writes them: "150", "1.250", a range of variants "200-230".</summary>
        private static readonly Regex Amount = new(@"^\d[\d.,]*(-\d[\d.,]*)?$", RegexOptions.Compiled);

        private static readonly Regex VariantCount = new(@"^x(\d+)$", RegexOptions.Compiled);

        public static bool IsAmount(string text) => Amount.IsMatch(text);

        /// <summary>
        ///     The buy button, which draws a coin and a number: what it buys, the price and the points left to spend; once
        ///     pressed, the game's own caption and prompt with the item's name.
        /// </summary>
        public static (string Label, string Value) Button(in ShopOffer offer)
        {
            var balance = IsAmount(offer.Balance) ? Announcements.ShopBalance(offer.Balance) : string.Empty;
            if (offer.ConfirmTip.Length > 0 && offer.Caption.Length > 0)
            {
                return (offer.Caption, Join(offer.ConfirmTip, offer.Item, offer.PointsTip, balance));
            }

            var price = IsAmount(offer.Caption) ? Announcements.ShopPrice(offer.Caption) : offer.Caption;
            var was = IsAmount(offer.OldPrice) ? Announcements.ShopOldPrice(offer.OldPrice) : string.Empty;
            return (Announcements.ShopBuy(offer.Item), Join(price, was, offer.ConfirmTip, Flag(offer.Owned), offer.PointsTip, balance));
        }

        /// <summary>A card's line besides its name and price: "x3" is its count of variants, "- Ya lo tienes -" loses its dashes.</summary>
        public static string CardLine(string controlName, string text) =>
            controlName == GameLayout.InGameUi.ShopVariantCountLabel && VariantCount.Match(text) is { Success: true } count
                ? Announcements.ShopVariants(count.Groups[1].Value)
                : Flag(text);

        /// <summary>A flag the shop frames with dashes ("- Ya lo tienes -").</summary>
        public static string Flag(string text) => text.Trim(' ', '-');

        private static string Join(params string[] parts) => string.Join(", ", parts.Where(part => part.Length > 0));
    }
}
