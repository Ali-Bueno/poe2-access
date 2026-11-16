using System.Text;
using System.Text.RegularExpressions;

/// <summary>
///     The room (.arm) and tile (.tdt) files read for the `rooms` command, and the class each room cell or placed tile has:
///     a tag, a wall, a floor, excavation, stairs or an untagged big tile by its key (docs/research/rooms.md).
/// </summary>
internal static partial class RoomFiles
{
    public const string Wall = "wall";
    public const string Floor = "floor";
    public const string Excavation = "excavation";
    public const string Stairs = "stairs";
    private const string TagPrefix = "tag:";

    /// <summary>Decorative tags the generator may leave out: a cell asking for one also takes a wall, a floor or a closed door.</summary>
    [GeneratedRegex("^tag:(?:doorclosed|fireplace|window\\w*|crest)$")]
    public static partial Regex SoftClass();

    /// <summary>What a soft cell also takes.</summary>
    public static readonly string[] SoftAccepts = { Wall, Floor, TagPrefix + "doorclosed" };

    /// <summary>One room slot (`k`): the slot's 24 numbers at its top-left cell.</summary>
    public sealed record Slot(int X, int Y, int[] Numbers);

    /// <summary>A parsed room file: its label, size in sub-tiles, string table and slots.</summary>
    public sealed record Room(string Label, int Width, int Height, string[] Strings, List<Slot> Slots);

    /// <summary>A tile's key: what the generator matches a slot against; a null tag is the parent's.</summary>
    public sealed record TileKey(string? Tag, string[] Edges, int Width, int Height, string[] Grounds);

    [GeneratedRegex("^[knsfo]( |$)")]
    private static partial Regex GridLine();

    /// <summary>
    ///     A room file: `version`, the string count and strings, four header lines (the third its label), the room key
    ///     `k W H …`, then H grid rows: `k` + 24 numbers a slot, `n` a cell a bigger slot covers, `s` free, `o`, `f i` forced.
    /// </summary>
    public static Room Parse(string text)
    {
        var lines = text.TrimStart('﻿').Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
        if (!lines[0].StartsWith("version", StringComparison.Ordinal))
        {
            throw new FormatException("no version line");
        }

        var count = int.Parse(lines[1].Trim());
        var strings = lines[2..(2 + count)].Select(line => line.Trim().Trim('"')).ToArray();
        var at = 2 + count;
        var label = lines[at + 2].Trim().Trim('"').Trim();
        at += 4;
        var key = lines[at].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var (width, height) = (int.Parse(key[1]), int.Parse(key[2]));
        at++;
        while (!GridLine().IsMatch(lines[at]))
        {
            at++;
        }

        var slots = new List<Slot>();
        for (var y = 0; y < height; y++)
        {
            var tokens = lines[at + y].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var x = 0;
            for (var j = 0; j < tokens.Length; x++)
            {
                switch (tokens[j])
                {
                    case "k":
                        slots.Add(new Slot(x, y, tokens[(j + 1)..(j + 25)].Select(int.Parse).ToArray()));
                        j += 25;
                        break;
                    case "f":
                        j += 2;
                        break;
                    case "n" or "s" or "o":
                        j++;
                        break;
                    default:
                        throw new FormatException($"grid token {tokens[j]}");
                }
            }
        }

        return new Room(label, width, height, strings, slots);
    }

    /// <summary>A .tdt read: its key (a null tag is its parent's), and the parent .tdt a variant names (lower case), if any.</summary>
    public sealed record TileFile(TileKey? Key, string? Parent);

    /// <summary>How far past its parent's offset (bytes) a variant's key may start: its flags and the fields before it.</summary>
    private const int VariantScanBytes = 40;

    /// <summary>
    ///     A .tdt after its UTF-16 string table (offsets in characters). A plain tile: two offsets (none, its model), the tag,
    ///     four edges, width and height as bytes and four grounds. A variant ("elegant", "chest") names its parent first, then
    ///     flags and only the fields it changes: its key is where four edges, a size and four grounds read as such, its tag
    ///     just before; without one it is its parent's. Null when nothing reads.
    /// </summary>
    public static TileFile? Read(byte[] data)
    {
        if (data.Length < 8)
        {
            return null;
        }

        var chars = BitConverter.ToInt32(data, 4);
        var start = 8 + (2 * (long)chars);
        if (chars < 0 || start + 4 > data.Length)
        {
            return null;
        }

        var table = Encoding.Unicode.GetString(data, 8, 2 * chars);
        string? String(int offset)
        {
            if (offset < 0 || offset >= table.Length)
            {
                return null;
            }

            var end = table.IndexOf('\0', offset);
            return end < 0 ? table[offset..] : table[offset..end];
        }

        bool Is(int offset, string extension) => offset == -1 || (String(offset)?.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ?? false);

        TileKey? KeyAt(long tagAt, bool variant)
        {
            if (tagAt + 38 > data.Length)
            {
                return null;
            }

            var at = (int)tagAt;
            var edges = Enumerable.Range(0, 4).Select(i => BitConverter.ToInt32(data, at + 4 + (4 * i))).ToArray();
            var grounds = Enumerable.Range(0, 4).Select(i => BitConverter.ToInt32(data, at + 22 + (4 * i))).ToArray();
            var (width, height) = (data[at + 20], data[at + 21]);
            if (width == 0 || height == 0 || !edges.All(edge => Is(edge, ".et")) || !grounds.All(ground => Is(ground, ".gt")) || edges.Concat(grounds).All(offset => offset == -1) && variant)
            {
                return null;
            }

            var tagOffset = BitConverter.ToInt32(data, at);
            var tag = tagOffset == -1 ? string.Empty
                : String(tagOffset) is { } text && !text.Contains('/') && !text.Contains('.') ? text
                : variant ? null : string.Empty;
            return new TileKey(tag, edges.Select(edge => String(edge) ?? string.Empty).ToArray(), width, height, grounds.Select(ground => String(ground) ?? string.Empty).ToArray());
        }

        var parent = String(BitConverter.ToInt32(data, (int)start)) is { } named && named.EndsWith(".tdt", StringComparison.OrdinalIgnoreCase) ? named.ToLowerInvariant() : null;
        if (parent == null && KeyAt(start + 8, false) is { } plain)
        {
            return new TileFile(plain, null);
        }

        for (var tagAt = start + 5; tagAt <= start + VariantScanBytes; tagAt++)
        {
            if (KeyAt(tagAt, true) is { } found)
            {
                return new TileFile(found, parent);
            }
        }

        return parent != null ? new TileFile(null, parent) : null;
    }

