using System.Diagnostics;
using Index = LibBundle3.Index;

/// <summary>
///     `batch` command: several extractions on one opened index (opening it takes most of a run's time and memory), for
///     the launcher after a game patch; they run side by side. Each output appears complete or not at all.
/// </summary>
internal static class Batch
{

    /// <summary>How many commands run at once on the shared index: each holds tables and file reads in memory.</summary>
    internal static int MaxParallel { get; set; } = 3;

    private static int abandonedRuns;

    /// <summary>
    ///     Tells the batches running now that nobody waits for them any more (the launcher gave up on a step that timed out):
    ///     steps not yet started do not start and a step that ends makes no output, so the worker ends by itself within the
    ///     step it was in, and never leaves a file the launcher has already judged. A batch begun later is not affected.
    /// </summary>
    internal static void Abandon() => Interlocked.Increment(ref abandonedRuns);

    private static readonly Dictionary<string, Func<string, Index, string, int>> Commands = new(StringComparer.OrdinalIgnoreCase)
    {
        ["item-sounds"] = ItemSoundExtractor.Run,
        ["areas"] = (_, index, output) => AreaExtractor.Run(index, output),
        ["dat-columns"] = (_, index, output) => DatColumnExtractor.Run(index, output),
        ["experience"] = (_, index, output) => ExperienceExtractor.Run(index, output),
        ["hit-times"] = (_, index, output) => HitTimeExtractor.Run(index, output),
        ["hazards"] = (_, index, output) => HazardExtractor.Run(index, output),
        ["attack-reach"] = (_, index, output) => AttackReachExtractor.Run(index, output),
        ["action-types"] = (_, index, output) => ActionTypeExtractor.Run(index, output),
        ["telegraph-skills"] = (_, index, output) => TelegraphExtractor.RunSkills(index, output),
        ["telegraph-effects"] = (_, index, output) => TelegraphExtractor.RunEffects(index, output),
        ["cull-skills"] = (_, index, output) => CullSkillExtractor.Run(index, output),
        ["game-ids"] = (_, index, output) => GameIdExtractor.Run(index, output),
        ["game-keys"] = (_, index, output) => GameKeyExtractor.Run(index, output),
        ["game-names"] = (_, index, output) => GameNameExtractor.Run(index, output),
        ["buff-names"] = (_, index, output) => BuffNameExtractor.Run(index, output),
        ["passive-overrides"] = (_, index, output) => PassiveOverrideExtractor.Run(index, output),
        ["class-mechanics"] = (_, index, output) => ClassMechanicExtractor.Run(index, output),
        ["currency-art"] = (_, index, output) => CurrencyArtExtractor.Run(index, output),
        ["places"] = (_, index, output) => PlaceExtractor.Run(index, output),
        ["endgame"] = (_, index, output) => EndgameExtractor.Run(index, output),
        ["ui-catalog"] = (_, index, output) => UiScripts.BuildCatalog(index, output),
        ["trial-data"] = (_, index, output) => TrialDataExtractor.Run(index, output),
        ["quest-goals"] = (_, index, output) => QuestGoalExtractor.Run(index, output),
        ["inline-icons"] = (_, index, output) => InlineIconExtractor.Run(index, output),
        ["boss-mechanics"] = (_, index, output) => BossMechanicsExtractor.Run(index, output),
        ["gems"] = (_, index, output) => GemExtractor.Run(index, output),
        ["passive-tree"] = (_, index, output) => PassiveTreeExtractor.Run(index, output),
        ["item-bases"] = (_, index, output) => ItemBaseExtractor.Run(index, output),
        ["mod-texts"] = (_, index, output) => ModTextExtractor.Run(index, output),
        [ModStatColumns.Step] = (_, index, output) => ModStatExtractor.Run(index, output),
    };

    /// <param name="pairs">Command and output file, repeated.</param>
    /// <returns>0 when every command made its output, 1 when one did not, <see cref="BundleIndexCheck.ExitCode"/> when the index would not open.</returns>
    public static int Run(string gameDir, string[] pairs)
    {
        var commands = pairs.Where((_, i) => i % 2 == 0).ToList();
        if (pairs.Length == 0 || pairs.Length % 2 != 0 || commands.Any(command => !Commands.ContainsKey(command)))
        {
            Say.Out.WriteLine($"Usage: Poe2UiExtract.exe batch <gameDir> <command> <outFile> [...]; commands: {string.Join(", ", Commands.Keys)}");
            return 1;
        }

        IndexLease lease;
        try
        {
            lease = SharedBundles.OpenParsed(gameDir);
        }
        catch (Exception error)
        {
            // One failure for the whole batch: the steps cannot say anything the index has not.
            Say.Out.WriteLine($"{Extractor.BatchIndexLinePrefix}failed: {SingleLine(error.ToString())}");
            return BundleIndexCheck.ExitCode;
        }

        using var opened = lease;
        var generation = Volatile.Read(ref abandonedRuns);
        bool Abandoned() => Volatile.Read(ref abandonedRuns) != generation;
        var index = lease.Index;
        using var slots = new SemaphoreSlim(Math.Max(1, MaxParallel));
        var runs = pairs.Chunk(2).Select(pair => Task.Run(() =>
        {
            slots.Wait();
            try
            {
                return !Abandoned() && RunOne(gameDir, index, pair[0], pair[1], Abandoned);
            }
            finally
            {
                slots.Release();
            }
        })).ToArray();
        Task.WaitAll(runs);
        return runs.All(run => run.Result) ? 0 : 1;
    }

    // The commands only read the index once its paths are parsed, and every file read opens its own bundle stream.
    private static bool RunOne(string gameDir, Index index, string command, string output, Func<bool> abandoned)
    {
        var part = output + Extractor.BatchPartSuffix;
        var watch = Stopwatch.StartNew();
        using var capture = Say.StartCapture();
        var audit = DatColumnAudit.Start();
        var formats = GameFormats.Start();
        string? thrown = null;
        int code;
        try
        {
            File.Delete(part);
            code = Commands[command](gameDir, index, part);
            code = code == 0 && !(audit.Report(command) & formats.Report(command)) ? 1 : code;
        }
        catch (Exception error)
        {
            Say.Out.WriteLine($"{command}: {error}");
            thrown = $"{error.GetType().Name}: {error.Message}";
            code = 1;
        }

        var made = code == 0 && !abandoned() && File.Exists(part) && new FileInfo(part).Length > 0;
        if (made)
        {
            File.Move(part, output, true);
        }
        else
        {
            File.Delete(part);
        }

        // The reason goes on the result line: the launcher keeps only these lines in its log.
        var reason = made ? string.Empty : $": {SingleLine(thrown ?? capture.Reason())}";
        Say.Out.WriteLine($"{Extractor.BatchLinePrefix}{command} {(made ? "done" : "failed")} in {watch.Elapsed.TotalSeconds:0.0} s{reason}");
        return made;
    }

    private static string SingleLine(string text) => text.ReplaceLineEndings(" ");
}
