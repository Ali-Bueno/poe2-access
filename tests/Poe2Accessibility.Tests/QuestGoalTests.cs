namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The quest step's goal areas by tracker line and language, and the way toward them over the areas' connections.</summary>
    public static class QuestGoalTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(GoalsComeByLineAndLanguageWithEnglishAsFallback), GoalsComeByLineAndLanguageWithEnglishAsFallback),
            (nameof(ALineIsMatchedWhateverTheHudDoesToIt), ALineIsMatchedWhateverTheHudDoesToIt),
            (nameof(AMissingFileIsAnEmptyCatalog), AMissingFileIsAnEmptyCatalog),
            (nameof(NextDoorTheGoalItselfIsTheStep), NextDoorTheGoalItselfIsTheStep),
            (nameof(TwoHopsGiveTheFirstExitOfAShortestWay), TwoHopsGiveTheFirstExitOfAShortestWay),
            (nameof(HereIsTheGoalNeedsNoStep), HereIsTheGoalNeedsNoStep),
            (nameof(AnUnconnectedAreaHasNoStep), AnUnconnectedAreaHasNoStep),
        };

        private static QuestGoalCatalog Sample() => new(
            new Dictionary<string, Dictionary<string, string[]>>
            {
                ["en"] = new() { ["Find the Sanctum"] = new[] { "G2_4_3" }, ["Reach the Grelwood"] = new[] { "G1_4" } },
                ["es"] = new() { ["Encuentra el Santuario"] = new[] { "G2_4_3" } },
            },
            new Dictionary<string, string[]>
            {
                ["G2_4_1"] = new[] { "G2_4_2" },
                ["G2_4_2"] = new[] { "G2_4_1", "G2_4_3" },
                ["G2_4_3"] = new[] { "G2_4_2" },
                ["G2_3"] = new[] { "G2_4_1", "G2_4_3" },
            });

        private static void Expect<T>(T expected, T actual, string what) => Check.Expect(expected, actual, what);

        private static void GoalsComeByLineAndLanguageWithEnglishAsFallback()
        {
            var catalog = Sample();
            Expect("G2_4_3", string.Join(",", catalog.GoalsOf(new[] { "Encuentra el Santuario" }, "es")), "the language's line");
            Expect("G1_4", string.Join(",", catalog.GoalsOf(new[] { "Reach the Grelwood" }, "es")), "a line only English has");
            Expect("G2_4_3,G1_4", string.Join(",", catalog.GoalsOf(new[] { "Find the Sanctum", "Reach the Grelwood" }, "en")), "two lines, both goals");
            Expect(0, catalog.GoalsOf(new[] { "Talk to someone" }, "en").Count, "an unknown line");
            Expect(1, catalog.GoalsOf(new[] { "Find the Sanctum" }, "xx").Count, "an unknown language falls back");
        }

        private static void ALineIsMatchedWhateverTheHudDoesToIt()
        {
            var catalog = Sample();
            Expect(1, catalog.GoalsOf(new[] { "  Find   the\tSanctum " }, "en").Count, "blanks");
            Expect(1, catalog.GoalsOf(new[] { "<white>{Find the Sanctum}" }, "en").Count, "colour");
            Expect(1, catalog.GoalsOf(new[] { "[Find|Find] the Sanctum" }, "en").Count, "a link");
            Expect(2, catalog.GoalsOf(new[] { "Find the Sanctum\r\nReach the Grelwood" }, "en").Count, "a text of two lines");
        }

        private static void AMissingFileIsAnEmptyCatalog()
        {
            var log = new PluginLog(Path.Join(Path.GetTempPath(), "poe2access-tests"), false);
            var catalog = QuestGoalCatalog.Load(log, Path.Join(Path.GetTempPath(), "poe2access-tests", "no-quest-goals.json"));
            Expect(0, catalog.GoalsOf(new[] { "Find the Sanctum" }, "en").Count, "no goals");
            Expect(0, catalog.StepsToward("G2_4_2", new[] { "G2_4_3" }).Count, "no way");
        }

        private static void NextDoorTheGoalItselfIsTheStep() =>
            Expect("G2_4_3", string.Join(",", Sample().StepsToward("G2_4_2", new[] { "G2_4_3" })), "next door");

        private static void TwoHopsGiveTheFirstExitOfAShortestWay() =>
            Expect("G2_4_2", string.Join(",", Sample().StepsToward("G2_4_1", new[] { "G2_4_3" })), "G2_4_1 to G2_4_3 through G2_4_2");

        private static void HereIsTheGoalNeedsNoStep()
        {
            Expect(0, Sample().StepsToward("G2_4_3", new[] { "G2_4_3", "G1_4" }).Count, "here is a goal");
            var both = Sample().StepsToward("G2_3", new[] { "G2_4_3", "G2_4_1" }).OrderBy(area => area).ToList();
            Expect("G2_4_1,G2_4_3", string.Join(",", both), "two goals next door");
        }

        private static void AnUnconnectedAreaHasNoStep()
        {
            Expect(0, Sample().StepsToward("G9_9", new[] { "G2_4_3" }).Count, "an area with no connections");
            Expect(0, Sample().StepsToward("G2_4_2", new[] { "G1_4" }).Count, "a goal nothing leads to");
        }
    }
}
