using Poe2Accessibility.Services;
using Index = LibBundle3.Index;

/// <summary>
///     `game-names` command: each name the plugin's rules match by its text (<see cref="GameNames"/>: buff and animation
///     Ids, the Trials' trap and door scripts and their states, the boss tails) looked up in the game's files. Writes the
///     names found, a name a line; a name gone fails the step, its last line naming each one and the rule it silences, so
///     the launcher lists the step as failed and logs that line (docs/patch-day.md).
/// </summary>
internal static class GameNameExtractor
{
    private const string ScriptExtension = ".ot";

    /// <summary>The list of the UI images: a line each, the image's name quoted first, then its sheet and place.</summary>
    private const string UiImageList = "art/uiimages1.txt";

    public static int Run(Index index, string outFile)
    {
        var ids = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var name in new[] { "buffdefinitions", "animation", "monstervarieties", "minimapicons", "rulesets" })
        {
            // Each holds its Id as a string at the row's start, as game-keys reads them.
            if (DatTable.Open(index, name) is not { } table || !DatColumnSearch.IsText(table, 0, _ => true))
            {
                Say.Out.WriteLine($"Table {name} missing or not starting with an Id: game names left out.");
                return 1;
            }

            ids[name] = Enumerable.Range(0, table.RowCount).Select(row => table.String(row, 0)).Where(id => id.Length > 0).ToHashSet(StringComparer.Ordinal);
        }

        var scripts = BundleFiles.ByLowerPath(index);
        string? Script(string path) => scripts.TryGetValue((path + ScriptExtension).ToLowerInvariant(), out var file) ? file.ReadText() : null;
        if (!scripts.TryGetValue(UiImageList, out var imageList))
        {
            Say.Out.WriteLine($"{UiImageList} missing: game names left out.");
            return 1;
        }

        var files = new GameNameCheck.Files(ids["buffdefinitions"], ids["animation"], ids["monstervarieties"], Script, ids["minimapicons"], GameNameCheck.UiArtNames(imageList.ReadText()), ids["rulesets"]);
        var names = GameNames.All().ToList();
        // A monster is matched by its variety's Id, which need not be a file's path: both are where a path may start.
        var paths = scripts.Keys.Concat(ids["monstervarieties"].Select(id => id.ToLowerInvariant()));
        var gone = GameNameCheck.Gone(names, files).Concat(GonePaths(paths, outFile)).ToList();
        if (gone.Count > 0)
        {
            Say.Out.WriteLine(GameNameCheck.Line(gone));
            return 1;
        }

        File.WriteAllLines(outFile, new[] { "# The names the plugin's rules match, each found in the game's files (tools/Poe2UiExtract game-names)." }
            .Concat(names.Select(name => $"{name.Kind}\t{name.Value}\t{name.State}\t{name.Rule}")));
        Say.Out.WriteLine($"{names.Count} names found in the game's files, written to {outFile}");
        return 0;
    }

    /// <summary>
    ///     The paths the plugin's source matches (the list shipped beside the output, <see cref="PathNames"/>) that start
    ///     none of <paramref name="lowerPaths"/> (the game's files and its monsters' Ids), as names gone; none when the list
    ///     is not there.
    /// </summary>
    private static IEnumerable<GameName> GonePaths(IEnumerable<string> lowerPaths, string outFile)
    {
        var list = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outFile)) ?? string.Empty, PathNames.FileName);
        if (!File.Exists(list))
        {
            Say.Out.WriteLine($"{PathNames.FileName} is not beside the output: the paths the plugin matches are not checked.");
            return Array.Empty<GameName>();
        }

        var sorted = lowerPaths.ToArray();
        Array.Sort(sorted, StringComparer.Ordinal);
        var entries = PathNames.Parse(File.ReadLines(list));
        var gone = PathNames.Gone(sorted, entries);
        Say.Out.WriteLine($"{entries.Count - gone.Count} of {entries.Count} paths the plugin matches start a file or a monster of the game");
        return gone.Select(path => new GameName(GameNameKind.Path, path.Path, null, path.Where));
    }
}
