namespace Poe2Access;

using System.ComponentModel;

/// <summary>The mod's one executable: start-up steps, then the host loop in this same process.</summary>
internal static class Program
{
    private const string Usage =
        "Poe2Access [start|restart|stop|update|extract ...] [<game exe> <its arguments>]\n" +
        "  start    (default) offer a newer version if there is one, open Path of Exile 2 if needed, update the game data, then run the mod until the game closes\n" +
        "  restart  stop the running mod and run it here (does not open the game)\n" +
        "  stop     stop the running mod\n" +
        "  update   look for a newer version now and offer to install it\n" +
        "  extract  run a game-data extractor command (tools/README.md)\n" +
        "  Steam starts the mod with the game from the launch options: \"<this file's path>\" %command%";

    private const string LogName = "Poe2Access.log";
    private const int ErrorCancelled = 1223; // the UAC prompt was declined
    private const int AlreadyRunningCode = 0;

    /// <summary>What a start or restart does once its steps are done.</summary>
    private enum Next
    {
        Finished,
        Elevate,
        RunInstalled,
    }

    [STAThread]
    private static int Main(string[] args)
    {
        var call = CommandLine.Parse(args);
        switch (call.Command)
        {
            case Command.Help:
                Dialogs.ShowUsage(Usage);
                return 0;
            case Command.Invalid:
                Dialogs.Fail($"{call.Error}\n\n{Usage}");
                return 1;
            case Command.Extract:
                Dialogs.AttachParentConsole();
                return Extractor.Run(call.ExtractArguments);
        }

        Updater.DeleteLeftover();
        var folders = AppFolders.Resolve(AppContext.BaseDirectory, Directory.Exists);
        Log.Configure(Path.Join(folders.Data, LogName));
        Directory.SetCurrentDirectory(folders.Exe);
        try
        {
            // With the Korean client the mod runs as administrator: only an administrator can stop or replace it.
            if (NeedsAdministrator(folders))
            {
                return RunAsAdministrator(args);
            }

            return call.Command switch
            {
                Command.Stop => Stop(folders, call.ParentId),
                Command.Update => Update(folders, call.ParentId),
                _ => StartOrRestart(call, args, folders),
            };
        }
        catch (Exception error)
        {
            Log.Write(error.ToString());
            Dialogs.Fail(error.Message);
            return 1;
        }
    }

    private static int Stop(AppFolders folders, int? parent)
    {
        Log.Write("--- stop");
        using var instance = new Instance(parent);
        instance.Stop(folders);
        return 0;
    }

    private static int Update(AppFolders folders, int? parent)
    {
        Log.Write("--- update");
        using var instance = new Instance(parent);
        Updater.Offer(folders, () => instance.Stop(folders), manual: true);
        return 0;
    }

    private static int StartOrRestart(Invocation call, string[] args, AppFolders folders)
    {
        Next next;
        using (var instance = new Instance(call.ParentId))
        {
            if (call.Command == Command.Restart)
            {
                instance.Stop(folders);
            }
            else if (!instance.TryOwn(0))
            {
                return AlreadyRunning(call.GameCommand, folders);
            }

            if (!instance.Owned)
            {
                throw new InvalidOperationException("The running Poe2Access did not stop.");
            }

            using var log = HostLog.Open(Path.Join(folders.Data, LogName));
            AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write($"Unhandled: {e.ExceptionObject}");
            using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, Instance.StopEventName);
            stop.Reset();
            Log.Write($"--- {call.Command.ToString().ToLowerInvariant()}");
            next = Prepare(call, folders, instance, stop);
            if (next == Next.Finished)
            {
                if (!stop.WaitOne(0))
                {
                    HostRun.Run(folders, stop);
                }

                return 0;
            }
        }

        return next switch
        {
            Next.Elevate => RunAsAdministrator(args),
            Next.RunInstalled => Updater.StartInstalled(args, wait: call.GameCommand is not null),
            _ => 0,
        };
    }

    /// <summary>The steps before the loop: update, game, data, leftovers. Finished means the loop may run.</summary>
    private static Next Prepare(Invocation call, AppFolders folders, Instance instance, EventWaitHandle stop)
    {
        if (call.Command == Command.Restart)
        {
            Cleanup(folders, instance);
            return Next.Finished;
        }

        if (Updater.Offer(folders, () => instance.Stop(folders), manual: false))
        {
            return Next.RunInstalled; // the version just installed carries on
        }

        Cleanup(folders, instance);
        if (!GameLauncher.EnsureRunning(call.GameCommand, stop))
        {
            Log.Write("Stopped before the game was up");
            return Next.Finished;
        }

        if (NeedsAdministrator(folders))
        {
            return Next.Elevate; // the game has just been started: the administrator instance carries on
        }

        GameDataSetup.EnsureCurrent(folders, GameLauncher.InstallDirectory());
        return Next.Finished;
    }

    /// <summary>Deletes what earlier layouts left (GameHelper, the separate host and extractor, runtime files) and any old copy still running.</summary>
    private static void Cleanup(AppFolders folders, Instance instance)
    {
        instance.Stop(folders);
        LegacyHost.Delete(folders.Data);
        if (folders.Packaged && Directory.Exists(folders.Data))
        {
            foreach (var name in AppCleanup.Unshipped(Directory.EnumerateFiles(folders.Data).Select(Path.GetFileName).OfType<string>()))
            {
                try
                {
                    File.Delete(Path.Join(folders.Data, name));
                    Log.Write($"Deleted {name}: the package no longer ships it");
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    Log.Write($"Could not delete {name}: {error.Message}");
                }
            }
        }
    }

    /// <summary>A second start: the running instance stays alone. With Steam's command it starts the game and stays until it closes.</summary>
    private static int AlreadyRunning(string[]? steamCommand, AppFolders folders)
    {
        if (steamCommand is null)
        {
            Log.Write("Already running");
            Dialogs.Show(IntPtr.Zero, "Poe2Access is already running.", "Poe2Access", Dialogs.Information);
            return AlreadyRunningCode;
        }

        // Steam answering an instance that asked it for the game: that one goes on once the window is up.
        Log.Write("--- start from Steam, for the instance already running");
        try
        {
            GameLauncher.Start(steamCommand);
            GameLauncher.WaitForExit();
            Log.Write("Game closed");
        }
        catch (Exception error)
        {
            Log.Write(error.ToString());
            Dialogs.Fail($"Path of Exile 2 could not start: {error.Message}");
        }

        return AlreadyRunningCode;
    }

    private static bool NeedsAdministrator(AppFolders folders)
    {
        var others = Instance.OthersNeedAdministrator(folders);
        var needed = GameInstall.NeedsAdministrator(GameLauncher.RunningProcessName(), others, Environment.IsPrivilegedProcess);
        if (needed)
        {
            Log.Write(others
                ? "Poe2Access runs as administrator: restarting as administrator to stop or replace it"
                : "Korean client running: restarting as administrator, which the mod needs to read it");
        }

        return needed;
    }

    /// <summary>Runs this program again as administrator (the UAC prompt) and returns its exit code once it ends.</summary>
    private static int RunAsAdministrator(string[] args)
    {
        try
        {
            return GameLauncher.RunAsAdministrator(args);
        }
        catch (Win32Exception error) when (error.NativeErrorCode == ErrorCancelled)
        {
            Dialogs.Fail("The Korean client of Path of Exile 2 can only be read by a program run as administrator. Start Poe2Access again and accept the prompt.");
            return 1;
        }
    }
}
