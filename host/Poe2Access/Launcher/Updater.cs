using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Poe2Access;

/// <summary>
///     Installs the latest GitHub release over this package before the host starts, when nothing in "app" is in use:
///     asks first, keeps the player's settings, moves the running launcher aside and puts everything back on a failure.
/// </summary>
internal static class Updater
{
    public const string VersionFile = "version.txt";
    private const string LauncherExe = "Poe2Access.exe";
    private const int NotesShown = 1500;
    private const int LeftoverTries = 10;
    private const int LeftoverWaitMilliseconds = 300;
    private const uint MessageBoxYesNo = 0x4;
    private const uint MessageBoxQuestion = 0x20;
    private const uint MessageBoxError = Dialogs.Error;
    private const uint MessageBoxInformation = Dialogs.Information;
    private const uint MessageBoxForeground = 0x10000 | 0x40000; // MB_SETFOREGROUND | MB_TOPMOST: above a running game
    private const int AnswerYes = 6;
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan DownloadQuiet = TimeSpan.FromSeconds(60); // the package's body, which the client's timeout does not cover

    /// <summary>The player's own settings, kept over the package's defaults.</summary>
    private static readonly string[] KeptFolders = { Path.Join("app", "Plugins", "Poe2Accessibility", "config") };

    private static string InstallRoot => AppContext.BaseDirectory;

    private static string WorkRoot => Path.Join(Path.GetTempPath(), "Poe2AccessUpdate");

    private static string OldLauncher => Path.Join(InstallRoot, LauncherExe + PackageInstall.OldSuffix);

    private static string SettingsPath(AppFolders folders) => Path.Join(folders.Plugin("Poe2Accessibility"), "config", "settings.txt");

    /// <summary>
    ///     Looks for a newer release and, if the player agrees, installs it; true when installed. Automatic (at start)
    ///     it is silent unless there is something to install and follows the CheckForUpdates setting.
    /// </summary>
    public static bool Offer(AppFolders folders, Action stopRunning, bool manual)
    {
        var installedTag = InstalledTag(folders);
        if (installedTag is null || ReleaseTag.Parse(installedTag) is not { } installed)
        {
            Log.Write("No version file: a development build, no update check");
            if (manual)
            {
                Tell(Text("This copy has no version number (a development build), so it cannot update itself.",
                    "Esta copia no tiene número de versión (es de desarrollo), así que no puede actualizarse sola."), MessageBoxInformation);
            }

            return false;
        }

        if (!manual && !AutomaticCheckOn(folders))
        {
            Log.Write("Update check off in the settings");
            return false;
        }

        Release release;
        try
        {
            release = Latest(installedTag);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Write($"Update check failed: {error.Message}");
            if (manual)
            {
                Tell(Text($"Could not check for updates: {error.Message}", $"No se pudo buscar actualizaciones: {error.Message}"), MessageBoxError);
            }

            return false;
        }

        Log.Write($"Installed {installedTag}, latest {release.Tag}");
        if (release.Version <= installed)
        {
            if (manual)
            {
                Tell(Text($"Poe2Access is up to date ({installedTag}).", $"Poe2Access está al día ({installedTag})."), MessageBoxInformation);
            }

            return false;
        }

        var notes = release.Notes.Length > NotesShown ? release.Notes[..NotesShown] + "…" : release.Notes;
        var question = Text(
            $"A new version of Poe2Access is out: {release.Tag} (you have {installedTag}).\n\nDownload and install it now? It takes about a minute with nothing said; your settings are kept and a message tells you when it is done.\n\n{notes}",
            $"Hay una versión nueva de Poe2Access: {release.Tag} (tienes la {installedTag}).\n\n¿Descargarla e instalarla ahora? Tarda alrededor de un minuto sin decir nada; tus ajustes se conservan y un mensaje avisa al terminar.\n\n{notes}");
        if (Dialogs.Show(IntPtr.Zero, question, "Poe2Access", MessageBoxYesNo | MessageBoxQuestion | MessageBoxForeground) != AnswerYes)
        {
            Log.Write("Update declined");
            return false;
        }

        try
        {
            Install(release, installedTag, stopRunning);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            Log.Write($"Update failed: {error}");
            Tell(Text($"The update could not be installed: {error.Message}\n\nPoe2Access stays on {installedTag}.",
                $"No se pudo instalar la actualización: {error.Message}\n\nPoe2Access sigue en la {installedTag}."), MessageBoxError);
            return false;
        }
        finally
        {
            TryDeleteDirectory(WorkRoot);
        }

        Log.Write($"Updated to {release.Tag}");
        Tell(manual
            ? Text($"Poe2Access was updated to {release.Tag}.", $"Poe2Access se actualizó a la {release.Tag}.")
            : Text($"Poe2Access was updated to {release.Tag}. It starts now.", $"Poe2Access se actualizó a la {release.Tag}. Ahora se abre."), MessageBoxInformation);
        return true;
    }

