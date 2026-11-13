using System;
using System.Collections.Generic;
using System.Linq;
using Index = LibBundle3.Index;

/// <summary>The arena rooms read from the game: the areas' terrain folders by boss, and every room file.</summary>
internal static partial class BossArenaRooms
{
    private const string RoomExtension = ".arm";
    private const string GraphsFolder = "/graphs/";

    /// <summary>
    ///     An area whose bosses come from more monster folders than this is a gauntlet (0.5.5f: the boss rush's areas list five each;
    ///     the Trial of Chaos, with its rotating bosses, four).
    /// </summary>
    private const int MaxAreaFolders = 4;

    /// <summary>Each boss's terrain folders (lower case, the part before "/graphs/" of its areas' topologies), by MonsterVarieties row.</summary>
    public static Dictionary<int, HashSet<string>> Folders(BossCensusColumns.Tables t, BossCensusColumns c)
    {
        var folders = new Dictionary<int, HashSet<string>>();
        for (var row = 0; row < t.Areas.RowCount; row++)
        {
            var dirs = t.Areas.KeyList(row, c.AreaTopologies).Select(key => DatAnchors.IdOf(t.Topologies, key) is null ? null : t.Topologies.StringOrNull((int)key, c.TopologyPath))
                .OfType<string>().Select(path => path.ToLowerInvariant()).Where(path => path.Contains(GraphsFolder, StringComparison.Ordinal))
                .Select(path => path[..path.LastIndexOf(GraphsFolder, StringComparison.Ordinal)] + "/").ToList();
            var bosses = t.Areas.KeyList(row, c.AreaBosses).Where(key => key >= 0 && key < t.Monsters.RowCount).Select(key => (int)key).ToList();
            if (bosses.Select(boss => FolderOf(t.Monsters.String(boss, 0))).Distinct(StringComparer.OrdinalIgnoreCase).Count() > MaxAreaFolders)
            {
                continue; // a gauntlet of other bosses' arenas (the boss rush): its rooms are no one boss's
            }

            foreach (var boss in bosses)
            {
                if (!folders.TryGetValue(boss, out var set))
                {
                    folders[boss] = set = new HashSet<string>(StringComparer.Ordinal);
                }

                set.UnionWith(dirs);
            }
        }

        return folders;
    }

    /// <summary>Every room of the game, read bundle by bundle.</summary>
    public static Rooms Read(Index index)
    {
        var wanted = BundleFiles.ByLowerPath(index).Where(pair => pair.Key.EndsWith(RoomExtension, StringComparison.Ordinal)).ToDictionary(pair => pair.Value, pair => pair.Key);
        var read = new System.Collections.Concurrent.ConcurrentBag<Room>();
        BundleFiles.ReadGrouped(wanted.Keys, (file, bytes) => read.Add(Parse(wanted[file], GameFormats.Decode(bytes, wanted[file]))));
        return Rooms.Of(read);
    }
}
