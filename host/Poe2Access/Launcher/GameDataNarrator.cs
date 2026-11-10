namespace Poe2Access;

/// <summary>Says that the game data is being regenerated, how far it is, and when it is done, in the plugin's language.</summary>
internal sealed class GameDataNarrator : IDisposable
{
    private const int ProgressSeconds = 30;
    private const int FinalLineHoldMilliseconds = 2_000;

    private readonly LauncherVoice voice;
    private readonly int total;
    private readonly ProgressPacer pacer = new(TimeSpan.FromSeconds(ProgressSeconds));
    private int done;
    private int failed;

    private GameDataNarrator(LauncherVoice voice, int total)
    {
        this.voice = voice;
        this.total = total;
    }

    /// <summary>Opens PRISM from the plugin folder and says the opening line; silent (logged) when PRISM is not there.</summary>
    public static GameDataNarrator Start(string pluginFolder, int total)
    {
        var narrator = new GameDataNarrator(new LauncherVoice(pluginFolder), total);
        var now = DateTime.UtcNow;
        narrator.pacer.Start(now);
        narrator.voice.Say(StartupPhrases.Starting, interrupt: true);
        return narrator;
    }

    public void StepDone() => Interlocked.Increment(ref this.done);

    /// <summary>A step kept its earlier data: the closing line says how many did, so "ready" is not said of a failed update.</summary>
    public void StepFailed() => Interlocked.Increment(ref this.failed);

    /// <summary>Called while waiting: says the progress when a full interval has passed since the last line.</summary>
    public void Poll()
    {
        var count = Volatile.Read(ref this.done);
        var now = DateTime.UtcNow;
        if (this.pacer.Due(count, now))
        {
            this.voice.Say(StartupPhrases.Progress, false, count, this.total);
        }
    }

    public void Dispose()
    {
        var kept = Volatile.Read(ref this.failed);
        if (kept > 0)
        {
            this.voice.Say(StartupPhrases.Partial, false, kept, this.total);
        }
        else
        {
            this.voice.Say(StartupPhrases.Ready);
        }

        this.voice.Dispose(FinalLineHoldMilliseconds);
    }
}
