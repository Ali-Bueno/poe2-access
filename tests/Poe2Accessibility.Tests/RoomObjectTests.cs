namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Named rooms placed by their entity objects (outdoors): a made-up catalog, then Clearfell's dumps against the shipped catalog.</summary>
    public static class RoomObjectTests
    {
        private const int Sub = 23;

        private const string Debug = "host/Poe2Access/bin/Release/net10.0-windows/win-x64/Plugins/Poe2Accessibility/debug";

        /// <summary>A camp (3 by 2 sub-tiles) in folder x with a chest only camps have, a checkpoint and an NPC; a hallway of x
        /// with a checkpoint and a waypoint; a bridge of folder y, a family the area does not use.</summary>
        private const string Catalog = @"{
            ""classes"": [], ""soft"": [], ""softAccepts"": [], ""families"": [""a/b"", ""c/d""], ""tiles"": {},
            ""objectKeys"": [""Metadata/Chests/CampChest"", ""Metadata/MiscellaneousObjects/Checkpoint"", ""Metadata/NPC/Camper"", ""Metadata/MiscellaneousObjects/Waypoint""],
            ""uniqueKeys"": [0],
            ""rooms"": [
                { ""label"": ""campsite"", ""set"": ""x"", ""w"": 3, ""h"": 2, ""families"": [0], ""runs"": [], ""objects"": [5, 9, 0, 50, 8, 1, 33, 40, 2] },
                { ""label"": ""hallway"", ""set"": ""x"", ""w"": 2, ""h"": 2, ""families"": [], ""runs"": [], ""objects"": [5, 11, 1, 40, 11, 3] },
                { ""label"": ""bridge"", ""set"": ""y"", ""w"": 2, ""h"": 2, ""families"": [1], ""runs"": [], ""objects"": [5, 5, 1, 40, 5, 3] }
            ]
        }";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheCatalogKeysEntityPaths), TheCatalogKeysEntityPaths),
            (nameof(OnlyRoomsOfTheAreasFoldersAreTried), OnlyRoomsOfTheAreasFoldersAreTried),
            (nameof(ARoomIsPlacedTurnedByTwoOfItsObjects), ARoomIsPlacedTurnedByTwoOfItsObjects),
            (nameof(OneObjectPlacesOnlyARoomOfItsOwn), OneObjectPlacesOnlyARoomOfItsOwn),
            (nameof(AFindTheTilesMadeIsKeptOnce), AFindTheTilesMadeIsKeptOnce),
            (nameof(ClearfellsRoomsAreFoundByTheirObjects), ClearfellsRoomsAreFoundByTheirObjects),
        };

        private static void TheCatalogKeysEntityPaths()
        {
            var catalog = RoomCatalog.Parse(Catalog);
            Equal(0, catalog.KeyOf("Metadata/Chests/CampChest"), "a room object's path");
            Equal(2, catalog.KeyOf("Metadata/NPC/Camper@2"), "the level suffix of a live path goes");
            Equal(1, catalog.KeyOf("metadata/miscellaneousobjects/checkpoint"), "any case");
            Equal(-1, catalog.KeyOf("Metadata/Monsters/Wolf@2"), "no room places it");
            Equal("5,9,0 50,8,1 33,40,2", string.Join(" ", catalog.Rooms[0].Objects.Select(spot => $"{spot.X},{spot.Y},{spot.Key}")), "the camp's objects");
            Equal(true, catalog.UniqueKeys[0] && !catalog.UniqueKeys[1], "the chest is the camps' own, the checkpoint everyone's");
        }

        private static void OnlyRoomsOfTheAreasFoldersAreTried()
        {
            var catalog = RoomCatalog.Parse(Catalog);
            var area = Ground(20, 20, "a/b");
            Equal("campsite, hallway", string.Join(", ", RoomObjectMatcher.Candidates(catalog, area).Select(room => room.Label)),
                "folder x has a room of the area's family (the hallway names none); folder y's bridge is not tried");
            Equal(0, RoomObjectMatcher.Candidates(catalog, Ground(2, 4, "a/b")).Count, "a room as big as half the area is the area itself");
        }

        private static void ARoomIsPlacedTurnedByTwoOfItsObjects()
        {
            var catalog = RoomCatalog.Parse(Catalog);
            var rooms = RoomObjectMatcher.Candidates(catalog, Ground(20, 20, "a/b"));

            // Turn 5 (axes swapped, mirrored across) at sub-tile (4, 2): the camp is 2 by 3 sub-tiles there; a cell or two off.
            var corner = new Vector2(4 * Sub, 2 * Sub);
            Vector2 At(int x, int y) => corner + new Vector2((2 * Sub) - 1 - y, x);
            var live = new List<(int Key, Vector2 Grid)> { (1, At(50, 8) + new Vector2(1, -2)), (2, At(33, 40) + new Vector2(-1, 1)), (1, new Vector2(400, 50)) };
            Equal($"campsite {4 * Sub},{2 * Sub} {2 * Sub}x{3 * Sub}", Said(RoomObjectMatcher.Match(catalog, rooms, live, Sub)), "the checkpoint and the camper agree on one turn and corner");

            Equal(string.Empty, Said(RoomObjectMatcher.Match(catalog, rooms, live.Take(1).ToList(), Sub)), "a lone checkpoint places nothing");
            live[1] = (2, At(33, 40) + new Vector2(5, 0));
            Equal(string.Empty, Said(RoomObjectMatcher.Match(catalog, rooms, live, Sub)), "the camper five cells off is not the camp's");
        }

        private static void OneObjectPlacesOnlyARoomOfItsOwn()
        {
            var catalog = RoomCatalog.Parse(Catalog);
            var rooms = RoomObjectMatcher.Candidates(catalog, Ground(20, 20, "a/b"));
            var chest = new Vector2((6 * Sub) + 5, (3 * Sub) + 9);
            Equal($"campsite {6 * Sub},{3 * Sub} {3 * Sub}x{2 * Sub}", Said(RoomObjectMatcher.Match(catalog, rooms, new List<(int, Vector2)> { (0, chest) }, Sub)),
                "only camps have the chest, and one turn alone puts the camp's corner on the sub-tile grid");

            // The hallway's checkpoint and waypoint (row 11 of 46) fit it as they stand and mirrored down, a sub-tile higher.
            var hall = new Vector2(10 * Sub, 10 * Sub);
            var pair = new List<(int, Vector2)> { (1, hall + new Vector2(5, 11)), (3, hall + new Vector2(40, 11)) };
            Equal(string.Empty, Said(RoomObjectMatcher.Match(catalog, rooms, pair, Sub)), "two equal places on the same entities: neither is said");
            var stronger = pair.Concat(new (int, Vector2)[] { (2, new Vector2(4 * Sub, 2 * Sub) + new Vector2((2 * Sub) - 1 - 40, 33)), (1, new Vector2(4 * Sub, 2 * Sub) + new Vector2((2 * Sub) - 1 - 8, 50)) }).ToList();
            Equal($"campsite {4 * Sub},{2 * Sub} {2 * Sub}x{3 * Sub}", Said(RoomObjectMatcher.Match(catalog, rooms, stronger, Sub)), "a camp elsewhere is still said");
        }

        private static void AFindTheTilesMadeIsKeptOnce()
        {
            var tiles = new List<NamedRoom> { new("campsite", 92, 46, 69, 46) };
            var objects = new[] { new NamedRoom("campsite", 92, 46, 69, 46), new NamedRoom("hallway", 92, 46, 46, 46), new NamedRoom("campsite", 400, 400, 69, 46) };
            Equal("campsite 92,46 69x46; hallway 92,46 46x46; campsite 400,400 69x46", Said(RoomObjectMatcher.Merge(tiles, objects)),
                "the camp the tiles found is not added again; another label there and another camp elsewhere are");
        }

        /// <summary>
        ///     Clearfell's tile dumps (their entity lines) against the shipped catalog, when both are on this machine; silent
        ///     otherwise: the Mud Burrow's entrance by its waypoint, checkpoint and transition, the lumbermill's exterior by its
        ///     transition and checkpoint (docs/research/rooms.md).
        /// </summary>
        private static void ClearfellsRoomsAreFoundByTheirObjects()
        {
            var catalogFile = RoomTests.Find(Path.Join("Poe2Accessibility", "catalog", "rooms.json"));
            if (catalogFile == null)
            {
                return;
            }

            var catalog = RoomCatalog.Parse(File.ReadAllText(catalogFile));
            foreach (var (dump, expected) in new[] { ("tiles-G1_2-20261108-113444.txt", "mud 207,1104 138x138"), ("tiles-G1_2-20261108-143449.txt", "town 138,138 621x414") })
            {
                if (RoomTests.Find(Path.Join(Debug, dump)) is not { } file)
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                var live = lines.Where(line => line.StartsWith("entity ", StringComparison.Ordinal)).Select(line => line.Split(' '))
                    .Select(parts => (Key: catalog.KeyOf(parts[3]), Grid: new Vector2(int.Parse(parts[1]), int.Parse(parts[2])))).Where(entity => entity.Key >= 0).ToList();
                var rooms = RoomObjectMatcher.Candidates(catalog, RoomTests.Placements(lines));
                Equal(expected, Said(RoomObjectMatcher.Match(catalog, rooms, live, Sub)), $"{dump}: {live.Count} room objects, {rooms.Count} rooms tried");
            }
        }

        /// <summary>A plain area of one family, w by h sub-tiles.</summary>
        private static List<TilePlacement> Ground(int width, int height, string family) =>
            Enumerable.Range(0, width * height).Select(i => new TilePlacement($"metadata/terrain/{family}/fill.tdt", 0, 0, 0, new Vector2(i % width, i / width) * Sub)).ToList();

        private static string Said(IEnumerable<NamedRoom> rooms) => string.Join("; ", rooms.Select(room => $"{room.Label} {room.X},{room.Y} {room.Width}x{room.Height}"));
    }
}
