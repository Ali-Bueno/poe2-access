namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The shop's buy button, card flags and points (<see cref="ShopPurchase"/>), on the texts seen live 2026-11-10.</summary>
    public static class ShopPurchaseTests
    {
        private const string Pet = "Mascota Perro fantasma";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheBuyButtonSaysWhatItBuysAndForHowMuch), TheBuyButtonSaysWhatItBuysAndForHowMuch),
            (nameof(PressedItAsksToConfirmWithTheItemsName), PressedItAsksToConfirmWithTheItemsName),
        };

        private static void TheBuyButtonSaysWhatItBuysAndForHowMuch()
        {
            Localization.Detect("es");
            try
            {
                Equal(("Comprar Mascota Perro fantasma", "150 puntos, tienes 300 puntos"),
                    ShopPurchase.Button(new ShopOffer(Pet, "150", string.Empty, string.Empty, string.Empty, string.Empty, "300")), "a coin and 150 on screen");
                Equal(("Comprar Mascota Perro fantasma", "150 puntos, Ya lo tienes, tienes 150 puntos"),
                    ShopPurchase.Button(new ShopOffer(Pet, "150", string.Empty, string.Empty, string.Empty, "- Ya lo tienes -", "150")), "bought: the page's card says so");
                Equal(("Comprar Mascota Perro fantasma", "150 puntos, antes 300 puntos, tienes 300 puntos"),
                    ShopPurchase.Button(new ShopOffer(Pet, "150", string.Empty, string.Empty, "300", string.Empty, "300")), "a struck-through price");
                // The tip's own words were never seen (the label was hidden): whatever it shows is said.
                Equal(("Comprar Mascota Perro fantasma", "150 puntos, Necesitas 50 puntos más, tienes 100 puntos"),
                    ShopPurchase.Button(new ShopOffer(Pet, "150", string.Empty, "Necesitas 50 puntos más", string.Empty, string.Empty, "100")), "too few points");
                Equal(("Comprar", "150 puntos"), ShopPurchase.Button(new ShopOffer(string.Empty, "150", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)),
                    "no title and no points found: the price alone, still a buy button");
                Localization.Detect("en");
                Equal(("Buy Mascota Perro fantasma", "150 points, you have 300 points"),
                    ShopPurchase.Button(new ShopOffer(Pet, "150", string.Empty, string.Empty, string.Empty, string.Empty, "300")), "in English");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void PressedItAsksToConfirmWithTheItemsName()
        {
            Localization.Detect("es");
            try
            {
                Equal(("Confirmar", "Confirma tu compra de 150 puntos, Mascota Perro fantasma, tienes 300 puntos"),
                    ShopPurchase.Button(new ShopOffer(Pet, "Confirmar", "Confirma tu compra de 150 puntos", string.Empty, string.Empty, string.Empty, "300")),
                    "the game's caption and prompt, never the Equip button under it");
            }
            finally
            {
                Localization.Detect("en");
            }
        }
    }
}
