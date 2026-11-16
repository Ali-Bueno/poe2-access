using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Index = LibBundle3.Index;

/// <summary>
///     `places` command: named map pieces and props for the plugin's place announcements (D2R named places), from the
///     terrain files (docs/research/places.md). A tile (.tdt) is named by its file name ("Slash_Bridge_01" → bridge); props come from the tile's .tmd and the area rooms' .arm object lines
///     (`x y … "model.ao" "class"`, x/y in grid cells from the file's origin). Read-only on the game.
/// </summary>
internal static partial class PlaceExtractor
{
    /// <summary>Kinds of map piece, each with the whole-token patterns that name it (tokens are lower-case words).</summary>
    private static readonly (string Kind, Regex Token)[] TileKinds =
    {
        ("bridge", W("bridge")),
        ("stairs", W("stair(s|case|way)?(down|up|lower|upper)?")),
        ("door", W("doors?|doorway")),
        ("gate", W("gates?|gateway|gatehouse")),
        ("ruins", W("ruins?")),
        ("church", W("church|chapel|cathedral")),
        ("graveyard", W("graveyard|cemetery")),
        ("hut", W("huts?|hovel|shack|cabin|cottage|houses?")),
        ("tower", W("towers?")),
        ("temple", W("temple")),
        ("arena", W("arena|bossroom|bossarena|boss")),
        ("entrance", W("entrance")),
        ("dock", W("docks?|dockedge|pier|jetty|wharf")),
        ("waterfall", W("waterfalls?")),
        ("encampment", W("encampment|camp")),
        ("courtyard", W("courtyard")),
        ("alley", W("alley|alleyway")),
        ("ramp", W("ramp|excavation")),
        ("lake", W("lake")),
        ("corridor", W("corridor")),
        ("tomb", W("tomb|crypt|mausoleum")),
        ("forge", W("forge|smithy|foundry")),
        ("statue", W("statues?")),
        ("well", W("well")),
        ("boat", W("boat|ship|shipwreck")),
        ("market", W("market|bazaar|stalls?")),
        ("garden", W("garden")),
        ("library", W("library")),
        ("ballroom", W("ballroom")),
        ("fireplace", W("fireplace")),
        ("window", W("windows?")),
        ("organ", W("organ")),
        ("hole", W("hole")),
        ("walkway", W("walkways?")),
        ("platform", W("platforms?")),
        ("throne", W("throne")),
        ("chasm", W("chasm")),
        ("pillar", W("pillars?|columns?")),
        ("cell", W("cells?")),
        ("oven", W("ovens?")),
    };

    /// <summary>
    ///     Kinds that also name wall and cliff trims ("walkwaytrim", "outer_edge_st_pillar"): a tile with one of these words is a trim, not the place.
    ///     Rejected whole after the sweep of 2026-11-16: ledge (684 cliff edges), crevice (206 cave edge pieces), ziggurat (outer walls).
    /// </summary>
    private static readonly HashSet<string> TrimKinds = new() { "walkway", "pillar", "platform", "hole" };

    private static readonly Regex TrimWord = new("^(?:walls?|trim|edge|thinwall|thickwall|cliffs?|abyss)$", RegexOptions.Compiled);

    /// <summary>Stairs' way from words of the file name or the designer tags: "StairsDown", "stairsup", "grand_stairs_lower", "TransitionDown".</summary>
    private static readonly Regex DownWord = new("^(?:down|lower|stairs?down|stairlower|stairslower|transitiondown)$", RegexOptions.Compiled);
    private static readonly Regex UpWord = new("^(?:up|upper|stairs?up|stairupper|stairsupper|transitionup)$", RegexOptions.Compiled);

