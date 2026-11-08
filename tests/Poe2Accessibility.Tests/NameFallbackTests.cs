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
            (nameof(AHarmfulBuffIsNamedOnceWhenItBegins), AHarmfulBuffIsNamedOnceWhenItBegins),
            (nameof(BuffNamesFollowTheLanguageThenEnglish), BuffNamesFollowTheLanguageThenEnglish),
            (nameof(ACurrencyIconIsNamedByItsArtFile), ACurrencyIconIsNamedByItsArtFile),
            (nameof(APasswordIsOnlyCounted), APasswordIsOnlyCounted),
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

        private static HarmfulBuffs Buffs() => new(new()
        {
            ["en"] = new() { ["creeping_agony"] = "Creeping Agony" },
            ["es"] = new() { ["creeping_agony"] = "Agonía rastrera" },
        });

        private static void AHarmfulBuffIsNamedOnceWhenItBegins()
        {
            var buffs = Buffs();
            Func<string, bool> none = _ => false;
            Expect(string.Empty, string.Join(",", buffs.Begun(new[] { "creeping_agony" }, 0, "en", none))); // area entry is no news
            Expect(string.Empty, string.Join(",", buffs.Begun(new[] { "creeping_agony" }, 100, "en", none)));
            Expect(string.Empty, string.Join(",", buffs.Begun(new[] { "unknown_buff" }, 200, "en", none)));
            Expect(string.Empty, string.Join(",", buffs.Begun(Array.Empty<string>(), 1000, "en", none)));
            Expect("Creeping Agony", string.Join(",", buffs.Begun(new[] { "creeping_agony_1F" }, 1100, "en", none)));
            Expect(string.Empty, string.Join(",", buffs.Begun(new[] { "creeping_agony_1F" }, 1200, "en", none)));
            Expect(string.Empty, string.Join(",", buffs.Begun(Array.Empty<string>(), 2000, "en", none)));
            Expect(string.Empty, string.Join(",", buffs.Begun(new[] { "creeping_agony" }, 2100, "en", _ => true))); // covered by something else
        }

        private static void BuffNamesFollowTheLanguageThenEnglish()
        {
            var buffs = Buffs();
            Expect("Agonía rastrera", buffs.NameOf("creeping_agony", "es"));
            Expect("Creeping Agony", buffs.NameOf("creeping_agony", "de"));
            Expect(string.Empty, buffs.NameOf("sprinting", "en"));
        }

        private static void ACurrencyIconIsNamedByItsArtFile()
        {
            CurrencyIcons.Set(new() { ["en"] = new() { ["art/2ditems/currency/currencyidentification.dds"] = "Scroll of Wisdom" } });
            Expect("Scroll of Wisdom", CurrencyIcons.FromTextureKey("Art/2DItems/Currency/CurrencyIdentification.dds|0,0,0,0|0") ?? "none");
            Expect("none", CurrencyIcons.FromTextureKey("Art/2DItems/Currency/Unknown.dds|0,0,0,0|0") ?? "none");
            Expect("none", CurrencyIcons.FromTextureKey("Art/Other/Thing.dds") ?? "none");
            Expect(Announcements.Gold, CurrencyIcons.FromTextureKey("Art/2DItems/Currency/Ruthless/CoinPileTier1.dds|0,0,0,0|0") ?? "none");
        }

        private static void APasswordIsOnlyCounted()
        {
            Localization.Set("en", new());
            Expect(string.Empty, Announcements.PasswordLength(0));
            Expect("1 character", Announcements.PasswordLength(1));
            Expect("12 characters", Announcements.PasswordLength(12));
        }
    }
}
