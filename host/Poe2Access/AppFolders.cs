namespace Poe2Access;

using System;
using System.IO;
using System.Linq;

/// <summary>
///     Where the mod's files are: the folder of the executable, the folder of its data (log, version, GameData) and the
///     plugin folder. A development build keeps Plugins beside the executable; the package keeps everything in "app".
/// </summary>
internal sealed record AppFolders(string Exe, string Data, string Plugins)
{
    public const string AppName = "app";
    public const string PluginsName = "Plugins";

    /// <summary>The mod's executables, past and present (Poe2Access.exe.old is the launcher an update moved aside).</summary>
    private static readonly string[] OwnExecutables = ["Poe2Access", "GameHelper", "Poe2UiExtract"];

    /// <summary>Whether the data lives in the package's "app" folder, apart from the executable's own.</summary>
    public bool Packaged => !string.Equals(this.Data, this.Exe, StringComparison.OrdinalIgnoreCase);

    /// <summary>Plugins beside the executable when that folder exists, else the package's app\Plugins.</summary>
    public static AppFolders Resolve(string exeDirectory, Func<string, bool> directoryExists)
    {
        var exe = Path.TrimEndingDirectorySeparator(exeDirectory);
        var beside = Path.Join(exe, PluginsName);
        if (directoryExists(beside))
        {
            return new AppFolders(exe, exe, beside);
        }

        var app = Path.Join(exe, AppName);
        return new AppFolders(exe, app, Path.Join(app, PluginsName));
    }

    /// <summary>The folder of one plugin.</summary>
    public string Plugin(string name) => Path.Join(this.Plugins, name);

    /// <summary>
    ///     Whether a process image is this install's: anything directly in "app", but in the executable's folder, which the
    ///     player chose (Downloads, the game's own folder), only the mod's executables; never the game.
    /// </summary>
    public bool OwnsImage(string? imagePath)
    {
        var folder = Path.GetDirectoryName(imagePath);
        var name = Path.GetFileName(imagePath);
        if (folder is null || string.IsNullOrEmpty(name)
            || GameInstall.ProcessNames.Contains(Path.GetFileNameWithoutExtension(name), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(folder, Path.Join(this.Exe, AppName), StringComparison.OrdinalIgnoreCase)
            || (string.Equals(folder, this.Exe, StringComparison.OrdinalIgnoreCase)
                && OwnExecutables.Any(own => name.StartsWith(own, StringComparison.OrdinalIgnoreCase)));
    }
}
