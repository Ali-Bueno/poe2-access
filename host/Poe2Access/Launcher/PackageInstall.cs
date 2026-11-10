using System; // also built into the tests, which have no implicit usings
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Poe2Access;

/// <summary>
///     The file work of an update, apart from asking and fetching so it can be tried on a folder: the download's copy
///     to disk, and a package's files put over an installation, which on any failure is left as it was.
/// </summary>
internal static class PackageInstall
{
    /// <summary>The running launcher, moved aside under this suffix while its replacement takes its name; deleted at the next start.</summary>
    public const string OldSuffix = ".old";
    private const string IncomingSuffix = ".new"; // a file written beside its target before it replaces it
    private const int CopyBytes = 81920;

    /// <summary>
    ///     Copies a download to its file and gives up (a <see cref="TimeoutException"/>) once nothing has arrived for
    ///     <paramref name="quiet"/>: the client's own timeout ends with the response's headers, so a connection gone
    ///     silent held the start for ever, with nothing said and the game not started.
    /// </summary>
    public static void Download(Stream from, Stream to, TimeSpan quiet)
    {
        var buffer = new byte[CopyBytes];
        using var silence = new CancellationTokenSource();
        while (true)
        {
            silence.CancelAfter(quiet); // counted again from each piece that arrives
            int read;
            try
            {
                read = from.ReadAsync(buffer, silence.Token).AsTask().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"No data for {quiet.TotalSeconds:0} seconds.");
            }

            if (read == 0)
            {
                return;
            }

            to.Write(buffer, 0, read);
        }
    }

    /// <summary>Copies the package over the installation; on any failure every file that can be is put back as it was.</summary>
    /// <param name="package">The folder holding the new files, laid out as the installation.</param>
    /// <param name="installRoot">The installation's folder.</param>
    /// <param name="backup">Where the files replaced are kept until the copy is done.</param>
    /// <param name="running">This process's executable: Windows lets a running file be renamed, not overwritten.</param>
    /// <param name="isKept">Whether a file of the installation (its path relative to the root) stays whatever the package holds.</param>
    /// <param name="log">Told what could not be put back.</param>
    public static void Replace(string package, string installRoot, string backup, string running, Func<string, bool> isKept, Action<string> log)
    {
        var created = new List<string>();
        var replaced = new List<string>();
        var aside = false;
        try
        {
            foreach (var source in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(package, source);
                var target = Path.Join(installRoot, relative);
                var existed = File.Exists(target);
                if (existed && isKept(relative))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (existed && string.Equals(target, running, StringComparison.OrdinalIgnoreCase))
                {
                    PutOverRunning(source, target);
                    aside = true; // from here the old launcher is its own backup
                    continue;
                }

                if (existed)
                {
                    var saved = Path.Join(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(target, saved, overwrite: true);
                }

                Put(source, target);
                (existed ? replaced : created).Add(relative); // a refused Put left its target as it was: nothing to undo there
            }
        }
        catch
        {
            Undo(installRoot, backup, running, aside, created, replaced, log);
            throw;
        }
    }

    /// <summary>
    ///     Puts the installation back, each file by itself: one that cannot be put back (a scanner holding it) must not
    ///     leave the others, nor the launcher, where the failed update dropped them. The launcher first: without it
    ///     nothing starts, and no one who cannot see the folder finds its ".old".
    /// </summary>
    private static void Undo(string installRoot, string backup, string running, bool aside, List<string> created, List<string> replaced, Action<string> log)
    {
        // Also when the launcher went aside and nothing took its name (its own undo failed): only then is ".old" this launcher.
        var old = running + OldSuffix;
        if (aside || (!File.Exists(running) && File.Exists(old)))
        {
            Try(() => File.Move(old, running, overwrite: true), $"put the launcher back from {old}", log);
        }

        foreach (var relative in created)
        {
            var path = Path.Join(installRoot, relative);
            Try(() => File.Delete(path), $"delete {path}", log);
        }

        foreach (var relative in replaced)
        {
            var path = Path.Join(installRoot, relative);
            Try(() => Put(Path.Join(backup, relative), path), $"put back {path}", log);
        }
    }

    private static void Try(Action action, string what, Action<string> log)
    {
        try
        {
            action();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            log($"Could not {what}: {error.Message}");
        }
    }

    /// <summary>
    ///     Writes the file beside its target, then moves it over: a library a process still maps (prism.dll, or under Proton
    ///     a Wine bridge's .dll.so, which Wine loads without a Windows handle) is never truncated in place under it, and a
    ///     move Windows refuses for a loaded file fails the update before anything is half written.
    /// </summary>
    private static void Put(string source, string target)
    {
        var incoming = target + IncomingSuffix;
        File.Copy(source, incoming, overwrite: true);
        try
        {
            File.Move(incoming, target, overwrite: true);
        }
        catch
        {
            TryDelete(incoming);
            throw;
        }
    }

    /// <summary>
    ///     The running launcher's replacement is written beside it first (the long part), and only then the launcher is
    ///     moved aside and the new file given its name: the folder is without a launcher for two renames, not for the
    ///     seconds of a copy in which a power cut or a kill would leave none to start.
    /// </summary>
    private static void PutOverRunning(string source, string target)
    {
        var incoming = target + IncomingSuffix;
        var old = target + OldSuffix;
        File.Copy(source, incoming, overwrite: true);
        try
        {
            File.Delete(old);
            File.Move(target, old);
            try
            {
                File.Move(incoming, target);
            }
            catch
            {
                File.Move(old, target);
                throw;
            }
        }
        catch
        {
            TryDelete(incoming);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left beside its target: the next update overwrites it.
        }
    }
}
