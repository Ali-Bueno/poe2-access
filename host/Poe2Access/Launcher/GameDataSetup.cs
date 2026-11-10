using System.Diagnostics;
using System.Runtime;
using Step = Poe2Access.GameDataSteps.Step;

namespace Poe2Access;

/// <summary>
///     Packaged builds never ship game data: Poe2UiExtract (a library of this program) regenerates the catalogs and sounds the
///     plugin reads from the player's own install on the first start and after every game patch, and a mod update reruns
///     only the steps it changed. All of it stays in the mod's folder, where no package overwrites it.
/// </summary>
internal static class GameDataSetup
{
    private const int StepTimeoutMilliseconds = 180_000;

    /// <summary>
    ///     The whole batch's wait: 3 minutes a step added up to an hour for 22 steps, all of it in silence if one hung. The
    ///     batch takes about 30 s here (2026-11-08; 150 s before the path index), so ten minutes leaves a slow disk its time;
    ///     what has not finished then is not stamped and is retried on the next start.
    /// </summary>
    private const int BatchTimeoutMilliseconds = 600_000;
    private const int PollMilliseconds = 1_000; // how often the wait lets the narrator speak
    private const string DataFolder = "GameData"; // beside the host: the catalogs made here, kept over the plugin's shipped copies
    private const string StampName = "game-version.txt";
    private const string SoundsStep = GameDataSteps.Sounds;
    private const string SoundsManifest = "manifest.txt";
    private const string NewSuffix = ".new"; // an output before it replaces the previous one
    private const string FailedStepsFile = "game-data-failed.txt";
    private static GameDataNarrator? narrator;


