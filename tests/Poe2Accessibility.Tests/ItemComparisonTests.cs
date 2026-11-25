namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The Compare view's lines (docs/research/ui-layers.md, "Compare view"): the felt cap and the worn rare of the 2026-11-25 dump.</summary>
    public static class ItemComparisonTests
    {
        private static readonly string[] Cap = { "Evasión: 127", "+27 al maná máximo", "+9% a la resistencia al fuego" };

        /// <summary>The worn helmet as the advanced details write it, a stat once per modifier.</summary>
        private static readonly string[] Worn =
        {
            "Evasión: 81", "+38 a la vida máxima", "Evasión aumentada un 20%", "+18 a la vida máxima", "Evasión aumentada un 20%", "+14 al maná máximo",
            "+15% a la resistencia al fuego", "Regeneras 5.3 de vida por segundo",
        };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AnItemIsSetAgainstTheWornOne), AnItemIsSetAgainstTheWornOne),
            (nameof(AStatsModifiersAddUp), AStatsModifiersAddUp),
            (nameof(TheRangesOfAModifierStandApart), TheRangesOfAModifierStandApart),
        };

        private static void AnItemIsSetAgainstTheWornOne()
        {
            var found = ItemComparison.Compare(Cap, Worn);
            Equal(true, found.Changed.SequenceEqual(new[] { ("Evasión: 127", "81"), ("+27 al maná máximo", "+14"), ("+9% a la resistencia al fuego", "+15%") }),
                "compare: the lines both have, each with the worn item's number");
            Equal(true, found.OnlyThis.Count == 0 && found.Same.Count == 0, "compare: nothing only the new one has, nothing equal");
            Equal(true, found.OnlyWorn.SequenceEqual(new[] { "+56 a la vida máxima", "Evasión aumentada un 40%", "Regeneras 5.3 de vida por segundo" }),
                "compare: what would be lost, a stat's modifiers added up as the plain tooltip does");

            var weapons = ItemComparison.Compare(new[] { "Daño físico: 47-96", "Agrega de 1 a 8 de daño de rayo" }, new[] { "Daño físico: 32-48", "Agrega de 1 a 7 de daño de rayo" });
            Equal(true, weapons.Changed.SequenceEqual(new[] { ("Daño físico: 47-96", "32-48"), ("Agrega de 1 a 8 de daño de rayo", "1 a 7") }), "compare: a range is kept whole");

            var same = ItemComparison.Compare(new[] { "+5 a la fuerza", "Otorga la habilidad: Rayo" }, new[] { "Otorga la habilidad: Rayo", "+5 a la fuerza" });
            Equal(true, same.Same.Count == 2 && same.Changed.Count + same.OnlyThis.Count + same.OnlyWorn.Count == 0, "compare: equal lines in any order");
        }

        private static void AStatsModifiersAddUp()
        {
            Equal(true, ItemComparison.Merged(new[] { "Agrega de 1 a 8 de daño", "Agrega de 1 a 7 de daño" }).Count == 2, "compare: lines of two numbers are not added up");
            Equal(true, ItemComparison.Merged(new[] { "Regeneras 4,1 de vida", "Regeneras 1,2 de vida" }).SequenceEqual(new[] { "Regeneras 5,3 de vida" }), "compare: a decimal comma stays");
            Equal(true, ItemComparison.Merged(new[] { "-10% a la velocidad", "-5% a la velocidad" }).SequenceEqual(new[] { "-15% a la velocidad" }), "compare: a loss stays a loss");
        }

        private static void TheRangesOfAModifierStandApart()
        {
            Equal(true, ItemModCheck.WithoutRange("De 7(5-7) a 8(7-10) de daño físico") == "De 7 a 8 de daño físico" &&
                 ItemModCheck.RangesOf("De 7(5-7) a 8(7-10) de daño físico").SequenceEqual(new[] { "5-7", "7-10" }), "compare: the ranges of a modifier apart from its line");
            Equal(true, FocusDetails.Detailed(new[] { "Casco", "+27 al maná máximo" }, new[] { "Casco: Nivel de objeto 28", "+27(25-34) al maná máximo", "T8" }) &&
                 !FocusDetails.Detailed(Array.Empty<string>(), new[] { "+27(25-34) al maná máximo" }) &&
                 !FocusDetails.Detailed(new[] { "+1(1-2)" }, new[] { "+2(1-2)" }), "compare: a tooltip redrawn with ranges is the view opening");
        }
    }
}
