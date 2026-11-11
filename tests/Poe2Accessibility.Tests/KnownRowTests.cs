namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Check;
    using Builder = DatColumnSearchTests.DatTableBuilder;

    /// <summary>
    ///     Every key column of the endgame and skill tables has a known row from the game's files (audit finding 12): a key
    ///     of the same table that fits by its shape, nearer where the column was measured, is refused by that row's value.
    /// </summary>
    public static class KnownRowTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AClientKeyPutWhereTheNodeStatNameWasIsRefused), AClientKeyPutWhereTheNodeStatNameWasIsRefused),
            (nameof(AnEndgameMapsAreaIsTheOneItsFlavourNames), AnEndgameMapsAreaIsTheOneItsFlavourNames),
            (nameof(ASkillsActiveSkillAndStatSetAreTheFireballs), ASkillsActiveSkillAndStatSetAreTheFireballs),
            (nameof(AKnownRowTheDataLacksIsSaid), AKnownRowTheDataLacksIsSaid),
        };

        private static DatTable Ids(params string[] ids)
        {
            var builder = new Builder(0x8);
            foreach (var id in ids)
            {
                builder.Row(w => w.Text(0, id));
            }

            return builder.Build();
        }

        private static readonly DatTable Visuals = Ids("Delirium", EndgameColumns.KnownNodeStatVisual);

        private static readonly DatTable Clients = Ids("Other", "Another", "DeathScreenRespawnLimited1", EndgameColumns.KnownNodeStatName, EndgameColumns.KnownNodeStatText,
            "MapDeliriumFogBankName", "MapDeliriumFogBankDescription");

        /// <summary>Node stats after a patch put a new client key at +0x54: the name and the description move 0x10 on.</summary>
        private static DatTable NodeStats() => new Builder(0x85)
            .Row(w => w.WideKey(0x0, 0).WideKey(0x44, 0).WideKey(0x54, 2).WideKey(0x64, 5).WideKey(0x74, 6))
            .Row(w => w.WideKey(0x0, 1).WideKey(0x44, 1).WideKey(0x54, 2).WideKey(0x64, 3).WideKey(0x74, 4))
            .Build();

        private static void AClientKeyPutWhereTheNodeStatNameWasIsRefused()
        {
            var search = new DatColumnSearch();
            var columns = new EndgameColumns();
            columns.FindNodeStats(search, NodeStats(), Visuals, Clients, Ids("dummy_stat_display_nothing", EndgameColumns.KnownNodeStat));
            Expect("44 64 74", $"{columns.NodeStatVisual:X} {columns.NodeStatName:X} {columns.NodeStatText:X}", "the mirror's row names its own name and description");
            Expect(true, search.AllFound && search.Unverified.Count == 0, "every known row was there and checked");
        }

        private static void AnEndgameMapsAreaIsTheOneItsFlavourNames()
        {
            // A world-area key put first: the area, the flavour and the pin move 0x10 on, and zeros lie where the pin was.
            var areas = Ids("G_Endgame_Town", EndgameColumns.KnownMapArea, EndgameColumns.KnownTowerMap);
            var pins = Ids("DefaultMap", EndgameColumns.KnownTowerMapPin, "CorruptedTower");
            var maps = new Builder(0x70)
                .Row(w => w.WideKey(0x0, 0).WideKey(0x10, 1).Text(0x30, EndgameColumns.KnownMapFlavour).WideKey(0x4C, 0).WideKey(0x5C, 2))
                .Row(w => w.WideKey(0x0, 0).WideKey(0x10, 2).Text(0x30, "The grandest of monuments, standing proudly before an audience of none.").WideKey(0x4C, 1).WideKey(0x5C, 2))
                .Build();
            var search = new DatColumnSearch();
            var columns = new EndgameColumns();
            columns.FindMaps(search, maps, areas, pins);
            Expect("10 4C", $"{columns.MapArea:X} {columns.MapPin:X}", "the Penitentiary's area, and the tower map's plain tower pin");
            Expect(true, search.Unverified.Count == 0, "both known rows found");
        }

        private static void ASkillsActiveSkillAndStatSetAreTheFireballs()
        {
            // An ActiveSkills key put at +0x4F: the active skill and the stat set move 0x10 on, zeros lie at +0xA8.
            var actives = Ids("spark", ProjectileColumns.KnownActiveSkill);
            var sets = Ids("SparkPlayer", ProjectileColumns.KnownSkill);
            var skills = new Builder(0xC8)
                .Row(w => w.Text(0, "SparkPlayer").WideKey(0x4F, 1).WideKey(0x5F, 0).WideKey(0xB8, 0))
                .Row(w => w.Text(0, ProjectileColumns.KnownSkill).WideKey(0x4F, 0).WideKey(0x5F, 1).WideKey(0xB8, 1))
                .Build();
            var search = new DatColumnSearch();
            var columns = new ProjectileColumns();
            columns.FindSkillKeys(search, skills, actives, sets);
            Expect("5F B8", $"{columns.SkillActive:X} {columns.SkillStatSet:X}", "the player's Fireball names fireball and its own stat set");
        }

        private static void AKnownRowTheDataLacksIsSaid()
        {
            var search = new DatColumnSearch();
            new EndgameColumns().FindNodeStats(search, NodeStats(), Visuals, Clients, Ids("dummy_stat_display_nothing", "renamed_stat"));
            Expect(3, search.Unverified.Count(line => line.Contains("its known row is not in the table", StringComparison.Ordinal)), "each of the three columns says it was judged by shape alone");
        }
    }
}
