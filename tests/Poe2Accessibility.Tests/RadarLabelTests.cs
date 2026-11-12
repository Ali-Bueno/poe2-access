namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Newtonsoft.Json;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Radar-table labels: a name the game has is said as the game names it, the rest as before.</summary>
    public static class RadarLabelTests
    {
        /// <summary>Labels the real radar tables must read in the game's language at least (measured 2026-11-08: 92 of 211).</summary>
        private const int MinNamedLabels = 90;

        /// <summary>Set to 1, the real tables' test lists every label, what it reads as in Spanish and the pieces left as written.</summary>
        private const string ListLabelsVariable = "POE2ACCESS_LIST_LABELS";

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

        private static LandmarkTable Table()
        {
            var directory = Path.Join(Path.GetTempPath(), "poe2access-tests", "radar-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var landmarks = Path.Join(directory, "landmarks.json");
            File.WriteAllText(landmarks, "{}");
            File.WriteAllText(Path.Join(directory, "area-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["G4_2_1"] = "Journey's End", ["G1_6"] = "The Galai Gates", ["G1_15"] = "Ogham Manor" },
                ["es"] = new() { ["G4_2_1"] = "El final del viaje", ["G1_6"] = "Las puertas de Galai", ["G1_15"] = "Mansión de Ogham" },
            }));
            File.WriteAllText(Path.Join(directory, "monster-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["Captain Hartlin"] = "Captain Hartlin", ["Zelina"] = "Zelina", ["Zolin"] = "Zolin", ["Mektul, The Forgemaster"] = "Mektul, the Forgemaster", ["Yama"] = "Yama The White", ["Foul Ritual"] = "Foul Ritual" },
                ["es"] = new() { ["Captain Hartlin"] = "Capitán Hartlin", ["Zelina"] = "Zelina", ["Zolin"] = "Zolin", ["Mektul, The Forgemaster"] = "Mektul, el Maestro de la forja", ["Yama"] = "Yama, el Blanco", ["Foul Ritual"] = "Ritual repugnante" },
            }));
            File.WriteAllText(Path.Join(directory, "item-names.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["CurrencyLesserJeweller"] = "Lesser Jeweller's Orb" },
                ["es"] = new() { ["CurrencyLesserJeweller"] = "Orbe de joyero menor" },
            }));
            File.WriteAllText(Path.Join(directory, "world-titles.json"), JsonConvert.SerializeObject(new Dictionary<string, Dictionary<string, string>>
            {
                ["en"] = new() { ["icon:TitanActive"] = "Dais of Reckoning", ["label:water"] = "Sacred Water", ["body:metadata/x"] = "Grants Sacred Water" },
                ["es"] = new() { ["icon:TitanActive"] = "Tarima del juicio final", ["label:water"] = "Agua sagrada", ["body:metadata/x"] = "Otorga agua sagrada" },
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
            Expect("Tarima del juicio final", table.Localize("Dais of Reckoning"), "a place the game names only on a minimap icon");
            Expect("Sacred Water", table.Localize("Sacred Water"), "a word of the mod's own tables is no name of a place");
            Expect("Otorga agua sagrada", table.WorldBody("Metadata/X"), "what a box says under its title");
            Localization.Detect("en");
        }

        private static void ANameWithACommaIsWholeBeforeItsNote()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("Mektul, el Maestro de la forja (Reforging Bench)", table.Localize("Mektul, The Forgemaster (Reforging Bench)"), "the comma stays inside the name");
            Expect("Mektul, el Maestro de la forja (Tarima del juicio final)", table.Localize("Mektul, The Forgemaster (Dais of Reckoning)"), "a note that is a name of the game's");
            Expect("Mansión de Ogham", table.Localize("Oghams Manor"), "a name the table misspells");
            Localization.Detect("en");
        }

        private static void CommonWordsAndNotesAreTranslatedNamesAreKept()
        {
            var table = Table();
            Localization.Detect("es");
            Expect("Canal Mechanism (punto de ruta)", table.Localize("Canal Mechanism (waypoint)"), "a note in lower case");
            Expect("Crab Rare", table.Localize("Crab Rare"), "no name, no common word: as written");
            Expect("Misión, Humming Pearl", table.Localize("Quest - Humming Pearl"), "a common word, the rest as written");
            Expect(true, table.UnnamedPieces("Quest - Humming Pearl").SequenceEqual(new[] { "Humming Pearl" }), "what is left unnamed");
            Expect("Misión, cofre del muerto", table.Localize("Quest - deadman chest"), "a whole note of the table's in the mod's words");
            Expect(false, table.UnnamedPieces("Quest - deadman chest").Any(), "and nothing of it left unnamed");
            Expect("Campana (recompensas)", table.Localize("Bell (Rewards)"), "a whole note is taken before its pieces");
            Expect("Cofre de la campana (gema de habilidad de nivel 6)", table.Localize("Bell Chest (Skill Gem Level 6)"), "a piece of the mod's and a reward note");
            Expect("Ritual repugnante (nivel 9)", table.Localize("Foul Ritual (Level 9)"), "a level in brackets");
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
            if (Environment.GetEnvironmentVariable(ListLabelsVariable) == "1")
            {
                foreach (var label in labels)
                {
                    Console.WriteLine($"      {(table.Localize(label) == label ? "=" : ">")} {label} | {table.Localize(label)} | unnamed: {string.Join("; ", table.UnnamedPieces(label))}");
                }
            }

            Localization.Detect("en");
            if (named < MinNamedLabels)
            {
                throw new Exception($"only {named} of {labels.Count} labels read in the game's language (expected at least {MinNamedLabels})");
            }
        }
    }
}
