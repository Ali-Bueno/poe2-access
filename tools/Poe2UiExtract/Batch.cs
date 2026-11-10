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
        ["buff-names"] = (_, index, output) => BuffNameExtractor.Run(index, output),
        ["passive-overrides"] = (_, index, output) => PassiveOverrideExtractor.Run(index, output),
        ["class-mechanics"] = (_, index, output) => ClassMechanicExtractor.Run(index, output),
        ["currency-art"] = (_, index, output) => CurrencyArtExtractor.Run(index, output),
        ["places"] = (_, index, output) => PlaceExtractor.Run(index, output),
        ["endgame"] = (_, index, output) => EndgameExtractor.Run(index, output),
        ["ui-catalog"] = (_, index, output) => UiScripts.BuildCatalog(index, output),
        ["trial-data"] = (_, index, output) => TrialDataExtractor.Run(index, output),
        ["inline-icons"] = (_, index, output) => InlineIconExtractor.Run(index, output),
        ["boss-mechanics"] = (_, index, output) => BossMechanicsExtractor.Run(index, output),
    };

    /// <param name="pairs">Command and output file, repeated.</param>
    /// <returns>0 when every command made its output, else 1.</returns>
    public static int Run(string gameDir, string[] pairs)
    {
        var commands = pairs.Where((_, i) => i % 2 == 0).ToList();
        if (pairs.Length == 0 || pairs.Length % 2 != 0 || commands.Any(command => !Commands.ContainsKey(command)))
        {
            Say.Out.WriteLine($"Usage: Poe2UiExtract.exe batch <gameDir> <command> <outFile> [...]; commands: {string.Join(", ", Commands.Keys)}");
            return 1;
        }

        using var index = SharedBundles.OpenParsed(gameDir);
        using var slots = new SemaphoreSlim(Math.Max(1, MaxParallel));
        var runs = pairs.Chunk(2).Select(pair => Task.Run(() =>
        {
            slots.Wait();
            try
            {
                return RunOne(gameDir, index, pair[0], pair[1]);
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
    private static bool RunOne(string gameDir, Index index, string command, string output)
    {
        var part = output + Extractor.BatchPartSuffix;
        var watch = Stopwatch.StartNew();
        using var capture = Say.StartCapture();
        string? thrown = null;
        int code;
        try
        {
            File.Delete(part);
            code = Commands[command](gameDir, index, part);
        }
        catch (Exception error)
        {
            Say.Out.WriteLine($"{command}: {error}");
            thrown = $"{error.GetType().Name}: {error.Message}";
            code = 1;
        }

        var made = code == 0 && File.Exists(part) && new FileInfo(part).Length > 0;
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
