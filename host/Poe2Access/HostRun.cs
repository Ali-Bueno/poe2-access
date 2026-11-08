namespace Poe2Access;

using GameReader;

/// <summary>Reads the game and runs the accessibility plugin at a fixed rate until asked to stop or the game closes.</summary>
internal static class HostRun
{
    private const string PluginName = "Poe2Accessibility";

    public static void Run(AppFolders folders, EventWaitHandle stop)
    {
        Core.CloseRequested = () => stop.Set();
        var plugin = PluginHost.Load(folders.Plugin(PluginName), PluginName);
        Log.Write($"Poe2Access {typeof(HostRun).Assembly.GetName().Version}, plugin {(plugin == null ? "missing" : "loaded")}");
        HostFrame.Start();
        plugin?.Enable();
        new FrameLoop(stop, () => plugin?.Tick()).Run();

        plugin?.Disable();
        HostFrame.Stop();
        Log.Write("Stopped");
    }
}
