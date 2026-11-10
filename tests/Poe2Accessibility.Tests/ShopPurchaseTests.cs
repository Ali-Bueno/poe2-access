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
            (nameof(ACardsFlagsAreWords), ACardsFlagsAreWords),
            (nameof(ThePointsAreSaidWhenShownAndWhenTheyChange), ThePointsAreSaidWhenShownAndWhenTheyChange),
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

        private static void ACardsFlagsAreWords()
        {
            Localization.Detect("es");
            try
            {
                Equal("3 variantes", ShopPurchase.CardLine(GameLayout.InGameUi.ShopVariantCountLabel, "x3"), "the badge of an item sold in variants");
                Equal("Ya lo tienes", ShopPurchase.CardLine(GameLayout.InGameUi.ShopOwnedLabel, "- Ya lo tienes -"), "dashes are drawing");
                Equal("¡NUEVO!", ShopPurchase.CardLine("new_flag_label", "¡NUEVO!"), "any other line as shown");
                Equal("x3", ShopPurchase.CardLine("count_label", "x3"), "x3 elsewhere is not a count of variants");
            }
            finally
            {
                Localization.Detect("en");
            }

            Equal(true, ShopPurchase.IsAmount("150") && ShopPurchase.IsAmount("1.250") && ShopPurchase.IsAmount("200-230"), "points and a range of them");
            Equal(false, ShopPurchase.IsAmount("Confirmar") || ShopPurchase.IsAmount(string.Empty) || ShopPurchase.IsAmount("27/27"), "a caption and a count are no price");
        }

        private static void ThePointsAreSaidWhenShownAndWhenTheyChange()
        {
            var watch = new ShopBalanceWatch();
            Equal(null, watch.Heard(null, 0), "no shop");
            Equal("300", watch.Heard("300", 1000), "the shop opened");
            Equal(null, watch.Heard("300", 1100), "the same points are no news");
            Equal(null, watch.Heard(null, 1200), "a page being rebuilt");
            Equal(null, watch.Heard("300", 1500), "brings them back unchanged");
            Equal("150", watch.Heard("150", 2000), "a purchase");
            Equal(null, watch.Heard("Cargando...", 2100), "not points");
            Equal(null, watch.Heard(null, 6000), "the shop closed");
            Equal("150", watch.Heard("150", 9000), "opened again: said again");

            var guild = new ShopBalanceWatch(settleMs: 600);
            Equal(null, guild.Heard("0", 1000), "a label that came with its tab waits for the tab's line");
            Equal(null, guild.Heard("0", 1500), "still settling");
            Equal("0", guild.Heard("0", 1600), "said once it has shown long enough");
            Equal(null, guild.Heard("0", 1700), "and once");
            Equal(null, guild.Heard(null, 1800), "the tab left before another value settled");
        }
    }
}
