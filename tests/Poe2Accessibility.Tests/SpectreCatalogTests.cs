namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The spectre catalog: the Spirit a monster's corpse asks, from its experience multiplier (docs/research/corpse-scanning.md).</summary>
    public static class SpectreCatalogTests
    {
        private const string Sample = """
            { "spectres": {
              "Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving": { "x": 100, "a": "ranged", "t": true },
              "Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving": { "x": 110, "w": "Bow", "a": "ranged", "t": true },
              "Metadata/Monsters/QuillCrab/QuillCrabPoison": { "x": 70 },
              "Metadata/Monsters/OldForm": 200,
              "Metadata/Monsters/Worthless": { "x": 0 }
            } }
            """;
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheSpiritIsHalfTheExperienceToTheNearestTen), TheSpiritIsHalfTheExperienceToTheNearestTen),
            (nameof(AMonsterIsFoundByItsFile), AMonsterIsFoundByItsFile),
            (nameof(AMissingOrBadFileIsAnEmptyCatalog), AMissingOrBadFileIsAnEmptyCatalog),
            (nameof(TheShippedCatalogHoldsTheKnownCosts), TheShippedCatalogHoldsTheKnownCosts),
        };

        /// <summary>The published tooltip costs (poe2wiki, Bind Spectre) of the monsters measured on 2026-11-17.</summary>
        private static void TheSpiritIsHalfTheExperienceToTheNearestTen()
        {
            Equal(50, SpectreCatalog.SpiritFor(100), "Vaal Guard (grenade), 100");
            Equal(60, SpectreCatalog.SpiritFor(110), "Vaal Guard (spear), 110");
            Equal(100, SpectreCatalog.SpiritFor(200), "Priest of the Sun, 200");
            Equal(40, SpectreCatalog.SpiritFor(70), "Venomous Crab, 70");
            Equal(70, SpectreCatalog.SpiritFor(140), "Doryani's Elite, 140");
            Equal(60, SpectreCatalog.SpiritFor(120), "Vaal Embalmed Bearer, 120");
            Equal(40, SpectreCatalog.SpiritFor(80), "Ribrattle, 80");
            Equal(120, SpectreCatalog.SpiritFor(230), "Faridun Impaler, 230");
            Equal(null, SpectreCatalog.SpiritFor(0), "a multiplier worth nothing");
        }

        private static void AMonsterIsFoundByItsFile()
        {
            var catalog = SpectreCatalog.Parse(Sample);
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "a known monster");
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving@40"), "an entity's path, with its level after the '@'");
            Equal(40, catalog.SpiritOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "another");
            Equal(100, catalog.SpiritOf("Metadata/Monsters/OldForm"), "a row of the catalog's first making (the multiplier alone)");
            Equal(null, catalog.SpiritOf("Metadata/Monsters/Worthless"), "one worth nothing is named without a cost");
            Equal(null, catalog.SpiritOf("Metadata/Monsters/NotThere"), "one the catalog lacks");
            Equal(null, catalog.SpiritOf(string.Empty), "no file at all");
            Equal(("Bow", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving"), "the bow guard's variety");
            Equal(("", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the grenade guard holds no weapon");
            Equal(null, catalog.VarietyOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "a name of one variety needs no telling");
            Equal(null, catalog.VarietyOf("Metadata/Monsters/NotThere"), "an unknown monster's variety");
        }

        private static void AMissingOrBadFileIsAnEmptyCatalog()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2access-tests");
            var log = new PluginLog(folder, false);
            Equal(0, SpectreCatalog.Load(log, Path.Join(folder, "no-spectres.json")).Count, "a missing file");
            var bad = Path.Join(folder, "bad-spectres.json");
            File.WriteAllText(bad, "{ not json");
            Equal(0, SpectreCatalog.Load(log, bad).Count, "a file that is not JSON");
            Equal(null, SpectreCatalog.Shared.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the shared catalog is the empty one");
            File.Delete(bad);
        }

        /// <summary>The catalog as shipped, from the MonsterVarieties table read on 2026-11-17 (docs/research/corpse-scanning.md).</summary>
        private static void TheShippedCatalogHoldsTheKnownCosts()
        {
            var directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Join(directory, "Poe2Accessibility", "catalog", "spectres.json")))
            {
                directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException("spectres.json is not under any folder above the tests");
            }

            var catalog = SpectreCatalog.Parse(File.ReadAllText(Path.Join(directory, "Poe2Accessibility", "catalog", "spectres.json")));
            Equal(2794, catalog.Count, "every row of MonsterVarieties with a multiplier");
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "Vaal Guard (grenade)");
            Equal(60, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving"), "Vaal Guard (spear)");
            Equal(100, catalog.SpiritOf("Metadata/Monsters/BloodBathers/VaalApparition/SunVaalApparition"), "Priest of the Sun");
            Equal(("Bow", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving"), "the bow guard, told from its namesakes");
            Equal(("Warstaff", "melee"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving"), "the spear guard (its spear is a Warstaff in the data)");
            Equal(("", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the grenade guard");
            Equal(40, catalog.SpiritOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "Venomous Crab");
            Equal(100, catalog.SpiritOf("Metadata/Monsters/Gargoyle/GargoyleGolemRed"), "Gargoyle Demon");
        }
    }
}
