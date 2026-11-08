namespace Poe2Access;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
///     Says a few lines through the plugin folder's prism.dll while the game data is regenerated, then lets PRISM go,
///     since the plugin initialises it again. Every PRISM call runs on one worker thread, as in the plugin.
/// </summary>
internal sealed unsafe class StartupSpeech : IDisposable
{
    private const string LibraryFile = "prism.dll";
    private const int ShutdownWaitMilliseconds = 3_000;

    private readonly BlockingCollection<(string Text, bool Interrupt)> queue = [];
    private readonly Thread worker;
    private IntPtr library;
    private volatile int holdMilliseconds;

    private StartupSpeech(IntPtr library)
    {
        this.library = library;
        this.worker = new Thread(this.Run) { IsBackground = true, Name = "Poe2Access startup speech" };
        this.worker.Start();
    }

    /// <summary>Null (after logging) when the plugin folder has no prism.dll or PRISM cannot start.</summary>
    public static StartupSpeech? TryCreate(string pluginFolder)
    {
        var path = Path.Join(pluginFolder, LibraryFile);
        if (!File.Exists(path))
        {
            Log.Write($"No {LibraryFile} in {pluginFolder}: the game data update is not spoken");
            return null;
        }

        try
        {
            return new StartupSpeech(NativeLibrary.Load(path));
        }
        catch (Exception error) when (error is DllNotFoundException or BadImageFormatException)
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

    /// <summary>Lets the queued lines finish, then releases PRISM and the library: the plugin starts it afresh.</summary>
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

        if (this.library != IntPtr.Zero)
        {
            NativeLibrary.Free(this.library);
            this.library = IntPtr.Zero;
        }
    }

    private void Run()
    {
        var configInit = (delegate* unmanaged[Cdecl]<byte>)this.Export("prism_config_init");
        var init = (delegate* unmanaged[Cdecl]<byte*, IntPtr>)this.Export("prism_init");
        var shutdown = (delegate* unmanaged[Cdecl]<IntPtr, void>)this.Export("prism_shutdown");
        var acquireBest = (delegate* unmanaged[Cdecl]<IntPtr, IntPtr>)this.Export("prism_registry_acquire_best");
        var initialize = (delegate* unmanaged[Cdecl]<IntPtr, int>)this.Export("prism_backend_initialize");
        var speak = (delegate* unmanaged[Cdecl]<IntPtr, byte*, byte, int>)this.Export("prism_backend_speak");
        var free = (delegate* unmanaged[Cdecl]<IntPtr, void>)this.Export("prism_backend_free");
        var configuration = configInit();
        var context = init(&configuration);
        var backend = context == IntPtr.Zero ? IntPtr.Zero : acquireBest(context);
        if (backend == IntPtr.Zero)
        {
            Log.Write("PRISM found no speech backend: the game data update is not spoken");
        }
        else
        {
            initialize(backend); // acquire_best may have done it already
        }

        var spoke = false;
        foreach (var (text, interrupt) in this.queue.GetConsumingEnumerable())
        {
            if (backend == IntPtr.Zero)
            {
                continue;
            }

            spoke = true;
            fixed (byte* utf8 = Encoding.UTF8.GetBytes(text + '\0'))
            {
                var result = speak(backend, utf8, interrupt ? (byte)1 : (byte)0);
                Log.Write($"Spoken ({result}): {text}");
            }
        }

        if (spoke)
        {
            Thread.Sleep(this.holdMilliseconds);
        }

        if (backend != IntPtr.Zero)
        {
            free(backend);
        }

        if (context != IntPtr.Zero)
        {
            shutdown(context);
        }
    }

    private IntPtr Export(string name) => NativeLibrary.GetExport(this.library, name);
}
