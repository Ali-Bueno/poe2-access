using System.Text.Json;

namespace Poe2AccessLauncher;

/// <summary>
///     The GameHelper host that packages before Poe2AccessHost carried: GameHelper.exe and the random-named copies the
///     old launcher started it as, listed in two files. An update copies the new package over them; this removes them.
/// </summary>
internal static class LegacyHost
{
    private const string GameHelperExe = "GameHelper.exe";
    private const string GameHelperStoreFile = "tempFileLocations.dat"; // JSON string array, paths relative to the host folder
    private const string LauncherStoreFile = "Poe2Access.copies.txt"; // one file name per line
    private static readonly string[] Kept = ["Poe2AccessHost.exe", "Poe2Access.exe", "Poe2UiExtract.exe"]; // a corrupt list must never delete these

    public static void Delete(string hostDirectory)
    {
        var files = new List<string> { Path.Join(hostDirectory, GameHelperExe) };
        var launcherStore = Path.Join(hostDirectory, LauncherStoreFile);
        var gameHelperStore = Path.Join(hostDirectory, GameHelperStoreFile);
        try
        {
            if (File.Exists(launcherStore))
            {
                files.AddRange(File.ReadAllLines(launcherStore).Where(line => line.Length > 0).Select(line => Path.Join(hostDirectory, line)));
            }

            if (File.Exists(gameHelperStore))
            {
                using var names = JsonDocument.Parse(File.ReadAllText(gameHelperStore));
                files.AddRange(names.RootElement.EnumerateArray().Select(name => name.GetString()).OfType<string>().Select(name => Path.Join(hostDirectory, name)));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Log.Write($"Old host copies unreadable: {error.Message}");
        }

        files.Add(launcherStore);
        files.Add(gameHelperStore);
        foreach (var file in files.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Kept.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase) || !File.Exists(file))
            {
                continue;
            }

            try
            {
                File.Delete(file);
                Log.Write($"Deleted old host file {file}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log.Write($"Could not delete {file}: {error.Message}");
            }
        }
    }
}
