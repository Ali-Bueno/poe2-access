using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     A boss's arena rooms (docs/research/boss-mechanics.md §13): WorldAreas.Bosses names the areas a boss is fought in, their
///     Topologies the terrain folders whose rooms (.arm, UTF-16 text) hold the objects placed in the arena. Of those rooms and the
///     rooms placing the fight's objects, the arena scores as the census scored it (§14). Pure but for <c>BossArenaRooms.Files.cs</c>.
/// </summary>
internal static partial class BossArenaRooms
{
    /// <summary>A room's placed objects (entity paths) and named points.</summary>
    internal sealed record Room(string Path, IReadOnlyList<string> Objects, IReadOnlyList<string> Points);

    /// <summary>Rooms in path order, and the rooms placing each object and an object of each monster folder (lower case).</summary>
    internal sealed record Rooms(IReadOnlyList<Room> All, ILookup<string, Room> ByObject, ILookup<string, Room> ByFolder)
    {
        public static Rooms Of(IEnumerable<Room> rooms)
        {
            var all = rooms.OrderBy(room => room.Path, StringComparer.Ordinal).ToList();
            var placed = all.SelectMany(room => room.Objects.Select(path => path.ToLowerInvariant()).Distinct().Select(path => (Path: path, Room: room))).ToList();
            return new Rooms(all, placed.ToLookup(pair => pair.Path, pair => pair.Room, StringComparer.Ordinal),
                placed.Select(pair => (Folder: FolderOf(pair.Path), pair.Room)).Distinct().ToLookup(pair => pair.Folder, pair => pair.Room, StringComparer.Ordinal));
        }
    }

    private const string AoExtension = ".ao";
    private const string MetadataPrefix = "Metadata/";

    // The census's scores (cards.py rooms_for): an object of the boss's folder, an object of the fight, an area's room named for a boss.
    private const int FolderScore = 3;
    private const int FightScore = 2;
    private const int NamedScore = 2;
    private const int MinScore = 2;

    /// <summary>A fight has its main arena and at most a couple of sub-rooms (Jamanra's two carts, the Crowbell's three arenas).</summary>
    private const int MaxRooms = 3;

    private static readonly Regex Number = new(@"^-?[0-9.]+(?:e-?\d+)?$");
    private static readonly Regex PointLine = new(@"^(-?[0-9.]+(?:e-?\d+)?) (-?[0-9.]+(?:e-?\d+)?) (-?[0-9.]+(?:e-?\d+)?) ""([^""]*)""$");
    private static readonly Regex Quoted = new(@"""([^""]*)""");
    private static readonly Regex Scenery = new(@"/(Doodad|Doodad_LeanToPlayer|Arena_Blocker|PatrolPathNode\w*)$|Doodads/");

    /// <summary>A room's object lines (x y … "art.ao" "entity path" …) and named points (x y rotation "name").</summary>
    public static Room Parse(string path, string text)
    {
        var objects = new List<string>();
        var points = new List<string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('﻿');
            if (PointLine.Match(line) is { Success: true } point)
            {
                var name = point.Groups[4].Value;
                (name.StartsWith(MetadataPrefix, StringComparison.Ordinal) ? objects : points).Add(name);
                continue;
            }

            if (line.Contains('"' + MetadataPrefix, StringComparison.Ordinal) && line.Split(' ') is { Length: > 1 } numbers && Number.IsMatch(numbers[0]) && Number.IsMatch(numbers[1]))
            {
                var quoted = Quoted.Matches(line).Select(match => match.Groups[1].Value).ToList();
                objects.Add(quoted.LastOrDefault(text => text.StartsWith(MetadataPrefix, StringComparison.Ordinal) && !text.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase)) ?? quoted.LastOrDefault() ?? string.Empty);
            }
        }

        return new Room(path, objects, points);
    }

    /// <summary>A path's first three parts, the folder of a monster's or object's family ("Metadata/Monsters/PerennialKingStage2").</summary>
    internal static string FolderOf(string path) => string.Join('/', path.Split('/').Take(3));

    /// <summary>
    ///     The fight's rooms, best first: of its areas' rooms (lower-case folders) and the rooms placing its objects or an object of
    ///     its folder, those scoring the census's way (objects of the boss's folder, objects of the fight, an area's room named for a boss).
    /// </summary>
    /// <param name="folder">The boss script's first three path parts ("Metadata/Monsters/PerennialKingStage2").</param>
    /// <param name="fightObjects">The fight's own object paths, lower case.</param>
    public static IReadOnlyList<Room> Of(string folder, ISet<string> fightObjects, IReadOnlyCollection<string> areaFolders, Rooms rooms)
    {
        var own = folder.ToLowerInvariant();
        var candidates = rooms.ByFolder[own].Concat(fightObjects.SelectMany(path => rooms.ByObject[path]))
            .Concat(areaFolders.Count == 0 ? Enumerable.Empty<Room>() : rooms.All.Where(room => InArea(room, areaFolders))).Distinct();
        return candidates.Select(room => (Room: room, Score: Score(room, own + "/", fightObjects, InArea(room, areaFolders)))).Where(scored => scored.Score >= MinScore)
            .OrderByDescending(scored => scored.Score).ThenBy(scored => scored.Room.Path, StringComparer.Ordinal).Take(MaxRooms).Select(scored => scored.Room).ToList();
    }

    /// <summary>The objects of the rooms a fight may care about: its folder's, the game's /Objects/ and quest objects, no scenery and no monster.</summary>
    public static IEnumerable<string> ObjectsOf(IEnumerable<Room> rooms, string folder, Func<string, bool> isMonster) =>
        rooms.SelectMany(room => room.Objects).Where(path => path.Length > 0 && !path.EndsWith(AoExtension, StringComparison.OrdinalIgnoreCase) && !Scenery.IsMatch(path) && !isMonster(path) &&
                                                          (path.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase) || path.Contains("/Objects/", StringComparison.Ordinal) ||
                                                           path.Contains("QuestObjects", StringComparison.Ordinal))).Distinct(StringComparer.OrdinalIgnoreCase);

    private static bool InArea(Room room, IReadOnlyCollection<string> areaFolders) => areaFolders.Any(dir => room.Path.StartsWith(dir, StringComparison.Ordinal));

    private static int Score(Room room, string own, ISet<string> fightObjects, bool inArea)
    {
        var score = 0;
        foreach (var path in room.Objects.Select(path => path.ToLowerInvariant()))
        {
            score += path.StartsWith(own, StringComparison.Ordinal) && !path.EndsWith(AoExtension, StringComparison.Ordinal) ? FolderScore : fightObjects.Contains(path) ? FightScore : 0;
        }

        var file = room.Path[(room.Path.LastIndexOf('/') + 1)..];
        var named = file.Contains("boss", StringComparison.Ordinal) || room.Points.Any(point => point.StartsWith("mapboss", StringComparison.Ordinal) || point == "boss");
        return score + (inArea && named ? NamedScore : 0);
    }
}