    /// <summary>Kinds of prop a player would name, by the whole words of the model's file name; the first kind matching wins.</summary>
    private static readonly (string Kind, Regex Token)[] PropKinds =
    {
        ("cart", W("carts?|wagons?|haywagon|wheelbarrow|caravan")),
        ("well", W("wells?")),
        ("campfire", W("fireplace|campfire|campfiregroup|firepit|bonfire|pyre|brazier")),
        ("anvil", W("anvil")),
        ("noticeboard", W("notice|noticeboard|bountyboard|sign|signpost")),
        ("waterwheel", W("millwheel|waterwheel|windmill")),
        ("tent", W("tents?|whare|pavilion")),
        ("stall", W("stalls?|marketstall")),
        ("statue", W("statues?|idol")),
        ("altar", W("altars?")),
        ("fountain", W("fountains?")),
        ("boat", W("boats?|rowboat|canoe|raft|shipwrecks?|dreadnought")),
        ("throne", W("throne")),
        ("barrel", W("barrels?")),
        ("crate", W("crates?|cargo")),
        ("table", W("table|craftingbench")),
        ("pillar", W("pillars?|columns?|obelisk|monolith|megalith|stele")),
        ("urn", W("urns?|urnstack|pots|jars|pitcher")),
        ("corpse", W("corpses?|prisoners|executed|bloodbags|stretcher")),
        ("banner", W("banners?|flags?")),
        ("cage", W("cages?|caged")),
        ("bench", W("bench")),
        ("gallows", W("gallows|gibbets?|hangman|stocks|impaled")),
        ("skeleton", W("skeletons?|skulls?|ribs")),
        ("ladder", W("ladder|standladder")),
        ("bridge", W("bridges?|gangplank|causeway")),
        ("deadtree", W("deadtree|driftwood")),
        ("chair", W("chair|stool")),
        ("bell", W("bells?")),
        ("hut", W("hut|house|shack|dwellings?|cabin|coop")),
        ("weaponrack", W("weapons?|armou?r|swords?|spear|racks?")),
        ("fence", W("fences?|palisade|hedges?")),
        ("machine", W("machines?|mechanism|device|elevator|lift|crane|conveyor")),
        ("tomb", W("coffin|sarcophagus|tomb|crypt|mausoleum|grave|graveyard")),
        ("totem", W("totems?")),
        ("gate", W("gates?|archway|waygate")),
        ("forge", W("forge|furnace|smelting|foundry|smithy|kiln")),
        ("haystack", W("haystack|haystand|bales?")),
        ("bed", W("bed|hammock|cot")),
        ("stump", W("stump")),
        ("bookshelf", W("bookshelf|bookcase|bookpile")),
        ("tower", W("tower|lighthouse")),
        ("cannon", W("cannons?")),
    };

    /// <summary>Model words that make a prop a ground decal, an effect or a roof, not an object ("BarrelCactus", "CartTracks", "KaruiTotemEyes_glow", "BoatOceanPlane").</summary>
    private static readonly Regex NotAProp = W("cactus|tracks?|overlay|rooffade|roof|trigger|connector|marker|glow|eyes|plane|decals?");

