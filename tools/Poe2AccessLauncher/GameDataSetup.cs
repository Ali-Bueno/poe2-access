using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>
///     Packaged builds never ship game data: Poe2UiExtract (beside the launcher) regenerates the catalogs and sounds the
///     plugin reads from the player's own install on the first start and after every game patch, and a mod update reruns
///     only the steps it changed. All of it stays in the mod's folder, where no package overwrites it.
/// </summary>
internal static class GameDataSetup
{
    private const string ExtractorExe = "Poe2UiExtract.exe";
    private const int StepTimeoutMilliseconds = 180_000;
    private const string DataFolder = "GameData"; // beside GameHelper: the catalogs made here, kept over the plugin's shipped copies
    private const string StampName = "game-version.txt";
    private const string ItemSoundsStep = "item sounds";
    private const string SoundsStep = "sounds";
    private const string SoundsManifest = "manifest.txt";

    /// <param name="Version">Raise it when the step's output changes: that step alone reruns, once, on every machine.</param>
    /// <param name="Output">The catalog file it makes, or null for the sounds (the plugin's own folder, never shipped).</param>
    /// <param name="Optional">Its failure keeps the previous file and still counts as done: a patch that moves the
    /// tables these read must not rerun the step on every start (review 2026-10-25).</param>
    /// <param name="Input">The step whose output it reads: it reruns whenever that one does.</param>
    private sealed record Step(string Name, int Version, string? Output, Func<string?> Run, bool Optional = false, string? Input = null);

    public static void EnsureCurrent(string hostDirectory, string? gameDirectory)
    {
        var extractor = Path.Join(hostDirectory, ExtractorExe);
        if (!File.Exists(extractor))
        {
            return; // a development checkout extracts by hand (tools/README.md)
        }

        if (gameDirectory is null)
        {
            Log.Write("Game folder unknown: game data not checked, the mod keeps what it has");
            return;
        }

        var install = DescribeInstall(gameDirectory);
        if (install is null)
        {
            Log.Write($"Not a Path of Exile 2 install, game data not checked: {gameDirectory}");
            return;
        }

        try
        {
            Regenerate(hostDirectory, extractor, gameDirectory, install);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Game data not checked, the mod keeps what it has: {error.Message}");
        }
    }

