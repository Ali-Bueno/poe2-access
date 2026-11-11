namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;
    using DatTableBuilder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>The `mod-stats` step: the Mods columns a modifier's values follow and the handlers of the description file (docs/research/item-mods.md).</summary>
    public static class ModStatColumnTests
    {
        /// <summary>Where the tests' Mods table holds a key into Stats outside the run of stat columns.</summary>
        private const int LaterStatOffset = 0xA8;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ModStatColumnsAreFoundByTheKnownModifiers), ModStatColumnsAreFoundByTheKnownModifiers),
            (nameof(AModifierWhoseValuesCannotBeToldApartIsUnproven), AModifierWhoseValuesCannotBeToldApartIsUnproven),
            (nameof(TheHiddenStatsOfADescriptionFileAreKept), TheHiddenStatsOfADescriptionFileAreKept),
            (nameof(EveryHandlerOfTheShippedTextsIsKnown), EveryHandlerOfTheShippedTextsIsKnown),
        };

        /// <summary>Stats, and Mods with the two known modifiers, one of four stats and enough plain ones that a stray row is one in more than a hundred.</summary>
        private static (DatTable Mods, DatTable Stats) Tables(int shift = 0, int hybridLeast = 14, int lifeMost = 39)
        {
            var stats = new DatTableBuilder(0x10);
            foreach (var id in new[] { ModStatColumns.LifeStat, ModStatColumns.ArmourStat, "base_maximum_mana", "third_stat", "fourth_stat", "later_stat" })
            {
                stats.Row(w => w.Text(0x0, id));
            }

            const int Columns = 4;
            var afterRun = ModStatColumns.FirstStatOffset + shift + (Columns * ModStatColumns.KeyBytes);
            var mods = new DatTableBuilder(0xC0 + shift);
            void Row(string id, int[] keys, int[] ranges, int later = -1, int strayRoll = 0) => mods.Row(w =>
            {
                // A number and a text after the run, as the game's row has: no key into Stats.
                w.Text(0x0, id).Int(afterRun, 1).Text(afterRun + sizeof(int), "name");
                for (var stat = 0; stat < Columns; stat++)
                {
                    w.WideKey(ModStatColumns.FirstStatOffset + shift + (stat * ModStatColumns.KeyBytes), stat < keys.Length ? keys[stat] : -1);
                }

                for (var at = 0; at < ranges.Length; at++)
                {
                    w.Int(ModStatColumns.RangesOffset + shift + (at * sizeof(int)), ranges[at]);
                }

                if (strayRoll != 0)
                {
                    w.Int(ModStatColumns.RangesOffset + shift + ((Columns - 1) * ModStatColumns.RangeBytes), strayRoll);
                }

                w.WideKey(LaterStatOffset + shift, later);
            });
            Row(ModStatColumns.KnownLife, new[] { 0 }, new[] { 30, lifeMost });
            Row(ModStatColumns.KnownHybrid, new[] { 1, 0 }, new[] { hybridLeast, 20, 11, 19 });
            Row("FourStats1", new[] { 2, 3, 4, 0 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            Row("LaterStat1", new[] { 2 }, new[] { 1, 2 }, later: 5);
            Row("Gap1", new[] { 2, -1, 3 }, new[] { 1, 2, 0, 0, 3, 4 });
            Row("Stray1", new[] { 2 }, new[] { 1, 2 }, strayRoll: 7);
            Row("Reversed1", new[] { 2 }, new[] { -18, -20 });
            for (var plain = 0; plain < ModStatColumns.RollsWithoutStatPer; plain++)
            {
                Row($"Plain{plain}", new[] { 2 }, new[] { 1, 2 });
            }

            return (mods.Build(), stats.Build());
        }

        private static void ModStatColumnsAreFoundByTheKnownModifiers()
        {
            var (mods, stats) = Tables();
            var c = ModStatColumns.Find(mods, stats) ?? throw new Exception("not found");
            Equal("30,46,62,78", string.Join(',', c.Stats), "the run of stat columns, ended by what is no key into Stats");
            Equal(ModStatColumns.RangesOffset, c.Ranges, "the ranges, by the hybrid's own");
            Equal(LaterStatOffset.ToString(), string.Join(',', c.Others), "another column that reads as a key into Stats");
            Equal(null, c.Problem(mods, stats), "the known modifiers read as the tooltips showed them");
            string Read(string id) => ModStatColumns.Written(c.Read(mods, stats, DatColumnSearch.RowOf(mods, id)));
            Equal("base_maximum_life 30 39", Read(ModStatColumns.KnownLife), "one stat with what it rolls");
            Equal("local_physical_damage_reduction_rating_+% 14 20 base_maximum_life 11 19", Read(ModStatColumns.KnownHybrid), "two stats in the order the values follow");
            Equal("base_maximum_mana 1 2 third_stat 3 4 fourth_stat 5 6 base_maximum_life 7 8", Read("FourStats1"), "four");
            Equal("base_maximum_mana -20 -18", Read("Reversed1"), "the least first, whichever way the row holds them");

            const int Shift = 8;
            var (movedMods, movedStats) = Tables(Shift);
            var moved = ModStatColumns.Find(movedMods, movedStats) ?? throw new Exception("not found after a shift");
            Expect((ModStatColumns.FirstStatOffset + Shift, ModStatColumns.RangesOffset + Shift), (moved.Stats[0], moved.Ranges), "found where a patch moved them");

            var (otherMods, otherStats) = Tables(hybridLeast: 15);
            Expect(true, ModStatColumns.Find(otherMods, otherStats) == null, "a hybrid that rolls otherwise: the ranges are not these, the step fails");
            var (lowMods, lowStats) = Tables(lifeMost: 31);
            Expect(true, ModStatColumns.Find(lowMods, lowStats)?.Problem(lowMods, lowStats) != null, "a life modifier that cannot roll what an item held: the step fails");
        }

        private static void AModifierWhoseValuesCannotBeToldApartIsUnproven()
        {
            var (mods, stats) = Tables();
            var c = ModStatColumns.Find(mods, stats) ?? throw new Exception("not found");
            ModStatColumns.Row Read(string id) => c.Read(mods, stats, DatColumnSearch.RowOf(mods, id));
            Expect((false, "base_maximum_mana later_stat"), (Read("LaterStat1").Proven, ModStatColumns.Written(Read("LaterStat1"))), "a stat in a column the values were never seen to follow");
            Expect((false, "base_maximum_mana third_stat"), (Read("Gap1").Proven, ModStatColumns.Written(Read("Gap1"))), "a stat after an empty column");
            Equal(false, Read("Stray1").Proven, "a number rolled where no stat is");
            Equal(true, Read("FourStats1").Proven, "the run filled from its start");
        }

        private static void TheHiddenStatsOfADescriptionFileAreKept()
        {
            var file = StatDescriptionFile.Parse("no_description level\ndescription\n\t1 base_maximum_life\n\t1\n\t\t# \"{0:+d} to maximum Life\"\nno_description item_drop_slots\n");
            Equal("level,item_drop_slots", string.Join(',', file.Hidden), "the stats the file says have no line");
            Equal(1, file.Descriptions.Count, "beside its blocks");
        }

        private static void EveryHandlerOfTheShippedTextsIsKnown()
        {
            Equal("negate,canonical_line,per_minute_to_per_second_1dp", string.Join(',', StatHandlers.NamesIn("#|-1 \"{0}% reduced\" negate 1 canonical_line per_minute_to_per_second_1dp 2")), "a line's handlers, the one without a stat number among them");
            Equal(true, StatHandlers.IsKnown("milliseconds_to_seconds_2dp_if_required") && !StatHandlers.IsKnown("multiply_by_pi"), "known by the name without its decimals part");
            if (FileCatalogTests.Shipped("mod-texts.json") is not { } file)
            {
                return; // away from the repository
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(file));
            var names = document.RootElement.GetProperty("texts").EnumerateArray().SelectMany(block => block.GetProperty("texts").EnumerateObject())
                .SelectMany(language => language.Value.EnumerateArray()).SelectMany(line => StatHandlers.NamesIn(line.GetString()!)).Distinct().Order(StringComparer.Ordinal).ToList();
            Equal(44, names.Count, "the handlers of the 0.5.x description file");
            Equal(string.Empty, string.Join(", ", names.Where(name => !StatHandlers.IsKnown(name))), "none the mod would have to guess");
            Equal(9, names.Count(StatHandlers.Lookups.Contains), "those that show a name looked up");
        }
    }
}
