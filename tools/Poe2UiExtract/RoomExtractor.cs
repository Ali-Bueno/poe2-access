using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Poe2Accessibility.Services;
using Index = LibBundle3.Index;

/// <summary>
///     `rooms` command: the named rooms of the game's room files (.arm) as patterns of tile classes and of the entity objects
///     they place, and the class of every tile (.tdt) those patterns can meet, for the plugin's RoomMatcher and
///     RoomObjectMatcher (docs/research/rooms.md). Read-only on the game.
/// </summary>
internal static class RoomExtractor
{
    /// <summary>A room with this many comparable cells is told apart by its shape alone (smaller ones matched by chance in the Manor).</summary>
    private const int ShapeCells = 40;

    /// <summary>A room with a distinctive tile still needs this many comparable cells.</summary>
    private const int MinCells = 12;

    /// <summary>A tag or big tile is distinctive when rooms of at most this many labels in its folder ask for it (the Manor's thin-wall door: 2).</summary>
    private const int DistinctiveLabels = 3;

    /// <summary>Variants of variants followed this deep (a loop in broken files ends).</summary>
    private const int MaxVariantDepth = 4;

    private const string TerrainPrefix = "metadata/terrain/";

    /// <summary>The world map's region rooms: their objects are map pins, not a place to walk.</summary>
    private const string WorldMaps = TerrainPrefix + "worldmaps/";

    public static int Run(Index index, string outFile)
    {
        var clock = Stopwatch.StartNew();
        var files = BundleFiles.ByLowerPath(index).Where(pair => pair.Key.StartsWith(TerrainPrefix, StringComparison.Ordinal)).ToDictionary(pair => pair.Key, pair => pair.Value);
        var arms = files.Where(pair => pair.Key.EndsWith(".arm", StringComparison.Ordinal)).ToDictionary(pair => pair.Value, pair => pair.Key);
        var parsed = new ConcurrentDictionary<string, RoomFiles.Room>(StringComparer.Ordinal);
        var objects = new ConcurrentDictionary<string, List<RoomObjects.Spot>>(StringComparer.Ordinal);
        var names = new ConcurrentDictionary<string, string?>(StringComparer.Ordinal);
        var failed = 0;
        BundleFiles.ReadGrouped(arms.Keys, (file, bytes) =>
        {
            try
            {
                var text = PlaceExtractor.Text(bytes, arms[file]);
                parsed[arms[file]] = RoomFiles.Parse(text);
                objects[arms[file]] = RoomObjects.Read(text);
                names[arms[file]] = RoomNaming.Key(arms[file], parsed[arms[file]].Label, PlaceExtractor.PropKindsOf(text), RoomObjects.Classes(text));
            }
            catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or ArgumentException or OverflowException)
            {
                Interlocked.Increment(ref failed);
            }
        });

        var cells = parsed.ToDictionary(pair => pair.Key, pair => RoomFiles.Cells(pair.Value), StringComparer.Ordinal);
        // Variants of one room share a label (the Manor's three studies); a room without one counts alone.
        var askedBy = new Dictionary<(string Set, string Class), HashSet<string>>();
        foreach (var (path, room) in cells)
        {
            foreach (var cls in room.Values.Distinct().Where(IsHard))
            {
                var labels = askedBy.TryGetValue((Set(path), cls), out var known) ? known : askedBy[(Set(path), cls)] = new HashSet<string>(StringComparer.Ordinal);
                labels.Add(parsed[path].Label.Length > 0 ? parsed[path].Label : path);
            }
        }

