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

    /// <summary>Barriers, exploration edges, doorways, minimap marks, map hints and the radar's way to unseen ground.</summary>
    public static class MapRadarTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(FarBarrierStaysClosedUntilSeenNear), FarBarrierStaysClosedUntilSeenNear),
            (nameof(UnexploredGroundLiesPastTheSeenEdge), UnexploredGroundLiesPastTheSeenEdge),
            (nameof(RadarFindsTheDoorwayToAnotherRoom), RadarFindsTheDoorwayToAnotherRoom),
            (nameof(MapCornerFollowsTheScreenNotTheGrid), MapCornerFollowsTheScreenNotTheGrid),
            (nameof(RadarHearsAnExitInItsWallAndOffItsLane), RadarHearsAnExitInItsWallAndOffItsLane),
            (nameof(FrontierTellsNooksFromGroundThatGoesOn), FrontierTellsNooksFromGroundThatGoesOn),
            (nameof(QuestWayLeavesTheSeenGroundTowardTheGoal), QuestWayLeavesTheSeenGroundTowardTheGoal),
            (nameof(MinimapMarksNameBossRoomsAndLandmarks), MinimapMarksNameBossRoomsAndLandmarks),
            (nameof(SignsAreNeverQuestTargetsAndQuestIconsAlways), SignsAreNeverQuestTargetsAndQuestIconsAlways),
            (nameof(MapHintsAreSaidOncePerPlace), MapHintsAreSaidOncePerPlace),
            (nameof(ShippedMapHintsAreWellFormed), ShippedMapHintsAreWellFormed),
            (nameof(LostMeansWalkingWithoutNewGround), LostMeansWalkingWithoutNewGround),
            (nameof(RadarMarksTheWayToUnseenGround), RadarMarksTheWayToUnseenGround),
            (nameof(ReachPingsOncePerContact), ReachPingsOncePerContact),
        };

        private static void FarBarrierStaysClosedUntilSeenNear()
        {
            // 400x10 open strip; a barrier at cells 380-381 reads closed (believed at any distance), then
            // walkable again while the player is still at x 10, past the network bubble: closed until they stand at x 300.
            const int row = 200;
            var open = Enumerable.Repeat((byte)0x11, row * 10).ToArray();
            var shut = (byte[])open.Clone();
            for (var y = 0; y < 10; y++)
            {
                shut[(y * row) + 190] = 0;
            }

            var memory = new BarrierMemory();
            memory.Observe(open, open, row, 10, 5);
            memory.Observe(shut, open, row, 10, 5);
            memory.Observe(open, open, row, 10, 5);
            var grid = (byte[])open.Clone();
            memory.Apply(grid);
            Equal(false, RoutePlanner.IsWalkable(grid, row, 380, 5), "seen open from afar: still closed");
            Equal(20, memory.ClosedCount, "remembered cells");
            memory.Observe(open, open, row, 300, 5);
            grid = (byte[])open.Clone();
            memory.Apply(grid);
            Equal(true, RoutePlanner.IsWalkable(grid, row, 380, 5), "seen open from near by");

            // Already closed at the first sample (the plugin started beside it): open in the grid as loaded is enough.
            var fresh = new BarrierMemory();
            fresh.Observe(shut, open, row, 10, 5);
            fresh.Observe(open, open, row, 10, 5);
            grid = (byte[])open.Clone();
            fresh.Apply(grid);
            Equal(false, RoutePlanner.IsWalkable(grid, row, 381, 5), "closed before the first sample");
        }

        private static void UnexploredGroundLiesPastTheSeenEdge()
        {
            // 400x200 open field split by a wall at x = 200 with no gap; the player stands at (40, 100).
            const int width = 400, height = 200;
            var row = width / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * height).ToArray();
            for (var y = 0; y < height; y++)
            {
                grid[(y * row) + 100] = 0; // cells 200 and 201
            }

            var map = new ExplorationMap();
            Equal(null, map.ExploredPercent(grid, row, new System.Numerics.Vector2(40, 100)), "nothing walked yet");
            map.Visit(new System.Numerics.Vector2(40, 100), width, height);
            var firstLook = map.ExploredPercent(grid, row, new System.Numerics.Vector2(40, 100));
            Equal(true, firstLook is > 0 and < 50, "a first look sees part of the reachable west field");
            var targets = map.Frontier(grid, row, new System.Numerics.Vector2(40, 100), 8).Select(place => place.Cell).ToList();
            Equal(true, targets.Count > 0, "unseen ground east of the start is offered");
            Equal(true, targets.All(cell => cell.X < 200), "nothing past the unbroken wall");
            Equal(true, targets.All(cell => System.Numerics.Vector2.Distance(cell, new System.Numerics.Vector2(40, 100)) > ExplorationMap.SeenRadiusCells - ExplorationMap.BlockCells),
                "targets lie at the seen edge");
            for (var x = 25; x < 200; x += 50)
            {
                for (var y = 25; y < 200; y += 50)
                {
                    map.Visit(new System.Numerics.Vector2(x, y), width, height);
                }
            }

            Equal(0, map.Frontier(grid, row, new System.Numerics.Vector2(120, 100), 8).Count, "the west field walked through: nothing left");
            Equal(100, map.ExploredPercent(grid, row, new System.Numerics.Vector2(120, 100)), "the unreachable east field does not count");
        }

        /// <summary>A walled grid with rectangles carved open (x even: two cells per byte).</summary>
        internal static byte[] Carved(int width, int height, params (int Left, int Top, int Right, int Bottom)[] rooms)
        {
            var grid = new byte[width / 2 * height];
            foreach (var (left, top, right, bottom) in rooms)
            {
                for (var y = top; y < bottom; y++)
                {
                    Array.Fill(grid, (byte)0x11, (y * width / 2) + (left / 2), (right - left) / 2);
                }
            }

            return grid;
        }

        private static void RadarFindsTheDoorwayToAnotherRoom()
        {
            var limits = new PassageScan.DoorwayLimits(23, 11, 4, 23);
            var east = new System.Numerics.Vector2(1, 0);
            var start = new System.Numerics.Vector2(40, 100);
            System.Numerics.Vector2? Door(byte[] grid, System.Numerics.Vector2 from) => PassageScan.Doorway(grid, 200, from, east, 80, limits);
            // Two halls joined by a gap 12 cells wide through a wall.
            var halls = Carved(400, 200, (10, 40, 80, 160), (80, 94, 92, 106), (92, 40, 200, 160));
            Equal(true, Door(halls, start) is { X: > 70 and < 100, Y: > 94 and < 106 }, "a doorway ahead, at its middle");
            Equal(null, PassageScan.Doorway(halls, 200, start, new System.Numerics.Vector2(0, 1), 80, limits), "toward the plain wall of the hall");
            Equal(null, Door(Carved(400, 200, (10, 40, 300, 160)), start), "one great hall: only floor");
            var corridor = Carved(400, 200, (10, 94, 390, 106));
            Equal(null, Door(corridor, new System.Numerics.Vector2(150, 100)), "along a corridor, nothing but corridor");
            var mouth = Carved(400, 200, (10, 40, 80, 160), (80, 94, 390, 106));
            Equal(true, Door(mouth, start) is { X: > 70 }, "out of a hall into a corridor");
            var opening = Carved(400, 200, (10, 94, 200, 106), (200, 40, 390, 160));
            Equal(true, Door(opening, new System.Numerics.Vector2(160, 100)) is { X: > 170 }, "from a corridor into a hall");
            var pillars = Carved(400, 200, (10, 40, 300, 160));
            for (var y = 60; y < 140; y += 20)
            {
                if (y != 100)
                {
                    pillars = Blocked(pillars, 200, 90, y, 94, y + 6);
                }
            }

            Equal(null, Door(pillars, start), "between pillars the ground runs on either side");
            var shallow = Carved(400, 200, (10, 40, 80, 160), (80, 94, 86, 106));
            var depth = PassageScan.Scan(shallow, 200, start, east, 2, 80, 23).Depth;
            Equal(null, PassageScan.Doorway(shallow, 200, start, east, depth, limits), "a nook no deeper than its gap: the way must go on past it");
        }

        private static byte[] Blocked(byte[] grid, int bytesPerRow, int left, int top, int right, int bottom)
        {
            for (var y = top; y < bottom; y++)
            {
                Array.Fill(grid, (byte)0, (y * bytesPerRow) + (left / 2), (right - left) / 2);
            }

            return grid;
        }

        private static void MapCornerFollowsTheScreenNotTheGrid()
        {
            const int width = 400, height = 400;
            var grid = Carved(width, height, (0, 0, width, height));
            var flatX = new System.Numerics.Vector2(1, 0);
            var flatY = new System.Numerics.Vector2(0, 1);
            (int, int)? Flat(float x, float y) => MapCorner.Of(grid, width / 2, new System.Numerics.Vector2(x, y), flatX, flatY);
            Equal(((int, int)?)(-1, 1), Flat(20, 380), "bottom left, a flat camera");
            Equal(((int, int)?)(1, -1), Flat(380, 20), "top right");
            Equal(((int, int)?)(0, 0), Flat(200, 200), "the middle");
            Equal(((int, int)?)(-1, 0), Flat(20, 200), "the left edge");
            // The game's camera: grid +X draws up and right, +Y down and right, so the grid's corners are the screen's edges.
            var isoX = new System.Numerics.Vector2(1, -0.5f);
            var isoY = new System.Numerics.Vector2(1, 0.5f);
            (int, int)? Iso(float x, float y) => MapCorner.Of(grid, width / 2, new System.Numerics.Vector2(x, y), isoX, isoY);
            Equal(((int, int)?)(-1, 0), Iso(10, 10), "the grid's first corner is the screen's left");
            Equal(((int, int)?)(1, 0), Iso(390, 390), "its opposite, the right");
            Equal(((int, int)?)(0, -1), Iso(390, 10), "up");
            Equal(((int, int)?)(0, 1), Iso(10, 390), "down");
            Equal(((int, int)?)(-1, 1), Iso(10, 200), "between left and down, a corner of the screen");
            var stray = Carved(1000, 400, (0, 0, 400, 400), (990, 0, 1000, 8));
            Equal(((int, int)?)(1, 0), MapCorner.Of(stray, 500, new System.Numerics.Vector2(380, 200), flatX, flatY), "a stray patch far out is no edge");
            Equal(null, MapCorner.Of(new byte[200 * 400], 200, new System.Numerics.Vector2(10, 10), flatX, flatY), "no ground");
        }

        private static void RadarHearsAnExitInItsWallAndOffItsLane()
        {
            var sensor = new RadarSensor(new RadarThresholds(20, 15, 3, 200, 100, 500));
            var east = new System.Numerics.Vector2(1, 0);
            var wall = new PassageScan.Result(12, false);
            sensor.Update(0, System.Numerics.Vector2.Zero, east, wall, false);
            RadarReading? heard = null;
            for (var step = 1; step <= 6 && heard == null; step++)
            {
                heard = sensor.Update(step * 100, new System.Numerics.Vector2(step, 0), east, wall, true);
            }

            Equal(RadarReading.Exit, heard, "a door in the wall ahead is the exit, not the wall");
            var south = new System.Numerics.Vector2(0, 1);
            var start = new System.Numerics.Vector2(30, 50);
            var door = new System.Numerics.Vector2(62, 40);
            var field = Carved(100, 100, (0, 0, 60, 100));
            Equal(true, PassageScan.ExitToward(field, 50, start, east, door, 69, 23), "off the lane, in the wall, inside the ray's quarter");
            Equal(false, PassageScan.ExitToward(field, 50, start, south, door, 69, 23), "not on another ray");
            Equal(false, PassageScan.ExitToward(field, 50, start, east, door, 20, 23), "out of reach");
            var walled = Carved(100, 100, (0, 0, 40, 100), (70, 0, 100, 100));
            Equal(false, PassageScan.ExitToward(walled, 50, start, east, new System.Numerics.Vector2(75, 50), 69, 23), "behind a wall of its own");
        }

        private static void FrontierTellsNooksFromGroundThatGoesOn()
        {
            const int width = 400, height = 200;
            var player = new System.Numerics.Vector2(60, 100);
            // A hall; a corridor east to a great room; a short dead end south.
            var grid = Carved(width, height, (20, 80, 100, 120), (100, 96, 300, 104), (300, 10, 390, 190), (60, 120, 68, 190));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var places = map.Frontier(grid, width / 2, player, 8);
            Equal(2, places.Count, "the corridor and the dead end");
            Equal(false, places[0].Pocket, "ground that goes on comes first");
            Equal(true, places[0].Cell.X > 100, "it is the corridor");
            Equal(true, places[1].Pocket, "the dead end is a nook");
            Equal(true, places[1].Cell.Y > 120, "south of the hall");
        }

        private static void QuestWayLeavesTheSeenGroundTowardTheGoal()
        {
            const int width = 800, height = 200;
            var player = new System.Numerics.Vector2(60, 100);
            var goal = new System.Numerics.Vector2(750, 100);
            // A hall with two ways on: north to one room, east to the goal's; a walled-off room apart.
            var grid = Carved(width, height, (20, 80, 100, 120), (40, 30, 48, 80), (40, 2, 390, 30), (100, 96, 700, 104), (700, 40, 790, 160), (200, 170, 240, 195));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var way = map.WayOn(grid, width / 2, player, goal);
            Equal(true, way is { X: > 100 and < 140, Y: > 94 and < 106 }, "the east corridor, at the edge of the seen ground, not the north way nor the goal");
            Equal(null, map.WayOn(grid, width / 2, player, new System.Numerics.Vector2(220, 180)), "no ground leads to a walled-off room");
            var further = new System.Numerics.Vector2(200, 100);
            map.Visit(further, width, height);
            Equal(true, map.WayOn(grid, width / 2, further, goal) is { X: > 240 and < 280 }, "the way moves on with the player");
            var seen = map.SeenBlocks;
            map.Visit(goal, width, height);
            Equal(true, map.SeenBlocks > seen, "new ground counts");
            seen = map.SeenBlocks;
            map.Visit(goal, width, height);
            Equal(seen, map.SeenBlocks, "ground seen twice does not");
            Equal(null, map.WayOn(grid, width / 2, further, goal), "ground seen: the place itself is known");
        }

        private static void MinimapMarksNameBossRoomsAndLandmarks()
        {
            Equal("ritual", MinimapMarks.DeviceKind(MinimapMarks.RitualIcon), "an altar still to do sounds");
            Equal(MinimapMarks.RitualDoneKind, MinimapMarks.DeviceKind(MinimapMarks.RitualDoneIcon), "a finished one has its own kind, out of the sounds");
            Equal("encounter", MinimapMarks.DeviceKind("ExpeditionDevice"), "another league's device stays an encounter");

            const string crowbellRoom = "Metadata/Monsters/Crowbell/Objects/BossRoomMinimapIcon";
            Equal((MinimapMarks.BossRoomKind, "El pilar de caza", false),
                  MinimapMarks.PointOfInterest(crowbellRoom, "CrowbellActive", "El pilar de caza"), "a campaign boss's arena by its label");
            Equal((MinimapMarks.BossRoomKind, "El pilar de caza", true),
                  MinimapMarks.PointOfInterest(crowbellRoom, "CrowbellInactive", "El pilar de caza"), "slain: its grey icon, the active row's label");
            Equal((MinimapMarks.BossRoomKind, string.Empty, false),
                  MinimapMarks.PointOfInterest("Metadata/MiscellaneousObjects/BossRoomMinimapIcon", "LootFilterLargeRedUpsideDownHouse", string.Empty), "a map boss's red house has no label");
            Equal((MinimapMarks.BossRoomKind, string.Empty, true),
                  MinimapMarks.PointOfInterest("Metadata/MiscellaneousObjects/BossRoomMinimapIcon", "LootFilterLargeGreyUpsideDownHouse", string.Empty), "and turns grey");
            Equal((MinimapMarks.LandmarkKind, "Ritual driádico", false),
                  MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act1/1_11/Objects/SecretRoomMinimapIcon", "HuntingGroundsSecretActive", "Ritual driádico"), "a secret by its label");
            Equal(null, MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act1/1_11/Objects/SecretRoomMinimapIcon", "HuntingGroundsSecretInactive", string.Empty), "a secret found is gone");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act2/2_10_2/Objects/RisuCage", "QuestObject", "Jaula de Risu")?.Kind,
                  "what the quest's icon marks sounds as a quest object");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.KindUnder("QuestObject", string.Empty), "a named object under the quest's icon sounds as a quest object");
            Equal("chest", MinimapMarks.KindUnder("QuestObject", "chest"), "a chest there keeps its kind, the quest sound goes over its own");
            Equal(string.Empty, MinimapMarks.KindUnder("Stash", string.Empty), "and another icon gives no kind");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.DeviceKind("QuestObject"), "a league's summon under the quest's icon too");
            Equal("encounter", MinimapMarks.DeviceKind("Strongbox"), "while another device stays an encounter");
            Equal(true, MinimapMarks.IsStoryBarrier("Metadata/Terrain/Gallows/Act2/2_2/Objects/BossDoor"), "the Ancient Seal is a campaign area's own barrier");
            Equal(false, MinimapMarks.IsStoryBarrier("Metadata/MiscellaneousObjects/Door"), "a dungeon's plain door is not");
            Equal(false, MinimapMarks.IsStoryBarrier("Metadata/Terrain/Gallows/Leagues/Incursion/Objects/RoomDoor"), "nor a league's");
            Equal(true, MinimapMarks.IsCage("Metadata/Terrain/Gallows/Act4/4_7/Objects/KaruiElderCage"), "a cage by its file");
            Equal(false, MinimapMarks.IsCage("Metadata/Terrain/Doodads/Desert/DreadnoughtVanguard/BossRoomCageDoor"), "and only by its end");
            Equal("Risu", MinimapMarks.BareName("Risu, Desertora faridun"), "a cage says who is inside without the title");
            Equal("Una", MinimapMarks.BareName("Una"), "a name with no title stays");
            Equal(null, MinimapMarks.PointOfInterest("Metadata/NPC/Four_Act1/HuntingGroundsSignpostGlyph", MinimapMarks.StoryGlyph, string.Empty), "a sign is no point of interest");
            Equal(true, MinimapMarks.IsQuestIcon("QuestObject") && MinimapMarks.IsQuestIcon("QuestItem") && !MinimapMarks.IsQuestIcon("NPC"), "the quest's own icons");
            Equal(true, MinimapMarks.IsSpent("TitanGrottoEntranceInActive"), "the table's other spelling of inactive");
            Equal(true, MinimapMarks.IsLitMark("RustObeliskActive"), "an area's own mark, lit: the obelisk still to use");
            Equal(false, MinimapMarks.IsLitMark("RustObeliskInactive") || MinimapMarks.IsLitMark("TitanGrottoEntranceInActive"), "dimmed once done, in either spelling");
            Equal(false, MinimapMarks.IsLitMark("CheckpointNotActive") || MinimapMarks.IsLitMark("Stash") || MinimapMarks.IsLitMark(string.Empty), "and no other icon is one");
            Equal("Legends Of Wraeclast", CardText.ShopImageTitle("C:/Users/x/AppData/Local/Path of Exile 2/ShopImages/public_news_2026-10-01_LegendsOfWraeclast_small.png|0,0,0,0|1"), "a featured banner by its image file");
            Equal("Cot A Box", CardText.ShopImageTitle("C:/Users/x/AppData/Local/Path of Exile 2/ShopImages/public_news_2026-09-21_CotABox_small.png|0,0,0,0|1"), "capitals in a run split before the small letters");
            Equal(string.Empty, CardText.ShopImageTitle("Art/2DArt/UIImages/InGame/Cross.dds|0"), "no shop image, no title");
            Equal("Early Access Supporter Packs", CardText.ShopImageTitle("C:/x/ShopImages/client_shop_assets_PoE2EarlyAccessSupporterPacks_Thumb.png|0"), "the file's own words dropped");
            Equal("On Sale", CardText.ShopImageTitle("C:/x/ShopImages/public_client_shop_OnSale_Thumb.png|0"), "and the folder-like prefix");

            const string tomb = "Metadata/Terrain/Woods/GraveyardDungeons/Feature/TombBoss_ArenaFloor.tdtx:2-y:2";
            Equal("TombBoss_ArenaFloor", MinimapMarks.TileFile(tomb), "a tile key's file");
            Equal("Asinia", MinimapMarks.ArenaBoss(tomb, "Asinia"), "the Tomb's arena names its boss");
            Equal(string.Empty, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/GraveyardDungeons/Feature/MausoleumBoss_ArenaFloor.tdtx:0-y:0", "MausoleumBoss_ArenaFloor"),
                  "the Mausoleum's arena, labelled with its own file name, names no one");
            Equal("Crowbell Arena (+2 Passive)", MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/Features_Cairns/DolmenDeAzutan_03.tdtx:0-y:9", "Crowbell Arena (+2 Passive)"),
                  "an arena known by its label");
            Equal("Captain Hartlin", MinimapMarks.ArenaBoss("Metadata/Terrain/Islands/Tiles/Island/JourneysEnd/BossArena_02.tdtx:0-y:0", "Boss - Captain Hartlin"), "the label's boss after its prefix");
            Equal(string.Empty, MinimapMarks.ArenaBoss("Metadata/Terrain/Interlude/Part2/P2_1/Salt_Waste_Bossroom.tdtx:14-y:11", "Bosses"), "a bare label");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/Village/Huts/VillageArena_02_courtyard_side.tdtx:0-y:0", "Entrance"), "the way into an arena");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Maps/Derelict/Tiles/HoltenBossStairsDown_01.tdtx:0-y:0", "Boss stairs"), "stairs down to a boss");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/HuntingGrounds/RitualClearing01.tdtx:0-y:4", "Ritual Site (Level 4 Skill Gem)"), "no arena");
            Equal(true, MinimapMarks.IsArenaTile("Metadata/Terrain/Interlude/Part2/P2_7/Qimah_bossroom.tdtx:3-y:1"), "an arena tile without a label");
            Equal(false, MinimapMarks.IsArenaTile("Metadata/Terrain/Woods/Mudburrow/Path/path_St_Spawner_v02_01.tdtx:1-y:0"), "a burrow path");
            var arenas = MinimapMarks.Groups(new[] { new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(400, 400), new System.Numerics.Vector2(46, 0), new System.Numerics.Vector2(23, 0) }, 46);
            Equal("0 2 3 | 1", string.Join(" | ", arenas.Select(group => string.Join(' ', group.OrderBy(member => member)))), "one arena's pieces side by side, another far off");
            Equal("Beira of the Rotten", MinimapMarks.WithoutNote("Beira of the Rotten (10% Cold Res)"), "the reward note left out");
            var notes = QuestMatcher.Targets(new[]
                {
                    new ScanEntry(5, ScanCategory.PointsOfInterest, "Sala del jefe: Arena o coto de caza", 0, 0, 0, MinimapMarks.BossRoomKind, Subject: "Arena o coto de caza"),
                    new ScanEntry(6, ScanCategory.PointsOfInterest, "Sala del jefe", 0, 0, 0, MinimapMarks.BossRoomKind),
                },
                new[] { "La caza", "Ve al Coto de caza y encuentra la Arena" });
            Equal(0, notes.Count, "a boss room's note sharing words with the objective is no target, nor an unnamed room");
            var bossTargets = QuestMatcher.Targets(new[] { new ScanEntry(4, ScanCategory.PointsOfInterest, "Sala del jefe: Asinia", 0, 0, 0, MinimapMarks.BossRoomKind, Subject: "Asinia") },
                                                   new[] { "La consorte", "Asinia te espera en la Tumba de la Consorte" });
            Equal(1, bossTargets.Count, "the quest naming the boss leads to the boss room");
        }

        private static void SignsAreNeverQuestTargetsAndQuestIconsAlways()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Objects, "Letrero: Pueblo de Ogham", 0, 0, 0, MinimapMarks.SignKind),
                new ScanEntry(2, ScanCategory.Exits, "Tierras de cultivo de Ogham", 0, 0, 0),
                new ScanEntry(3, ScanCategory.Objects, "Mecanismo antiguo", 0, 0, 0, QuestMarked: true),
            };
            var targets = QuestMatcher.Targets(entries, new[] { "El lobo rabioso de Ogham", "Infíltrate en la Mansión más allá del pueblo de Ogham" });
            Equal("Mecanismo antiguo | Tierras de cultivo de Ogham", string.Join(" | ", targets.Select(entry => entry.Name)),
                  "the quest's icon first, the exit by its proper noun, never the sign that names the village (user 2026-10-28)");
            Equal("Mecanismo antiguo", string.Join(" | ", QuestMatcher.Targets(entries, Array.Empty<string>()).Select(entry => entry.Name)), "the quest icon needs no tracker text");
        }

        private static void MapHintsAreSaidOncePerPlace()
        {
            var memory = new MapHintMemory();
            var start = new System.Numerics.Vector2(100, 100);
            Equal(true, memory.Note("mushrooms", start), "the first time");
            Equal(false, memory.Note("mushrooms", start + new System.Numerics.Vector2(MapHintMemory.AgainCells - 1, 0)), "the same patch, walking through it");
            Equal(true, memory.Note("roots", start), "another cue is news");
            Equal(true, memory.Note("mushrooms", start + new System.Numerics.Vector2(MapHintMemory.AgainCells, 0)), "far on, it may go another way");
            Equal(false, memory.Note("mushrooms", start), "back where it was said");

            var table = MapHintTable.Parse("{ 'G1_4': [ { 'match': 'FungiCluster', 'hint': 'mushrooms', 'goal': 'The Grim Tangle' }, { 'tile': 'Woods/RedvaleRiver/', 'hint': 'river' } ] }");
            Equal(2, table.For("G1_4").Count, "an area's rows");
            Equal(0, table.For("G1_5").Count, "an area with none");
            Equal("mushrooms", MapHintTable.Matching(table.For("G1_4"), "Metadata/Doodads/fungicluster03.ao")?.Hint, "a model, in any case");
            Equal(null, MapHintTable.Matching(table.For("G1_4"), "Metadata/Terrain/Woods/RedvaleRiver/ccmm_01.tdt"), "a tile row is not an entity's");
        }

        private static void ShippedMapHintsAreWellFormed()
        {
            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "map-hints.json")))
            {
                directory = System.IO.Path.GetDirectoryName(directory.TrimEnd(System.IO.Path.DirectorySeparatorChar))!;
            }

            var table = MapHintTable.Parse(System.IO.File.ReadAllText(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "map-hints.json")));
            foreach (var area in table.Areas)
            {
                foreach (var row in table.For(area))
                {
                    var sources = new[] { row.Match, row.Tile, row.Sign }.Count(source => source.Length > 0);
                    Equal(1, sources, $"{area}: found one way");
                    Equal(true, row.Sign.Length > 0 || row.Hint.Length > 0, $"{area}: a cue has its line");
                    Equal(true, !row.Reveals || row.Goal.Length > 0, $"{area}: reveals something");
                }
            }
        }

        private static void LostMeansWalkingWithoutNewGround()
        {
            var watch = new LostWatch();
            const int after = 3000;
            bool Polls(long from, long to, int seen, bool walking)
            {
                var due = false;
                for (var now = from; now <= to; now += 250)
                {
                    due |= watch.Update(now, seen, walking, after);
                }

                return due;
            }

            Equal(false, Polls(1000, 3000, 5, true), "the count starts with the area's ground");
            Equal(true, Polls(3250, 4500, 5, true), "walking that long without new ground");
            Equal(false, Polls(4750, 6000, 5, true), "then it counts again from nothing");
            Equal(false, Polls(6250, 9000, 6, true) | Polls(9250, 9500, 7, true), "new ground: not lost");
            Equal(false, Polls(9750, 60000, 7, false), "standing still or fighting is not being lost");
            Equal(true, Polls(60250, 63500, 7, true), "only walking counts, and it adds up");
            watch.Reset();
            Equal(false, watch.Update(70000, 7, true, after), "a new area starts again");
            Equal(false, watch.Update(90000, 7, true, after), "a gap is not walking");
        }

        private static void RadarMarksTheWayToUnseenGround()
        {
            // An open field 600 cells square, an unbroken wall at x = 400; the player walks up from (200, 500) to (200, 200).
            const int width = 600, height = 600;
            var row = width / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * height).ToArray();
            for (var y = 0; y < height; y++)
            {
                grid[(y * row) + 200] = 0; // cells 400 and 401
            }

            var up = new System.Numerics.Vector2(0, -1);
            var right = new System.Numerics.Vector2(1, 0);
            var reach = ExplorationMap.MarkReachCells;
            var map = new ExplorationMap();
            var from = new System.Numerics.Vector2(200, 200);
            for (var y = 500; y >= 200; y -= 10)
            {
                map.Visit(new System.Numerics.Vector2(200, y), width, height);
            }

            Equal(false, map.LeavesTheSeen(from, up, reach), "nothing is marked before the ground was read");
            // The ground is read off the frame thread, on a map sharing the seen blocks, and taken once found.
            var elsewhere = new ExplorationMap();
            elsewhere.Visit(from, width, height);
            var stale = elsewhere.Snapshot();
            stale.Frontier(grid, row, from, 8);
            Equal(false, map.Adopt(stale), "what another area's search found is not taken");
            Equal(false, map.LeavesTheSeen(from, up, reach), "and marks nothing");
            var search = map.Snapshot();
            Equal(true, search.Frontier(grid, row, from, 8).Count > 0, "the search finds the places on the shared blocks");
            Equal(true, map.Adopt(search), "and its ground is taken");
            Equal(true, map.LeavesTheSeen(from, up, reach), "unseen ground ahead marks the ray");
            Equal(true, map.LeavesTheSeen(from, -right, reach), "and the ground beside the way, never seen");
            // "Up is unexplored", then a few seconds on "down is": the edge of the walked strip lay in the ray's quarter (user 2026-11-11).
            Equal(false, map.LeavesTheSeen(from, -up, reach), "the way the player came is not unexplored");
            Equal(false, map.LeavesTheSeen(from, System.Numerics.Vector2.Normalize(new System.Numerics.Vector2(0.4f, 1)), reach), "nor a way back a little aslant");
            var byTheWall = new System.Numerics.Vector2(340, 200);
            map.Visit(byTheWall, width, height);
            Equal(false, map.LeavesTheSeen(byTheWall, right, reach), "ground past a wall with no way round is no way on");

            // Walked on, what was ahead is behind and seen: nothing stale marks it.
            for (var y = 190; y >= 60; y -= 10)
            {
                map.Visit(new System.Numerics.Vector2(200, y), width, height);
            }

            Equal(false, map.LeavesTheSeen(new System.Numerics.Vector2(200, 60), -up, reach), "what was ahead is seen once walked");
            map.Reset();
            Equal(false, map.LeavesTheSeen(from, up, reach), "a new area knows nothing of the last one's ground");
            const int rate = 44100;
            var open = RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Up, rate);
            var marked = RadarSynth.WithUnexploredTail(open, RadarSynth.Direction.Up, rate);
            Equal(true, marked.Length > open.Length + (rate / 10) && marked.Take(open.Length).SequenceEqual(open), "the cue untouched, then the pips");
            Equal(true, marked.Skip(open.Length).Max(Math.Abs) is > 0.1f and <= 1f, "the pips are heard and do not clip");
        }

        private static void ReachPingsOncePerContact()
        {
            // Reach 40, release 25: a fight swaying around the edge pings once; it arms again only past 65 or with no enemy.
            var gate = new ReachGate();
            var pings = new[] { 120f, 50f, 38f, 45f, 30f, 60f, 70f, 39f, (float?)null, 10f }
                .Select(gap => gate.Update(gap, 40f, 25f) ? "P" : ".");
            Equal("..P....P.P", string.Join(string.Empty, pings), "one ping per contact, re-armed past reach + release or with no enemy");
        }
    }
}
