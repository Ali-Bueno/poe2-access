namespace Poe2Access;

/// <summary>Says that the game data is being regenerated, how far it is, and when it is done, in the plugin's language.</summary>
internal sealed class GameDataNarrator : IDisposable
{
    private const int ProgressSeconds = 30;
    private const int FirstSilenceSeconds = 30;
    private const int SilenceEverySeconds = 60;
    private const int FinalLineHoldMilliseconds = 2_000;

    private readonly LauncherVoice voice;
    private readonly string[] order;
    private readonly ProgressPacer pacer = new(TimeSpan.FromSeconds(ProgressSeconds));
    private readonly SilenceReminder silence = new(TimeSpan.FromSeconds(FirstSilenceSeconds), TimeSpan.FromSeconds(SilenceEverySeconds));
    private int done;
    private int failed;

    private GameDataNarrator(LauncherVoice voice, string[] order)
    {
        this.voice = voice;
        this.order = order;
    }

    /// <summary>Opens PRISM from the plugin folder and says the opening line; silent (logged) when PRISM is not there.</summary>
    /// <param name="order">The steps in the order they run, so a silent spell can say which one works.</param>
    public static GameDataNarrator Start(string pluginFolder, string[] order)
    {
        var narrator = new GameDataNarrator(new LauncherVoice(pluginFolder), order);
        var now = DateTime.UtcNow;
        narrator.pacer.Start(now);
        narrator.silence.Start(0, now);
        narrator.voice.Say(StartupPhrases.Starting, interrupt: true);
        return narrator;
    }

    public void StepDone() => Interlocked.Increment(ref this.done);

    /// <summary>A step kept its earlier data: the closing line says how many did, so "ready" is not said of a failed update.</summary>
    public void StepFailed() => Interlocked.Increment(ref this.failed);

    /// <summary>Called while waiting: says the progress when a full interval has passed since the last line, or that a step still works when the count stays put.</summary>
    public void Poll()
    {
        var count = Volatile.Read(ref this.done);
        var now = DateTime.UtcNow;
        if (this.pacer.Due(count, now))
        {
            this.voice.Say(StartupPhrases.Progress, false, count, this.order.Length);
        }

        if (this.silence.Due(count, now) is { } minutes && this.order.Length > 0)
        {
            this.voice.Say(StartupPhrases.StepSilent, false, this.order[Math.Min(count, this.order.Length - 1)], minutes);
        }
    }

    public void Dispose()
    {
        var kept = Volatile.Read(ref this.failed);
        if (kept > 0)
        {
            this.voice.Say(StartupPhrases.Partial, false, kept, this.order.Length);
        }
        else
        {
            this.voice.Say(StartupPhrases.Ready);
        }

        this.voice.Dispose(FinalLineHoldMilliseconds);
    }
}
