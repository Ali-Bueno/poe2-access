namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Runs every test; exit code 1 when any fails. Run: dotnet run --project tests/Poe2Accessibility.Tests</summary>
    public static class Program
    {
        /// <summary>Dev command: compare the x64 decoder with a full capstone dump (re/decoder_truth.py).</summary>
        private const string DecoderTruthCommand = "x64-truth";

        /// <summary>Dev command: run the semantic rules of a catalog on an exe.</summary>
        private const string SemanticCommand = "semantic";

        private static int failures;

        public static int Main(string[] args)
        {
            if (args is [DecoderTruthCommand, var truth])
            {
                return X64DecoderTests.CompareFile(truth);
            }

            if (args is [SemanticCommand, var exe, var catalog])
            {
                return SemanticRuleTests.CheckExe(exe, catalog);
            }

            if (args is [StaticAnchorTests.CheckCommand, var image])
            {
                return StaticAnchorTests.CheckExe(image);
            }

            if (args is [StaticAnchorTests.GateCommand, var build, var catalogs])
            {
                return StaticAnchorTests.CheckBuild(build, catalogs);
            }

            if (args is [PrismTests.CheckCommand, var prismFolder])
            {
                return PrismTests.Check(prismFolder);
            }

            foreach (var (name, test) in X64DecoderTests.All().Concat(SemanticRuleTests.All()))
            {
                Run(name, test);
            }

            // One suite a line: two branches adding one each then merge by themselves.
            var suites = new[]
            {
                ScreenAndInputTests.All(),
                CueAndRadarSoundTests.All(),
                RouteAndFightTests.All(),
                GameStateFindingTests.All(),
                EnemyStateTests.All(),
                MapRadarTests.All(),
                ExplorationHintTests.All(),
                ChangeSignTests.All(),
                ShrineCostTests.All(),
                BreakablePromptTests.All(),
                PassiveAndMenuTests.All(),
                ReleaseAndQuestTests.All(),
                TrialAndPanelTests.All(),
                CodeFieldAndMarkerTests.All(),
                ObjectAndPhraseTests.All(),
                LayoutSearchTests.All(),
                HostSearchTests.All(),
                StdMapWalkTests.All(),
                TickCostsTests.All(),
                ReaderBufferTests.All(),
                ListingTests.All(),
                GridSamplesTests.All(),
                SettlingMemoTests.All(),
                NameFallbackTests.All(),
                ReachableRuleTests.All(),
                TrialDataTests.All(),
                QuestGoalTests.All(),
                TrialFloorLocatorTests.All(),
                PanelWidgetTests.All(),
                DodgeGateTests.All(),
                ClassMechanicTests.All(),
                LiveValueTests.All(),
                DodgeDataTests.All(),
                TelegraphTests.All(),
                RouteSpeedTests.All(),
                RadarLabelTests.All(),
                OpenedWaysTests.All(),
                PassiveStickTests.All(),
                DatColumnSearchTests.All(),
                DatColumnKindTests.All(),
                GameFormatTests.All(),
                GemColumnTests.All(),
                ExtractorColumnTests.All(),
                ItemReadingTests.All(),
                BuildGemTests.All(),
                BuildGearTests.All(),
                FileCatalogTests.All(),
                AgreedChoiceTests.All(),
                BaselineGraceTests.All(),
                ShopPurchaseTests.All(),
                BlockedMoveTests.All(),
                GridBlocksTests.All(),
                TooltipSettleTests.All(),
                ItemNameWordsTests.All(),
                StatusLineTests.All(),
                StatKeyTests.All(),
                SkillNameTests.All(),
                GemIndexTests.All(),
                GearCatalogTests.All(),
                ModStatColumnTests.All(),
                ItemModTests.All(),
                StaleValueTests.All(),
                StaticAnchorTests.All(),
                ExtractionGuardTests.All(),
                RobustnessTests.All(),
                HostAndToolsAuditTests.All(),
                KnownRowTests.All(),
                GameNameTests.All(),
                HostRuleTests.All(),
                LayoutRuleTests.All(),
                LauncherWaitTests.All(),
                RequiredStepTests.All(),
                PackageCheckTests.All(),
                SpeechEventTests.All(),
                SpokenOnceTests.All(),
                WorldCombatAuditTests.All(),
                PlayerItemNewsTests.All(),
                UnseenReaderTests.All(),
            };
            foreach (var (name, test) in suites.SelectMany(suite => suite))
            {
                Run(name, test);
            }

            foreach (var (name, test) in BossFightTests.All())
            {
                Run(name, test);
            }

            foreach (var (name, test) in EndgameTests.All().Concat(BossMechanicsTests.All()).Concat(BossCensusTests.All()).Concat(StartUpTests.All()).Concat(StartupSpeechTests.All()).Concat(PrismTests.All()))
            {
                Run(name, test);
            }

            Console.WriteLine(failures == 0 ? "All tests passed." : $"{failures} test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }
    }
}
