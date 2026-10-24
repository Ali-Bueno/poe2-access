using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>Starts Path of Exile 2 through Steam and waits for its main window.</summary>
internal static class GameLauncher
{
    private const string SteamUrl = "steam://rungameid/2694490"; // Path of Exile 2 Steam app id
    private static readonly string[] ProcessNames = ["PathOfExileSteam", "PathOfExile"];
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(240); // first start compiles shaders
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);

    public static void EnsureRunning()
    {
        if (FindWindow())
        {
            Log.Write("Game already running");
            return;
        }

        Log.Write($"Opening {SteamUrl}");
        Process.Start(new ProcessStartInfo(SteamUrl) { UseShellExecute = true })?.Dispose();

        var deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (FindWindow())
            {
                Log.Write("Game window is up");
                return;
            }

            Thread.Sleep(Poll);
        }

        throw new TimeoutException($"The game window did not appear within {Timeout.TotalSeconds:0} seconds.");
    }

    /// <summary>The folder of the running game (its fmod.dll and FMOD banks), or null when it cannot be read.</summary>
    public static string? InstallDirectory()
    {
        foreach (var process in ProcessNames.SelectMany(Process.GetProcessesByName))
        {
            using (process)
            {
                try
                {
                    if (Path.GetDirectoryName(process.MainModule?.FileName) is { Length: > 0 } directory)
                    {
                        return directory;
                    }
                }
                catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
                {
                    Log.Write($"Game folder unreadable: {error.Message}");
                }
            }
        }

        return null;
    }

    private static bool FindWindow()
    {
        foreach (var process in ProcessNames.SelectMany(Process.GetProcessesByName))
        {
            using (process)
            {
                if (process.MainWindowHandle != IntPtr.Zero)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
