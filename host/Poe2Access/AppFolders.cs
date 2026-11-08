namespace Poe2Access;

using System;
using System.IO;

/// <summary>
///     Where the mod's files are: the folder of the executable, the folder of its data (log, version, GameData) and the
///     plugin folder. A development build keeps Plugins beside the executable; the package keeps everything in "app".
/// </summary>
internal sealed record AppFolders(string Exe, string Data, string Plugins)
{
    public const string AppName = "app";
    public const string PluginsName = "Plugins";

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

    /// <summary>Whether a process image lies directly in the executable's folder or in "app": this install's.</summary>
    public bool OwnsImage(string? imagePath)
    {
        var folder = Path.GetDirectoryName(imagePath);
        return folder is not null
            && (string.Equals(folder, this.Exe, StringComparison.OrdinalIgnoreCase) || string.Equals(folder, this.Data, StringComparison.OrdinalIgnoreCase)
                || string.Equals(folder, Path.Join(this.Exe, AppName), StringComparison.OrdinalIgnoreCase));
    }
}