    public static void EnsureCurrent(AppFolders folders, string? gameDirectory)
    {
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
            Regenerate(folders, gameDirectory, install);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Log.Write($"Game data not checked, the mod keeps what it has: {error.Message}");
        }
        finally
        {
            ReleaseMemory();
        }
    }

    private static void Regenerate(AppFolders folders, string gameDirectory, string install)
    {
        var plugin = folders.Plugin("Poe2Accessibility");
        var pluginCatalog = Path.Join(plugin, "catalog");
        var sounds = Path.Join(plugin, "sounds");
        var data = Path.Join(folders.Data, DataFolder);
        var catalog = Path.Join(data, "catalog");
        var stampFile = Path.Join(data, StampName);
        Directory.CreateDirectory(catalog);
        var steps = GameDataSteps.All;

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
        var watch = Stopwatch.StartNew();
        var failed = new List<string>();
        using var spoken = GameDataNarrator.Start(plugin, stale.Count);
        narrator = spoken;
        void Finish(Step step, string? error)
        {
            if (error is not null)
            {
                Log.Write($"Game data: {step.Name} failed, previous kept ({error})");
                var listed = error == Extractor.BundleIndexName ? error : step.Name; // the index failing fails every step: one entry says it
                if (!failed.Contains(listed))
                {
                    failed.Add(listed);
                }
            }
            else if (step.Output is { } output)
            {
                CopyIntoPlugin(catalog, pluginCatalog, output);
            }

            if (step.Command is null)
            {
                spoken.StepDone(); // the catalogs are counted as the batch reports them
            }

            if (error is null)
            {
                stamp = stamp.With(install, step.Name, step.Version);
                File.WriteAllText(stampFile, stamp.ToString());
            }
            else if (step.Optional)
            {
                // Unstamped, so the next start retries it, up to GameDataStamp.MaxAttempts starts.
                stamp = stamp.WithFailure(install, step.Name, step.Version);
                File.WriteAllText(stampFile, stamp.ToString());
                Log.Write(stamp.GivenUp().Contains(step.Name)
                    ? $"Game data: {step.Name} failed on {GameDataStamp.MaxAttempts} starts: given up for this game build and listed as failed"
                    : $"Game data: {step.Name} failed on start {stamp.Attempts(step.Name, step.Version)} of {GameDataStamp.MaxAttempts}; retried on the next start");
            }
        }

        // One opened index for the batch and the object sounds after it: opening it twice was 12 s of a first run.
        using var index = Extractor.KeepIndexOpen();
        var catalogs = steps.Where(step => step.Command is not null && stale.Contains(step.Name)).ToList();
        if (catalogs.Count > 0)
        {
            var made = catalogs.Select(step => Path.Join(catalog, step.Output)).ToList();
            var error = Guarded(() => RunBatch(gameDirectory, catalogs.Select(step => step.Command!).Zip(made.Select(file => file + NewSuffix))));
            foreach (var (step, file) in catalogs.Zip(made))
            {
                var promoted = Promote(file + NewSuffix, file);
                Finish(step, promoted is null ? null : error ?? promoted);
            }
        }

        if (stale.Contains(SoundsStep))
        {
            Finish(steps.Single(step => step.Name == SoundsStep), Guarded(() => ReplaceSounds(gameDirectory, sounds, Path.Join(pluginCatalog, "item-sounds.json"))));
        }

        // The plugin says it: a catalog left behind by a patch would otherwise only show in this log.
        var failedFile = Path.Join(pluginCatalog, FailedStepsFile);
        failed.AddRange(stamp.GivenUp().Where(name => !failed.Contains(name))); // given up on an earlier start, still not working
        if (failed.Count > 0)
        {
            File.WriteAllLines(failedFile, failed);
        }
        else
        {
            File.Delete(failedFile);
        }

        narrator = null;
        Log.Write($"Game data: regenerated in {watch.Elapsed.TotalSeconds:0.0} s");
    }

    private static string? Guarded(Func<string?> run)
    {
        try
        {
            return run();
        }
        catch (Exception exception)
        {
            return exception.Message;
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

    // The index (in the Korean client, the Content.ggpk holding it) lists every bundle, so its size and time move with
    // each content patch; the game executable (whose version resource is empty) and fmod.dll cover patches that leave
    // the bundles alone.
    private static string? DescribeInstall(string gameDirectory)
    {
        if (GameInstall.ContentFile(gameDirectory) is not { } content)
        {
            return null;
        }

        var index = new FileInfo(content);
        var executable = Directory.GetFiles(gameDirectory, "PathOfExile*.exe").Order(StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        var fmod = Path.Join(gameDirectory, "fmod.dll");
        return $"index={Describe(index)}\nexe={(executable is null ? "-" : Describe(new FileInfo(executable)))}\n" +
               $"fmod={(File.Exists(fmod) ? FileVersionInfo.GetVersionInfo(fmod).ProductVersion : "-")}\n";
    }

    private static string Describe(FileInfo file) => $"{file.Length}:{file.LastWriteTimeUtc.Ticks}";

    /// <summary>One extractor run for all the stale catalogs: it opens the game's index once and makes them side by side.</summary>
    private static string? RunBatch(string gameDirectory, IEnumerable<(string Command, string Output)> jobs)
    {
        var list = jobs.ToList();
        var leftovers = list.SelectMany(job => new[] { job.Output, job.Output + Extractor.BatchPartSuffix }).ToList();
        leftovers.ForEach(File.Delete);
        try
        {
            return Run(["batch", gameDirectory, .. list.SelectMany(job => new[] { job.Command, job.Output })], Math.Min(StepTimeoutMilliseconds * list.Count, BatchTimeoutMilliseconds),
                line =>
                {
                    if (line.StartsWith(Extractor.BatchLinePrefix, StringComparison.Ordinal))
                    {
                        Log.Write($"Game data: {line[Extractor.BatchLinePrefix.Length..]}");
                        narrator?.StepDone();
                    }
                    else if (line.StartsWith(Extractor.BatchIndexLinePrefix, StringComparison.Ordinal))
                    {
                        Log.Write($"Game data: bundle index {line[Extractor.BatchIndexLinePrefix.Length..]}");
                    }
                });
        }
        finally
        {
            // A timed-out run's unfinished outputs; the abandoned worker may still hold one open, which the next start clears.
            foreach (var part in leftovers.Where(file => file.EndsWith(Extractor.BatchPartSuffix, StringComparison.Ordinal)))
            {
                try
                {
                    File.Delete(part);
                }
                catch (IOException error)
                {
                    Log.Write($"Could not delete {part}: {error.Message}");
                }
            }
        }
    }

    private static string? ReplaceSounds(string gameDirectory, string sounds, string itemSounds)
    {
        var fresh = sounds + ".new";
        var old = sounds + ".old";
        if (Directory.Exists(old) && !Directory.Exists(sounds))
        {
            Directory.Move(old, sounds); // a run stopped between the two moves below
        }

        foreach (var folder in new[] { fresh, old })
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        var error = Run(["sounds", gameDirectory, fresh, itemSounds], StepTimeoutMilliseconds, line =>
        {
            if (line.StartsWith(Extractor.SoundsMissingPrefix, StringComparison.Ordinal))
            {
                Log.Write($"Game data: sounds {line}"); // the cues a bank no longer holds, by name
            }
        });
        if (error is null && !File.Exists(Path.Join(fresh, SoundsManifest)))
        {
            error = "no manifest written";
        }

        if (error is null && ShortOfManifest(fresh) is { } short_)
        {
            error = short_;
        }

        if (error is not null)
        {
            return error;
        }

        // Apart: failing costs only the objects' own sounds, and they fall back on the common break.
        if (Run(["object-sounds", gameDirectory, fresh], StepTimeoutMilliseconds) is { } objectError)
        {
            Log.Write($"Game data: object sounds failed: {objectError}");
        }

        if (Directory.Exists(sounds))
        {
            Directory.Move(sounds, old);
        }

        try
        {
            Directory.Move(fresh, sounds);
        }
        catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
        {
            // A file still open in the new folder (a timed-out step's worker, a virus scan): the previous sounds stay.
            if (Directory.Exists(old) && !Directory.Exists(sounds))
            {
                Directory.Move(old, sounds);
            }

            throw;
        }

        if (Directory.Exists(old))
        {
            Directory.Delete(old, true);
        }

        return null;
    }

    /// <summary>Whether the extracted folder holds fewer samples than its manifest lists (a write cut short); the cause, or null.</summary>
    private static string? ShortOfManifest(string folder)
    {
        var listed = File.ReadLines(Path.Join(folder, SoundsManifest)).Count(line => line.Length > 0 && line[0] != '#');
        var written = Directory.EnumerateFiles(folder, "*.wav", SearchOption.AllDirectories).Count();
        return written < listed ? $"{written} samples written, {listed} listed" : null;
    }

    private static string? Promote(string temp, string target)
    {
        if (!File.Exists(temp) || new FileInfo(temp).Length == 0)
        {
            File.Delete(temp);
            return "empty output";
        }

        if (File.Exists(target) && GameDataStamp.Collapsed(new FileInfo(target).Length, new FileInfo(temp).Length))
        {
            var sizes = $"{new FileInfo(target).Length} to {new FileInfo(temp).Length} bytes";
            File.Delete(temp);
            return $"output fell from {sizes}, a column probably moved";
        }

        if (GameDataCounts.Problem(Path.GetFileName(target), File.Exists(target) ? File.ReadAllText(target) : null, File.ReadAllText(temp)) is { } problem)
        {
            File.Delete(temp);
            return problem;
        }

        File.Move(temp, target, true);
        return null;
    }

    /// <summary>Runs one extractor command in this process on a worker; gives up waiting after the timeout, as the process kill used to.</summary>
    private static string? Run(string[] arguments, int timeoutMilliseconds, Action<string>? onLine = null)
    {
        Say.Redirect(new LineWriter(onLine)); // drained: the extractor lists every path it sees
        var work = Task.Factory.StartNew(() => Extractor.Run(arguments), TaskCreationOptions.LongRunning);
        try
        {
            var clock = Stopwatch.StartNew();
            while (!work.Wait(PollMilliseconds))
            {
                narrator?.Poll();
                if (clock.ElapsedMilliseconds > timeoutMilliseconds)
                {
                    Say.Redirect(TextWriter.Null); // the abandoned worker goes on without filling the log
                    return "timed out";
                }
            }

            return work.Result == 0 ? null : work.Result == Extractor.BundleIndexExit ? Extractor.BundleIndexName : $"exit code {work.Result}";
        }
        catch (AggregateException error)
        {
            return error.GetBaseException().Message;
        }
    }

    /// <summary>The extraction allocates a lot (the index, every table): hand it back before the frame loop starts.</summary>
    private static void ReleaseMemory()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>Hands the lines the extractor prints, whole, to a callback.</summary>
    private sealed class LineWriter(Action<string>? onLine) : TextWriter
    {
        private readonly System.Text.StringBuilder line = new();

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void WriteLine(string? value) => this.Emit(value ?? string.Empty);

        public override void Write(string? value)
        {
            lock (this.line)
            {
                foreach (var character in value ?? string.Empty)
                {
                    this.Write(character);
                }
            }
        }

        /// <summary>One whole line under the lock, so lines from commands running side by side do not interleave.</summary>
        private void Emit(string text)
        {
            lock (this.line)
            {
                onLine?.Invoke(this.line.Append(text).ToString().TrimEnd('\r'));
                this.line.Clear();
            }
        }

        public override void Write(char value)
        {
            lock (this.line)
            {
                if (value == '\n')
                {
                    onLine?.Invoke(this.line.ToString().TrimEnd('\r'));
                    this.line.Clear();
                }
                else
                {
                    this.line.Append(value);
                }
            }
        }
    }
}