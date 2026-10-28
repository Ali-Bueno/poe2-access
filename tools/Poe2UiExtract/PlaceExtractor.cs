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
        ("stairs", W("stair(s|case|way)?")),
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
        ("ramp", W("ramp")),
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
    };

    /// <summary>Kinds of prop a player would name, by the whole tokens of the model's file name.</summary>
    private static readonly (string Kind, Regex Token)[] PropKinds =
    {
        ("cart", W("carts?|wagons?|wheelbarrow")),
        ("well", W("well")),
        ("campfire", W("fireplace|campfire|campfiregroup|firepit|bonfire")),
        ("anvil", W("anvil")),
        ("noticeboard", W("notice|noticeboard|bountyboard")),
        ("waterwheel", W("millwheel|waterwheel")),
        ("tent", W("tents?")),
        ("stall", W("stalls?|marketstall")),
        ("statue", W("statue")),
        ("altar", W("altar")),
        ("fountain", W("fountain")),
        ("boat", W("boat|rowboat|canoe")),
        ("throne", W("throne")),
    };

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

    public static int Run(string gameDir, string outFile)
    {
        using var index = SharedBundles.OpenParsed(gameDir);
        return Run(index, outFile);
    }

    /// <summary>On an index already opened with its paths parsed (the batch command).</summary>
    public static int Run(Index index, string outFile)
    {
        var files = index.Files.Values.Where(file => file.Path != null && file.Path.StartsWith(TerrainPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
        var byPath = files.ToDictionary(file => file.Path!.ToLowerInvariant(), StringComparer.Ordinal);

        var tiles = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var tileProps = new SortedDictionary<string, List<object[]>>(StringComparer.Ordinal);
        foreach (var (path, file) in byPath.Where(pair => pair.Key.EndsWith(".tdt", StringComparison.Ordinal)))
        {
            // The file name only: the binary's other strings ("door" in ledge tiles, edge types) named 395 tiles wrongly
            // (review 2026-10-25), and the useful designer tags (SlashBridge, EncampmentFront) repeat the name anyway.
            if (Kind(TileKinds, Path.GetFileNameWithoutExtension(path)) is { } kind)
            {
                tiles[path] = kind;
            }

            if (byPath.TryGetValue(path[..^4] + ".tmd", out var tmd) && Props(Text(tmd.Read().ToArray()), out _) is { Count: > 0 } props)
            {
                tileProps[path] = props;
            }
        }

        var rooms = new List<object>();
        foreach (var (path, file) in byPath.Where(pair => pair.Key.EndsWith(".arm", StringComparison.Ordinal)))
        {
            var props = Props(Text(file.Read().ToArray()), out var anchors);
            if (props.Count > 0 && anchors.Count > 0)
            {
                rooms.Add(new { arm = path, anchors, props });
            }
        }

        var catalog = new { tiles, tileProps, rooms };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = false }));
        Console.WriteLine($"Wrote {outFile}: {tiles.Count} named tiles ({string.Join(", ", tiles.Values.GroupBy(kind => kind).OrderByDescending(group => group.Count()).Select(group => $"{group.Key} {group.Count()}"))}), " +
                          $"{tileProps.Count} tiles with props, {rooms.Count} rooms with anchors and props");
        return tiles.Count == 0 ? 1 : 0;
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

    private static string Text(byte[] data) =>
        data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE ? Encoding.Unicode.GetString(data, 2, data.Length - 2)
        : data.Length >= 2 && data[1] == 0 ? Encoding.Unicode.GetString(data)
        : Encoding.UTF8.GetString(data);

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

            if (Kind(PropKinds, Path.GetFileNameWithoutExtension(line.Groups["ao"].Value)) is { } kind && seen.Add((x / 2, y / 2, kind)))
            {
                props.Add(new object[] { x, y, kind });
            }
        }

        return props;
    }

    /// <summary>Words of a file name or tag: "LumbermillWagon_02" → Lumbermill, Wagon; "slash_bridge_01" → slash, bridge.</summary>
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])")]
    private static partial Regex Words();

    [GeneratedRegex(@"^(?<x>\d+) (?<y>\d+) .*?""(?<ao>[^""]+\.ao)"" ""(?<cls>[^""]*)""", RegexOptions.Multiline)]
    private static partial Regex ObjectLine();
}
