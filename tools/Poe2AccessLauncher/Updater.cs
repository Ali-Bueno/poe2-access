using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace Poe2AccessLauncher;

/// <summary>A published release: its tag, notes, and the package's download link and SHA-256.</summary>
internal sealed record Release(string Tag, Version Version, string Notes, string ZipUrl, string Sha256);

/// <summary>
///     Installs the latest GitHub release over this package before GameHelper starts, when nothing in "app" is in use:
///     asks first, keeps the player's settings, moves the running launcher aside and puts everything back on a failure.
/// </summary>
internal static class Updater
{
    public const string VersionFile = "version.txt";
    private const string LauncherExe = "Poe2Access.exe";
    private const string LatestReleaseApi = "https://api.github.com/repos/Ali-Bueno/poe2-access/releases/latest";
    private const string PackageAsset = "Poe2Access.zip";
    private const string DigestPrefix = "sha256:";
    private const int NotesShown = 1500;
    private const int LeftoverTries = 10;
    private const int LeftoverWaitMilliseconds = 300;
    private const uint MessageBoxYesNo = 0x4;
    private const uint MessageBoxQuestion = 0x20;
    private const uint MessageBoxError = 0x10;
    private const uint MessageBoxInformation = 0x40;
    private const uint MessageBoxForeground = 0x10000 | 0x40000; // MB_SETFOREGROUND | MB_TOPMOST: above a running game
    private const int AnswerYes = 6;
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);

    /// <summary>The player's own settings (GameHelper's and the plugin's), kept over the package's defaults.</summary>
    private static readonly string[] KeptFolders = { Path.Join("app", "configs"), Path.Join("app", "Plugins", "Poe2Accessibility", "config") };

    private static string InstallRoot => AppContext.BaseDirectory;

    private static string WorkRoot => Path.Join(Path.GetTempPath(), "Poe2AccessUpdate");

    private static string OldLauncher => Path.Join(InstallRoot, LauncherExe + ".old");

    private static string SettingsPath => Path.Join(HostController.PackagedHostDirectory, "Plugins", "Poe2Accessibility", "config", "settings.txt");

    /// <summary>
    ///     Looks for a newer release and, if the player agrees, installs it; true when installed. Automatic (at start)
    ///     it is silent unless there is something to install and follows the CheckForUpdates setting.
    /// </summary>
    public static bool Offer(HostController controller, bool manual)
    {
        var installedTag = InstalledTag();
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

        if (!manual && !AutomaticCheckOn())
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
        if (Program.MessageBox(IntPtr.Zero, question, "Poe2Access", MessageBoxYesNo | MessageBoxQuestion | MessageBoxForeground) != AnswerYes)
        {
            Log.Write("Update declined");
            return false;
        }

        try
        {
            Install(release, installedTag, controller);
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
    public static void StartInstalled(string[] args)
    {
        var info = new ProcessStartInfo(Path.Join(InstallRoot, LauncherExe)) { UseShellExecute = false, WorkingDirectory = InstallRoot };
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        using var started = Process.Start(info);
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

    private static string? InstalledTag()
    {
        var path = Path.Join(HostController.PackagedHostDirectory, VersionFile);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    private static bool AutomaticCheckOn()
    {
        try
        {
            using var settings = JsonDocument.Parse(File.ReadAllText(SettingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            return !(settings.RootElement.TryGetProperty("CheckForUpdates", out var value) && value.ValueKind == JsonValueKind.False);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        using var response = client.Send(request);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(response.Content.ReadAsStream());
        var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        var version = ReleaseTag.Parse(tag) ?? throw new InvalidDataException($"Unknown release tag {tag}");
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != PackageAsset)
            {
                continue;
            }

            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() ?? string.Empty : string.Empty;
            if (!digest.StartsWith(DigestPrefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Release {tag} has no checksum for {PackageAsset}");
            }

            var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;
            return new Release(tag, version, notes.Trim(), asset.GetProperty("browser_download_url").GetString() ?? string.Empty, digest[DigestPrefix.Length..].Trim());
        }

        throw new InvalidDataException($"Release {tag} has no {PackageAsset}");
    }

    private static void Install(Release release, string installedTag, HostController controller)
    {
        var work = Path.Join(WorkRoot, release.Tag);
        TryDeleteDirectory(work);
        Directory.CreateDirectory(work);
        var zip = Path.Join(work, PackageAsset);
        using (var client = Client(installedTag, DownloadTimeout))
        using (var response = client.Send(new HttpRequestMessage(HttpMethod.Get, release.ZipUrl), HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var download = response.Content.ReadAsStream();
            using var file = File.Create(zip);
            download.CopyTo(file);
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
            ?? throw new InvalidDataException($"{PackageAsset} holds no {LauncherExe} beside an app folder");
        controller.Stop();
        Replace(package, Path.Join(work, "backup"));
    }

    /// <summary>Copies the package over the installation; on any failure the files are as they were.</summary>
    private static void Replace(string package, string backup)
    {
        var created = new List<string>();
        var replaced = new List<string>();
        var running = Environment.ProcessPath ?? string.Empty;
        var movedAside = string.Equals(running, Path.Join(InstallRoot, LauncherExe), StringComparison.OrdinalIgnoreCase);
        if (movedAside)
        {
            // Windows lets a running executable be renamed, not overwritten.
            File.Delete(OldLauncher);
            File.Move(running, OldLauncher);
        }

        try
        {
            foreach (var source in Directory.EnumerateFiles(package, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(package, source);
                var target = Path.Join(InstallRoot, relative);
                if (File.Exists(target))
                {
                    if (IsKept(relative))
                    {
                        continue;
                    }

                    var saved = Path.Join(backup, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(saved)!);
                    File.Copy(target, saved, overwrite: true);
                    replaced.Add(relative);
                }
                else
                {
                    created.Add(relative);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
        }
        catch
        {
            foreach (var relative in created)
            {
                TryDeleteFile(Path.Join(InstallRoot, relative));
            }

            foreach (var relative in replaced)
            {
                File.Copy(Path.Join(backup, relative), Path.Join(InstallRoot, relative), overwrite: true);
            }

            if (movedAside && !File.Exists(running))
            {
                File.Move(OldLauncher, running);
            }

            throw;
        }
    }

    private static bool IsKept(string relative) =>
        KeptFolders.Any(folder => relative.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Could not delete {path}: {error.Message}");
        }
    }

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

    private static void Tell(string message, uint icon) => Program.MessageBox(IntPtr.Zero, message, "Poe2Access", icon | MessageBoxForeground);
}
