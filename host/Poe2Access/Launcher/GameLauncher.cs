using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2Access;

/// <summary>Starts Path of Exile 2 through Steam (or asks for the Korean client to be started) and waits for its main window.</summary>
internal static class GameLauncher
{
    private const string SteamUrl = "steam://rungameid/" + GameInstall.SteamAppId;
    private const string SteamKey = @"HKEY_CURRENT_USER\Software\Valve\Steam"; // written by Steam
    private const string KakaoKey = @"HKEY_CURRENT_USER\Software\Kakaogames\POE2"; // written by the Korean client's installer
    private static readonly string[] ProcessNames = GameInstall.ProcessNames;
    // The wait goes on while Steam or the game runs; with neither it gives up after this long (a first start compiles shaders).
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(240);
    private static readonly TimeSpan FirstReminder = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReminderEvery = TimeSpan.FromSeconds(60);
    private static readonly string[] SteamProcessNames = ["steam"];
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(500);

    /// <param name="steamCommand">The game's command when Steam started this program (%command%), else null: Steam is asked for the game.</param>
    /// <param name="stop">Set when the mod is asked to stop while it waits.</param>
    /// <param name="voice">Says that the wait goes on; null says nothing.</param>
    /// <returns>True once the game window is up; false when the wait ended because of <paramref name="stop"/>.</returns>
    public static bool EnsureRunning(string[]? steamCommand, WaitHandle stop, LauncherVoice? voice = null)
    {
        if (FindWindow())
        {
            Log.Write("Game already running");
            return true;
        }

        var kakao = KakaoDirectory();
        if (steamCommand is not null)
        {
            Start(steamCommand);
        }
        else if (GameInstall.StartsKakaoClient(GameInstall.SteamHasGame(SteamDirectory()), kakao))
        {
            // The Korean client only starts from the Kakao Games website, which hands it the login token.
            Log.Write($"Korean client installed in {kakao}: waiting for it to be started from the Kakao Games website");
            Dialogs.Show(IntPtr.Zero, "Start Path of Exile 2 from the Kakao Games website, then press OK.", "Poe2Access", Dialogs.Information);
        }
        else
        {
            Log.Write($"Opening {SteamUrl}");
            Process.Start(new ProcessStartInfo(SteamUrl) { UseShellExecute = true })?.Dispose();
        }

        var wait = new GameWait(Patience, FirstReminder, ReminderEvery);
        wait.Start(DateTime.UtcNow);
        var steamRuns = steamCommand is not null || !GameInstall.StartsKakaoClient(GameInstall.SteamHasGame(SteamDirectory()), kakao);
        while (true)
        {
            if (FindWindow())
            {
                Log.Write("Game window is up");
                return true;
            }

            switch (wait.Look(SomethingCouldStartTheGame(steamRuns), DateTime.UtcNow))
            {
                case WaitStep.GiveUp:
                    throw new TimeoutException($"The game window did not appear: nothing that could start the game ran for {Patience.TotalSeconds:0} seconds.");
                case WaitStep.Remind:
                    Log.Write("Still waiting for the game window");
                    voice?.Say(StartupPhrases.WaitingForGame);
                    break;
            }

            if (stop.WaitOne(Poll))
            {
                return false;
            }
        }
    }

    /// <summary>Steam (it patches, downloads and queues the game) or the game process itself, window or not yet.</summary>
    private static bool SomethingCouldStartTheGame(bool steamRuns) =>
        RunningProcessName() is not null || (steamRuns && SteamProcessNames.Any(Runs));

    private static bool Runs(string name)
    {
        var processes = Process.GetProcessesByName(name);
        Array.ForEach(processes, process => process.Dispose());
        return processes.Length > 0;
    }

    /// <summary>Starts the game from the command Steam gave, unless it runs already; it inherits Steam's environment from this program.</summary>
    public static void Start(string[] steamCommand)
    {
        if (RunningProcessName() is not null)
        {
            return;
        }

        Log.Write($"Starting {string.Join(' ', steamCommand)}");
        var info = new ProcessStartInfo(steamCommand[0]) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(steamCommand[0]) };
        foreach (var arg in steamCommand.Skip(1))
        {
            info.ArgumentList.Add(arg);
        }

        Process.Start(info)?.Dispose();
    }

    /// <summary>Returns once no game process is left.</summary>
    public static void WaitForExit()
    {
        while (ProcessNames.SelectMany(Process.GetProcessesByName).ToArray() is { Length: > 0 } running)
        {
            foreach (var process in running)
            {
                using (process)
                {
                    process.WaitForExit();
                }
            }
        }
    }

    /// <summary>The process name of the running game, or null when it is not running.</summary>
    public static string? RunningProcessName() =>
        ProcessNames.FirstOrDefault(name =>
        {
            var processes = Process.GetProcessesByName(name);
            Array.ForEach(processes, process => process.Dispose());
            return processes.Length > 0;
        });

    /// <summary>Whether a running game's memory is denied to this process (<see cref="ProcessAccess.ReadDenied"/>).</summary>
    public static bool RunningGameReadDenied()
    {
        var denied = false;
        foreach (var process in ProcessNames.SelectMany(Process.GetProcessesByName))
        {
            using (process)
            {
                denied |= ProcessAccess.ReadDenied(process.Id);
            }
        }

        return denied;
    }

    /// <summary>
    ///     Runs this program again as administrator (the UAC prompt), with the same arguments, and returns its exit code
    ///     once it ends: whoever ran "stop" (Stop the mod.bat, the build scripts) goes on only once the mod is stopped.
    ///     The new process is told this one's id, so it does not kill it as a stray copy.
    /// </summary>
    public static int RunAsAdministrator(string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
        info.ArgumentList.Add(CommandLine.ParentOption);
        info.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var started = Process.Start(info) ?? throw new InvalidOperationException("The administrator launcher did not start.");
        started.WaitForExit();
        return started.ExitCode;
    }

    /// <summary>The folder of the running game (its fmod.dll and FMOD banks), or null when it cannot be read.</summary>
    public static string? InstallDirectory()
    {
        foreach (var process in ProcessNames.SelectMany(Process.GetProcessesByName))
        {
            using (process)
            {
                if (Path.GetDirectoryName(ProcessAccess.ImagePath(process.Id)) is { Length: > 0 } directory)
                {
                    return directory;
                }

                Log.Write($"Game folder unreadable: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            }
        }

        return null;
    }

    /// <summary>Steam's folder, or null when Steam is not installed.</summary>
    private static string? SteamDirectory() =>
        Microsoft.Win32.Registry.GetValue(SteamKey, "SteamPath", null) is string path && Directory.Exists(path) ? path : null;

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
