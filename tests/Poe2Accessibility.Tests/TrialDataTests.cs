namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The trials' data-driven pieces: HUD formats as patterns, counters said on change, icons named, room doors opening.</summary>
    public static class TrialDataTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheRoundFormatMatchesTheShownLine), TheRoundFormatMatchesTheShownLine),
            (nameof(TheCrystalsFormatMatchesInAnyLanguage), TheCrystalsFormatMatchesInAnyLanguage),
            (nameof(AFormatWithoutNumbersIsNoPattern), AFormatWithoutNumbersIsNoPattern),
            (nameof(ACounterIsSaidOnChangeNotOnFirstSight), ACounterIsSaidOnChangeNotOnFirstSight),
            (nameof(AFallingCounterIgnoresRises), AFallingCounterIgnoresRises),
            (nameof(ACounterGoneStartsOver), ACounterGoneStartsOver),
            (nameof(AnIconIsNamedByItsArtInTheLanguageThenEnglish), AnIconIsNamedByItsArtInTheLanguageThenEnglish),
            (nameof(TheKindComesFromTheKey), TheKindComesFromTheKey),
            (nameof(ADoorIsKnownByItsFileAndOpenState), ADoorIsKnownByItsFileAndOpenState),
            (nameof(AnOpeningDoorIsNewOnce), AnOpeningDoorIsNewOnce),
            (nameof(AClassIsKnownByItsCardsLine), AClassIsKnownByItsCardsLine),
            (nameof(TheLargerCardIsTheChosenOne), TheLargerCardIsTheChosenOne),
            (nameof(AnAscendancysStartIsKnownByItsId), AnAscendancysStartIsKnownByItsId),
            (nameof(TheBuildsNextNodeIsOneThereArePointsFor), TheBuildsNextNodeIsOneThereArePointsFor),
            (nameof(AnAscendancysStartIsNoStepOfTheBuild), AnAscendancysStartIsNoStepOfTheBuild),
        };

        private static void AnAscendancysStartIsNoStepOfTheBuild()
        {
            var plan = new BuildPlan("build", new[]
            {
                new BuildPassive("melee17", 0, 0, string.Empty),
                new BuildPassive("AscendancyWarrior3Start", 1, 0, string.Empty),
                new BuildPassive("AscendancyWarrior3Notable6_0", 2, 0, string.Empty),
            });
            var both = plan.StepsOver(new HashSet<string> { "melee17", "AscendancyWarrior3Start", "AscendancyWarrior3Notable6_0" });
            Expect(2, both.Count);
            Expect(1, both.Steps["AscendancyWarrior3Notable6_0"]);
            // Before ascending the class's tree shows neither: one node is yet to come, not two.
            Expect(1, plan.StepsOver(new HashSet<string> { "melee17" }).NotShown);
        }

        private static void TheBuildsNextNodeIsOneThereArePointsFor()
        {
            // No skill points and two of the ascendancy's (yarkidius, just ascended): the ascendancy's nodes first.
            Expect(true, BuildPlan.PointsOrder(true, 0, 2) < BuildPlan.PointsOrder(false, 0, 2));
            Expect(true, BuildPlan.PointsOrder(false, 3, 0) < BuildPlan.PointsOrder(true, 3, 0));
            // Points in both, or the counts unread: the file's order alone.
            Expect(BuildPlan.PointsOrder(false, 1, 1), BuildPlan.PointsOrder(true, 1, 1));
            Expect(BuildPlan.PointsOrder(false, null, null), BuildPlan.PointsOrder(true, null, null));
            // None in either: the next point is a level's, the class's tree first.
            Expect(true, BuildPlan.PointsOrder(false, 0, 0) < BuildPlan.PointsOrder(true, 0, 0));

            // The Build list: as many nodes of each tree as it has points, in the order given; one alone with no points.
            var pending = new[] { ("forge", true), ("anvil", true), ("coal", true), ("area", false), ("cry", false) };
            Expect("forge anvil", string.Join(' ', BuildPlan.TakeNow(pending, 0, 2)));
            Expect("forge area cry", string.Join(' ', BuildPlan.TakeNow(pending, 5, 1)));
            Expect("forge", string.Join(' ', BuildPlan.TakeNow(pending, 0, 0)));
            Expect(5, BuildPlan.TakeNow(pending, null, null).Count);
        }

        private static void AnAscendancysStartIsKnownByItsId()
        {
            Expect(true, BuildPlan.IsAscendancyStart("AscendancyWarrior3Start"));
            Expect(true, BuildPlan.IsAscendancyStart("AscendancyWitch1Start__"));
            Expect(false, BuildPlan.IsAscendancyStart("AscendancyWarrior3Notable6_0"));
            Expect(false, BuildPlan.IsAscendancyStart("marauder_start"));
        }

        private static void AClassIsKnownByItsCardsLine()
        {
            var catalog = new TrialCatalog(new(), new(StringComparer.Ordinal)
            {
                ["en"] = new(StringComparer.Ordinal) { ["ascendancy:Warrior3"] = "Smith of Kitava", ["ascendancy:Warrior3:about"] = "You hunger for power.", ["effect:BoonX:about"] = "Shared line." },
                ["es"] = new(StringComparer.Ordinal) { ["ascendancy:Warrior3:about"] = "Ansías el poder." },
            });
            Expect("ascendancy:Warrior3", catalog.AscendancyOf("Ansías el poder."));
            Expect("ascendancy:Warrior3", catalog.AscendancyOf("You hunger for power."));
            Expect(null, catalog.AscendancyOf("Shared line."));
            Expect(null, catalog.AscendancyOf("Smith of Kitava"));
        }

        /// <summary>The altar as dumped on 2026-11-14: Titan enlarged on the left, the build's class on the right.</summary>
        private static void TheLargerCardIsTheChosenOne()
        {
            Localization.Set("en", new());
            var cards = AscendancyChoice.InOrder(new[]
            {
                new AscendancyCard("Warbringer", "A good man must be roused to war.", false, 1279.5f, 585),
                new AscendancyCard("Smith of Kitava", "You hunger for power.", true, 1864.5f, 585),
                new AscendancyCard("Titan", "Now is the time to rise.", false, 739.5f, 819),
            });
            Expect(0, AscendancyChoice.Chosen(cards));
            Expect("Titan, 1 of 3. Now is the time to rise.", AscendancyChoice.Line(cards, 0));
            Expect("Smith of Kitava, 3 of 3, in the build. You hunger for power.", AscendancyChoice.Line(cards, 2));
            Expect(string.Empty, AscendancyChoice.Line(cards, -1));
            var level = cards.Select(card => card with { Width = 585 }).ToList();
            Expect(-1, AscendancyChoice.Chosen(level));
            Expect(-1, AscendancyChoice.Chosen(new[] { cards[0] }));
            var opened = cards.Select(card => card with { Width = card.InBuild ? 1170 : 585 }).ToList();
            Expect(2, AscendancyChoice.Chosen(opened));
            Expect(false, AscendancyChoice.Opened(cards, 0));
            Expect("Smith of Kitava, tree preview", AscendancyChoice.Line(opened, 2));
        }

        private static void Expect<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"expected '{expected}', got '{actual}'");
            }
        }

        private static void TheRoundFormatMatchesTheShownLine()
        {
            var pattern = TrialCatalog.PatternOf("Round <ultimatumnumber>{{{0}/{1}}}")!;
            var line = TrialLines.Read(pattern, new[] { "Honor: 288/513", "Round 3/10" })!;
            Expect("Round 3/10", line.Text);
            Expect(3, line.Value);
            Expect(10, line.Of);
            Expect(null, TrialLines.Read(pattern, new[] { "Round" }));
        }

        private static void TheCrystalsFormatMatchesInAnyLanguage()
        {
            var line = TrialLines.Read(TrialCatalog.PatternOf("{0} cristales de la muerte restantes"), new[] { "5 cristales de la muerte restantes" })!;
            Expect(5, line.Value);
            Expect(0, line.Of);
            Expect(null, TrialLines.Read(TrialCatalog.PatternOf("{0} Death Crystals remaining"), new[] { "Death Crystal" }));
        }

        private static void AFormatWithoutNumbersIsNoPattern()
        {
            Expect(null, TrialCatalog.PatternOf("Active Modifiers"));
            Expect(null, TrialLines.Read(null, new[] { "Round 3/10" }));
        }

        private static void ACounterIsSaidOnChangeNotOnFirstSight()
        {
            var rounds = new CounterWatch(fallsOnly: false);
            Expect(null, rounds.Heard(new CounterLine("Round 1/10", 1, 10)));
            Expect(null, rounds.Heard(new CounterLine("Round 1/10", 1, 10)));
            Expect(2, rounds.Heard(new CounterLine("Round 2/10", 2, 10))?.Value);
        }

        private static void AFallingCounterIgnoresRises()
        {
            var crystals = new CounterWatch(fallsOnly: true);
            Expect(null, crystals.Heard(new CounterLine("5 left", 5, 0)));
            Expect(4, crystals.Heard(new CounterLine("4 left", 4, 0))?.Value);
            Expect(null, crystals.Heard(new CounterLine("6 left", 6, 0)));
            Expect(2, crystals.Heard(new CounterLine("2 left", 2, 0))?.Value);
        }

        private static void ACounterGoneStartsOver()
        {
            var rounds = new CounterWatch(fallsOnly: false);
            rounds.Heard(new CounterLine("Round 4/10", 4, 10));
            Expect(null, rounds.Heard(null));
            Expect(null, rounds.Heard(new CounterLine("Round 1/10", 1, 10)));
        }

        private static TrialCatalog Catalog() => new(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["art/2dart/uiimages/ingame/sanctum/booncurseicons/cursehonedclawsicon"] = "effect:AfflictionMonsterMoreDamage",
                ["art/2dart/uiimages/ingame/ultimatum/icons/reducedrecovery"] = "mod:PlayerDebuffRecoveryReduction1",
            },
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["en"] = new(StringComparer.Ordinal)
                {
                    ["effect:AfflictionMonsterMoreDamage"] = "Honed Claws",
                    ["effect:AfflictionMonsterMoreDamage:about"] = "Monsters deal 30% more Damage",
                    ["mod:PlayerDebuffRecoveryReduction1"] = "Reduced Recovery",
                    ["label:round"] = "Round <ultimatumnumber>{{{0}/{1}}}",
                },
                ["es"] = new(StringComparer.Ordinal) { ["effect:AfflictionMonsterMoreDamage:about"] = "Los monstruos infligen un 30% más de daño" },
            });

        private static void AnIconIsNamedByItsArtInTheLanguageThenEnglish()
        {
            var catalog = Catalog();
            var key = catalog.KeyOfIcon("Art/2DArt/UIImages/InGame/Sanctum/BoonCurseIcons/CurseHonedClawsIcon.dds")!;
            Expect("effect:AfflictionMonsterMoreDamage", key);
            Expect("Honed Claws", catalog.Text(key));
            Expect(null, catalog.KeyOfIcon("Art/2DItems/QuestItems/RewardCraftingBench.dds"));
            Expect("Reduced Recovery", catalog.Text(catalog.KeyOfIcon("Art/2DArt/UIImages/InGame/Ultimatum/Icons/ReducedRecovery")!));
            Expect(string.Empty, catalog.About("mod:PlayerDebuffRecoveryReduction1"));
            Expect(true, catalog.Pattern(TrialCatalog.RoundLabel)!.IsMatch("Round 7/10"));
            Expect(null, catalog.Pattern(TrialCatalog.CrystalsLabel));
            Expect(2, catalog.Count);
        }

        private static void TheKindComesFromTheKey()
        {
            Expect(TrialEntryKind.Affliction, TrialCatalog.KindOf("effect:AfflictionTrapSpeed"));
            Expect(TrialEntryKind.Boon, TrialCatalog.KindOf("effect:BoonMovementSpeed"));
            Expect(TrialEntryKind.Modifier, TrialCatalog.KindOf("mod:MonsterNoFlaskCharges"));
            Expect(TrialEntryKind.Room, TrialCatalog.KindOf("room:Gauntlet"));
            Expect(TrialEntryKind.Ascendancy, TrialCatalog.KindOf("ascendancy:Warrior1"));
            Expect(TrialEntryKind.Other, TrialCatalog.KindOf("label:round"));
        }

        private static void ADoorIsKnownByItsFileAndOpenState()
        {
            Expect(true, TrialDoors.IsDoor("Metadata/Terrain/Gallows/Leagues/Sanctum/Objects/SanctumRoomSelectorDoor@12"));
            Expect(false, TrialDoors.IsDoor("Metadata/Terrain/Gallows/Leagues/Sanctum/Objects/FloorTransitionDoor"));
            Expect(true, TrialDoors.IsOpen(new[] { ("wall", 0L), ("open", 1L) }));
            Expect(false, TrialDoors.IsOpen(new[] { ("wall", 1L), ("open", 0L) }));
            Expect(false, TrialDoors.IsOpen(Array.Empty<(string, long)>()));
        }

        private static void AnOpeningDoorIsNewOnce()
        {
            var watch = new OpenDoorWatch();
            Expect(0, watch.NewlyOpen(new[] { (1u, false), (2u, false), (3u, false) }).Count);
            Expect(2u, watch.NewlyOpen(new[] { (1u, false), (2u, true), (3u, false) }).Single());
            Expect(0, watch.NewlyOpen(new[] { (1u, false), (2u, true), (3u, false) }).Count);
            watch.Reset();
            Expect(2u, watch.NewlyOpen(new[] { (2u, true) }).Single());
        }
    }
}