        // A spoken name's own object classes, among every room file's (a room that stays silent and has no label counts alone).
        var unique = RoomObjects.Unique(parsed.Select(pair => (names[pair.Key] ?? (RoomLabels.Key(pair.Value.Label) is { Length: > 0 } key ? key : pair.Key), (IReadOnlyList<RoomObjects.Spot>)objects[pair.Key])));
        var kept = new List<(string Path, string Label, RoomFiles.Room Room, Dictionary<(int X, int Y), string> Cells, List<string> Families, List<RoomObjects.Spot> Objects)>();
        int silent = 0, silentByObjects = 0;
        foreach (var (path, room) in parsed.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var label = names[path];
            var pattern = cells[path];
            var distinctive = pattern.Values.Distinct().Any(cls => IsHard(cls) && askedBy[(Set(path), cls)].Count <= DistinctiveLabels);
            var byTiles = pattern.Count >= ShapeCells || (distinctive && pattern.Count >= MinCells);
            var byObjects = RoomObjects.Placeable(objects[path], unique) && !path.StartsWith(WorldMaps, StringComparison.Ordinal);
            if (label == null && (byTiles || byObjects))
            {
                silent++;
                silentByObjects += byObjects ? 1 : 0;
            }

            if (label != null && (byTiles || byObjects))
            {
                var families = room.Strings.Select(RoomFiles.Family).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList();
                kept.Add((path, label, room, byTiles ? pattern : new Dictionary<(int X, int Y), string>(), families, byObjects ? objects[path] : new List<RoomObjects.Spot>()));
            }
        }

        var used = kept.SelectMany(room => room.Cells.Values).Concat(RoomFiles.SoftAccepts).Distinct().Order(StringComparer.Ordinal).ToList();
        var classIds = used.Select((cls, id) => (cls, id)).ToDictionary(pair => pair.cls, pair => pair.id, StringComparer.Ordinal);
        var familyList = kept.SelectMany(room => room.Families).Distinct().Order(StringComparer.Ordinal).ToList();
        var familyIds = familyList.Select((family, id) => (family, id)).ToDictionary(pair => pair.family, pair => pair.id, StringComparer.Ordinal);
        var keyList = kept.SelectMany(room => room.Objects).Select(spot => spot.Class).Distinct().Order(StringComparer.Ordinal).ToList();
        var keyIds = keyList.Select((key, id) => (key, id)).ToDictionary(pair => pair.key, pair => pair.id, StringComparer.Ordinal);

        // Only the tiles of the families of rooms told by their tiles can be part of one; a tile of any other class is no cell's.
        var tileFamilies = kept.Where(room => room.Cells.Count > 0).SelectMany(room => room.Families).ToHashSet(StringComparer.Ordinal);
        var tdts = files.Keys.Where(path => path.EndsWith(".tdt", StringComparison.Ordinal) && RoomFiles.Family(path) is { } family && tileFamilies.Contains(family)).ToList();
        var read = ReadTiles(files, tdts);
        var tiles = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var unread = 0;
        foreach (var path in tdts)
        {
            if (Resolve(read, path, 0) is not { } key)
            {
                unread++;
            }
            else if (RoomFiles.Class(key) is { } cls && classIds.TryGetValue(cls, out var id))
            {
                tiles[path[TerrainPrefix.Length..]] = id;
            }
        }

        var catalog = new
        {
            classes = used,
            soft = used.Where(cls => RoomFiles.SoftClass().IsMatch(cls)).Select(cls => classIds[cls]).ToList(),
            softAccepts = RoomFiles.SoftAccepts.Select(cls => classIds[cls]).ToList(),
            families = familyList,
            objectKeys = keyList,
            uniqueKeys = keyList.Where(unique.Contains).Select(key => keyIds[key]).ToList(),
            tiles = tiles.GroupBy(tile => tile.Key[..tile.Key.LastIndexOf('/')], StringComparer.Ordinal)
                .ToDictionary(folder => folder.Key, folder => folder.ToDictionary(tile => tile.Key[(folder.Key.Length + 1)..], tile => tile.Value)),
            rooms = kept.Select(room => new
            {
                label = room.Label,
                set = Set(room.Path)[Math.Min(TerrainPrefix.Length, Set(room.Path).Length)..],
                w = room.Room.Width,
                h = room.Room.Height,
                families = room.Families.Select(family => familyIds[family]).ToList(),
                runs = Runs(room.Cells, classIds),
                objects = room.Objects.SelectMany(spot => new[] { spot.X, spot.Y, keyIds[spot.Class] }).ToList(),
            }).ToList(),
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        File.WriteAllText(outFile, JsonSerializer.Serialize(catalog));
        Say.Out.WriteLine($"Wrote {outFile}: {kept.Count} rooms of {parsed.Count} room files ({failed} unreadable; {kept.Count(room => !RoomLabels.Spoken.Contains(room.Label))} named by file name or props, {silent} more silent, {silentByObjects} of them with entity objects), {kept.Count(room => room.Cells.Count > 0)} by tiles, " +
                          $"{kept.Count(room => room.Objects.Count > 0)} by {kept.Sum(room => room.Objects.Count)} objects of {keyList.Count} classes, {kept.Select(room => room.Label).Distinct().Count()} labels " +
                          $"({string.Join(", ", kept.GroupBy(room => room.Label).OrderByDescending(group => group.Count()).Take(12).Select(group => $"{group.Key} {group.Count()}"))}); " +
                          $"{tiles.Count} tiles of {tdts.Count} in {familyList.Count} families ({unread} unreadable), {used.Count} classes; {new FileInfo(outFile).Length / 1024} KB in {clock.Elapsed.TotalSeconds:0.0} s");
        return kept.Count == 0 ? 1 : 0;
    }

