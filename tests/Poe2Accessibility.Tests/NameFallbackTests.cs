namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;

    /// <summary>Names made from an object's metadata path when the game's tables give none.</summary>
    public static class NameFallbackTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(CamelCaseFileBecomesWords), CamelCaseFileBecomesWords),
            (nameof(VariantSuffixAndUnderscoresAreDropped), VariantSuffixAndUnderscoresAreDropped),
            (nameof(AcronymsStayTogether), AcronymsStayTogether),
            (nameof(LeagueNameIsTheGameLabelFirst), LeagueNameIsTheGameLabelFirst),
            (nameof(LeagueNameFallsBackToItsFolder), LeagueNameFallsBackToItsFolder),
        };

        private static void Expect(string expected, string actual)
        {
            if (expected != actual)
            {
                throw new Exception($"expected '{expected}', got '{actual}'");
            }
        }

        private static void CamelCaseFileBecomesWords() =>
            Expect("Ritual Altar", ObjectNames.FromPath("Metadata/Terrain/Leagues/Ritual/RitualAltar"));

        private static void VariantSuffixAndUnderscoresAreDropped() =>
            Expect("Sanctum Generic Lever2", ObjectNames.FromPath("Metadata/Terrain/Sanctum_GenericLever2@14"));

        private static void AcronymsStayTogether() =>
            Expect("NPC Hideout Door", ObjectNames.FromPath("Metadata/NPCHideoutDoor"));

        private static void LeagueNameIsTheGameLabelFirst() =>
            Expect("Abismo", ObjectNames.LeagueName("Abismo", "Metadata/Terrain/Leagues/Delirium/Thing"));

        private static void LeagueNameFallsBackToItsFolder()
        {
            Expect("Delirium", ObjectNames.LeagueName(string.Empty, "Metadata/Terrain/Leagues/Delirium/Thing"));
            Expect(string.Empty, ObjectNames.LeagueName(string.Empty, "Metadata/Terrain/Other/Thing"));
        }
    }
}
