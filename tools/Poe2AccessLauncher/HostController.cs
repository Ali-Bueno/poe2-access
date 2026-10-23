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
                if (process.Id == Environment.ProcessId || !IsHostImage(process, out var path))
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
        info.Environment["__COMPAT_LAYER"] = "RunAsInvoker"; // GameHelper only reads game memory, no elevation needed
        using var host = Process.Start(info) ?? throw new InvalidOperationException("GameHelper did not start.");
        if (host.WaitForExit(StartupCheckMilliseconds))
        {
            throw new InvalidOperationException($"GameHelper exited right after start (code {host.ExitCode}).");
        }

        Log.Write($"Started GameHelper (pid {host.Id})");
    }

    private bool IsHostImage(Process process, out string path)
    {
        path = string.Empty;
        try
        {
            path = process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception)
        {
            return false; // protected or already exited
        }

        return string.Equals(Path.GetDirectoryName(path), hostDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFileName(path), OriginalLauncherExe, StringComparison.OrdinalIgnoreCase);
    }
}
