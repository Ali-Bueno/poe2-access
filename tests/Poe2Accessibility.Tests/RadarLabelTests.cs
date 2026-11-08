namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using Poe2Accessibility.Services;

    /// <summary>Radar-table labels: a name the game has is said as the game names it, the rest as before.</summary>
    public static class RadarLabelTests
    {
        /// <summary>Labels the real radar tables must read in the game's language at least (measured 2026-11-08: 92 of 211).</summary>
        private const int MinNamedLabels = 90;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AWholeLabelIsTheAreaNameWhateverItsCaseOrApostrophes), AWholeLabelIsTheAreaNameWhateverItsCaseOrApostrophes),
            (nameof(PiecesOfALabelAreNamedByTheGame), PiecesOfALabelAreNamedByTheGame),
            (nameof(ANameWithACommaIsWholeBeforeItsNote), ANameWithACommaIsWholeBeforeItsNote),
            (nameof(CommonWordsAndNotesAreTranslatedNamesAreKept), CommonWordsAndNotesAreTranslatedNamesAreKept),
            (nameof(EnglishSaysTheGamesOwnSpelling), EnglishSaysTheGamesOwnSpelling),
            (nameof(ALanguageWithoutTheTablesKeepsTheLabel), ALanguageWithoutTheTablesKeepsTheLabel),
            (nameof(TheRealTablesNameMostOfWhatTheGameNames), TheRealTablesNameMostOfWhatTheGameNames),
        };

        private static void Expect<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected '{expected}', got '{actual}'");
            }
        }

        private static LandmarkTable Table()
        {
            var directory = Path.Join(Path.GetTempPath(), "poe2access-tests", "radar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var landmarks = Path.Join(directory, "landmarks.json");
            File.WriteAllText(landmarks, "{}");
            File.WriteAllText(Path.Join(directory, "area-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["G4_2_1"] = "Journey's End", ["G1_6"] = "The Galai Gates" },
                ["es"] = new() { ["G4_2_1"] = "El final del viaje", ["G1_6"] = "Las puertas de Galai" },
            }));
            File.WriteAllText(Path.Join(directory, "monster-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Captain Hartlin"] = "Captain Hartlin", ["Zelina"] = "Zelina", ["Zolin"] = "Zolin", ["Mektul, The Forgemaster"] = "Mektul, the Forgemaster", ["Yama"] = "Yama The White" },
                ["es"] = new() { ["Captain Hartlin"] = "Capitán Hartlin", ["Zelina"] = "Zelina", ["Zolin"] = "Zolin", ["Mektul, The Forgemaster"] = "Mektul, el Maestro de la forja", ["Yama"] = "Yama, el Blanco" },
            }));
            File.WriteAllText(Path.Join(directory, "item-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["CurrencyLesserJeweller"] = "Lesser Jeweller's Orb" },
                ["es"] = new() { ["CurrencyLesserJeweller"] = "Orbe de joyero menor" },
            }));
            return LandmarkTable.Load(new PluginLog(Path.Join(Path.GetTempPath(), "poe2access-tests"), false), landmarks);
        }

        private static void AWholeLabelIsTheAreaNameWhateverItsCaseOrApostrophes()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("El final del viaje", table.Localize("Journey's end"), "case");
            Expect("El final del viaje", table.Localize("Journeys End"), "apostrophe");
            Expect("Las puertas de Galai", table.Localize("Galai Gates"), "without The");
            Expect("Las puertas de Galai", table.Localize("The Galai Gates"), "with The");
            Localization.Detect("en");
        }

        private static void PiecesOfALabelAreNamedByTheGame()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("Jefe, Capitán Hartlin", table.Localize("Boss - Captain Hartlin"), "a boss after a common word");
            Expect("Zelina & Zolin", table.Localize("Zelina & Zolin"), "two names");
            Expect("Orok Campfire (Orbe de joyero menor)", table.Localize("Orok Campfire (Lesser Jeweller's Orb)"), "an item in brackets");
            Expect("Arena / El final del viaje", table.Localize("Arena / Journey's End"), "a place after a slash");
            Localization.Detect("en");
        }

        private static void ANameWithACommaIsWholeBeforeItsNote()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("Mektul, el Maestro de la forja (Reforging Bench)", table.Localize("Mektul, The Forgemaster (Reforging Bench)"), "the comma stays inside the name");
            Localization.Detect("en");
        }

        private static void CommonWordsAndNotesAreTranslatedNamesAreKept()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("Canal Mechanism (punto de ruta)", table.Localize("Canal Mechanism (waypoint)"), "a note in lower case");
            Expect("Crab Rare", table.Localize("Crab Rare"), "no name, no common word: as written");
            Expect("Misión, deadman chest", table.Localize("Quest - deadman chest"), "a common word, the rest as written");
            Expect(true, table.UnnamedPieces("Quest - deadman chest").SequenceEqual(new[] { "deadman chest" }), "what is left unnamed");
            Expect(false, table.HasGameName("Crab Rare"), "no game name");
            Expect(true, table.HasGameName("Boss - Yama"), "a game name in a piece");
            Localization.Detect("en");
        }

        private static void EnglishSaysTheGamesOwnSpelling()
        {
            var table = Table();
            Localization.Detect("en");
            Expect("Journey's End", table.Localize("Journey's end"), "area");
            Expect("Boss - Yama The White", table.Localize("Boss - Yama"), "monster");
            Expect("Stairs Up", table.Localize("Stairs Up"), "a note stays");
        }

        private static void ALanguageWithoutTheTablesKeepsTheLabel()
        {
            var table = Table();
            Localization.Detect("fr");
            Expect("Boss, Captain Hartlin", table.Localize("Boss - Captain Hartlin"), "no French table: the name as written");
            Localization.Detect("en");
        }

        private static string? CatalogDirectory()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    var catalog = Path.Join(directory.FullName, "Poe2Accessibility", "catalog");
                    if (File.Exists(Path.Join(catalog, "important_tgt_files.txt")))
                    {
                        return catalog;
                    }
                }
            }

            return null;
        }

        private static void TheRealTablesNameMostOfWhatTheGameNames()
        {
            var catalog = CatalogDirectory();
            if (catalog == null)
            {
                Console.WriteLine("    (catalog folder not found from the test folder: skipped)");
                return;
            }

            var table = LandmarkTable.Load(
                new PluginLog(Path.Join(Path.GetTempPath(), "poe2access-tests"), false),
                Path.Join(catalog, "landmarks.json"),
                Path.Join(catalog, "important_tgt_files.txt"));
            var labels = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in new[] { "landmarks.json", "important_tgt_files.txt" })
            {
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(Path.Join(catalog, file))) ?? new();
                labels.UnionWith(parsed.Values.SelectMany(tiles => tiles.Values).Select(label => label.Trim()));
            }

            Localization.Detect("es");
            var named = labels.Count(table.HasGameName);
            var untouched = labels.Where(label => table.Localize(label) == label).ToList();
            Console.WriteLine($"    {labels.Count} labels, {named} with a game name in Spanish, {untouched.Count} untouched");
            Localization.Detect("en");
            if (named < MinNamedLabels)
            {
                throw new Exception($"only {named} of {labels.Count} labels read in the game's language (expected at least {MinNamedLabels})");
            }
        }
    }
}
