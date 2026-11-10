namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;

    /// <summary>What is read of an item beyond its tooltip's lines.</summary>
    public static class ItemReadingTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(OnlyTheUnmetRequirementIsNamed), OnlyTheUnmetRequirementIsNamed),
            (nameof(AnUnmetLevelKeepsItsWord), AnUnmetLevelKeepsItsWord),
            (nameof(MetRequirementsNameNothing), MetRequirementsNameNothing),
            (nameof(AKeywordLinkInARequirementIsItsShownText), AKeywordLinkInARequirementIsItsShownText),
            (nameof(ASearchIsNewsOnceItSettles), ASearchIsNewsOnceItSettles),
            (nameof(ManyMatchesAreCountedPastTheFirstFew), ManyMatchesAreCountedPastTheFirstFew),
        };

        private static void Expect(string? expected, string? actual)
        {
            if (expected != actual)
            {
                throw new Exception($"expected '{expected ?? "nothing"}', got '{actual ?? "nothing"}'");
            }
        }

        // The stash searched for "guante" (2026-11-10): three gloves lit, the rest dimmed; then the search cleared.
        private static void ASearchIsNewsOnceItSettles()
        {
            var news = new SearchHighlightNews();
            var gloves = new List<(string, int, int)> { ("Guantes", 2, 0), ("Vendajes", 4, 10) };
            Expect(null, news.Line(null));
            Expect(null, news.Line(null));
            Expect(null, news.Line(gloves));
            Expect("2 highlighted: Guantes, column 3, row 1. Vendajes, column 5, row 11", news.Line(gloves));
            Expect(null, news.Line(gloves));
            Expect(null, news.Line(new List<(string, int, int)>()));
            Expect("Nothing highlighted", news.Line(new List<(string, int, int)>()));
            Expect(null, news.Line(null));
            Expect("Search cleared", news.Line(null));
            Expect(null, news.Line(null));
        }

        private static void ManyMatchesAreCountedPastTheFirstFew()
        {
            var news = new SearchHighlightNews();
            var many = new List<(string, int, int)>();
            for (var i = 0; i < SearchHighlightNews.MaxNamed + 3; i++)
            {
                many.Add(($"Item {i}", i, 0));
            }

            news.Line(many);
            var line = news.Line(many) ?? string.Empty;
            if (!line.StartsWith($"{many.Count} highlighted: Item 0, column 1, row 1", StringComparison.Ordinal) || !line.EndsWith(". And 3 more", StringComparison.Ordinal))
            {
                throw new Exception($"got '{line}'");
            }
        }

        private static void Expect(string expected, IEnumerable<string> actual)
        {
            var joined = string.Join(" | ", actual);
            if (expected != joined)
            {
                throw new Exception($"expected '{expected}', got '{joined}'");
            }
        }

        // The sceptre a level 23 Mercenary with 17 Int could not hold (tooltip dump 2026-11-10).
        private static void OnlyTheUnmetRequirementIsNamed() =>
            Expect("21 Int", UnmetRequirements.Of("Requiere: <valuedefault>{Nivel <valuedefault>{19}, <augmented>{10} Fue, <unmet>{21} Int}"));

        private static void AnUnmetLevelKeepsItsWord() =>
            Expect("Nivel 30 | 40 Des", UnmetRequirements.Of("Requiere: <valuedefault>{Nivel <unmet>{30}, <valuedefault>{10} Fue, <unmet>{40} Des}"));

        private static void MetRequirementsNameNothing() =>
            Expect(string.Empty, UnmetRequirements.Of("Requiere: <valuedefault>{Nivel <valuedefault>{19}, <augmented>{10} Fue}"));

        private static void AKeywordLinkInARequirementIsItsShownText() =>
            Expect("21 Inteligencia", UnmetRequirements.Of("<<set:MS>>Requiere: <unmet>{21} [Intelligence|Inteligencia]"));
    }
}
