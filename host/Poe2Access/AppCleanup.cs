namespace Poe2Access;

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Which of the files at the top of the package's "app" folder the current layout never ships.</summary>
internal static class AppCleanup
{
    private static readonly string[] KeptSuffixes = [".log", ".log.old"];
    private static readonly string[] KeptFiles = ["version.txt", "oo2core.dll"];

    /// <summary>What earlier versions shipped in the plugin's own folder: the Prismatoid binding, replaced by PrismLibrary.</summary>
    public static readonly string[] UnshippedPluginFiles = ["Prismatoid.dll"];

    /// <summary>The names to delete: everything but logs, the version, the Oodle library (Plugins and GameData are folders).</summary>
    public static IReadOnlyList<string> Unshipped(IEnumerable<string> fileNames) =>
        fileNames.Where(name => !Keeps(name)).ToList();

    private static bool Keeps(string name) =>
        KeptFiles.Contains(name, StringComparer.OrdinalIgnoreCase)
        || KeptSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
