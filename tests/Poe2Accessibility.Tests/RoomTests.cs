namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Named rooms: label keys, the matcher on a made-up grid, a cut of the Manor and the whole dump, and when a room is said.</summary>
    public static class RoomTests
    {
        private const int Sub = 23;

        /// <summary>The Ogham Manor dump the study matched (docs/research/rooms.md), in the development host's debug folder.</summary>
        private const string ManorDump = "tiles-G1_15-20261108-124222.txt";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(RoomLabelsBecomeKeys), RoomLabelsBecomeKeys),
            (nameof(GenericRoomsAreNamedByTheirFileOrTheirPropsOrStaySilent), GenericRoomsAreNamedByTheirFileOrTheirPropsOrStaySilent),
            (nameof(ARoomIsFoundTurnedAndWithItsDecorLeftOut), ARoomIsFoundTurnedAndWithItsDecorLeftOut),
            (nameof(TheStudyIsFoundInACutOfTheManor), TheStudyIsFoundInACutOfTheManor),
            (nameof(TheManorsRoomsAreFound), TheManorsRoomsAreFound),
            (nameof(ARoomIsSaidOnceAndAgainAfterGoingFar), ARoomIsSaidOnceAndAgainAfterGoingFar),
        };

        private static void RoomLabelsBecomeKeys()
        {
            Equal("wine cellar", RoomLabels.Key("WineCellar"), "camel case splits");
            Equal("boss entry", RoomLabels.Key("boss_entry"), "underscores split");
            Equal("armoury", RoomLabels.Key("armoury_lvl2"), "level words go");
            Equal("library", RoomLabels.Key("libraryH"), "a single letter goes");
            Equal("level hole", RoomLabels.Key("MidLevelHole"), "role words go");
            Equal("", RoomLabels.Key("entrance_2"), "nothing left: no key");
            Equal(false, RoomLabels.Spoken.Contains(RoomLabels.Key("StairsDown1")), "stairs are the tiles' to name");
            Equal("", string.Join(", ", RoomLabels.Spoken.Where(key => Announcements.RoomName(key) == null)), "every spoken key has a name");
            Equal("Wine cellar", Announcements.RoomName("wine cellar"), "named");
        }

        private static void GenericRoomsAreNamedByTheirFileOrTheirPropsOrStaySilent()
        {
            const string Rooms = "metadata/terrain/gallows/act1/1_1/rooms/";
            var none = Array.Empty<string>();
            string? Name(string file, string label = "", string[]? props = null, string[]? classes = null) => RoomNaming.Key(Rooms + file, label, props ?? none, classes ?? none);

            Equal("boss", Name("boss_01.arm", "Boss"), "a spoken label stays as it is");
            Equal("cart", Name("carts_01.arm", "landmark"), "a generic label, a file name that says what it is");
            Equal("cart", Name("smallcarts2x2.arm"), "a compound word of the vocabulary");
            Equal("huts", Name("clearing_huts_01_01.arm", "clearing huts"), "the file name before a label that is not spoken");
            Equal("tree of souls", Name("tree_of_souls.arm", "soultree"), "several words in a row");
            Equal("smithy", Name("blacksmith.arm", "blacksmith"), "a trade is its place");
            Equal("with campfire", Name("bonfire_fence.arm"), "a bonfire is a campfire");
            Equal(null, Name("cnr_04.arm"), "a corner piece says nothing");
            Equal(null, Name("fill_dwelling_01.arm"), "a fill is the map's fabric, however it is called");
            Equal(null, Name("tomb_end_1.arm"), "so is the end of a tile run");
            Equal(null, Name("2open_cr_3.arm", props: new[] { "statue" }), "a piece with a statue is still a piece");
            Equal(null, Name("entrance_01.arm", "entrance", new[] { "cart", "cart" }), "a role file with a prop in it is not named by the prop");
            Equal(null, Name("mission_02.arm", "mission", classes: new[] { "Metadata/Terrain/Foo/Objects/SmashableProps" }), "nothing to say: silent");
            Equal(null, Name("concave_1.arm"), "'cave' inside a word is no cave");
            Equal(null, Name("shallowcorner_01.arm"), "'hall' inside a word is no hall");
            Equal("with statue", Name("landmark_01.arm", "landmark", new[] { "statue", "statue", "bell" }), "a landmark is what it holds most of");
            Equal("with altar", Name("landmark_02.arm", "landmark", new[] { "bell", "altar" }), "a tie goes to the weightier kind");
            Equal(null, Name("landmark_03.arm", "landmark", new[] { "table", "chair", "bench" }), "furniture names nothing");
            Equal("shrine", Name("feature_01.arm", "feature", classes: new[] { "Metadata/Terrain/Foo/Objects/GoblinShrineCosmetic" }), "a shrine object in a generic feature");
            Equal(null, Name("mini_boss.arm", "mini boss", classes: new[] { "Metadata/Terrain/Foo/Objects/Checkpoint" }), "a minor boss label alone promises nothing");
            Equal("miniboss", Name("miniboss_01.arm", "miniboss", classes: new[] { "Metadata/MiscellaneousObjects/CheckpointBoss" }), "a boss's own marker makes it true");
            Equal(null, RoomNaming.Key("metadata/terrain/characterselection/gallowsareas/rooms/login_gallows.arm", "", none, none), "the character selection's scenery is no place");
            Equal("", string.Join(", ", RoomNaming.Keys.Where(key => Announcements.RoomName(key) == null)), "every key has a phrase");
        }

        private static void ARoomIsFoundTurnedAndWithItsDecorLeftOut()
        {
            // wall, floor, a closed door (decor: a wall may stand for it) and an organ; a throne room in family a/b, the same
            // shape as a chapel of a family the area does not use, and a hallway that fits everywhere.
            const string Room = @"""w"": 4, ""h"": 3, ""runs"": [0, 0, 4, 3, 1, 0, 1, 3, 1, 1, 1, 2, 1, 2, 1, 0, 1, 3, 1, 1, 2, 0, 4, 3]";
            var catalog = RoomCatalog.Parse(@"{
                ""classes"": [""floor"", ""tag:doorclosed"", ""tag:organ"", ""wall""], ""soft"": [1], ""softAccepts"": [0, 1, 3],
                ""families"": [""a/b"", ""c/d""],
                ""tiles"": { ""a/b"": { ""floor.tdt"": 0, ""door.tdt"": 1, ""organ.tdt"": 2, ""wall.tdt"": 3 } },
                ""rooms"": [
                    { ""label"": ""throne room"", ""set"": ""x"", ""families"": [0], " + Room + @" },
                    { ""label"": ""chapel"", ""set"": ""y"", ""families"": [1], " + Room + @" },
                    { ""label"": ""hallway"", ""set"": ""x"", ""families"": [0], ""w"": 3, ""h"": 1, ""runs"": [0, 0, 3, 0] }
                ]
            }");
            var grid = new Dictionary<(int X, int Y), string>();
            for (var x = 0; x < 12; x++)
            {
                for (var y = 0; y < 12; y++)
                {
                    grid[(x, y)] = "floor";
                }
            }

            // Turned a quarter and mirrored (turn 5: axes swapped, then mirrored across), at sub-tile (6, 2); the door is a wall.
            var names = new[] { "floor", "door", "organ", "wall" };
            foreach (var (x, y, cls) in catalog.Rooms[0].Cells)
            {
                grid[(6 + (catalog.Rooms[0].Height - 1 - y), 2 + x)] = cls == 1 ? "wall" : names[cls];
            }

            var placements = grid.Select(cell => new TilePlacement($"metadata/terrain/a/b/{cell.Value}.tdt", 0, 0, 0, new Vector2(cell.Key.X * Sub, cell.Key.Y * Sub))).ToList();
            var found = RoomMatcher.Match(catalog, placements, Sub);
            Equal($"throne room {6 * Sub},{2 * Sub} {3 * Sub}x{4 * Sub}", string.Join("; ", found.Select(room => $"{room.Label} {room.X},{room.Y} {room.Width}x{room.Height}")),
                "found turned; a wall stands for its door; the chapel's family is not here and the hallway fits too many places");
            Equal("throne room", RoomMatcher.At(found, new Vector2(7 * Sub, 3 * Sub))?.Label, "a cell inside it");
            Equal(null, RoomMatcher.At(found, new Vector2(0, 0)), "outside");

            grid[(8, 2)] = "floor"; // the room's corner wall
            placements = grid.Select(cell => new TilePlacement($"metadata/terrain/a/b/{cell.Value}.tdt", 0, 0, 0, new Vector2(cell.Key.X * Sub, cell.Key.Y * Sub))).ToList();
            Equal(0, RoomMatcher.Match(catalog, placements, Sub).Count, "one wall wrong in a small room: not it");
        }

        private static void TheStudyIsFoundInACutOfTheManor()
        {
            var catalog = RoomCatalog.Parse(File.ReadAllText(Fixture("rooms-manor-study.json")));
            var found = RoomMatcher.Match(catalog, Placements(File.ReadLines(Fixture("tiles-manor-study.txt"))), Sub);
            Equal("study 66,90", string.Join("; ", found.Select(room => $"{room.Label} {room.X / Sub},{room.Y / Sub}")), "the study where the Manor has it; the throne room and torture room lie outside the cut");
        }

        /// <summary>The whole Manor dump against the shipped catalog, when both are on this machine; silent otherwise.</summary>
        private static void TheManorsRoomsAreFound()
        {
            var dump = Find(Path.Join("host", "Poe2Access", "bin", "Release", "net10.0-windows", "win-x64", "Plugins", "Poe2Accessibility", "debug", ManorDump));
            var catalogFile = Find(Path.Join("Poe2Accessibility", "catalog", "rooms.json"));
            if (dump == null || catalogFile == null)
            {
                return;
            }

            var catalog = RoomCatalog.Parse(File.ReadAllText(catalogFile));
            var placements = Placements(File.ReadLines(dump));
            RoomMatcher.Match(catalog, placements, Sub);
            var clock = Stopwatch.StartNew();
            var rooms = RoomMatcher.Match(catalog, placements, Sub);
            Console.WriteLine($"     Manor: {rooms.Count} named rooms in {clock.ElapsedMilliseconds} ms ({catalog.Rooms.Count} in the catalog)");
            Equal("boss, boss entry, chapel, level hole, study, throne room, top level hole, tortureroom, wine cellar", string.Join(", ", rooms.Select(room => room.Label).Order()), "the Manor's named rooms, each once");

            // Where the study (docs/research/rooms.md) placed each room file's corner, in sub-tiles; the box starts at its first cell.
            foreach (var (label, x, y) in new[] { ("chapel", 60, 144), ("study", 66, 90), ("wine cellar", 36, 18), ("throne room", 12, 90), ("tortureroom", 48, 30) })
            {
                var room = rooms.Single(room => room.Label == label);
                Equal(true, room.X / Sub - x is >= 0 and <= 2 && room.Y / Sub - y is >= 0 and <= 2, $"{label} at ({x}, {y}), found at ({room.X / Sub}, {room.Y / Sub})");
            }
        }

        private static void ARoomIsSaidOnceAndAgainAfterGoingFar()
        {
            var rooms = new RoomTracker(awayCells: 100);
            var chapel = new NamedRoom("chapel", 0, 0, 50, 50);
            Equal<bool?>(false, rooms.Enter(chapel, new Vector2(10, 10)), "walking into a room is news");
            Equal<bool?>(null, rooms.Enter(chapel, new Vector2(20, 10)), "staying in it is not");
            Equal<bool?>(null, rooms.Enter(null, new Vector2(70, 10)) ?? rooms.Enter(chapel, new Vector2(45, 10)), "stepping out and back in nearby is not either");
            Equal<bool?>(null, rooms.Enter(null, new Vector2(300, 10)), "going far away");
            Equal<bool?>(true, rooms.Enter(chapel, new Vector2(45, 10)), "coming back is news, as visited");

            var empty = Array.Empty<(ForkWay State, IReadOnlyList<string> Sides)>();
            var ways = ForkTracker.Tell(new[] { ("up", ForkWay.GoesOn) });
            string? Say((string Name, bool Again, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> Ways)? line) =>
                line is { } said ? Announcements.Fork(said.Name, said.Again, said.Ways) : null;
            Equal("Chapel", Say(RoomTracker.Line("Chapel", false, null, empty)), "a room alone is its name");
            Equal("Chapel: goes on up", Say(RoomTracker.Line("Chapel", false, ("Chapel", false), ways)), "a fork walked into with the room: one line, named after the room");
            Equal(null, Say(RoomTracker.Line("Chapel", null, ("Chapel", false), empty)), "a fork with nothing new in a room already named says nothing");
            Equal("Chapel: goes on up", Say(RoomTracker.Line("Chapel", null, ("Chapel", false), ways)), "one with a way on names the room again with it");
            Equal("Chapel, you've been here: goes on up", Say(RoomTracker.Line("Chapel", true, ("Chapel", false), ways)), "come back to the room through a new fork");
            Equal("Fork 2", Say(RoomTracker.Line(null, null, ("Fork 2", false), empty)), "a fork outside rooms as before");
            Equal(null, Say(RoomTracker.Line(null, null, null, empty)), "nothing");

            var forks = new ForkTracker(awayCells: 100);
            Equal("Chapel", forks.Enter(3, Vector2.Zero, () => ("Fork", true), "Chapel")?.Name, "a fork in a named room takes its name");
            Equal("Chapel", forks.Enter(4, new Vector2(30, 0), () => ("Fork", true), "Chapel")?.Name, "a second fork in it too, not numbered");
            Equal("Fork 1", forks.Enter(5, new Vector2(60, 0), () => ("Fork", true))?.Name, "outside, the forks are numbered from the first");
        }

        /// <summary>A tile dump's sub-tiles: "x y path idX idY rotation", x and y the sub-tile's top-left grid cell.</summary>
        internal static List<TilePlacement> Placements(IEnumerable<string> lines) =>
            lines.Select(line => line.Split(' ')).Where(parts => parts.Length == 6 && int.TryParse(parts[0], out _))
                .Select(parts => new TilePlacement(parts[2], int.Parse(parts[3]), int.Parse(parts[4]), int.Parse(parts[5]), new Vector2(int.Parse(parts[0]), int.Parse(parts[1])))).ToList();

        private static string Fixture(string name) =>
            Find(Path.Join("tests", "Poe2Accessibility.Tests", "Fixtures", name)) ?? throw new FileNotFoundException(name);

        /// <summary>A file under this repository or a checkout above it (a worktree finds the main checkout's debug dumps); null when none has it.</summary>
        internal static string? Find(string relative)
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (var directory = start; directory != null; directory = Path.GetDirectoryName(directory))
                {
                    var path = Path.Join(directory, relative);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }
    }
}
