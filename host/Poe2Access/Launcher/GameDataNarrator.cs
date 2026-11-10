namespace Poe2Access;

using Poe2Accessibility;

/// <summary>Says that the game data is being regenerated, how far it is, and when it is done, in the plugin's language.</summary>
internal sealed class GameDataNarrator : IDisposable
{
    private const string SettingsFile = "settings.txt";
    private const string ConfigFolder = "config";
    private const string PhraseFolder = "catalog/lang";
    private const int ProgressSeconds = 30;
    private const int FinalLineHoldMilliseconds = 2_000;

    private readonly StartupSpeech? speech;
    private readonly string language;
    private readonly IReadOnlyDictionary<string, string>? table;
    private readonly int total;
    private readonly ProgressPacer pacer = new(TimeSpan.FromSeconds(ProgressSeconds));
    private int done;
    private int failed;

    private GameDataNarrator(StartupSpeech? speech, string language, IReadOnlyDictionary<string, string>? table, int total)
    {
        this.speech = speech;
        this.language = language;
        this.table = table;
        this.total = total;
    }

    /// <summary>Opens PRISM from the plugin folder and says the opening line; silent (logged) when PRISM is not there.</summary>
    public static GameDataNarrator Start(string pluginFolder, int total)
    {
        var settings = Path.Join(pluginFolder, ConfigFolder, SettingsFile);
        var language = StartupPhrases.ResolveLanguage(
            File.Exists(settings) ? File.ReadAllText(settings) : null, Localization.GameLanguage(), System.Globalization.CultureInfo.CurrentUICulture.Name);
        var table = StartupPhrases.LoadTable(Path.Join(pluginFolder, PhraseFolder, language + ".json"));
        var narrator = new GameDataNarrator(StartupSpeech.TryCreate(pluginFolder), language, table, total);
        Log.Write($"Game data update spoken in '{language}'");
        narrator.pacer.Start(DateTime.UtcNow);
        narrator.speech?.Speak(StartupPhrases.Text(StartupPhrases.Starting, language, table), interrupt: true);
        return narrator;
    }

    public void StepDone() => Interlocked.Increment(ref this.done);

    /// <summary>A step kept its earlier data: the closing line says how many did, so "ready" is not said of a failed update.</summary>
    public void StepFailed() => Interlocked.Increment(ref this.failed);

    /// <summary>Called while waiting: says the progress when a full interval has passed since the last line.</summary>
    public void Poll()
    {
        var count = Volatile.Read(ref this.done);
        if (this.pacer.Due(count, DateTime.UtcNow))
        {
            this.speech?.Speak(StartupPhrases.Format(StartupPhrases.Progress, this.language, this.table, count, this.total));
        }
    }

    public void Dispose()
    {
        var kept = Volatile.Read(ref this.failed);
        this.speech?.Speak(kept > 0
            ? StartupPhrases.Format(StartupPhrases.Partial, this.language, this.table, kept, this.total)
            : StartupPhrases.Text(StartupPhrases.Ready, this.language, this.table));
        if (this.speech is not null)
        {
            this.speech.Dispose(FinalLineHoldMilliseconds);
        }
    }
}
