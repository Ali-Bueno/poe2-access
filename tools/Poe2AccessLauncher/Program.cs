using System.Runtime.InteropServices;

namespace Poe2AccessLauncher;

internal static partial class Program
{
    private const string Usage =
        "Poe2Access [start|restart|stop|update] [--host <dir>]\n" +
        "  start    (default) offer a newer version if there is one, open Path of Exile 2 if needed, then restart GameHelper\n" +
        "  restart  restart GameHelper only\n" +
        "  stop     stop GameHelper only\n" +
        "  update   look for a newer version now and offer to install it\n" +
        "  --host   GameHelper folder (default: this folder, or the repository build output)";

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
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--help" or "-h" or "/?":
                    ShowUsage();
                    return 0;
                case "--host" when i + 1 < args.Length:
                    host = args[++i];
                    break;
                case "start" or "restart" or "stop" or "update":
                    command = args[i].ToLowerInvariant();
                    break;
                default:
                    Fail($"Unknown argument: {args[i]}\n\n{Usage}");
                    return 1;
            }
        }

        Updater.DeleteLeftover();
        int code;
        var updated = false;
        var elevate = false;
        using (var mutex = new Mutex(true, @"Local\Poe2Access", out var owned))
        {
            if (!owned)
            {
                Fail("Poe2Access is already running.");
                return 1;
            }

            code = Run(command, host, ref updated, ref elevate);
            mutex.ReleaseMutex();
        }

        if (updated && command == "start")
        {
            Updater.StartInstalled(args);
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

        return code;
    }

    private static int Run(string command, string? host, ref bool updated, ref bool elevate)
    {
        try
        {
            Log.Write($"--- {command}");
            var controller = new HostController(HostController.ResolveDirectory(host));
            // With the Korean client running, GameHelper runs as administrator: only an administrator launcher can
            // stop or replace it (a plain one cannot even tell it from other processes).
            if (elevate = NeedsAdministrator())
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

                    GameLauncher.EnsureRunning();
                    if (elevate = NeedsAdministrator())
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

    private static bool NeedsAdministrator()
    {
        var needed = GameInstall.NeedsAdministrator(GameLauncher.RunningProcessName(), Environment.IsPrivilegedProcess);
        if (needed)
        {
            Log.Write("Korean client running: restarting as administrator, which GameHelper needs to read it");
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
