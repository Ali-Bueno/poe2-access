namespace Poe2Accessibility
{
    /// <summary>Phrases of the panels not reached in play yet: inline pictures, art-only controls (docs/research/ui-unseen-panels.md).</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     The word an inline picture means, by its art's file stem (lower case; `InlineIcons`): the game's own icons
        ///     for gold, spirit, tribute, map portals and revives, a tick, a check box, a deferred favour. Null for any other.
        /// </summary>
        public static string? InlineIconWord(string artStem) => artStem switch
        {
            "marketicongold" => Gold,
            "killpointicon" => T("tribute", "tributo"),
            "spiriticon" => T("spirit", "espíritu"),
            "partyportalicon" => T("portals", "portales"),
            "partyrevivesicon" => T("revives", "resurrecciones"),
            "stashtickicon" => T("completed", "completado"),
            "checkboxchecked" => Checked,
            "checkboxunchecked" => Unchecked,
            "savedshopitemicon" => T("deferred", "reservado"),
            _ => null,
        };

        /// <summary>A table cell under its column heading, both in the game's words ("Jugadores: 3"; `TableHeadings`).</summary>
        public static string TableCell(string heading, string value) => T($"{heading}: {value}", $"{heading}: {value}");

        /// <summary>A banner of the shop's featured page (pictures only); A opens what it shows.</summary>
        public static string ShopFeatured => T("featured", "destacado");

        /// <summary>A shop item's price: the shop's own currency, points ("250 puntos").</summary>
        public static string ShopPrice(string price) => T($"{price} points", $"{price} puntos");

        /// <summary>The shop's buy button, which shows a coin and the price alone: what it buys ("Comprar Mascota Perro fantasma").</summary>
        public static string ShopBuy(string item) => item.Length > 0 ? T($"Buy {item}", $"Comprar {item}") : T("Buy", "Comprar");

        /// <summary>The price a discounted shop item had, struck through beside the new one.</summary>
        public static string ShopOldPrice(string price) => T($"was {price} points", $"antes {price} puntos");

        /// <summary>The account's points, shown top right in the shop.</summary>
        public static string ShopBalance(string points) => T($"you have {points} points", $"tienes {points} puntos");

        /// <summary>A shop card sold in several variants (the card's "x3").</summary>
        public static string ShopVariants(string count) => T($"{count} variants", $"{count} variantes");
    }
}
