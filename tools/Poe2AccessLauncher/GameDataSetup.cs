using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>
///     First start of a packaged build: the game's own sounds are never shipped, so Poe2UiExtract (packaged beside the
///     launcher) extracts them from the player's install into the plugin's sounds folder; about a second's work.
/// </summary>
internal static class SoundSetup
{
    private const string ExtractorExe = "Poe2UiExtract.exe";
    private const int TimeoutMilliseconds = 120_000;

    public static void EnsureExtracted(string hostDirectory, string? gameDirectory)
    {
        var extractor = Path.Join(hostDirectory, ExtractorExe);
        var plugin = Path.Join(hostDirectory, "Plugins", "Poe2Accessibility");
        var sounds = Path.Join(plugin, "sounds");
        if (!File.Exists(extractor) || File.Exists(Path.Join(sounds, "manifest.txt")))
        {
            return; // a development checkout extracts by hand (tools/README.md), or this was done before
        }

        if (gameDirectory is null)
        {
            Log.Write("Game folder unknown: game sounds not extracted, the mod plays its own cues only");
            return;
        }

        var info = new ProcessStartInfo(extractor) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = hostDirectory };
        foreach (var argument in new[] { "sounds", gameDirectory, sounds, Path.Join(plugin, "catalog", "item-sounds.json") })
        {
            info.ArgumentList.Add(argument);
        }

        Log.Write($"Extracting the game's sounds from {gameDirectory}");
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Poe2UiExtract did not start.");
        if (!process.WaitForExit(TimeoutMilliseconds))
        {
            process.Kill();
            Log.Write("Sound extraction timed out");
            return;
        }

        Log.Write($"Sound extraction finished (exit code {process.ExitCode})");
    }
}
