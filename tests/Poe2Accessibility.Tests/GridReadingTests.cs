namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>A grid said whole (<see cref="GridReading"/>): its count, the build's pieces first, the wording of each item.</summary>
    public static class GridReadingTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheBuildsPiecesComeFirstThenReadingOrder), TheBuildsPiecesComeFirstThenReadingOrder),
            (nameof(AGridIsSaidWithItsCountAndEachItemOnce), AGridIsSaidWithItsCountAndEachItemOnce),
            (nameof(AnEmptyGridSaysSo), AnEmptyGridSaysSo),
        };

        private static GridItem Item(string named, int column, int row, bool onBase = false, params string[] fit) =>
            new(named, column, row, Announcements.GridPosition(column, row), onBase, fit);

        private static void TheBuildsPiecesComeFirstThenReadingOrder()
        {
            var ordered = GridReading.Ordered(new[]
            {
                Item("c", 2, 1), Item("a", 0, 0), Item("boots", 4, 2, true), Item("b", 5, 0), Item("gloves", 1, 2, true),
            });
            Equal("gloves boots a b c", string.Join(" ", ordered.Select(item => item.Named)), "on the build's base first, each group by row then column");
            Equal(0, BuildGear.SlotsFor("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpearGamble").Count, "a gamble's random spear has no base to fit a slot");
            Equal(true, BuildGear.SlotsFor("Metadata/Items/Weapons/OneHandWeapons/OneHandSpears/FourSpear5").Count > 0, "a spear fits the weapon slots");
        }

        private static void AGridIsSaidWithItsCountAndEachItemOnce()
        {
            Localization.Detect("es");
            try
            {
                Equal("3 objetos. Escarpes de malla, raro, columna 5, fila 3, Build, Botas: misma base. Pergamino de sabiduría, 8, columna 1, fila 1. " +
                      "Guantes sombríos, raro, columna 3, fila 1, Build, Guantes: otra base",
                    GridReading.Say(new[]
                    {
                        Item("Pergamino de sabiduría, 8", 0, 0),
                        Item("Guantes sombríos, raro", 2, 0, false, "Build, Guantes: otra base"),
                        Item("Escarpes de malla, raro", 4, 2, true, "Build, Botas: misma base"),
                    }), "count, then the build's piece, then the rest");
                Equal("1 objeto. Anillo de rubí, único", GridReading.Say(new[] { new GridItem("Anillo de rubí, único", 0, 0, string.Empty, false, Array.Empty<string>()) }),
                    "one item in a slot without a place");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void AnEmptyGridSaysSo()
        {
            Localization.Detect("es");
            try
            {
                Equal("Vacío", GridReading.Say(Array.Empty<GridItem>()), "no items");
            }
            finally
            {
                Localization.Detect("en");
            }
        }
    }
}
