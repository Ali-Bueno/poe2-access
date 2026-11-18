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
              "Metadata/Monsters/Worthless": { "x": 0 },
              "Metadata/Monsters/Balbala/BalbalaFog": { "x": 100, "t": true }
            } }
            """;
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheSpiritFollowsTheGamesCurve), TheSpiritFollowsTheGamesCurve),
            (nameof(AMonsterIsFoundByItsFile), AMonsterIsFoundByItsFile),
            (nameof(AMissingOrBadFileIsAnEmptyCatalog), AMissingOrBadFileIsAnEmptyCatalog),
            (nameof(TheShippedCatalogHoldsTheKnownCosts), TheShippedCatalogHoldsTheKnownCosts),
        };

        /// <summary>
        ///     The game's curve since 0.3.0 (Path of Building's minion export), anchored on the bound gems read live on
        ///     2026-11-18: the Vaal Guard's 50 at a multiplier of 100 and the Vaal Embalmed Axeman's 53 at 110 — which
        ///     no rounding to tens can give. The round tens published before 0.3.0 (200: 100, 70: 40) no longer hold.
        /// </summary>
        private static void TheSpiritFollowsTheGamesCurve()
        {
            Equal(50, SpectreSpirit.For(100), "Vaal Guard (grenade), 100: the curve's anchor, heard on the bound gem");
            Equal(53, SpectreSpirit.For(110), "Vaal Embalmed Axeman, 110: heard on the bound gem (2026-11-18)");
            Equal(84, SpectreSpirit.For(200), "Priest of the Sun, 200 (100 before 0.3.0)");
            Equal(38, SpectreSpirit.For(70), "Venomous Crab, 70 (40 before 0.3.0)");
            Equal(64, SpectreSpirit.For(140), "Doryani's Elite, 140");
            Equal(57, SpectreSpirit.For(120), "Vaal Embalmed Bearer, 120");
            Equal(42, SpectreSpirit.For(80), "Ribrattle, 80");
            Equal(93, SpectreSpirit.For(230), "Faridun Impaler, 230 (120 before 0.3.0)");
            Equal(48, SpectreSpirit.For(95), "95: just under the anchor, not rounded up to it");
            Equal(46, SpectreSpirit.For(90), "90: under half of 100's cost, where a halving rule says 50");
            Equal(8, SpectreSpirit.For(10), "10: the least multiplier the shipped catalog holds, still a cost");
            Equal(null, SpectreSpirit.For(0), "a multiplier worth nothing");
            Equal(null, SpectreSpirit.For(-1), "a multiplier under nothing");
        }

        private static void AMonsterIsFoundByItsFile()
        {
            var catalog = SpectreCatalog.Parse(Sample);
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "a known monster");
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving@40"), "an entity's path, with its level after the '@'");
            Equal(38, catalog.SpiritOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "another");
            Equal(84, catalog.SpiritOf("Metadata/Monsters/OldForm"), "a row of the catalog's first making (the multiplier alone)");
            Equal(null, catalog.SpiritOf("Metadata/Monsters/Worthless"), "one worth nothing is named without a cost");
            Equal(null, catalog.SpiritOf("Metadata/Monsters/NotThere"), "one the catalog lacks");
            Equal(null, catalog.SpiritOf(string.Empty), "no file at all");
            Equal(("Bow", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving"), "the bow guard's variety");
            Equal(("", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the grenade guard holds no weapon");
            Equal(null, catalog.VarietyOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "a name of one variety needs no telling");
            Equal(null, catalog.VarietyOf("Metadata/Monsters/NotThere"), "an unknown monster's variety");
            Equal(("Bow", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving@40"), "a variety by an entity's path, with its level");
            Equal(("", ""), catalog.VarietyOf("Metadata/Monsters/Balbala/BalbalaFog"), "told apart by cost alone: a variety with no weapon or reach to say");
            Equal(null, catalog.SpiritOf(null!), "no path at all");
            Equal(null, catalog.SpiritOf("@40"), "a level with no file before it");
            var (spirit, variety) = catalog.Lookup("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving@40");
            Equal(53, spirit, "one lookup: the bow guard's cost");
            Equal(("Bow", "ranged"), variety, "one lookup: the bow guard's variety");
        }

        private static void AMissingOrBadFileIsAnEmptyCatalog()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2access-tests", "spectres-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var log = new PluginLog(folder, false);
                var good = Path.Join(folder, "spectres.json");
                File.WriteAllText(good, Sample);
                Equal(50, SpectreCatalog.Load(log, good).SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "a good file, loaded");
                Equal(50, SpectreCatalog.Shared.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "and shared, as the plugin's start leaves it");
                Equal(0, SpectreCatalog.Load(log, Path.Join(folder, "no-spectres.json")).Count, "a missing file");
                var bad = Path.Join(folder, "bad-spectres.json");
                foreach (var (text, what) in new[]
                {
                    ("{ not json", "a file that is not JSON"),
                    ("{}", "JSON with no spectres in it"),
                    ("{ \"spectres\": null }", "spectres of nothing"),
                    ("{ \"spectres\": { \"Metadata/Monsters/X\": \"abc\" } }", "a row that is a word"),
                    ("{ \"spectres\": { \"Metadata/Monsters/X\": { \"x\": \"abc\" } } }", "a multiplier that is a word"),
                    ("{ \"spectres\": { \"Metadata/Monsters/X\": { \"x\": {} } } }", "a multiplier that is an object"),
                })
                {
                    File.WriteAllText(bad, text);
                    Equal(0, SpectreCatalog.Load(log, bad).Count, what);
                }

                Equal(null, SpectreCatalog.Shared.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the shared catalog is the empty one");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
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
            Equal(2767, catalog.Count, "every monster row of MonsterVarieties with a multiplier (the table's escort summons and its Any row are no corpses)");
            Equal(50, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "Vaal Guard (grenade)");
            Equal(53, catalog.SpiritOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving"), "Vaal Guard (spear)");
            Equal(84, catalog.SpiritOf("Metadata/Monsters/BloodBathers/VaalApparition/SunVaalApparition"), "Priest of the Sun");
            Equal(("Bow", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardBowLiving"), "the bow guard, told from its namesakes");
            Equal(("Spear", "melee"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardSpearLiving"), "the spear guard: a Warstaff in the data, a spear by its id");
            Equal(("", "ranged"), catalog.VarietyOf("Metadata/Monsters/VaalMonsters/Living/VaalGuardMortarLiving"), "the grenade guard");
            Equal(38, catalog.SpiritOf("Metadata/Monsters/QuillCrab/QuillCrabPoison"), "Venomous Crab");
            Equal(84, catalog.SpiritOf("Metadata/Monsters/Gargoyle/GargoyleGolemRed"), "Gargoyle Demon");
            Equal(48, catalog.SpiritOf("Metadata/Monsters/Anchorite/AnchoriteFlathead"), "a multiplier of 95, no multiple of ten");
            Equal(("", ""), catalog.VarietyOf("Metadata/Monsters/Balbala/BalbalaFog"), "a told variety with no weapon or reach to say");
            Equal(("Unarmed", ""), catalog.VarietyOf("Metadata/Monsters/Baron/BaronBossHumanFormWolf"), "an unarmed variety, told from the sword form of its name");
            Equal(53, catalog.SpiritOf("Metadata/Monsters/Procession/ProcessionAxeShield"), "the Vaal Embalmed Axeman bound live (2026-11-18)");
        }
    }
}
