using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Poe2AccessLauncher;

/// <summary>Starts Path of Exile 2 through Steam (or asks for the Korean client to be started) and waits for its main window.</summary>
internal static partial class GameLauncher
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const string SteamUrl = "steam://rungameid/" + GameInstall.SteamAppId;
    private const string SteamKey = @"HKEY_CURRENT_USER\Software\Valve\Steam"; // written by Steam
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

        var kakao = KakaoDirectory();
        if (GameInstall.StartsKakaoClient(GameInstall.SteamHasGame(SteamDirectory()), kakao))
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

    /// <summary>The process name of the running game, or null when it is not running.</summary>
    public static string? RunningProcessName() =>
        ProcessNames.FirstOrDefault(name =>
        {
            var processes = Process.GetProcessesByName(name);
            Array.ForEach(processes, process => process.Dispose());
            return processes.Length > 0;
        });

    /// <summary>
    ///     Runs this launcher again as administrator (the UAC prompt), with the same arguments, and returns its exit code
    ///     once it ends: whoever ran "stop" (Stop the mod.bat, the build scripts) goes on only once GameHelper is stopped.
    /// </summary>
    public static int RunAsAdministrator(string[] args)
    {
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, Verb = "runas" };
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
                if (Path.GetDirectoryName(ImagePath(process.Id)) is { Length: > 0 } directory)
                {
                    return directory;
                }

                Log.Write($"Game folder unreadable: {new Win32Exception(Marshal.GetLastPInvokeError()).Message}");
            }
        }

        return null;
    }

    // Asks only for limited query access: Process.MainModule needs to read the game's memory, which the Korean client
    // denies (access denied, 2026-10-31).
    private static unsafe string? ImagePath(int processId)
    {
        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            const int capacity = 1024;
            var buffer = stackalloc char[capacity];
            var length = capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref length) ? new string(buffer, 0, length) : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageName(IntPtr process, uint flags, char* name, ref int size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);

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
