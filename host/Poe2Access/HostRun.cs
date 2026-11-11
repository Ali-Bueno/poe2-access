namespace Poe2Access;

using GameReader;

/// <summary>Reads the game and runs the accessibility plugin at a fixed rate until asked to stop or the game closes.</summary>
internal static class HostRun
{
    public const string PluginName = "Poe2Accessibility";
    private const int MissingLineHoldMilliseconds = 6_000; // the line is long: PRISM must not go before it is heard

    public static void Run(AppFolders folders, EventWaitHandle stop)
    {
        Core.CloseRequested = () => stop.Set();
        var plugin = PluginHost.Load(folders.Plugin(PluginName), PluginName);
        Log.Write($"Poe2Access {typeof(HostRun).Assembly.GetName().Version}, plugin {(plugin == null ? "missing" : "loaded")}");
        if (plugin == null)
        {
            SayPluginMissing(folders);
        }

        HostFrame.Start();
        plugin?.Enable();
        var watch = new GameUpdateWatch(GameDataSetup.MadeFor);
        new FrameLoop(stop, () =>
        {
            plugin?.Tick();
            WarnWhenPatched(watch);
        }).Run();

        plugin?.Disable();
        HostFrame.Stop();
        Log.Write("Stopped");
    }

    /// <summary>
    ///     Logs once that the game was patched under this host. Log only: the plugin owns the screen reader once the game runs,
    ///     and a second PRISM session from here could cut its speech.
    /// </summary>
    private static void WarnWhenPatched(GameUpdateWatch watch)
    {
        if (watch.Poll(HostFrame.GameOpened, () => GameLauncher.InstallDirectory() is { } directory ? GameDataSetup.DescribeInstall(directory) : null))
        {
            Log.Write("The game was updated since the game data was made: restart the mod to update its game data");
        }
    }

    /// <summary>The plugin is what speaks the game: without it the player would hear nothing and not know why.</summary>
    private static void SayPluginMissing(AppFolders folders)
    {
        var voice = new LauncherVoice(folders.Plugin(PluginName));
        voice.Say(StartupPhrases.PluginMissing, interrupt: true);
        voice.Dispose(MissingLineHoldMilliseconds);
    }
}
