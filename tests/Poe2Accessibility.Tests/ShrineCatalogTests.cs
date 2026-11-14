namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The shrine catalog: its file read, a shrine found by its row Id or by the buff its use gives, its name by language.</summary>
    public static class ShrineCatalogTests
    {
        private const string Sample = """
            { "shrines": [
              { "id": "Acceleration", "lesser": false, "buff": "shrine_acceleration", "seconds": 45, "names": { "en": "Acceleration Shrine", "es": "Santuario de aceleración" }, "descriptions": { "en": "Death doesn't wait." } },
              { "id": "FireShrine", "lesser": false, "buff": "shrine_fire_2", "seconds": 45, "names": { "en": "Meteoric Shrine" }, "descriptions": {} },
              { "id": "RoyaleShrineBurning", "lesser": false, "buff": "shrine_fire_2", "seconds": 10, "names": { "en": "Royale Burning" }, "descriptions": {} },
              { "id": "AccelerationLesser", "lesser": true, "buff": "shrine_acceleration", "seconds": 20, "names": { "en": "Lesser Acceleration Shrine", "es": "Santuario de aceleración menor" }, "descriptions": {} },
              { "id": "Unnamed", "lesser": false, "buff": "", "seconds": 5, "names": {}, "descriptions": {} }
            ] }
            """;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ARowIsFoundByItsId), ARowIsFoundByItsId),
            (nameof(ARowIsFoundByTheBuffItGives), ARowIsFoundByTheBuffItGives),
            (nameof(ANameFallsBackToEnglishThenToNothing), ANameFallsBackToEnglishThenToNothing),
            (nameof(AMissingOrBadFileIsAnEmptyCatalog), AMissingOrBadFileIsAnEmptyCatalog),
            (nameof(TheShippedCatalogHoldsEveryRowAndTheKnownBuffs), TheShippedCatalogHoldsEveryRowAndTheKnownBuffs),
        };

        private static void ARowIsFoundByItsId()
        {
            var catalog = ShrineCatalog.Parse(Sample);
            Equal(5, catalog.Count, "every row");
            var entry = catalog.ById("AccelerationLesser")!;
            Equal(true, entry.Lesser, "the lesser flag");
            Equal(20, entry.Seconds, "the seconds");
            Equal("shrine_acceleration", entry.Buff, "the buff");
            Equal(null, catalog.ById("Nothing"), "an unknown Id");
            Equal(null, catalog.ById(string.Empty), "an empty Id");
        }

        private static void ARowIsFoundByTheBuffItGives()
        {
            var catalog = ShrineCatalog.Parse(Sample);
            Equal("FireShrine", catalog.ByBuff("shrine_fire_2")!.Id, "the first row of a shared buff");
            Equal("AccelerationLesser", catalog.ByBuff("shrine_acceleration")!.Id, "a lesser shrine takes over a buff the older row shares");
            Equal(null, catalog.ByBuff("shrine_unknown"), "an unknown buff");
            Equal(null, catalog.ByBuff(string.Empty), "no buff: the row without one is not found by it");
        }

        private static void ANameFallsBackToEnglishThenToNothing()
        {
            var catalog = ShrineCatalog.Parse(Sample);
            var lesser = catalog.ById("AccelerationLesser")!;
            Equal("Santuario de aceleración menor", catalog.NameOf(lesser, "es"), "the language's name");
            Equal("Lesser Acceleration Shrine", catalog.NameOf(lesser, "de"), "English for a language it lacks");
            Equal("Meteoric Shrine", catalog.NameOf(catalog.ById("FireShrine")!, "es"), "English when the row has only that");
            Equal(string.Empty, catalog.NameOf(catalog.ById("Unnamed")!, "en"), "no name at all");
        }

        private static void AMissingOrBadFileIsAnEmptyCatalog()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2access-tests");
            var log = new PluginLog(folder, false);
            Equal(0, ShrineCatalog.Load(log, Path.Join(folder, "no-shrines.json")).Count, "a missing file");
            var bad = Path.Join(folder, "bad-shrines.json");
            File.WriteAllText(bad, "{ not json");
            Equal(0, ShrineCatalog.Load(log, bad).Count, "a file that is not JSON");
            Equal(null, ShrineCatalog.Shared.ById("Acceleration"), "the shared catalog is the empty one");
            File.Delete(bad);
        }

        /// <summary>The catalog as shipped, from the game's tables of 0.5.5f (docs/research/league-mechanics.md §7).</summary>
        private static void TheShippedCatalogHoldsEveryRowAndTheKnownBuffs()
        {
            var directory = AppContext.BaseDirectory;
            while (!File.Exists(Path.Join(directory, "Poe2Accessibility", "catalog", "shrines.json")))
            {
                directory = Path.GetDirectoryName(directory) ?? throw new FileNotFoundException("shrines.json is not under any folder above the tests");
            }

            var catalog = ShrineCatalog.Parse(File.ReadAllText(Path.Join(directory, "Poe2Accessibility", "catalog", "shrines.json")));
            Equal(72, catalog.Count, "every row of the Shrines table");
            foreach (var buff in LeaguePieces.ShrineBuffs.Keys)
            {
                var entry = catalog.ByBuff(buff);
                Equal(true, entry != null && !entry.Lesser && entry.Names.ContainsKey("en"), $"the ordinary shrine of {buff}");
            }

            Equal(15, LeaguePieces.ShrineBuffs.Count, "the buffs the league watch knows");
            var lesser = catalog.ById("AccelerationLesser")!;
            Equal("Santuario de aceleración menor", catalog.NameOf(lesser, "es"), "a lesser shrine's Spanish name");
            Equal("AccelerationLesser BrutalLesser RegenLesser MassiveLesser ResistanceLesser DefenceLesser",
                string.Join(" ", new[] { "AccelerationLesser", "BrutalLesser", "RegenLesser", "MassiveLesser", "ResistanceLesser", "DefenceLesser" }.Where(id => catalog.ById(id)!.Lesser)), "the six lesser shrines");
            foreach (var id in new[] { "shrine_acceleration", "shrine_brutal", "shrine_regen", "shrine_massive", "shrine_resistance", "shrine_defence" })
            {
                Equal(true, catalog.ByBuff(id)!.Lesser, $"the lesser shrine named from its buff {id}");
            }
        }
    }
}
