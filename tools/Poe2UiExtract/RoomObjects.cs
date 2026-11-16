using System.Text.RegularExpressions;

/// <summary>
///     The objects of a room file (.arm) the plugin also sees as live entities, by which an outdoor room is placed: chests,
///     NPCs, quest objects, shrines, transitions, checkpoints, waypoints, stashes, wells, doors and an area's own objects
///     (docs/research/rooms.md).
/// </summary>
internal static partial class RoomObjects
{
    /// <summary>One object: grid cells from the room's corner and its class, the path its entity has ("@level" dropped).</summary>
    public readonly record struct Spot(int X, int Y, string Class);

    /// <summary>
    ///     Classes that are entities at run time (seen in the debug dumps: chests, NPCs, quest and area objects, the
    ///     miscellaneous objects below). Doodads are client-only, monsters and critters walk away from where they stand.
    /// </summary>
    [GeneratedRegex(@"^Metadata/(?:Chests|NPC|QuestObjects|Shrines)/|^Metadata/MiscellaneousObjects/(?:AreaTransition|Checkpoint|Waypoint|Stash|GuildStash|HealingWell|Door)|^Metadata/Terrain/.+/Objects/")]
    private static partial Regex EntityClass();

    /// <summary>Editor and world-map helpers among those classes: never an entity to meet.</summary>
    [GeneratedRegex("MapPin|Marker|PatrolPath|Audio|RailPath|RailTrack|RailSegment|PowerLine")]
    private static partial Regex Helper();

    [GeneratedRegex(@"^(?<x>\d+) (?<y>\d+) .*?""[^""]+\.ao"" ""(?<cls>[^""]*)""", RegexOptions.Multiline)]
    private static partial Regex ObjectLine();

    [GeneratedRegex("@\\d+$")]
    private static partial Regex Level();

    /// <summary>The entity objects of a room file's text, each spot once.</summary>
    public static List<Spot> Read(string text)
    {
        var spots = new HashSet<Spot>();
        foreach (Match line in ObjectLine().Matches(text))
        {
            var cls = Level().Replace(line.Groups["cls"].Value, string.Empty);
            if (EntityClass().IsMatch(cls) && !Helper().IsMatch(cls))
            {
                spots.Add(new Spot(int.Parse(line.Groups["x"].Value), int.Parse(line.Groups["y"].Value), cls));
            }
        }

        return spots.OrderBy(spot => spot.Y).ThenBy(spot => spot.X).ThenBy(spot => spot.Class, StringComparer.Ordinal).ToList();
    }

    /// <summary>The classes of every object of a room file's text, entity or not (a boss marker names a room).</summary>
    public static List<string> Classes(string text) => ObjectLine().Matches(text).Select(line => line.Groups["cls"].Value).Distinct().ToList();

    /// <summary>The classes only rooms of one label use, across every room file: one of them alone tells its room.</summary>
    public static HashSet<string> Unique(IEnumerable<(string Label, IReadOnlyList<Spot> Spots)> rooms)
    {
        var labels = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (label, spots) in rooms)
        {
            foreach (var spot in spots)
            {
                (labels.TryGetValue(spot.Class, out var known) ? known : labels[spot.Class] = new HashSet<string>(StringComparer.Ordinal)).Add(label);
            }
        }

        return labels.Where(pair => pair.Value.Count == 1).Select(pair => pair.Key).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether a room's objects can place it: two of them, or one only its label uses.</summary>
    public static bool Placeable(IReadOnlyList<Spot> spots, HashSet<string> unique) => spots.Count >= 2 || spots.Any(spot => unique.Contains(spot.Class));
}
