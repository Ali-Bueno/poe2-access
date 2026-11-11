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
        new FrameLoop(stop, () => plugin?.Tick()).Run();

        plugin?.Disable();
        HostFrame.Stop();
        Log.Write("Stopped");
    }

    /// <summary>The plugin is what speaks the game: without it the player would hear nothing and not know why.</summary>
    private static void SayPluginMissing(AppFolders folders)
    {
        var voice = new LauncherVoice(folders.Plugin(PluginName));
        voice.Say(StartupPhrases.PluginMissing, interrupt: true);
        voice.Dispose(MissingLineHoldMilliseconds);
    }
}
