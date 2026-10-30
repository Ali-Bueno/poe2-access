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
        using (var mutex = new Mutex(true, @"Local\Poe2Access", out var owned))
        {
            if (!owned)
            {
                Fail("Poe2Access is already running.");
                return 1;
            }

            code = Run(command, host, ref updated);
            mutex.ReleaseMutex();
        }

        if (updated && command == "start")
        {
            Updater.StartInstalled(args);
        }

        return code;
    }

    private static int Run(string command, string? host, ref bool updated)
    {
        try
        {
            Log.Write($"--- {command}");
            var controller = new HostController(HostController.ResolveDirectory(host));
            switch (command)
            {
                case "start":
                    if (Updater.Offer(controller, manual: false))
                    {
                        updated = true;
                        break; // the launcher just installed carries on
                    }

                    GameLauncher.EnsureRunning();
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