    /// <summary>Entities and backdrops the mod names another way or that cannot be walked up to (see docs/research/places.md).</summary>
    private static readonly Regex NotAPropClass = new("Chest|Shrine|Door|HealingWell|Waypoint|Stash|Checkpoint|/NPC/|AreaTransition|Spawner|Emitter|FlowBlocking|Distant|ExtendDistance", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Room objects the plugin also sees as entities: they place a room (an area's fixed layout) on the live map.</summary>
    private static readonly Dictionary<string, string> AnchorClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Metadata/MiscellaneousObjects/Waypoint"] = "waypoint",
        ["Metadata/MiscellaneousObjects/Stash"] = "stash",
        ["Metadata/MiscellaneousObjects/HealingWell"] = "well",
        ["Metadata/MiscellaneousObjects/Checkpoint"] = "checkpoint",
    };

    private const string TerrainPrefix = "metadata/terrain/";

    private static Regex W(string words) => new($"^(?:{words})$", RegexOptions.Compiled);

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var files = index.Files.Values.Where(file => file.Path != null && file.Path.StartsWith(TerrainPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        var byPath = files.ToDictionary(file => file.Path!.ToLowerInvariant(), StringComparer.Ordinal);

        // Every .arm and every .tmd beside a .tdt, read bundle by bundle and parsed side by side; the catalog is then built in the paths' own order.
        var parsed = new System.Collections.Concurrent.ConcurrentDictionary<string, (List<object[]> Props, List<object[]> Anchors)>(StringComparer.Ordinal);
        // A tile's props are in the .tmd its .tdt names, which is not always the tile's own name (902 tiles).
        var tmdOf = new System.Collections.Concurrent.ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var tdts = byPath.Where(pair => pair.Key.EndsWith(".tdt", StringComparison.Ordinal)).ToDictionary(pair => pair.Value, pair => pair.Key);
        BundleFiles.ReadGrouped(tdts.Keys, (file, bytes) =>
        {
            if (TmdReference(bytes) is { } reference && byPath.ContainsKey(reference))
            {
                tmdOf[tdts[file]] = reference;
            }
        });
        string TmdPath(string tdt) => tmdOf.TryGetValue(tdt, out var reference) ? reference : tdt[..^4] + ".tmd";
        var wanted = byPath.Where(pair => pair.Key.EndsWith(".arm", StringComparison.Ordinal))
            .Concat(tdts.Values.Select(TmdPath).Distinct(StringComparer.Ordinal).Where(byPath.ContainsKey).Select(path => KeyValuePair.Create(path, byPath[path])))
            .ToDictionary(pair => pair.Value, pair => pair.Key);
        BundleFiles.ReadGrouped(wanted.Keys, (file, bytes) =>
        {
            var props = Props(Text(bytes, wanted[file]), out var anchors);
            parsed[wanted[file]] = (props, anchors);
        });

        var tiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var tileProps = new SortedDictionary<string, List<object[]>>(StringComparer.Ordinal);
        foreach (var (path, file) in byPath.Where(pair => pair.Key.EndsWith(".tdt", StringComparison.Ordinal)))
        {
            // The file name only: the binary's other strings ("door" in ledge tiles, edge types) named 395 tiles wrongly
            // (review 2026-10-25), and the useful designer tags (SlashBridge, EncampmentFront) repeat the name anyway.
            if (TileKind(Path.GetFileNameWithoutExtension(path)) is { } kind)
            {
                if (kind == "stairs")
                {
                    kind = StairsKind(Path.GetFileNameWithoutExtension(path), file);
                }

                tiles[path] = kind;
            }

            if (parsed.TryGetValue(TmdPath(path), out var tmd) && tmd.Props.Count > 0)
            {
                tileProps[path] = tmd.Props;
            }
        }

        var rooms = new List<object>();
        foreach (var (path, file) in byPath.Where(pair => pair.Key.EndsWith(".arm", StringComparison.Ordinal)))
        {
            var (props, anchors) = parsed[path];
            if (props.Count > 0 && anchors.Count > 0)
            {
                rooms.Add(new { arm = path, anchors, props });
            }
        }

        var catalog = new { tiles, tileProps, rooms };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = false }));
        Say.Out.WriteLine($"Wrote {outFile}: {tiles.Count} named tiles ({string.Join(", ", tiles.Values.GroupBy(kind => kind).OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}"))}), " +
                          $"{tileProps.Count} tiles with props, {rooms.Count} rooms with anchors and props");
        return tiles.Count == 0 ? 1 : 0;
    }

    /// <summary>A designer tag that is only the stairs' way ("TransitionDown", "StairLower", "Stairs_Upper"); the strings around it are model paths and edge types.</summary>
    private static readonly Regex DirectionTag = new("^(?:transition|stairs?)_?(?:up|down|lower|upper)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>"stairs_down", "stairs_up" or plain "stairs": the way the file name says, else the .tdt's designer tag; both ways or none stay plain.</summary>
    private static string StairsKind(string name, LibBundle3.Records.FileRecord file)
    {
        var way = Way(Words().Matches(name).Select(match => match.Value.ToLowerInvariant()));
        if (way == null)
        {
            way = Way(TdtTags(file.Read().ToArray()).Where(tag => DirectionTag.IsMatch(tag)).SelectMany(tag => Words().Matches(tag).Select(match => match.Value.ToLowerInvariant())));
        }

        return way ?? "stairs";
    }

    internal static string? TileKind(string name)
    {
        var kind = Kind(TileKinds, name);
        return kind != null && TrimKinds.Contains(kind) && Words().Matches(name).Any(match => TrimWord.IsMatch(match.Value.ToLowerInvariant())) ? null : kind;
    }

    internal static string? Way(IEnumerable<string> tokens)
    {
        var list = tokens.ToList();
        var down = list.Any(DownWord.IsMatch);
        var up = list.Any(UpWord.IsMatch);
        return down == up ? null : down ? "stairs_down" : "stairs_up";
    }

    /// <summary>The .tmd path a .tdt's UTF-16 strings name, lower-case; null when none.</summary>
    internal static string? TmdReference(byte[] data)
    {
        foreach (var start in new[] { 0, 1 })
        {
            var text = Encoding.Unicode.GetString(data, start, (data.Length - start) & ~1);
            var match = TmdName().Match(text);
            if (match.Success)
            {
                return match.Value.ToLowerInvariant();
            }
        }

        return null;
    }

    [GeneratedRegex(@"metadata/terrain/[\w/.\-]+?\.tmd", RegexOptions.IgnoreCase)]
    private static partial Regex TmdName();

    /// <summary>The designer tags of a .tdt: its UTF-16 strings that are not paths.</summary>
    private static IEnumerable<string> TdtTags(byte[] data)
    {
        var text = Encoding.Unicode.GetString(data, 0, data.Length & ~1);
        return Regex.Matches(text, @"[A-Za-z_]{4,40}").Select(match => match.Value);
    }

    /// <summary>The first kind whose pattern matches a whole token of the name (camel case and separators split it).</summary>
    internal static string? Kind((string Kind, Regex Token)[] kinds, string name)
    {
        var tokens = Words().Matches(name).Select(match => match.Value.ToLowerInvariant()).ToList();
        foreach (var (kind, token) in kinds)
        {
            if (tokens.Any(token.IsMatch))
            {
                return kind;
            }
        }

        return null;
    }

    internal static string Text(byte[] data, string? path) => GameFormats.Noted(path,
        data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE ? Encoding.Unicode.GetString(data, 2, data.Length - 2)
        : data.Length >= 2 && data[1] == 0 ? Encoding.Unicode.GetString(data)
        : Encoding.UTF8.GetString(data));

    /// <summary>Named props of a .tmd or .arm ([x, y, kind], one per kind and spot) and the room's anchors.</summary>
    private static List<object[]> Props(string text, out List<object[]> anchors)
    {
        anchors = new List<object[]>();
        var props = new List<object[]>();
        var seen = new HashSet<(int, int, string)>();
        foreach (Match line in ObjectLine().Matches(text))
        {
            var x = int.Parse(line.Groups["x"].Value);
            var y = int.Parse(line.Groups["y"].Value);
            if (AnchorClasses.TryGetValue(line.Groups["cls"].Value, out var anchor))
            {
                anchors.Add(new object[] { x, y, anchor });
                continue;
            }

            if (!NotAPropClass.IsMatch(line.Groups["cls"].Value + line.Groups["rest"].Value) && PropKind(Path.GetFileNameWithoutExtension(line.Groups["ao"].Value)) is { } kind && seen.Add((x / 2, y / 2, kind)))
            {
                props.Add(new object[] { x, y, kind });
            }
        }

        return props;
    }

    /// <summary>The kinds of the props of a .arm's text, one per prop.</summary>
    internal static List<string> PropKindsOf(string text) => Props(text, out _).Select(prop => (string)prop[2]).ToList();

    internal static string? PropKind(string model) =>
        Words().Matches(model).Any(match => NotAProp.IsMatch(match.Value.ToLowerInvariant())) ? null : Kind(PropKinds, model);

    /// <summary>Words of a file name or tag: "LumbermillWagon_02" → Lumbermill, Wagon; "slash_bridge_01" → slash, bridge.</summary>
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])")]
    private static partial Regex Words();

    [GeneratedRegex(@"^(?<x>\d+) (?<y>\d+) .*?""(?<ao>[^""]+\.ao)"" ""(?<cls>[^""]*)""(?<rest>.*)", RegexOptions.Multiline)]
    private static partial Regex ObjectLine();
}
