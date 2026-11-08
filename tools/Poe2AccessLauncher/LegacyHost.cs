using System.Text.Json;

namespace Poe2AccessLauncher;

/// <summary>
///     The GameHelper host that packages before Poe2AccessHost carried: GameHelper.exe, the random-named copies the
///     old launcher started it as (listed in two files), its overlay's libraries and folders. An update by the old
///     launcher copies the new package over them without removing anything; this removes them.
/// </summary>
internal static class LegacyHost
{
    private const string GameHelperExe = "GameHelper.exe";
    private const string GameHelperStoreFile = "tempFileLocations.dat"; // JSON string array, paths relative to the host folder
    private const string LauncherStoreFile = "Poe2Access.copies.txt"; // one file name per line
    private static readonly string[] Kept = ["Poe2AccessHost.exe", "Poe2Access.exe", "Poe2UiExtract.exe"]; // a corrupt list must never delete these

    /// <summary>GameHelper's own files and its overlay's (ImGui, Direct3D, ImageSharp); the host's library is GameReader.dll.</summary>
    private static readonly string[] OverlayFiles =
    [
        "GameHelper.dll", "GameHelper.pdb", "GameHelper.deps.json", "GameHelper.runtimeconfig.json", "imgui.ini",
        "cimgui.dll", "ImGui.NET.dll", "ClickableTransparentOverlay.dll", "SixLabors.ImageSharp.dll",
        "SharpGen.Runtime.dll", "SharpGen.Runtime.COM.dll", "Vortice.D3DCompiler.dll", "Vortice.Direct3D11.dll",
        "Vortice.DirectX.dll", "Vortice.DXGI.dll", "Vortice.Mathematics.dll",
    ];

    /// <summary>GameHelper's settings, fonts and overlay translations; nothing reads them now.</summary>
    public static readonly string[] Folders = ["configs", "fonts", "Localization"];

    public static void Delete(string hostDirectory)
    {
        var files = new List<string> { Path.Join(hostDirectory, GameHelperExe) };
        files.AddRange(OverlayFiles.Select(name => Path.Join(hostDirectory, name)));
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
            if (Kept.Contains(Path.GetFileName(file), StringComparer.OrdinalIgnoreCase) || !File.Exists(file) || !InFolder(file, hostDirectory))
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

        foreach (var folder in Folders.Select(name => Path.Join(hostDirectory, name)).Where(Directory.Exists))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
                Log.Write($"Deleted old host folder {folder}");
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Log.Write($"Could not delete {folder}: {error.Message}");
            }
        }
    }

    /// <summary>Only files directly in the host folder: a corrupt list naming "..\x" must not reach outside it.</summary>
    private static bool InFolder(string file, string folder) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(file)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)), StringComparison.OrdinalIgnoreCase);
}
