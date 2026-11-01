using System.ComponentModel;
using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>Stops and starts the GameHelper host that carries the accessibility plugin.</summary>
internal sealed class HostController(string hostDirectory)
{
    private const string HostExe = "GameHelper.exe";
    private const string OriginalLauncherExe = "Launcher.exe";
    private const int ExitWaitMilliseconds = 10_000;
    private const int StartupCheckMilliseconds = 1_500;

    /// <summary>The test package keeps GameHelper and everything else in "app" beside this one executable.</summary>
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

        var relative = Path.Join("external", "GameHelper2", "GameHelper", "bin", "Release", "net10.0-windows", "win-x64");
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Join(directory.FullName, relative);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException($"GameHelper folder not found from {AppContext.BaseDirectory}; pass --host <dir>.");
    }

    public void Stop()
    {
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
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(ExitWaitMilliseconds);
                    Log.Write($"Stopped {path} (pid {process.Id})");
                }
                catch (Exception error) when (error is InvalidOperationException or Win32Exception)
                {
                    Log.Write($"Could not stop pid {process.Id}: {error.Message}");
                }
            }
        }
    }

    /// <summary>Whether a GameHelper runs that only an administrator may stop, as one started for the Korean client.</summary>
    // Matched by name: a process run as administrator may hide its path from a launcher that is not.
    public bool HostNeedsAdministrator()
    {
        var names = Directory.GetFiles(hostDirectory, "*.exe")
            .Where(file => !string.Equals(Path.GetFileName(file), OriginalLauncherExe, StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileNameWithoutExtension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
        var source = Path.Join(hostDirectory, HostExe);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"{HostExe} not found in {hostDirectory}", source);
        }

        Stop();
        HostCopy.DeleteOldCopies(hostDirectory);
        var copy = HostCopy.Create(hostDirectory, source);
        Log.Write($"Created {copy}");

        var info = new ProcessStartInfo(copy)
        {
            WorkingDirectory = hostDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.Environment["__COMPAT_LAYER"] = "RunAsInvoker"; // never its own prompt: it runs with this launcher's rights
        using var host = Process.Start(info) ?? throw new InvalidOperationException("GameHelper did not start.");
        if (host.WaitForExit(StartupCheckMilliseconds))
        {
            throw new InvalidOperationException($"GameHelper exited right after start (code {host.ExitCode}).");
        }

        Log.Write($"Started GameHelper (pid {host.Id})");
    }

    private bool IsHost(Process process, out string path)
    {
        path = ProcessAccess.ImagePath(process.Id) ?? string.Empty;
        return process.Id != Environment.ProcessId
            && string.Equals(Path.GetDirectoryName(path), hostDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileName(path), OriginalLauncherExe, StringComparison.OrdinalIgnoreCase);
    }
}
