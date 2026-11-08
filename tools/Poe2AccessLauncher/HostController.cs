using System.ComponentModel;
using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>Stops and starts Poe2AccessHost, the headless host that carries the accessibility plugin.</summary>
internal sealed class HostController(string hostDirectory)
{
    private const string HostExe = "Poe2AccessHost.exe";
    private const string StopEventName = @"Local\Poe2AccessHost.Stop"; // the host waits on it and exits (host/Poe2AccessHost)
    private const int GracefulExitMilliseconds = 5_000;
    private const int ExitWaitMilliseconds = 10_000;
    private const int StartupCheckMilliseconds = 1_500;

    /// <summary>The test package keeps the host and everything else in "app" beside this one executable.</summary>
    public static string PackagedHostDirectory => Path.Join(AppContext.BaseDirectory, "app");

    public static string ResolveDirectory(string? overrideDirectory)
    {
        if (overrideDirectory is not null)
        {
            return Path.GetFullPath(overrideDirectory);
        }

        if (File.Exists(Path.Join(AppContext.BaseDirectory, HostExe)))
        {
            return Path.GetFullPath(AppContext.BaseDirectory);
        }

        if (File.Exists(Path.Join(PackagedHostDirectory, HostExe)))
        {
            return Path.GetFullPath(PackagedHostDirectory);
        }

        var relative = Path.Join("host", "Poe2AccessHost", "bin", "Release", "net10.0-windows", "win-x64");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Join(directory.FullName, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"Host folder not found from {AppContext.BaseDirectory}; pass --host <dir>.");
    }

    /// <summary>Asks the host to exit and kills whatever else runs from the host folder (an old GameHelper copy, or a host that did not answer).</summary>
    public void Stop()
    {
        var signalled = SignalStop();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (!IsHost(process, out var path))
                {
                    continue;
                }

                try
                {
                    if (signalled && IsHostExe(path) && process.WaitForExit(GracefulExitMilliseconds))
                    {
                        Log.Write($"Stopped {path} (pid {process.Id})");
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(ExitWaitMilliseconds);
                    Log.Write($"Killed {path} (pid {process.Id})");
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception)
                {
                    Log.Write($"Could not stop pid {process.Id}: {error.Message}");
                }
            }
        }
    }

    /// <summary>Whether a host runs that only an administrator may stop, as one started for the Korean client.</summary>
    // Matched by name: a process run as administrator may hide its path from a launcher that is not.
    public bool HostNeedsAdministrator()
    {
        var names = Directory.GetFiles(hostDirectory, "*.exe").Select(Path.GetFileNameWithoutExtension).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var processes = Process.GetProcesses();
        try
        {
            return processes.Any(process => names.Contains(process.ProcessName) && ProcessAccess.TerminateDenied(process.Id));
        }
        finally
        {
            Array.ForEach(processes, process => process.Dispose());
        }
    }

    public void Restart()
    {
        var exe = Path.Join(hostDirectory, HostExe);
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException($"{HostExe} not found in {hostDirectory}", exe);
        }

        Stop();
        LegacyHost.Delete(hostDirectory);

        // Its own manifest asks for no administrator rights: it runs with this launcher's, no window, no console.
        var info = new ProcessStartInfo(exe)
        {
            WorkingDirectory = hostDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var host = Process.Start(info) ?? throw new InvalidOperationException("The host did not start.");
        if (host.WaitForExit(StartupCheckMilliseconds))
        {
            throw new InvalidOperationException($"The host exited right after start (code {host.ExitCode}).");
        }

        Log.Write($"Started {HostExe} (pid {host.Id})");
    }

    /// <summary>Sets the host's stop event; false when no host holds one (none runs, or an old GameHelper does).</summary>
    private static bool SignalStop()
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(StopEventName, out var stop))
            {
                return false;
            }

            using (stop)
            {
                stop.Set();
            }

            return true;
        }
        catch (UnauthorizedAccessException error)
        {
            Log.Write($"Stop event denied (an administrator host?): {error.Message}");
            return false;
        }
    }

    private static bool IsHostExe(string path) => string.Equals(Path.GetFileName(path), HostExe, StringComparison.OrdinalIgnoreCase);

    /// <summary>A process whose image lies in the host folder, other than this launcher (another copy of it may wait for the game).</summary>
    private bool IsHost(Process process, out string path)
    {
        path = ProcessAccess.ImagePath(process.Id) ?? string.Empty;
        return process.Id != Environment.ProcessId
            && string.Equals(Path.GetDirectoryName(path), hostDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
    }
}
