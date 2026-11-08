namespace Poe2Access;

using System.ComponentModel;
using System.Diagnostics;

/// <summary>The one running instance: the mutex it holds, the event that asks it to stop, and the other copies in the install.</summary>
internal sealed class Instance(int? spareProcessId = null) : IDisposable
{
    public const string StopEventName = @"Local\Poe2Access.Stop";
    private const string MutexName = @"Local\Poe2Access";
    private const string LegacyStopEventName = @"Local\Poe2AccessHost.Stop"; // the separate host of earlier versions
    private const int GracefulExitMilliseconds = 5_000;
    private const int ExitWaitMilliseconds = 10_000;
    private static readonly string[] LegacyProcessNames = ["Poe2AccessHost", "GameHelper"];

    private readonly Mutex mutex = new(false, MutexName);

    public bool Owned { get; private set; }

    /// <summary>Takes the mutex, waiting up to <paramref name="waitMilliseconds"/>; an instance that was killed leaves it abandoned, which counts as free.</summary>
    public bool TryOwn(int waitMilliseconds)
    {
        if (this.Owned)
        {
            return true;
        }

        try
        {
            this.Owned = this.mutex.WaitOne(waitMilliseconds);
        }
        catch (AbandonedMutexException)
        {
            this.Owned = true;
        }

        return this.Owned;
    }

    /// <summary>
    ///     Asks the running instance to exit and waits for it (it holds the mutex), then kills any other copy in the
    ///     install: an instance that did not answer, an old separate host, a GameHelper copy. Never this process or the spared one.
    /// </summary>
    public void Stop(AppFolders folders)
    {
        if (!this.Owned)
        {
            SignalStop();
            if (!this.TryOwn(GracefulExitMilliseconds))
            {
                Log.Write("The running instance did not exit in time");
            }
        }

        KillOthers(folders);
        if (!this.Owned && !this.TryOwn(ExitWaitMilliseconds))
        {
            Log.Write("The mutex is still held after killing the other copies");
        }
    }

    public void Dispose()
    {
        if (this.Owned)
        {
            this.mutex.ReleaseMutex();
            this.Owned = false;
        }

        this.mutex.Dispose();
    }

    /// <summary>Whether another copy runs that only an administrator may end, as one started for the Korean client.</summary>
    // Matched by name: a process run as administrator may hide its path from a program that is not.
    public static bool OthersNeedAdministrator(AppFolders folders)
    {
        var names = Directory.EnumerateFiles(folders.Exe, "*.exe")
            .Concat(Directory.Exists(folders.Data) ? Directory.EnumerateFiles(folders.Data, "*.exe") : [])
            .Select(Path.GetFileNameWithoutExtension)
            .Concat(LegacyProcessNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var processes = Process.GetProcesses();
        try
        {
            return processes.Any(process => process.Id != Environment.ProcessId && names.Contains(process.ProcessName) && ProcessAccess.TerminateDenied(process.Id));
        }
        finally
        {
            Array.ForEach(processes, process => process.Dispose());
        }
    }

    private static void SignalStop()
    {
        foreach (var name in new[] { StopEventName, LegacyStopEventName })
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(name, out var stop))
                {
                    using (stop)
                    {
                        stop.Set();
                    }
                }
            }
            catch (UnauthorizedAccessException error)
            {
                Log.Write($"Stop event denied (an administrator instance?): {error.Message}");
            }
        }
    }

    private void KillOthers(AppFolders folders)
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == Environment.ProcessId || process.Id == spareProcessId || ProcessAccess.ImagePath(process.Id) is not { } path || !folders.OwnsImage(path))
                {
                    continue;
                }

                try
                {
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
}
