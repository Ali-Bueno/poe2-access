namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     A grid cell with an item in the hand is said by what lies where that item would go (<see cref="GridBlocks"/>,
    ///     <see cref="Announcements.UnderHand"/>): the bag of 2026-11-11, a short bow of 2 x 4 cells held.
    /// </summary>
    public static class GridBlocksTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AHeldItemLiesOverTheItemsItsCellsTouch), AHeldItemLiesOverTheItemsItsCellsTouch),
            (nameof(TheCellUnderTheHandSaysWhatLiesThere), TheCellUnderTheHandSaysWhatLiesThere),
        };

        private static void AHeldItemLiesOverTheItemsItsCellsTouch()
        {
            // The bow's top left corner on column 6, row 2 (zero-based 5, 1): the hood at 5, 0 (2 x 2) is under its first row.
            Equal(true, GridBlocks.Overlap(5, 1, 2, 4, 5, 0, 2, 2), "the hood its first row touches");
            Equal(false, GridBlocks.Overlap(5, 1, 2, 4, 7, 0, 2, 2), "the sandals beside it");
            Equal(false, GridBlocks.Overlap(5, 1, 2, 4, 3, 0, 2, 4), "the other bow, ending where it begins");
            Equal(false, GridBlocks.Overlap(5, 2, 2, 3, 5, 0, 2, 2), "a row lower: under the hood, not on it");
            Equal(true, GridBlocks.Overlap(0, 0, 1, 1, 0, 0, 1, 1), "one cell on itself");
            Equal(true, GridBlocks.Overlap(6, 1, 2, 4, 7, 2, 2, 2), "a corner shared is enough");
        }

        private static void TheCellUnderTheHandSaysWhatLiesThere()
        {
            Localization.Set("es", new());
            try
            {
                Equal("vacío", Announcements.UnderHand(Array.Empty<string>()), "nothing there: it can be put down");
                Equal("sobre Mirada de odio, raro", Announcements.UnderHand(new[] { "Mirada de odio, raro" }), "one item: A swaps them (live: the hood came to the hand)");
                Equal("sobre 2 objetos: Sandalias de paja, mágico; Yelmo con ala, mágico",
                    Announcements.UnderHand(new[] { "Sandalias de paja, mágico", "Yelmo con ala, mágico" }), "several: it cannot be put there");
                Equal(true, Announcements.IsCellGrid(null), "a stash tab or a merchant's page");
                Equal(true, Announcements.IsCellGrid(GameReader.RemoteEnums.InventoryName.MainInventory1), "the bag");
                Equal(false, Announcements.IsCellGrid(GameReader.RemoteEnums.InventoryName.Flask1), "the flasks' row has slots");
                Equal(false, Announcements.IsCellGrid(GameReader.RemoteEnums.InventoryName.Helm1), "an equipment slot is named");
            }
            finally
            {
                Localization.Set("en", new());
            }
        }
    }
}
