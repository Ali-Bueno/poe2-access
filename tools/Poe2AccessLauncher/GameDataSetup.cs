using System.Diagnostics;

namespace Poe2AccessLauncher;

/// <summary>
///     Packaged builds never ship game data: Poe2UiExtract (beside the launcher) regenerates the catalogs and sounds the
///     plugin reads from the player's own install on the first start and after every game patch.
/// </summary>
internal static class GameDataSetup
{
    private const string ExtractorExe = "Poe2UiExtract.exe";
    private const int StepTimeoutMilliseconds = 180_000;
    private const int StampFormat = 3; // bump when the regeneration steps change, to rebuild once on every machine

    /// <param name="Optional">Its failure keeps the shipped file and still lets the stamp be written: a patch that moves
    /// the tables these read must not rerun every step on every start (review 2026-10-25).</param>
    private sealed record Step(string Name, Func<string?> Run, bool Optional = false);

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

        var plugin = Path.Join(hostDirectory, "Plugins", "Poe2Accessibility");
        var catalog = Path.Join(plugin, "catalog");
        var sounds = Path.Join(plugin, "sounds");
        var stampFile = Path.Join(catalog, "game-version.txt");
        var stamp = ComputeStamp(gameDirectory);
        if (stamp is null)
        {
            Log.Write($"Not a Path of Exile 2 install, game data not checked: {gameDirectory}");
            return;
        }

        var current = ReadStamp(stampFile);
        if (current == stamp && File.Exists(Path.Join(sounds, "manifest.txt")))
        {
            return;
        }

        Log.Write($"Game data is {(current is null ? "missing" : "out of date")}: regenerating from {gameDirectory}");
        Directory.CreateDirectory(catalog);
        var itemSounds = Path.Join(catalog, "item-sounds.json");
        var uiScripts = Path.Join(Path.GetTempPath(), "poe2-ui-" + Environment.ProcessId);
        var steps = new[]
        {
            new Step("item sounds", () => ReplaceFile(extractor, itemSounds, temp => ["item-sounds", gameDirectory, temp])),
            new Step("area names", () => ReplaceFile(extractor, Path.Join(catalog, "area-names.json"), temp => ["areas", gameDirectory, temp])),
            new Step("dat columns", () => ReplaceFile(extractor, Path.Join(catalog, "dat-columns.json"), temp => ["dat-columns", gameDirectory, temp])),
            new Step("experience", () => ReplaceFile(extractor, Path.Join(catalog, "experience.json"), temp => ["experience", gameDirectory, temp])),
            new Step("hit times", () => ReplaceFile(extractor, Path.Join(catalog, "hit-times.txt"), temp => ["hit-times", gameDirectory, temp]), true),
            new Step("places", () => ReplaceFile(extractor, Path.Join(catalog, "places.json"), temp => ["places", gameDirectory, temp]), true),
            new Step("UI catalog", () => BuildUiCatalog(extractor, gameDirectory, uiScripts, Path.Join(catalog, "ui-catalog.json"))),
            new Step("sounds", () => ReplaceSounds(extractor, gameDirectory, sounds, itemSounds)),
        };

        var allDone = true;
        foreach (var step in steps)
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

            allDone &= error is null || step.Optional;
            Log.Write(error is null ? $"Game data: {step.Name} done in {watch.Elapsed.TotalSeconds:0.0} s" : $"Game data: {step.Name} failed, previous kept ({error})");
        }

        if (allDone)
        {
            File.WriteAllText(stampFile, stamp);
        }
    }

    // The index lists every bundle, so its size and time move with each content patch; the game executable (whose
    // version resource is empty) and fmod.dll cover patches that leave the bundles alone.
    private static string? ComputeStamp(string gameDirectory)
    {
        var index = new FileInfo(Path.Join(gameDirectory, "Bundles2", "_.index.bin"));
        if (!index.Exists)
        {
            return null;
        }

        var executable = Directory.GetFiles(gameDirectory, "PathOfExile*.exe").Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        var fmod = Path.Join(gameDirectory, "fmod.dll");
        return $"format={StampFormat}\nindex={Describe(index)}\nexe={(executable is null ? "-" : Describe(new FileInfo(executable)))}\n" +
               $"fmod={(File.Exists(fmod) ? FileVersionInfo.GetVersionInfo(fmod).ProductVersion : "-")}\n";
    }

    private static string Describe(FileInfo file) => $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";

    private static string? ReadStamp(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

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
        if (error is null && !File.Exists(Path.Join(fresh, "manifest.txt")))
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
