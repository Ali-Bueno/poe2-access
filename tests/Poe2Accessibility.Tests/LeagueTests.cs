namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The league mechanics' objects: which are listed by their states and icons, and which changes are said, each once.</summary>
    public static class LeagueTests
    {
        private const string Remnant = "Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter";
        private const string Marker = "Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker";
        private const string Shard = "Metadata/MiscellaneousObjects/Delirium/DeliriumShardSeethingChyme";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APieceIsListedWhileItsStatesSayItIsThereToFind), APieceIsListedWhileItsStatesSayItIsThereToFind),
            (nameof(ARemnantsProgressIsSaidOnceAtEachStep), ARemnantsProgressIsSaidOnceAtEachStep),
            (nameof(EachMechanicsChangeIsSaidOnce), EachMechanicsChangeIsSaidOnce),
            (nameof(AStrongboxAmbushThenItCanBeOpened), AStrongboxAmbushThenItCanBeOpened),
            (nameof(ARogueExileIsSaidOnceAsTheFightStarts), ARogueExileIsSaidOnceAsTheFightStarts),
            (nameof(TheShrineUsedAndThePossessedAreSaidOnce), TheShrineUsedAndThePossessedAreSaidOnce),
            (nameof(TheFogIsSaidOnlyWhileTheMirrorRuns), TheFogIsSaidOnlyWhileTheMirrorRuns),
            (nameof(WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften), WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften),
            (nameof(LeagueLinesReadAsTheGameWritesThem), LeagueLinesReadAsTheGameWritesThem),
        };

        private static (string, long)[] States(params (string, long)[] states) => states;

        private static void APieceIsListedWhileItsStatesSayItIsThereToFind()
        {
            var remnant = LeaguePieces.RuleOf(Remnant + "@12")!;
            Equal(LeaguePiece.VerisiumRemnant, remnant.Piece, "a piece by its file, whatever its instance tail");
            Equal(true, remnant.WithoutTarget, "listed through its waves, when the game makes it untargetable");
            Equal("0 1 2 5 6", string.Join(" ", Enumerable.Range(0, 9).Where(value => LeaguePieces.IsListed(remnant, States(("activated", value)), string.Empty))),
                "listed until done (7) and not once spoiled (3, 4, 8)");
            Equal(false, LeaguePieces.IsListed(remnant, States(), string.Empty), "silent when its states are not read");
            Equal(null, LeaguePieces.RuleOf(Remnant + "Daemon"), "the remnant's daemon is no remnant");

            var marker = LeaguePieces.RuleOf(Marker + "@3")!;
            Equal(true, LeaguePieces.IsListed(marker, States(), "ExpeditionChest2"), "a marker by its icon");
            Equal(false, LeaguePieces.IsListed(marker, States(), string.Empty), "not before the game gives it one");
            Equal(ExpeditionFind.Elite, LeaguePieces.FindOf(LeaguePiece.ExpeditionMarker, "ExpeditionEliteMonster"), "what its icon says it buries");
            Equal(null, LeaguePieces.FindOf(LeaguePiece.ExpeditionMarker, "ExpeditionDynamite"), "a charge's icon is no find");
            var relic = LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionRelic")!;
            Equal(true, LeaguePieces.IsListed(relic, States(("exploded", 0)), "ExpeditionPOI"), "a relic with its icon");
            Equal(false, LeaguePieces.IsListed(relic, States(("exploded", 1)), "ExpeditionPOI"), "and not once blown");

            var shard = LeaguePieces.RuleOf(Shard)!;
            Equal(LeaguePiece.ShardPortal, shard.Piece, "the red shard opens a portal");
            Equal(true, LeaguePieces.IsListed(shard, States(("visible", 1), ("detonate", 0)), string.Empty), "a shard risen and whole");
            Equal(false, LeaguePieces.IsListed(shard, States(("visible", 1), ("detonate", 1)), string.Empty), "not once shattered");
            Equal(false, LeaguePieces.IsListed(shard, States(("visible", 0), ("detonate", 0)), string.Empty), "nor before it rises");
            Equal(null, LeaguePieces.RuleOf(Shard + "Return"), "a shard's family is its exact files");
            Equal(true, LeaguePieces.IsListed(LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Delirium/DeliriumGigaMirror")!, States(("enabled", 1)), "GrandMirrorActive"), "the Grand Mirror once enabled");

            var spirit = LeaguePieces.RuleOf("Metadata/Monsters/TormentedSpirits/TormentedSpiritoftheOwlPrimal@5")!;
            Equal(LeaguePiece.AzmeriSpirit, spirit.Piece, "a spirit by the start of its name");
            Equal(true, LeaguePieces.IsListed(spirit, States(("active", 1), ("wisp_time", 30)), "SpiritActivated"), "listed while its icon is on");
            Equal(false, LeaguePieces.IsListed(spirit, States(("active", 0)), "SpiritActivated"), "not before");
            Equal(null, LeaguePieces.RuleOf("Metadata/Monsters/TormentedSpirits/TormentedSpiritoftheOwlPrimalPossesedDaemon"), "the daemon in a possessed monster is no wisp to follow");
            Equal(true, LeaguePieces.IsListed(LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Brequel/BrequelSkillObject")!, States(("ready", 1)), string.Empty), "a hive's skill once ready");
            Equal(false, LeaguePieces.RuleOf("Metadata/Chests/StrongBoxes/CasterStrongboxHigh")!.Lists, "a strongbox is followed, the chest rule lists it");
            Equal(LeaguePiece.ExpeditionDetonator, LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionDetonatorTreasureIsland@2")!.Piece, "every detonator");
        }

        private static void ARemnantsProgressIsSaidOnceAtEachStep()
        {
            var news = new LeagueNews();
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 1)), "Expedition2RemnantActive").Count, "what it shows when first seen is no news");
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 1)), "Expedition2RemnantActive").Count, "nor while it stands");
            Equal("RemnantWaves", string.Join(" ", news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 5)), "Expedition2RemnantActive")), "its waves");
            Equal("RemnantReward", string.Join(" ", news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 6)), "Expedition2RemnantActive")), "its reward");
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 7)), "Expedition2RemnantDeactivated").Count, "done: dropped from the list, nothing said");
            foreach (var spoiled in new[] { 3L, 4L, 8L })
            {
                var id = (uint)(10 + spoiled);
                news.Heard(id, LeaguePiece.VerisiumRemnant, States(("activated", 2)), string.Empty);
                Equal("RemnantSpoiled", string.Join(" ", news.Heard(id, LeaguePiece.VerisiumRemnant, States(("activated", spoiled)), string.Empty)), $"{spoiled} spoils it");
            }

            news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 0)), string.Empty);
            Equal("RemnantTome", string.Join(" ", news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 1)), string.Empty)), "the campaign's: its tome once its first fight is over");
            news.Reset();
            Equal(0, news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 5)), string.Empty).Count, "a new area knows nothing of the last");
        }

        private static void EachMechanicsChangeIsSaidOnce()
        {
            var news = new LeagueNews();
            string Heard(uint id, LeaguePiece piece, string icon, params (string, long)[] states) => string.Join(" ", news.Heard(id, piece, states, icon));

            Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 0), ("activated", 0));
            Equal("DetonatorReady", Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 1), ("activated", 0)), "every charge placed");
            Equal("Detonated", Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 1), ("activated", 1)), "and blown");

            Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 0), ("wave", 0));
            Equal("SimulacrumWave", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 1), ("wave", 2)), "a wave begins");
            Equal(3, LeaguePieces.WaveOf(States(("active", 1), ("wave", 2))), "counted from 1");
            Equal("SimulacrumWaveCleared", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 0), ("wave", 3)), "and is over");
            Equal("SimulacrumOver", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 2), ("wave", 7)), "the Simulacrum's end");

            Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 0));
            Equal("BreachOpen", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 1)), "a breach opens");
            Equal("BreachStabilised", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 2)), "is stabilised");
            Equal("StableBreachCollapsed", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 3)), "and collapses");
            Heard(4, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 1));
            Equal("UnstableBreachCollapsed", Heard(4, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 4)), "one never stabilised");

            Heard(5, LeaguePiece.VaalBeacon, "IncursionArchitectUpgrade", ("activated", 1), ("open", 0));
            Equal("BeaconReady", Heard(5, LeaguePiece.VaalBeacon, "IncursionArchitectUpgrade", ("activated", 2), ("open", 0)), "a beacon to use");
            Equal("BeaconDone BeaconPortal", Heard(5, LeaguePiece.VaalBeacon, "IncursionEncounterComplete", ("activated", 3), ("open", 1)), "used, with its portal");

            Heard(6, LeaguePiece.RitualAltar, "RitualRune", ("current_state", 2));
            Equal("RitualDone", Heard(6, LeaguePiece.RitualAltar, "RitualRuneFinished", ("current_state", 3)), "a ritual won");
            Equal(string.Empty, Heard(6, LeaguePiece.RitualAltar, "RitualRuneFinished", ("current_state", 4)), "its favours bought are no news");

            Heard(7, LeaguePiece.AbyssPit, "AbyssPitActive");
            Equal("AbyssClosed", Heard(7, LeaguePiece.AbyssPit, LeaguePieces.PitClosedIcon), "a pit closes as its icon dims");
            Equal(string.Empty, Heard(8, LeaguePiece.AbyssPit, LeaguePieces.PitClosedIcon), "one closed before it was seen is not");
        }

        private static void AStrongboxAmbushThenItCanBeOpened()
        {
            var news = new LeagueNews();
            string Heard(uint id, long started, long opened) => string.Join(" ", news.Heard(id, LeaguePiece.Strongbox, States(("started", started), ("opened", opened), ("robot", 0)), "Strongbox"));
            Heard(1, 0, 0);
            Equal("StrongboxAmbush", Heard(1, 1, 0), "opened by the player: the ambush");
            Equal(string.Empty, Heard(1, 1, 0), "said once");
            Equal("StrongboxReady", Heard(1, 0, 0), "the fight over, it can be opened");
            Heard(2, 1, 0);
            Equal(string.Empty, Heard(2, 0, 1), "not when it has opened itself");
        }

        private static void ARogueExileIsSaidOnceAsTheFightStarts()
        {
            var news = new LeagueNews();
            string Heard(uint id, long bar) => string.Join(" ", news.Heard(id, LeaguePiece.RogueExile, States(("boss_life_bar", bar), ("fleeing", 0)), "RogueExile"));
            Equal("RogueExileFight", Heard(1, 1), "already fighting when first seen");
            Heard(1, 0);
            Equal(string.Empty, Heard(1, 1), "once, though the bar comes back");
            Equal(string.Empty, Heard(2, 0), "another not yet engaged");
            Equal("RogueExileFight", Heard(2, 1), "engages");
        }

        private static void TheShrineUsedAndThePossessedAreSaidOnce()
        {
            var news = new LeagueNews();
            Equal(0, news.Shrines(new[] { "shrine_fire_2", "frozen" }).Count, "a shrine's buff there as the area opens is not news");
            Equal("Divine", string.Join(" ", news.Shrines(new[] { "shrine_fire_2", "shrine_godmode_2_1A" })), "a new shrine, by its buff whatever the host's suffix");
            Equal(0, news.Shrines(new[] { "shrine_fire_2", "shrine_godmode_2" }).Count, "not while it lasts");
            Equal(0, news.Shrines(new[] { "shrine_godmode_2_boss" }).Count, "a boss's own copy is no shrine of the player's");
            Equal("Meteoric", string.Join(" ", news.Shrines(new[] { "shrine_fire_2" })), "the same shrine used again");
            Equal(true, news.Possessed(40), "a possessed monster");
            Equal(false, news.Possessed(40), "once");
        }

        private static void TheFogIsSaidOnlyWhileTheMirrorRuns()
        {
            var news = new LeagueNews();
            Equal(null, news.Fog(1), "no mirror walked through: the stat is not news");
            Equal(null, news.Fog(0), "nor its change");
            news.Heard(9, LeaguePiece.DeliriumMirror, States(("interacted", 0)), "AfflictionInitiator");
            Equal("DeliriumBegan", string.Join(" ", news.Heard(9, LeaguePiece.DeliriumMirror, States(("interacted", 1)), "AfflictionInitiator")), "walked through the mirror");
            Equal(true, news.DeliriumRunning, "the encounter runs");
            Equal(LeagueLine.InFog, news.Fog(1), "in the fog");
            Equal(null, news.Fog(1), "said once");
            Equal(null, news.Fog(null), "unread is no change");
            Equal(LeagueLine.OutOfFog, news.Fog(0), "out of it");
            news.Reset();
            Equal(false, news.DeliriumRunning, "a new area");
        }

        private static void WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften()
        {
            var placing = new PlacementNews();
            var none = Array.Empty<(uint, ExpeditionFind)>();
            var chests = new[] { (1u, ExpeditionFind.Chest), (2u, ExpeditionFind.Chest), (3u, ExpeditionFind.Elite) };
            string Said(IReadOnlyList<(ExpeditionFind Find, int Count)>? finds) =>
                finds == null ? "-" : string.Join(", ", finds.Select(find => $"{find.Count} {find.Find}"));

            Equal("-", Said(placing.Covers(chests, false, 0)), "not placing: silent");
            Equal("-", Said(placing.Covers(none, true, 100)), "placing starts over nothing: nothing to say yet");
            Equal("2 Chest, 1 Elite", Said(placing.Covers(chests, true, 1000)), "by kind and count");
            Equal("-", Said(placing.Covers(chests, true, 1100)), "only on change");
            Equal("-", Said(placing.Covers(chests.Take(1).ToArray(), true, 1000 + PlacementNews.CoverEveryMs - 1)), "not sooner than the limit");
            Equal("1 Chest", Said(placing.Covers(chests.Take(1).ToArray(), true, 1000 + PlacementNews.CoverEveryMs)), "the change once it may be said");
            Equal(string.Empty, Said(placing.Covers(none, true, 3000)), "covering nothing is said too");
            Equal("-", Said(placing.Covers(none, false, 3100)), "leaving placement is silent");

            Equal(true, placing.Refused(true), "a place refused");
            Equal(false, placing.Refused(true), "once");
            Equal(false, placing.Refused(false), "allowed again");

            Equal(null, placing.Placed(new[] { 7u, 8u }), "the charges there as the area opens");
            Equal(3, placing.Placed(new[] { 7u, 8u, 9u }), "a new charge: the count");
            Equal(null, placing.Placed(new[] { 9u }), "one walked away from is not a charge undone");
        }

        private static void LeagueLinesReadAsTheGameWritesThem()
        {
            Localization.Set("es", new());
            try
            {
                Equal("Cubre 2 cofres, 1 élite", Announcements.ExpeditionCovers(new[] { (ExpeditionFind.Chest, 2), (ExpeditionFind.Elite, 1) }), "what a charge would blow");
                Equal("No cubre nada", Announcements.ExpeditionCovers(Array.Empty<(ExpeditionFind, int)>()), "and nothing");
                Equal("Oleada 3 de 7", Announcements.League(LeagueLine.SimulacrumWave, string.Empty, 3), "the Simulacrum's wave");
                Equal("Caja fuerte arcana: emboscada", Announcements.League(LeagueLine.StrongboxAmbush, "Caja fuerte arcana", 0), "a strongbox by the game's name");
                Equal("Caja fuerte: ya se puede abrir", Announcements.League(LeagueLine.StrongboxReady, string.Empty, 0), "or the game's word for one");
                Equal("Cofre de expedición", Announcements.LeaguePieceName(LeaguePiece.ExpeditionMarker, ExpeditionFind.Chest), "a marker by what it buries");
                Equal(true, Announcements.Shrine(ShrineKind.Meteoric).StartsWith("Santuario de meteoritos.", StringComparison.Ordinal), "a shrine by its name first");
            }
            finally
            {
                Localization.Set("en", new());
            }

            Equal("Covers 1 boss, 2 relics", Announcements.ExpeditionCovers(new[] { (ExpeditionFind.Boss, 1), (ExpeditionFind.Relic, 2) }), "in English");
        }
    }
}
