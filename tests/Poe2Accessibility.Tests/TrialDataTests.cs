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
        };

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