    private static void Regenerate(string hostDirectory, string extractor, string gameDirectory, string install)
    {
        var plugin = Path.Join(hostDirectory, "Plugins", "Poe2Accessibility");
        var pluginCatalog = Path.Join(plugin, "catalog");
        var sounds = Path.Join(plugin, "sounds");
        var data = Path.Join(hostDirectory, DataFolder);
        var catalog = Path.Join(data, "catalog");
        var stampFile = Path.Join(data, StampName);
        Directory.CreateDirectory(catalog);
        var uiScripts = Path.Join(Path.GetTempPath(), "poe2-ui-" + Environment.ProcessId);
        Step Catalog(string name, int version, string file, string command, bool optional = false) =>
            new(name, version, file, () => ReplaceFile(extractor, Path.Join(catalog, file), temp => [command, gameDirectory, temp]), optional);
        var steps = new[]
        {
            Catalog(ItemSoundsStep, 1, "item-sounds.json", "item-sounds"),
            Catalog("area names", 1, "area-names.json", "areas"),
            Catalog("dat columns", 1, "dat-columns.json", "dat-columns"),
            Catalog("experience", 1, "experience.json", "experience"),
            Catalog("hit times", 1, "hit-times.txt", "hit-times", true),
            Catalog("places", 1, "places.json", "places", true),
            new Step("UI catalog", 1, "ui-catalog.json", () => BuildUiCatalog(extractor, gameDirectory, uiScripts, Path.Join(catalog, "ui-catalog.json"))),
            new Step(SoundsStep, 1, null, () => ReplaceSounds(extractor, gameDirectory, sounds, Path.Join(pluginCatalog, "item-sounds.json")), Input: ItemSoundsStep),
        };

        var stamp = LoadStamp(stampFile, install, pluginCatalog, catalog, steps);
        var stale = new HashSet<string>();
        foreach (var step in steps)
        {
            var missing = step.Output is { } output ? !step.Optional && !File.Exists(Path.Join(catalog, output)) : !File.Exists(Path.Join(sounds, SoundsManifest));
            if (missing || !stamp.IsCurrent(install, step.Name, step.Version) || (step.Input is { } input && stale.Contains(input)))
            {
                stale.Add(step.Name);
            }
            else if (step.Output is { } current)
            {
                CopyIntoPlugin(catalog, pluginCatalog, current); // an update has just put the shipped copy back
            }
        }

        if (stale.Count == 0)
        {
            return;
        }

        Log.Write($"Game data is {(stamp.Steps.Count == 0 ? "missing" : "out of date")}: regenerating {string.Join(", ", stale)} from {gameDirectory}");
        foreach (var step in steps.Where(step => stale.Contains(step.Name)))
        {
            var watch = Stopwatch.StartNew();
            string? error;
            try
            {
                error = step.Run();
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            Log.Write(error is null ? $"Game data: {step.Name} done in {watch.Elapsed.TotalSeconds:0.0} s" : $"Game data: {step.Name} failed, previous kept ({error})");
            if (error is null && step.Output is { } output)
            {
                CopyIntoPlugin(catalog, pluginCatalog, output);
            }

            if (error is null || step.Optional)
            {
                stamp = stamp.With(install, step.Name, step.Version);
                File.WriteAllText(stampFile, stamp.ToString());
            }
        }
    }

    /// <summary>The stamp in GameData; the first time, what the old layout's stamp vouches for, with those catalogs moved in.</summary>
    private static GameDataStamp LoadStamp(string stampFile, string install, string pluginCatalog, string catalog, Step[] steps)
    {
        if (File.Exists(stampFile))
        {
            return GameDataStamp.Parse(File.ReadAllText(stampFile));
        }

        var legacy = Path.Join(pluginCatalog, StampName);
        var stamp = GameDataStamp.FromLegacy(File.Exists(legacy) ? File.ReadAllText(legacy) : null, install, steps.Select(step => step.Name), SoundsStep);
        foreach (var step in steps.Where(step => step.Output is not null && stamp.Steps.ContainsKey(step.Name)))
        {
            var shipped = Path.Join(pluginCatalog, step.Output);
            if (File.Exists(shipped))
            {
                File.Copy(shipped, Path.Join(catalog, step.Output), overwrite: true);
            }
        }

        File.WriteAllText(stampFile, stamp.ToString());
        File.Delete(legacy);
        if (stamp.Steps.Count > 0)
        {
            Log.Write($"Game data: {string.Join(", ", stamp.Steps.Keys)} carried over from the previous layout");
        }

        return stamp;
    }

    /// <summary>Puts a catalog made from this install over the plugin's copy when they differ (a copy keeps the write time).</summary>
    private static void CopyIntoPlugin(string catalog, string pluginCatalog, string file)
    {
        var made = new FileInfo(Path.Join(catalog, file));
        var used = new FileInfo(Path.Join(pluginCatalog, file));
        if (!made.Exists || (used.Exists && used.Length == made.Length && used.LastWriteTimeUtc == made.LastWriteTimeUtc))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(pluginCatalog);
            made.CopyTo(used.FullName, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Game data: {file} not copied into the plugin ({error.Message})");
        }
    }

    // The index lists every bundle, so its size and time move with each content patch; the game executable (whose
    // version resource is empty) and fmod.dll cover patches that leave the bundles alone.
    private static string? DescribeInstall(string gameDirectory)
    {
        var index = new FileInfo(Path.Join(gameDirectory, "Bundles2", "_.index.bin"));
        if (!index.Exists)
        {
            return null;
        }

        var executable = Directory.GetFiles(gameDirectory, "PathOfExile*.exe").Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        var fmod = Path.Join(gameDirectory, "fmod.dll");
        return $"index={Describe(index)}\nexe={(executable is null ? "-" : Describe(new FileInfo(executable)))}\n" +
               $"fmod={(File.Exists(fmod) ? FileVersionInfo.GetVersionInfo(fmod).ProductVersion : "-")}\n";
    }

    private static string Describe(FileInfo file) => $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";

    private static string? ReplaceFile(string extractor, string target, Func<string, string[]> arguments)
    {
        var temp = target + ".new";
        File.Delete(temp);
        var error = Run(extractor, arguments(temp));
        return error ?? Promote(temp, target);
    }

    private static string? BuildUiCatalog(string extractor, string gameDirectory, string scripts, string target)
    {
        try
        {
            var error = Run(extractor, [Path.Join(gameDirectory, "Bundles2", "_.index.bin"), scripts]);
            return error ?? ReplaceFile(extractor, target, temp => ["catalog", scripts, temp]);
        }
        finally
        {
            if (Directory.Exists(scripts))
            {
                Directory.Delete(scripts, true);
            }
        }
    }

    private static string? ReplaceSounds(string extractor, string gameDirectory, string sounds, string itemSounds)
    {
        var fresh = sounds + ".new";
        var old = sounds + ".old";
        foreach (var folder in new[] { fresh, old })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        var error = Run(extractor, ["sounds", gameDirectory, fresh, itemSounds]);
        if (error is null && !File.Exists(Path.Join(fresh, SoundsManifest)))
        {
            error = "no manifest written";
        }

        if (error is not null)
        {
            return error;
        }

        if (Directory.Exists(sounds))
        {
            Directory.Move(sounds, old);
        }

        Directory.Move(fresh, sounds);
        if (Directory.Exists(old))
        {
            Directory.Delete(old, true);
        }

        return null;
    }

    private static string? Promote(string temp, string target)
    {
        if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
        {
            File.Delete(temp);
            return "empty output";
        }

        File.Move(temp, target, true);
        return null;
    }

    private static string? Run(string extractor, string[] arguments)
    {
        var info = new ProcessStartInfo(extractor)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(extractor)!,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Poe2UiExtract did not start.");
        process.OutputDataReceived += (_, _) => { }; // drained, not kept: the extractor lists every path it sees
        process.BeginOutputReadLine();
        if (!process.WaitForExit(StepTimeoutMilliseconds))
        {
            process.Kill(true);
            return "timed out";
        }

        process.WaitForExit(); // flush the output handlers
        return process.ExitCode == 0 ? null : $"exit code {process.ExitCode}";
    }
}
