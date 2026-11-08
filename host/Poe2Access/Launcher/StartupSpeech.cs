namespace Poe2Access;

using System.Collections.Concurrent;
using Poe2Accessibility.Services;

/// <summary>
///     Says a few lines through the plugin folder's prism.dll while the game data is regenerated, then lets PRISM go,
///     since the plugin initialises it again. Every PRISM call runs on one worker thread, as in the plugin.
/// </summary>
internal sealed class StartupSpeech : IDisposable
{
    private const int ShutdownWaitMilliseconds = 3_000;

    private readonly BlockingCollection<(string Text, bool Interrupt)> queue = [];
    private readonly Thread worker;
    private readonly PrismLibrary prism;
    private volatile int holdMilliseconds;

    private StartupSpeech(PrismLibrary prism)
    {
        this.prism = prism;
        this.worker = new Thread(this.Run) { IsBackground = true, Name = "Poe2Access startup speech" };
        this.worker.Start();
    }

    /// <summary>Null (after logging) when the plugin folder has no prism.dll or it cannot be loaded.</summary>
    public static StartupSpeech? TryCreate(string pluginFolder)
    {
        var path = Path.Join(pluginFolder, PrismLibrary.FileName);
        if (!File.Exists(path))
        {
            Log.Write($"No {PrismLibrary.FileName} in {pluginFolder}: the game data update is not spoken");
            return null;
        }

        try
        {
            return new StartupSpeech(PrismLibrary.Load(path));
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException)
        {
            Log.Write($"PRISM not loaded, the game data update is not spoken: {error.Message}");
            return null;
        }
    }

    public void Speak(string text, bool interrupt = false)
    {
        if (!this.queue.IsAddingCompleted)
        {
            this.queue.Add((text, interrupt));
        }
    }

    /// <summary>Lets the queued lines finish, then releases PRISM: the plugin starts it afresh.</summary>
    public void Dispose() => this.Dispose(0);

    /// <param name="holdMilliseconds">How long the last line is left to be heard before PRISM goes.</param>
    public void Dispose(int holdMilliseconds)
    {
        this.holdMilliseconds = holdMilliseconds;
        this.queue.CompleteAdding();
        if (!this.worker.Join(ShutdownWaitMilliseconds + holdMilliseconds))
        {
            Log.Write("The startup speech did not stop in time");
        }
    }

    private void Run()
    {
        var failure = this.prism.Start();
        if (failure is null)
        {
            Log.Write($"PRISM {this.prism.Version}, backend: {this.prism.BackendName}"
                + (PrismLibrary.WineVersion() is { } wine ? $" (under Wine {wine})" : string.Empty));
        }
        else
        {
            Log.Write($"PRISM {this.prism.Version}: {failure}: the game data update is not spoken");
        }

        var spoke = false;
        foreach (var (text, interrupt) in this.queue.GetConsumingEnumerable())
        {
            if (failure is null)
            {
                spoke = true;
                var result = this.prism.Speak(text, interrupt);
                Log.Write($"Spoken ({result}): {text}");
            }
        }

        if (spoke)
        {
            Thread.Sleep(this.holdMilliseconds);
        }

        this.prism.Dispose();
    }
}
