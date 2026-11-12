namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Route beacons and legs, radar labels, loot sounds, fights and boss bodies, local speech.</summary>
    public static class RouteAndFightTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(BeaconPointsAlongTheRoute), BeaconPointsAlongTheRoute),
            (nameof(ClosedWayLeadsToItsNearestPoint), ClosedWayLeadsToItsNearestPoint),
            (nameof(RockStandsAloneWallDoesNot), RockStandsAloneWallDoesNot),
            (nameof(RouteStartsOutsideAPocket), RouteStartsOutsideAPocket),
            (nameof(RadarLabelsSpeakSpanish), RadarLabelsSpeakSpanish),
            (nameof(RadarBossNamesSpeakTheGamesMonsterName), RadarBossNamesSpeakTheGamesMonsterName),
            (nameof(ExperienceProgressIsWithinTheLevel), ExperienceProgressIsWithinTheLevel),
            (nameof(LoopReaderKeepsAPitchedLoopClean), LoopReaderKeepsAPitchedLoopClean),
            (nameof(APileOfLoopsLeavesHeadroom), APileOfLoopsLeavesHeadroom),
            (nameof(DiabloIVSetCoversLootByKindAndRarity), DiabloIVSetCoversLootByKindAndRarity),
            (nameof(SteamsCommandIsFoundInTheLaunchersArguments), SteamsCommandIsFoundInTheLaunchersArguments),
            (nameof(ItemsSoundUnderTheirGroupsVolume), ItemsSoundUnderTheirGroupsVolume),
            (nameof(FightsLastWhileAnEngagingEnemyIsNear), FightsLastWhileAnEngagingEnemyIsNear),
            (nameof(AWaitingBossMakesNoFight), AWaitingBossMakesNoFight),
            (nameof(AHiddenMonsterIsToldByItsBuff), AHiddenMonsterIsToldByItsBuff),
            (nameof(ABossOfSeveralBodiesIsHeardOnce), ABossOfSeveralBodiesIsHeardOnce),
            (nameof(OnlyLinesSaidAloudNearbyAreLocalSpeech), OnlyLinesSaidAloudNearbyAreLocalSpeech),
            (nameof(LootFilterKeepsWhatThePlayerChose), LootFilterKeepsWhatThePlayerChose),
        };

        private static void BeaconPointsAlongTheRoute()
        {
            // A 20x20 open grid (two cells per byte, 0x11 = both walkable) with a wall at x = 10 except a gap at y = 18.
            const int size = 20;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            for (var y = 0; y < size; y++)
            {
                if (y != 18)
                {
                    grid[(y * row) + 5] &= 0xF0; // x = 10 is the low nibble of byte 5
                }
            }

            var route = new List<System.Numerics.Vector2>();
            for (var y = 2; y <= 18; y++)
            {
                route.Add(new System.Numerics.Vector2(5, y));
            }

            for (var x = 6; x <= 15; x++)
            {
                route.Add(new System.Numerics.Vector2(x, 18));
            }

            var player = new System.Numerics.Vector2(5, 2);
            var from = BeaconMath.NearestIndex(route, player);
            Equal(0, from, "the player stands on the first cell");
            var aim = BeaconMath.AimPoint(grid, row, route, from, player, 25f);
            Equal(true, aim.X <= 9, "the ping never points through the wall");
            Equal(true, aim.Y > 10, "it looks ahead down the corridor");
            Equal(true, BeaconMath.IntervalMs(50f) == BeaconMath.NearIntervalMs, "close: fastest");
            Equal(true, BeaconMath.IntervalMs(10000f) == BeaconMath.FarIntervalMs, "far: slowest");
            Equal(true, BeaconMath.IntervalMs(1000f) < BeaconMath.IntervalMs(2000f), "slower when farther");
            var (panRight, _) = BeaconMath.Voice(new System.Numerics.Vector2(1, 0));
            var (_, pitchUp) = BeaconMath.Voice(new System.Numerics.Vector2(0, -1));
            var (_, pitchDown) = BeaconMath.Voice(new System.Numerics.Vector2(0, 1));
            Equal(true, panRight > 0.9f && pitchUp > 1f && pitchDown < 1f, "right is right, up is higher, down is lower");
        }

        private static void ClosedWayLeadsToItsNearestPoint()
        {
            // 20x20 open grid split by a full wall at x = 10: the right half cannot be reached.
            const int size = 20;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            for (var y = 0; y < size; y++)
            {
                grid[(y * row) + 5] &= 0xF0;
            }

            var start = new System.Numerics.Vector2(2, 10);
            var goal = new System.Numerics.Vector2(17, 10);
            Equal(true, RoutePlanner.FindRoute(grid, row, start, goal) == null, "no full route");
            var partial = RoutePlanner.FindRoute(grid, row, start, goal, true, out var reached);
            Equal(false, reached, "the goal is not reached");
            Equal(true, partial != null && partial[^1].X == 9, "the way ends at the wall, nearest to the goal");
        }

        private static void RockStandsAloneWallDoesNot()
        {
            // 30x30 open grid: a 2x2 rock ahead (x 10-11, y 14-15) and, elsewhere, a wall across the whole map at y = 25.
            const int size = 30;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            void Block(int x, int y) => grid[(y * row) + (x / 2)] &= (byte)((x & 1) == 0 ? 0xF0 : 0x0F);
            foreach (var (x, y) in new[] { (10, 14), (11, 14), (10, 15), (11, 15) })
            {
                Block(x, y);
            }

            for (var x = 0; x < size; x++)
            {
                Block(x, 25);
            }

            var right = new System.Numerics.Vector2(1, 0);
            var down = new System.Numerics.Vector2(0, 1);
            Equal(true, PassageScan.IsSmallObstacle(grid, row, new System.Numerics.Vector2(5.5f, 14.5f), right, 4f, 5), "a rock standing alone");
            Equal(false, PassageScan.IsSmallObstacle(grid, row, new System.Numerics.Vector2(20.5f, 20.5f), down, 4f, 5), "a wall across the map");
        }

        private static void RouteStartsOutsideAPocket()
        {
            // 60x60 open ground; the player stands on a blocked block (x 20-24, y 20-24) whose centre cell (22,22) is a
            // walkable pocket of one cell. The nearest walkable cell is that pocket; the route must start outside it.
            const int size = 60;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            void Set(int x, int y, bool open) => grid[(y * row) + (x / 2)] = (byte)(open
                ? grid[(y * row) + (x / 2)] | ((x & 1) == 0 ? 0x01 : 0x10)
                : grid[(y * row) + (x / 2)] & ((x & 1) == 0 ? 0xF0 : 0x0F));
            for (var y = 20; y <= 24; y++)
            {
                for (var x = 20; x <= 24; x++)
                {
                    Set(x, y, false);
                }
            }

            Set(22, 22, true);
            var route = RoutePlanner.FindRoute(grid, row, new System.Numerics.Vector2(22.4f, 22.4f), new System.Numerics.Vector2(50, 50), false, out var reached);
            Equal(true, route != null && reached, "a route from the ground around the pocket");
        }

        private static void RadarLabelsSpeakSpanish()
        {
            Localization.Detect("es");
            Equal("Escaleras hacia arriba", Announcements.LandmarkLabel("Stairs Up"), "a whole label");
            Equal("Crop Circle (gema de habilidad de nivel 4)", Announcements.LandmarkLabel("Crop Circle (Skill Gem Level 4)"), "a reward note, the name kept");
            Equal("Sisters of Garukhan (10% de resistencia al rayo)", Announcements.LandmarkLabel("Sisters of Garukhan (10% Lightning Res)"), "a resistance");
            Equal("Kabala (+2 puntos de pasiva)", Announcements.LandmarkLabel("Kabala (+2 Passive)"), "passive points");
            Localization.Detect("en");
            Equal("Stairs Up", Announcements.LandmarkLabel("Stairs Up"), "English untouched");
        }

        private static void RadarBossNamesSpeakTheGamesMonsterName()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "bosses-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var landmarks = System.IO.Path.Join(directory, "landmarks.json");
            System.IO.File.WriteAllText(landmarks, "{}");
            System.IO.File.WriteAllText(System.IO.Path.Join(directory, "monster-names.json"),
                "{\"es\":{\"Beira of the Rotten\":\"Beira, de la manada putrefacta\"},\"en\":{\"Beira of the Rotten\":\"Beira of the Rotten Pack\"}}");
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var table = LandmarkTable.Load(log, landmarks);
            Localization.Detect("es");
            Equal("Beira, de la manada putrefacta (10% de resistencia al frío)", table.Localize("Beira of the Rotten (10% Cold Res)"), "a boss's name and note");
            Equal("Beira, de la manada putrefacta", table.Localize("Beira of the Rotten"), "a boss's name alone");
            Equal("The Moving Bramble (gema de habilidad de nivel 2)", table.Localize("The Moving Bramble (Skill Gem Level 2)"), "no monster: the name kept, the note translated");
            Localization.Detect("fr");
            Equal("Beira of the Rotten", table.Localize("Beira of the Rotten"), "a language without the table keeps the label");
            Localization.Detect("en");
        }

        private static void ExperienceProgressIsWithinTheLevel()
        {
            var table = new ExperienceTable(new uint[] { 0, 525, 1760, 4250334444, 4250334445 });
            Equal(0, table.PercentToNext(1, 0), "level 1 at its start");
            Equal(50, table.PercentToNext(2, 525 + 618), "half way through level 2");
            Equal(99, table.PercentToNext(3, 4250334443), "experience past int.MaxValue");
            Equal(null, table.PercentToNext(5, 4250334445), "the last level");
            Equal(null, table.PercentToNext(2, 100), "experience below the level (a stale read)");
            Equal(null, new ExperienceTable(Array.Empty<uint>()).PercentToNext(1, 0), "no table");
            Localization.Detect("es");
            Equal("Nivel 4, 35 por ciento hacia el nivel 5, nivel del área 2", Announcements.Levels(4, 2, 35), "vitals line");
            Equal("Nivel 4, nivel del área 2", Announcements.Levels(4, 2), "without the table");
            Localization.Detect("en");
        }

        private static void LoopReaderKeepsAPitchedLoopClean()
        {
            const int length = 44100;
            const int hertz = 8000;
            static double Tone(double position) => Math.Sin(2 * Math.PI * hertz * position / length);
            var loop = Enumerable.Range(0, length).Select(i => (float)Tone(i)).ToArray();

            Equal(true, MathF.Abs(LoopReader.At(loop, 5) - loop[5]) < 1e-4f, "on a sample it reads that sample");
            Equal(true, MathF.Abs(LoopReader.At(loop, 3) - loop[3]) < 1e-4f, "by the start it reads round the end");
            Equal(true, MathF.Abs(LoopReader.At(loop, length - 2) - loop[length - 2]) < 1e-4f, "by the end it reads round the start");

            foreach (var pitch in new[] { 0.8, 1.26 })
            {
                var error = 0.0;
                var count = 0;
                for (var position = length - 500.0; position < length + 500.0; position += pitch)
                {
                    var wrapped = position >= length ? position - length : position;
                    var difference = LoopReader.At(loop, wrapped) - Tone(position);
                    error += difference * difference;
                    count++;
                }

                Equal(true, Math.Sqrt(error / count) < 0.003, $"over the seam at pitch {pitch} the error stays 50 dB under a bright tone");
            }
        }

        private static void APileOfLoopsLeavesHeadroom()
        {
            const float near = 0.7f;
            const float far = near * 0.3f;
            Equal(1f, ItemSoundSet.PileScale(near * near, near), "one item by the player keeps its level");
            Equal(1f, ItemSoundSet.PileScale((near * near) + (3 * far * far), near), "nor do three far ones lower it");
            Equal(1f, ItemSoundSet.PileScale(0f, 0f), "with the volume off nothing is scaled");
            var each = near * ItemSoundSet.PileScale(4 * near * near, near);
            Equal(true, MathF.Abs(MathF.Sqrt(4 * each * each) - (1.25f * near)) < 1e-4f, "four near items are a little louder than one");
            Equal(true, each > near / 2f, "and each stays heard");
        }

        private static void DiabloIVSetCoversLootByKindAndRarity()
        {
            string? Sample(string kind, string baseId = "", LootTier tier = LootTier.Normal, string? gameDrop = null) =>
                ItemSoundSet.DiabloIVSample(kind, baseId, tier, gameDrop);

            Equal(ItemSoundSet.Game, ItemSoundSet.Sets[0], "the game's own sounds stay the default");
            Equal("gold", Sample("gold"), "gold");
            Equal("quest item", Sample("quest", gameDrop: "Scroll_Drop_01"), "a quest scroll is a quest item");
            Equal("rune", Sample("currency", "Metadata/Items/Currency/CurrencyUpgradeToRare", LootTier.Valuable), "an orb");
            Equal("scroll", Sample("currency", "Metadata/Items/Currency/CurrencyIdentification", LootTier.Valuable, "Scroll_Drop_01"), "a Scroll of Wisdom");
            Equal("potion", Sample("item", "Metadata/Items/Flasks/FlaskLife1"), "a flask");
            Equal("potion", Sample("item", "Metadata/Items/Flasks/FlaskLife1", LootTier.Magic), "a magic flask stays a flask");
            Equal("gem", Sample("item", "Metadata/Items/Gems/SkillGemFireball", LootTier.Valuable), "a gem");
            Equal("normal equipment", Sample("item", "Metadata/Items/Armours/Helmets/FourHelmetStr1"), "normal gear");
            Equal("magic equipment", Sample("item", "Metadata/Items/Rings/FourRing1", LootTier.Magic), "magic gear");
            Equal("rare equipment", Sample("rare", "Metadata/Items/Rings/FourRing1", LootTier.Valuable), "rare gear");
            Equal("legendary equipment", Sample("unique", "Metadata/Items/Rings/FourRing1", LootTier.Valuable), "unique gear");
            Equal("normal equipment", Sample("item", "Metadata/Items/Maps/MapKeyTier1", LootTier.Valuable), "worth unread: the plain sound");
            Equal("chest", Sample("chest"), "a chest");
            Equal("corpse", Sample("corpse"), "a body");
            Equal("shrine", Sample("shrine"), "a shrine");
            Equal(null, Sample("breakable"), "a breakable keeps the game's sound");
            Equal(null, Sample("waypoint"), "places keep the game's sounds");
        }

        private static void SteamsCommandIsFoundInTheLaunchersArguments()
        {
            const string Game = @"D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe";
            string Split(params string[] args)
            {
                var (own, game) = Poe2Access.GameInstall.SplitArguments(args);
                return $"{string.Join('|', own)} > {(game is null ? "none" : string.Join('|', game))}";
            }

            Equal($" > {Game}|--nopatch", Split(Game, "--nopatch"), "%command%: the game's path, then its arguments");
            Equal($"--host|C:\\mod\\app > {Game}|--nopatch", Split("--host", @"C:\mod\app", Game, "--nopatch"), "the launcher's own arguments come first");
            Equal($" > {Game}|--nopatch", Split(@"D:\games\steam\steamapps\common\Path", "of", "Exile", @"2\PathOfExileSteam.exe", "--nopatch"), "a path that came unquoted");
            Equal($"restart > {Game}", Split("restart", Game), "a command before it");
            Equal(" > none", Split(), "a double click");
            Equal("restart|--host|C:\\PathOfExile > none", Split("restart", "--host", @"C:\PathOfExile"), "a folder named like the game");
            Equal("C:\\tools\\notes.exe > none", Split(@"C:\tools\notes.exe"), "another program");
            const string Proton = @"Z:\home\deck\.local\share\Steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe";
            Equal($" > {Proton}|--nologo", Split(Proton, "--nologo"), "under Proton: the game's Linux path on Wine's Z: drive (poe2access-proton.sh)");
        }

        private static void ItemsSoundUnderTheirGroupsVolume()
        {
            Equal(ItemSoundGroup.Flask, ItemSoundGroup.Of("item", "Metadata/Items/Flasks/FlaskLife1"), "a flask");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("item", "Metadata/Items/Armours/Helmets/FourHelmetStr1"), "a helmet");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("rare", "Metadata/Items/Weapons/OneHandWeapons/Wands/FourWand1"), "a rare wand is equipment");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("item", "Metadata/Items/Rings/FourRing1"), "a ring");
            Equal(ItemSoundGroup.Gem, ItemSoundGroup.Of("item", "Metadata/Items/Gems/SkillGemFireball"), "a skill gem");
            Equal(ItemSoundGroup.Gem, ItemSoundGroup.Of("item", "Metadata/items/Gem/SupportGemFasterCast"), "the files' other gem folders");
            Equal("currency", ItemSoundGroup.Of("currency", "Metadata/Items/Currency/CurrencyIdentification"), "currency by its kind");
            Equal("gold", ItemSoundGroup.Of("gold", string.Empty), "gold");
            Equal("quest_item", ItemSoundGroup.Of("quest", "Metadata/Items/QuestItems/Book"), "a quest item");
            Equal(ItemSoundGroup.Other, ItemSoundGroup.Of("item", "Metadata/Items/MapFragments/Key"), "anything else");
            Equal(ItemSoundGroup.Other, ItemSoundGroup.Of("item", string.Empty), "an unread base");
        }

        private static void FightsLastWhileAnEngagingEnemyIsNear()
        {
            var fight = new FightState();
            Equal(false, fight.IsOn(0), "no fight before any enemy");
            Equal(false, fight.Update(false, 1000), "no enemy, no fight");
            Equal(true, fight.Update(true, 2000), "an enemy near");
            Equal(true, fight.Update(false, 2000 + FightState.HoldMs - 1), "held a moment after it is gone");
            Equal(false, fight.Update(false, 2000 + FightState.HoldMs), "then over");
            Equal(true, FightState.Modes.Contains(CullThreshold.Never), "the setting can be turned off");
            Equal("True True True True True", string.Join(" ", new[] { (ScanCategory.Items, "rare"), (ScanCategory.Chests, "chest"), (ScanCategory.Chests, "corpse"), (ScanCategory.Chests, "breakable"), (ScanCategory.Encounters, "ritual") }
                .Select(entry => FightState.QuietsInFight(entry.Item1, entry.Item2))), "loot, chests, bodies, breakables and altars keep quiet");
            Equal("False False False", string.Join(" ", new[] { (ScanCategory.Exits, "exit"), (ScanCategory.Chests, "shrine"), (ScanCategory.Waypoints, "checkpoint") }
                .Select(entry => FightState.QuietsInFight(entry.Item1, entry.Item2))), "exits, shrines and checkpoints still guide");
        }

        private static void AWaitingBossMakesNoFight()
        {
            var roster = new FightRoster();
            Equal(false, roster.Counts(706, false, 1000), "waiting in its arena, never in play");
            Equal(false, roster.Counts(706, false, 600000), "however long the player stays around");
            Equal(true, roster.Counts(706, true, 700000), "awake once the player walks in");
            Equal(true, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs - 1), "between phases it still counts");
            Equal(false, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs), "gone for good, it stops counting");
            roster.Prune(700000 + FightRoster.OutOfPlayMs);
            Equal(false, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs - 1), "forgotten: the next area uses the same ids");
        }

        private static void AHiddenMonsterIsToldByItsBuff()
        {
            Equal(true, BuffIds.IsHiddenMonster("hidden_monster_79"), "as GameHelper keys it, read on Count Geonor");
            Equal(true, BuffIds.IsHiddenMonster("hidden_monster"), "without the suffix");
            Equal(false, BuffIds.IsHiddenMonster("kitava_heart_damage_taken_79"), "his other buff");
            Equal(false, BuffIds.IsHiddenMonster("frozen"), "a buff a fought enemy has");
        }

        private static void ABossOfSeveralBodiesIsHeardOnce()
        {
            var repeats = new SpeechRepeats();
            var start = new DateTime(2026, 11, 5, 20, 24, 27, DateTimeKind.Utc);
            Equal(false, repeats.IsRepeat("Geonor: Congelado", start), "the first body");
            repeats.Note("Geonor: Congelado", start);
            Equal(true, repeats.IsRepeat("Geonor: Congelado", start.AddMilliseconds(1)), "the next body in the same look");
            Equal(false, repeats.IsRepeat("Geonor 9 por ciento", start.AddMilliseconds(1)), "another line goes through");
            Equal(false, repeats.IsRepeat("Geonor: Congelado", start.AddMilliseconds(SpeechRepeats.WindowMs)), "frozen again later is news");
        }

        private static void OnlyLinesSaidAloudNearbyAreLocalSpeech()
        {
            Equal(true, ChatDiff.IsNotice("Intercambio aceptado."), "the game's own notice");
            Equal(false, ChatDiff.IsNotice("Renly:Esto es lo que tengo."), "an NPC's line is no notice");
            Equal(false, ChatDiff.IsNotice("@De Dahnte:hola"), "a whisper is no notice");
            Equal(true, ChatDiff.IsLocalSpeech("El rey vudú:¡Arrodillaos!"), "a boss's shout");
            Equal(true, ChatDiff.IsLocalSpeech("Renly:Esto es lo que tengo."), "an NPC's line");
            Equal(false, ChatDiff.IsLocalSpeech("@De Dahnte:hola"), "a whisper");
            Equal(false, ChatDiff.IsLocalSpeech("%yarkey:vamos"), "party");
            Equal(false, ChatDiff.IsLocalSpeech("#Someone:wts"), "global");
            Equal(false, ChatDiff.IsLocalSpeech("Has entrado en Sierraclara."), "a system notice has no speaker");
            Equal(false, ChatDiff.IsLocalSpeech(string.Empty), "nothing");
        }

        private static void LootFilterKeepsWhatThePlayerChose()
        {
            Equal(true, LootFilter.Allows(LootFilter.All, LootTier.Normal), "all keeps white items");
            Equal(false, LootFilter.Allows(LootFilter.Magic, LootTier.Normal), "magic or better drops white items");
            Equal(true, LootFilter.Allows(LootFilter.Magic, LootTier.Magic), "magic or better keeps magic items");
            Equal(false, LootFilter.Allows(LootFilter.Valuable, LootTier.Magic), "valuable drops magic items");
            Equal(true, LootFilter.Allows(LootFilter.Valuable, LootTier.Valuable), "valuable keeps currency and rares");
            Equal(true, LootFilter.Allows("garbage", LootTier.Normal), "an unknown setting keeps everything");
            Equal(LootTier.Valuable, new ScanEntry(1, ScanCategory.Items, "x", 0, 0, 0).Loot, "an item of unknown worth stays heard");
        }
    }
}