    /// <summary>The class of each comparable cell of a room (room cells from its top-left); black and unknown cells are left out.</summary>
    public static Dictionary<(int X, int Y), string> Cells(Room room)
    {
        var cells = new Dictionary<(int X, int Y), string>();
        foreach (var slot in room.Slots)
        {
            var t = slot.Numbers;
            string At(int index) => index >= 1 && index <= room.Strings.Length ? room.Strings[index - 1] : string.Empty;
            var tag = At(t[22]);
            var edges = t[2..6].Select(At).Where(text => text.Length > 0).ToList();
            var grounds = t[14..18].Select(At).Where(text => text.Length > 0).ToList();
            var cls = tag.Length > 0 ? TagPrefix + tag.ToLowerInvariant()
                : t[0] * t[1] > 1 ? Big(t[0], t[1], edges, grounds)
                : edges.Any(IsWildcard) ? null
                : Plain(edges, grounds, placed: false);
            if (cls == null)
            {
                continue;
            }

            for (var dy = 0; dy < Math.Max(1, t[1]); dy++)
            {
                for (var dx = 0; dx < Math.Max(1, t[0]); dx++)
                {
                    cells[(slot.X + dx, slot.Y + dy)] = cls;
                }
            }
        }

        return cells;
    }

    /// <summary>The class of a placed tile by its key; null for black ground and keys that say nothing.</summary>
    public static string? Class(TileKey key)
    {
        var edges = key.Edges.Where(text => text.Length > 0).ToList();
        var grounds = key.Grounds.Where(text => text.Length > 0).ToList();
        return key.Tag is { Length: > 0 } tag ? TagPrefix + tag.ToLowerInvariant()
            : key.Width * key.Height > 1 ? Big(key.Width, key.Height, edges, grounds)
            : Plain(edges, grounds, placed: true);
    }

    /// <summary>The tile family a path names: its first two folders under metadata/terrain ("woods/manor"); null for fewer.</summary>
    public static string? Family(string path)
    {
        var parts = path.ToLowerInvariant().Split('/');
        return parts.Length >= 5 && parts[0] == "metadata" && parts[1] == "terrain" ? $"{parts[2]}/{parts[3]}" : null;
    }

    /// <summary>
    ///     Walls, floors, excavation and stairs by edge and ground types. Placed tiles test stairs before wildcards (a ledge
    ///     piece has both) and read a tile with no edges and no black ground as floor (the blank fill tiles have no ground).
    /// </summary>
    private static string? Plain(List<string> edges, List<string> grounds, bool placed)
    {
        var lower = edges.Select(text => text.ToLowerInvariant()).ToList();
        var ground = grounds.Select(text => text.ToLowerInvariant()).ToList();
        if (lower.Concat(ground).Any(text => text.Contains("excavation", StringComparison.Ordinal)))
        {
            return Excavation;
        }

        if (lower.Any(text => text.Contains("stairs", StringComparison.Ordinal) || text.Contains("ledge", StringComparison.Ordinal)))
        {
            return Stairs;
        }

        if (lower.Any(IsWildcard))
        {
            return null;
        }

        if (lower.Any(text => !text.EndsWith("rooms.et", StringComparison.Ordinal)))
        {
            return Wall;
        }

        if (placed && lower.Count == 0 && !ground.Any(text => text.Contains("black_inside_wall", StringComparison.Ordinal)))
        {
            return Floor;
        }

        return ground.Any(text => text.Contains("floor", StringComparison.Ordinal)) ? Floor : null;
    }

    /// <summary>An untagged tile over several sub-tiles, named by its size and edge and ground types, turned or not.</summary>
    private static string Big(int width, int height, IEnumerable<string> edges, IEnumerable<string> grounds)
    {
        static string Names(IEnumerable<string> paths) => string.Join(",", paths.Select(path => path.ToLowerInvariant().Split('/')[^1]).Order(StringComparer.Ordinal));
        return $"big:{Math.Min(width, height)}x{Math.Max(width, height)}:{Names(edges)}:{Names(grounds)}";
    }

    private static bool IsWildcard(string edge) => edge.Contains("wildcard.et", StringComparison.OrdinalIgnoreCase);
}
