namespace Poe2Access;

using Poe2Accessibility.Services;

/// <summary>
///     "Poe2Access.exe check": everything a start needs from the installed package, tried without the game or Steam and
///     without a word spoken. The release script runs it on the package before publishing, so a package that installs and
///     does not start never reaches a player.
/// </summary>
internal static class PackageStartCheck
{
    /// <summary>The faults found, one line each; empty when the package would start.</summary>
    public static List<string> Run(AppFolders folders)
    {
        var faults = new List<string>();
        var shipped = PackageInstall.ShippedFiles(folders.Exe);
        if (shipped.Count == 0)
        {
            Log.Write($"No {PackageInstall.FileList}: a development build, the files are not checked against a list");
        }

        faults.AddRange(PackageCheck.FileFaults(folders.Exe, shipped));
        var plugin = folders.Plugin(HostRun.PluginName);
        if (!PluginHost.TryLoad(plugin, HostRun.PluginName, out _, out var loadFault))
        {
            faults.Add(loadFault!);
        }

        if (PrismFault(plugin) is { } prism)
        {
            faults.Add(prism);
        }

        faults.AddRange(PackageCheck.CatalogFaults(plugin));
        return faults;
    }

    /// <summary>Loads the plugin folder's prism.dll and starts and releases it, as the plugin does, sending no speech.</summary>
    private static string? PrismFault(string pluginFolder)
    {
        var path = Path.Join(pluginFolder, PrismLibrary.FileName);
        if (!File.Exists(path))
        {
            return $"Missing {PrismLibrary.FileName} in {pluginFolder}";
        }

        try
        {
            using var prism = PrismLibrary.Load(path);
            return prism.Start(message => Log.Write(message)) is { } failure ? $"PRISM {prism.Version} does not start: {failure}" : null;
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            return $"PRISM does not load: {error.Message}";
        }
    }
}