    /// <summary>Starts the launcher just installed, with the same arguments, once this one has let go of its mutex.</summary>
    /// <param name="args">This launcher's arguments, passed on.</param>
    /// <param name="wait">Stay until the new launcher ends: Steam follows the launcher it started.</param>
    public static int StartInstalled(string[] args, bool wait)
    {
        var info = new ProcessStartInfo(Path.Join(InstallRoot, LauncherExe)) { UseShellExecute = false, WorkingDirectory = InstallRoot };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var started = Process.Start(info);
        if (!wait || started is null)
        {
            return 0;
        }

        started.WaitForExit();
        return started.ExitCode;
    }

    /// <summary>The launcher an update moved aside is still exiting when the new one starts: retry its deletion briefly.</summary>
    public static void DeleteLeftover()
    {
        for (var attempt = 0; attempt < LeftoverTries && File.Exists(OldLauncher); attempt++)
        {
            try
            {
                File.Delete(OldLauncher);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(LeftoverWaitMilliseconds);
            }
        }
    }

    private static string? InstalledTag(AppFolders folders)
    {
        var path = Path.Join(folders.Data, VersionFile);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static bool AutomaticCheckOn(AppFolders folders)
    {
        try
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(SettingsPath(folders)), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return !(settings.RootElement.ValueKind == JsonValueKind.Object && // TryGetProperty throws on anything else
                     settings.RootElement.TryGetProperty("CheckForUpdates", out var value) && value.ValueKind == JsonValueKind.False);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return true;
        }
    }

    private static HttpClient Client(string installedTag, TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Poe2Access", installedTag));
        return client;
    }

    private static Release Latest(string installedTag)
    {
        using var client = Client(installedTag, CheckTimeout);
        using var response = client.Send(new HttpRequestMessage(HttpMethod.Get, ReleaseManifest.LatestUrl));
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(response.Content.ReadAsStream());
        return ReleaseManifest.Parse(reader.ReadToEnd());
    }

    private static void Install(Release release, string installedTag, Action stopRunning)
    {
        var work = Path.Join(WorkRoot, release.Tag);
        TryDeleteDirectory(work);
        Directory.CreateDirectory(work);
        var zip = Path.Join(work, ReleaseManifest.PackageAsset);
        using (var client = Client(installedTag, DownloadTimeout))
        using (var response = client.Send(new HttpRequestMessage(HttpMethod.Get, release.ZipUrl), HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var download = response.Content.ReadAsStream();
            using var file = File.Create(zip);
            try
            {
                PackageInstall.Download(download, file, DownloadQuiet);
            }
            catch (TimeoutException)
            {
                throw new IOException(Text("the download stopped, no data for a minute", "la descarga se detuvo, un minuto sin datos"));
            }
        }

        using (var file = File.OpenRead(zip))
        {
            if (!Convert.ToHexString(SHA256.HashData(file)).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(Text("the download does not match the release's checksum", "la descarga no coincide con la suma de comprobación"));
            }
        }

        var extracted = Path.Join(work, "files");
        ZipFile.ExtractToDirectory(zip, extracted);
        var package = Directory.EnumerateFiles(extracted, LauncherExe, SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .FirstOrDefault(folder => folder is not null && Directory.Exists(Path.Join(folder, "app")))
            ?? throw new InvalidDataException($"{ReleaseManifest.PackageAsset} holds no {LauncherExe} beside an app folder");
        stopRunning();
        PackageInstall.Replace(package, InstallRoot, Path.Join(work, "backup"), Environment.ProcessPath ?? string.Empty, IsKept, Log.Write);
    }

    private static bool IsKept(string relative) =>
        KeptFolders.Any(folder => relative.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Could not delete {path}: {error.Message}");
        }
    }

    /// <summary>The launcher has no game language to follow: Spanish on a Spanish Windows, English otherwise.</summary>
    private static string Text(string english, string spanish) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? spanish : english;

    private static void Tell(string message, uint icon) => Dialogs.Show(IntPtr.Zero, message, "Poe2Access", icon | MessageBoxForeground);
}
