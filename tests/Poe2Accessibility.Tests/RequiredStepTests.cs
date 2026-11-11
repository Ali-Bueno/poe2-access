namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Access;
    using GameReader.Utils;
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
            (nameof(ABurstOfFailedReadsIsOneLine), ABurstOfFailedReadsIsOneLine),
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

        private static void ABurstOfFailedReadsIsOneLine()
        {
            var log = new ReadFailureLog(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30));
            var start = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            Expect(true, log.Report(start), "the first failure speaks");
            for (var i = 1; i <= 16; i++)
            {
                Expect(false, log.Report(start.AddMilliseconds(i * 10)), "the rest of the burst is left out");
            }

            Expect(16, log.TakeLeftOut(), "and counted");
            Expect(0, log.TakeLeftOut(), "once");
            Expect(true, log.Report(start.AddSeconds(10)), "a failure after a quiet spell speaks again");
            Expect(false, log.Report(start.AddSeconds(11)), "inside the new burst");
            for (var seconds = 12; seconds < 40; seconds++)
            {
                log.Report(start.AddSeconds(seconds));
            }

            Expect(true, log.Report(start.AddSeconds(41)), "a burst that never ends speaks again after 30 s");
        }
    }
}
