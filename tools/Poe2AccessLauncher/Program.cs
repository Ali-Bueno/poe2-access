using System.Runtime.InteropServices;

namespace Poe2AccessLauncher;

internal static partial class Program
{
    private const string Usage =
        "Poe2Access [start|restart|stop|update] [--host <dir>] [<game exe> <its arguments>]\n" +
        "  start    (default) offer a newer version if there is one, open Path of Exile 2 if needed, then restart GameHelper\n" +
        "  restart  restart GameHelper only\n" +
        "  stop     stop GameHelper only\n" +
        "  update   look for a newer version now and offer to install it\n" +
        "  --host   GameHelper folder (default: this folder, or the repository build output)\n" +
        "  Steam starts the mod with the game from the launch options: \"<this file's path>\" %command%";

    private const uint MessageBoxError = 0x10;
    internal const uint MessageBoxInformation = 0x40;
    private const int AttachParentProcess = -1;
    private const int ErrorCancelled = 1223; // the UAC prompt was declined

    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int MessageBox(IntPtr window, string text, string caption, uint type);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    private static int Main(string[] args)
    {
        var command = "start";
        string? host = null;
        var (ownArgs, steamCommand) = GameInstall.SplitArguments(args);
        for (var i = 0; i < ownArgs.Length; i++)
        {
            switch (ownArgs[i].ToLowerInvariant())
            {
                case "--help" or "-h" or "/?":
                    ShowUsage();
                    return 0;
                case "--host" when i + 1 < ownArgs.Length:
                    host = ownArgs[++i];
                    break;
                case "start" or "restart" or "stop" or "update":
                    command = ownArgs[i].ToLowerInvariant();
                    break;
                default:
                    Fail($"Unknown argument: {ownArgs[i]}\n\n{Usage}");
                    return 1;
            }
        }

        Updater.DeleteLeftover();
        var code = 0;
        var updated = false;
        var elevate = false;
        using (var mutex = new Mutex(true, @"Local\Poe2Access", out var owned))
        {
            if (owned)
            {
                code = Run(command, host, steamCommand, ref updated, ref elevate);
                mutex.ReleaseMutex();
            }
            else if (steamCommand is null)
            {
                Fail("Poe2Access is already running.");
                return 1;
            }
            else
            {
                // Steam answering a launcher that asked it for the game: that one goes on once the window is up.
                Log.Write("--- start from Steam, for the launcher already running");
                StartGame(steamCommand);
            }
        }

        if (updated && command == "start")
        {
            Updater.StartInstalled(args, wait: steamCommand is not null);
        }
        else if (elevate)
        {
            try
            {
                code = GameLauncher.RunAsAdministrator(args);
            }
            catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == ErrorCancelled)
            {
                Fail("The Korean client of Path of Exile 2 can only be read by a program run as administrator. Start Poe2Access again and accept the prompt.");
                return 1;
            }
            catch (Exception error)
            {
                Log.Write(error.ToString());
                Fail($"Poe2Access could not restart as administrator: {error.Message}");
                return 1;
            }
        }
        else if (steamCommand is not null)
        {
            FollowGame();
        }

        return code;
    }

    private static void StartGame(string[] steamCommand)
    {
        try
        {
            GameLauncher.Start(steamCommand);
        }
        catch (Exception error)
        {
            Log.Write(error.ToString());
            Fail($"Path of Exile 2 could not start: {error.Message}");
        }
    }

    /// <summary>Stays until the game closes: Steam counts it running while the launcher it started lives. GameHelper closes with the game by itself.</summary>
    private static void FollowGame()
    {
        try
        {
            GameLauncher.WaitForExit();
            Log.Write("Game closed");
        }
        catch (Exception error)
        {
            Log.Write(error.ToString());
        }
    }

    private static int Run(string command, string? host, string[]? steamCommand, ref bool updated, ref bool elevate)
    {
        try
        {
            Log.Write($"--- {command}");
            var controller = new HostController(HostController.ResolveDirectory(host));
            // With the Korean client, GameHelper runs as administrator: only an administrator launcher can stop or replace it.
            if (elevate = NeedsAdministrator(controller))
            {
                Log.Write("Done");
                return 0;
            }

            switch (command)
            {
                case "start":
                    if (Updater.Offer(controller, manual: false))
                    {
                        updated = true;
                        break; // the launcher just installed carries on
                    }

                    GameLauncher.EnsureRunning(steamCommand);
                    if (elevate = NeedsAdministrator(controller))
                    {
                        break; // the game has just been started: the administrator launcher carries on
                    }

                    GameDataSetup.EnsureCurrent(HostController.ResolveDirectory(host), GameLauncher.InstallDirectory());
                    controller.Restart();
                    break;
                case "restart":
                    controller.Restart();
                    break;
                case "update":
                    updated = Updater.Offer(controller, manual: true);
                    break;
                default:
                    controller.Stop();
                    break;
            }

            Log.Write("Done");
            return 0;
        }
        catch (Exception error)
        {
            Log.Write(error.ToString());
            Fail(error.Message);
            return 1;
        }
    }

    private static bool NeedsAdministrator(HostController controller)
    {
        var host = controller.HostNeedsAdministrator();
        var needed = GameInstall.NeedsAdministrator(GameLauncher.RunningProcessName(), host, Environment.IsPrivilegedProcess);
        if (needed)
        {
            Log.Write(host
                ? "GameHelper runs as administrator: restarting as administrator to stop or replace it"
                : "Korean client running: restarting as administrator, which GameHelper needs to read it");
        }

        return needed;
    }

    private static void ShowUsage()
    {
        if (AttachConsole(AttachParentProcess) || Console.IsOutputRedirected)
        {
            Console.WriteLine(Usage);
        }
        else
        {
            MessageBox(IntPtr.Zero, Usage, "Poe2Access", MessageBoxInformation);
        }
    }

    private static void Fail(string message)
    {
        Log.Write($"ERROR {message}");
        MessageBox(IntPtr.Zero, message, "Poe2Access", MessageBoxError);
    }
}
