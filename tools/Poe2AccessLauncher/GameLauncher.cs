using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>Starts Path of Exile 2 through Steam (or asks for the Korean client to be started) and waits for its main window.</summary>
internal static class GameLauncher
{
    private const string SteamUrl = "steam://rungameid/2694490"; // Path of Exile 2 Steam app id
    private const string KakaoKey = @"HKEY_CURRENT_USER\Software\Kakaogames\POE2"; // written by the Korean client's installer
    private static readonly string[] ProcessNames = GameInstall.ProcessNames;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(240); // first start compiles shaders
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);

    public static void EnsureRunning()
    {
        if (FindWindow())
        {
            Log.Write("Game already running");
            return;
        }

        if (KakaoDirectory() is { } kakao)
        {
            // The Korean client only starts from the Kakao Games website, which hands it the login token.
            Log.Write($"Korean client installed in {kakao}: waiting for it to be started from the Kakao Games website");
            Program.MessageBox(IntPtr.Zero, "Start Path of Exile 2 from the Kakao Games website, then press OK.", "Poe2Access", Program.MessageBoxInformation);
        }
        else
        {
            Log.Write($"Opening {SteamUrl}");
            Process.Start(new ProcessStartInfo(SteamUrl) { UseShellExecute = true })?.Dispose();
        }

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

    /// <summary>The Korean client's install folder, or null when it is not installed.</summary>
    private static string? KakaoDirectory() =>
        Microsoft.Win32.Registry.GetValue(KakaoKey, "InstallPath", null) is string path && Directory.Exists(path) ? path : null;

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
