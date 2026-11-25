namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The game's Compare view: the inspected item against the worn ones.</summary>
    public static partial class Announcements
    {
        public static string InspectView(bool compares) => compares ? T("Compare", "Comparar") : T("Inspect", "Inspeccionar");

        /// <summary>Over a pair's lines: "Comparado con Cuerno de armagedón, Gorro de fieltro, Equipado".</summary>
        public static string ComparedWith(string name, string worn)
        {
            var told = worn.Length > 0 ? $"{name}, {worn}" : name;
            return T($"Compared with {told}", $"Comparado con {told}");
        }

        /// <summary>The card the cursor is on: "Equipado, Cuerno de armagedón, Gorro de fieltro"; the inspected item's is its name.</summary>
        public static string ComparedCard(string name, string worn) => worn.Length > 0 ? $"{worn}, {name}" : name;

        /// <summary>
        ///     "Evasión: 127, el puesto 81", a line each; then what only one of the two has and what is equal:
        ///     "Solo el puesto: +56 a la vida máxima; Regeneras 5.3 de vida por segundo".
        /// </summary>
        public static IEnumerable<string> Difference(ItemDifference difference)
        {
            foreach (var (line, worn) in difference.Changed)
            {
                yield return T($"{line}, the worn one {worn}", $"{line}, el puesto {worn}");
            }

            if (difference.OnlyThis.Count > 0)
            {
                var lines = string.Join("; ", difference.OnlyThis);
                yield return T($"Only this one: {lines}", $"Solo este: {lines}");
            }

            if (difference.OnlyWorn.Count > 0)
            {
                var lines = string.Join("; ", difference.OnlyWorn);
                yield return T($"Only the worn one: {lines}", $"Solo el puesto: {lines}");
            }

            if (difference.Same.Count > 0)
            {
                var lines = string.Join("; ", difference.Same);
                yield return T($"The same in both: {lines}", $"Igual en los dos: {lines}");
            }
        }

        /// <summary>A modifier with the advanced details: "+9% a la resistencia al fuego, rango 6-10, T8, Sufijo".</summary>
        public static string CardModLine(string line, IReadOnlyList<string> ranges, string tier, string affix)
        {
            var rolled = string.Join(", ", ranges);
            return string.Join(", ", new[] { line, ranges.Count > 0 ? T($"range {rolled}", $"rango {rolled}") : string.Empty, tier, affix }.Where(part => part.Length > 0));
        }

        public static string AdvancedDetails(bool shown) =>
            shown ? T("Advanced details on", "Información avanzada activada") : T("Advanced details off", "Información avanzada desactivada");
    }
}
