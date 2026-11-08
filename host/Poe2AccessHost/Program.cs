namespace Poe2AccessHost
{
    using System;
    using System.IO;
    using System.Threading;
    using GameReader;

    /// <summary>Starts the reader, loads the accessibility plugin and runs both until asked to stop.</summary>
    internal static class Program
    {
        private const string InstanceName = @"Local\Poe2AccessHost";
        private const string StopEventName = @"Local\Poe2AccessHost.Stop";
        private const string PluginName = "Poe2Accessibility";

        [STAThread]
        private static int Main()
        {
            var baseDirectory = AppContext.BaseDirectory;
            Directory.SetCurrentDirectory(baseDirectory);
            using var instance = new Mutex(initiallyOwned: true, InstanceName, out var first);
            if (!first)
            {
                return 1;
            }

            using var log = HostLog.Open(Path.Join(baseDirectory, "Poe2AccessHost.log"));
            AppDomain.CurrentDomain.UnhandledException += (_, e) => HostLog.Line($"Unhandled: {e.ExceptionObject}");
            using var stop = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName);
            Core.CloseRequested = () => stop.Set();

            var plugin = PluginHost.Load(Path.Join(baseDirectory, "Plugins", PluginName), PluginName);
            HostLog.Line($"Poe2AccessHost {typeof(Program).Assembly.GetName().Version}, plugin {(plugin == null ? "missing" : "loaded")}");
            HostFrame.Start();
            plugin?.Enable();
            new FrameLoop(stop, () => plugin?.Tick()).Run();

            plugin?.Disable();
            HostFrame.Stop();
            HostLog.Line("Stopped");
            return 0;
        }
    }
}