    /// <summary>The tiles read, and the parents their variants name (a parent may lie in another family), by lower-case path.</summary>
    private static Dictionary<string, RoomFiles.TileFile?> ReadTiles(Dictionary<string, LibBundle3.Records.FileRecord> files, IEnumerable<string> paths)
    {
        var read = new ConcurrentDictionary<string, RoomFiles.TileFile?>(StringComparer.Ordinal);
        var wanted = paths.ToList();
        while (wanted.Count > 0)
        {
            var records = wanted.Where(files.ContainsKey).ToDictionary(path => files[path], path => path);
            BundleFiles.ReadGrouped(records.Keys, (file, bytes) => read[records[file]] = RoomFiles.Read(bytes));
            wanted = read.Values.Select(tile => tile?.Parent).OfType<string>().Distinct().Where(parent => !read.ContainsKey(parent) && files.ContainsKey(parent)).ToList();
        }

        return new Dictionary<string, RoomFiles.TileFile?>(read, StringComparer.Ordinal);
    }

    /// <summary>A tile's key with what its variant leaves out taken from its parent; null when neither reads.</summary>
    private static RoomFiles.TileKey? Resolve(Dictionary<string, RoomFiles.TileFile?> read, string path, int depth)
    {
        if (!read.TryGetValue(path, out var tile) || tile == null)
        {
            return null;
        }

        if (tile.Key is { Tag: not null } own)
        {
            return own;
        }

        var parent = tile.Parent != null && depth < MaxVariantDepth ? Resolve(read, tile.Parent, depth + 1) : null;
        return tile.Key == null ? parent : tile.Key with { Tag = parent?.Tag ?? string.Empty };
    }

    /// <summary>A room's cells as runs along its rows, four numbers each: row, first column, length, class.</summary>
    private static List<int> Runs(Dictionary<(int X, int Y), string> cells, Dictionary<string, int> classIds)
    {
        var runs = new List<int>();
        foreach (var ((x, y), cls) in cells.OrderBy(cell => cell.Key.Y).ThenBy(cell => cell.Key.X))
        {
            var id = classIds[cls];
            var last = runs.Count - 4;
            if (last >= 0 && runs[last] == y && runs[last + 1] + runs[last + 2] == x && runs[last + 3] == id)
            {
                runs[last + 2]++;
            }
            else
            {
                runs.AddRange(new[] { y, x, 1, id });
            }
        }

        return runs;
    }

    /// <summary>A tag that is not decorative, or a big tile: what a room is told apart by.</summary>
    private static bool IsHard(string cls) =>
        !RoomFiles.SoftClass().IsMatch(cls) && cls is not (RoomFiles.Wall or RoomFiles.Floor or RoomFiles.Excavation or RoomFiles.Stairs);

    /// <summary>A room file's folder of rooms: its path before "/rooms/", else its folder.</summary>
    private static string Set(string path)
    {
        var rooms = path.IndexOf("/rooms/", StringComparison.Ordinal);
        return rooms > 0 ? path[..rooms] : path[..path.LastIndexOf('/')];
    }
}
