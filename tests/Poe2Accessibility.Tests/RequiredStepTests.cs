namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Access;
    using static Check;

    /// <summary>A required game-data step that loses files or a language does not report done.</summary>
    public static class RequiredStepTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AFewLostUiScriptsAreToleratedAShareIsNot), AFewLostUiScriptsAreToleratedAShareIsNot),
            (nameof(ALanguageGoneFromAKeyedSectionIsAProblem), ALanguageGoneFromAKeyedSectionIsAProblem),
            (nameof(ALanguageTheClientNeverHadIsNoProblem), ALanguageTheClientNeverHadIsNoProblem),
            (nameof(TheShippedCatalogsPassTheirOwnRule), TheShippedCatalogsPassTheirOwnRule),
        };

        private static void AFewLostUiScriptsAreToleratedAShareIsNot()
        {
            Expect(true, UiLoss.Tolerated(0, 0), "nothing lost, nothing found: the entry count judges the rest");
            Expect(true, UiLoss.Tolerated(0, 500), "nothing lost");
            Expect(true, UiLoss.Tolerated(10, 500), "two in a hundred");
            Expect(false, UiLoss.Tolerated(11, 500), "more than two in a hundred");
            Expect(false, UiLoss.Tolerated(1, 10), "a tenth of a small set");
            Expect(true, UiLoss.Failure("script files", 11, 500).Contains("11 of 500 script files"), "the failure line names the loss");
        }

        private static string Gems(string namesLanguages, string skillsLanguages) =>
            "{\"gems\":{\"Metadata/Items/Gem/A\":1}," +
            $"\"names\":{{{namesLanguages}}},\"skills\":{{{skillsLanguages}}}}}";

        private static string Languages(params string[] codes) => string.Join(",", System.Linq.Enumerable.Select(codes, code => $"\"{code}\":{{\"a\":\"b\"}}"));

        private static void ALanguageGoneFromAKeyedSectionIsAProblem()
        {
            var before = Gems(Languages("de", "en", "ko"), Languages("de", "en", "ko"));
            Expect(null, GameDataCounts.Problem("other.json", before, Gems(Languages("de", "en", "ko"), Languages("de", "en", "ko"))), "all kept");
            Expect("language 'ko' of 'names' is gone", GameDataCounts.Problem("other.json", before, Gems(Languages("de", "en"), Languages("de", "en", "ko"))), "ko left out of one section");
            Expect(true, GameDataCounts.Problem("other.json", null, Gems(Languages("de", "en"), Languages("de", "en", "ko")))?.Contains("'ko'") == true, "no previous: the sections of the file must agree");
        }

        private static void ALanguageTheClientNeverHadIsNoProblem()
        {
            Expect(null, GameDataCounts.Problem("other.json", null, Gems(Languages("de", "en"), Languages("de", "en"))), "first run on a client with two languages");
            var before = Gems(Languages("en", "ko"), Languages("en", "ko"));
            Expect(null, GameDataCounts.Problem("other.json", before, Gems(Languages("en", "ko", "de"), Languages("en", "ko", "de"))), "a language gained");
        }

        private static void TheShippedCatalogsPassTheirOwnRule()
        {
            var directory = AppContext.BaseDirectory;
            while (directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory, "Poe2Access.slnx")))
            {
                directory = System.IO.Path.GetDirectoryName(directory);
            }

            Expect(true, directory is not null, "the repository root");
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(directory!, "Poe2Accessibility", "catalog"), "*.json");
            Expect(true, files.Length > 0, "shipped catalogs");
            foreach (var file in files)
            {
                var text = System.IO.File.ReadAllText(file);
                Expect(null, GameDataCounts.Problem(System.IO.Path.GetFileName(file), text, text), $"{System.IO.Path.GetFileName(file)} against itself");
            }
        }
    }
}
