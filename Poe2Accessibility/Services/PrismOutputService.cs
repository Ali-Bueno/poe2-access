namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Prismatoid;

    /// <summary>
    ///     PRISM screen-reader output via the official Prismatoid binding (bundles prism.dll for win-x64).
    ///     Every PRISM call runs on one worker thread because a backend speak can block for hundreds of ms.
    /// </summary>
    public sealed class PrismOutputService : IAccessibilityOutputService
    {
        /// <summary>Identical text arriving within this window is a frame-level duplicate, not new information.</summary>
        private const int DuplicateSuppressionMs = 300;

        /// <summary>Library name used by Prismatoid's DllImport (verified by reflection on Prismatoid 0.4.0).</summary>
        private const string NativeLibraryName = "prism";

        /// <summary>A backend call slower than this is logged, to measure how long speech blocks.</summary>
        private const int SlowCallMs = 100;

        /// <summary>How long Dispose waits for the worker to release PRISM before giving up.</summary>
        private const int ShutdownWaitMs = 2000;

        private static bool resolverInstalled;
        private static IntPtr nativeHandle;

        private readonly PluginLog log;
        private readonly object queueGate = new();
        private readonly Queue<SpeechRequest> pending = new();
        private readonly AutoResetEvent pendingSignal = new(false);
        private Thread? worker;
        private volatile bool stopping;
        private string lastText = string.Empty;
        private DateTime lastSpokenAt = DateTime.MinValue;

        private PrismOutputService(PluginLog log)
        {
            this.log = log;
        }

        public string BackendName { get; private set; } = "none";

        /// <summary>Until then interrupting phrases queue (see <see cref="Urge"/>).</summary>
        private DateTime urgedUntil = DateTime.MinValue;

        /// <summary>Returns null (after logging) when no backend can be acquired.</summary>
        public static PrismOutputService? TryCreate(string pluginDirectory, PluginLog log)
        {
            var service = new PrismOutputService(log);
            var ready = new TaskCompletionSource<bool>();
            service.worker = new Thread(() => service.Run(pluginDirectory, ready))
            {
                IsBackground = true,
                Name = "Poe2Accessibility speech",
            };
            service.worker.Start();
            if (ready.Task.Result)
            {
                return service;
            }

            service.worker.Join(ShutdownWaitMs);
            return null;
        }

        public void Speak(string text, bool interrupt = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var now = DateTime.UtcNow;
            if (!interrupt && text == this.lastText &&
                (now - this.lastSpokenAt).TotalMilliseconds < DuplicateSuppressionMs)
            {
                this.log.Info($"Speech held back (duplicate): {text}");
                return;
            }

            if (interrupt && now < this.urgedUntil)
            {
                interrupt = false; // an urgent phrase is still being heard: wait behind it
            }

            this.lastText = text;
            this.lastSpokenAt = now;
            this.log.Info($"Speech {(interrupt ? "interrupts" : "queued")}: {text}");
            this.Enqueue(new SpeechRequest(text, interrupt));
        }

        public void Urge(string text, int holdMs = 1500)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // An urgent phrase still being heard keeps the floor: this one waits behind it ("Life 25 percent" was cut
            // off by "Burning" 70 ms later, 2026-10-24), then holds in turn.
            var until = DateTime.UtcNow.AddMilliseconds(holdMs);
            this.Speak(text, true);
            this.urgedUntil = until > this.urgedUntil ? until : this.urgedUntil;
        }

        public void Repeat()
        {
            if (string.IsNullOrEmpty(this.lastText))
            {
                this.Speak(Announcements.NothingToRepeat, true);
                return;
            }

            this.Speak(this.lastText, true);
        }

        public void Stop() => this.Enqueue(new SpeechRequest(null, true));

        public void Dispose()
        {
            this.stopping = true;
            this.pendingSignal.Set();
            if (this.worker != null && !this.worker.Join(ShutdownWaitMs))
            {
                this.log.Warn("Speech worker did not stop in time; PRISM left to process exit");
            }
        }

        /// <summary>
        ///     Plugins load in their own AssemblyLoadContext and the host's resolver knows nothing about
        ///     our folder, so point Prismatoid's DllImport at the prism.dll shipped next to the plugin.
        /// </summary>
        private static void InstallNativeResolver(string pluginDirectory, PluginLog log)
        {
            if (resolverInstalled)
            {
                return;
            }

            var nativePath = Path.GetFullPath(Path.Join(pluginDirectory, "prism.dll"));
            NativeLibrary.SetDllImportResolver(typeof(PrismContext).Assembly, (name, _, _) =>
            {
                if (!name.StartsWith(NativeLibraryName, StringComparison.OrdinalIgnoreCase) || !File.Exists(nativePath))
                {
                    return IntPtr.Zero;
                }

                if (nativeHandle == IntPtr.Zero)
                {
                    nativeHandle = NativeLibrary.Load(nativePath);
                }

                return nativeHandle;
            });
            resolverInstalled = true;
            log.Debug($"Native resolver installed for {nativePath}");
        }

        /// <summary>An interrupt drops everything still waiting; a null text is a stop request.</summary>
        private void Enqueue(SpeechRequest request)
        {
            lock (this.queueGate)
            {
                if (request.Interrupt)
                {
                    this.pending.Clear();
                }

                this.pending.Enqueue(request);
            }

            this.pendingSignal.Set();
        }

        private void Run(string pluginDirectory, TaskCompletionSource<bool> ready)
        {
            PrismContext context;
            IPrismBackend backend;
            try
            {
                InstallNativeResolver(pluginDirectory, this.log);
                context = new PrismContext();
                foreach (var info in context.AvailableBackends)
                {
                    this.log.Debug($"PRISM backend available: {info.Name} (priority {info.Priority})");
                }

                backend = context.AcquireBestBackend();
                this.BackendName = backend.Name;
                this.log.Info($"PRISM initialised, backend: {backend.Name}");
            }
            catch (Exception ex)
            {
                this.log.Error("PRISM initialisation failed", ex);
                ready.SetResult(false);
                return;
            }

            ready.SetResult(true);
            try
            {
                while (!this.stopping)
                {
                    this.pendingSignal.WaitOne();
                    while (!this.stopping && this.TryDequeue(out var request))
                    {
                        this.Execute(backend, request);
                    }
                }
            }
            finally
            {
                backend.Dispose();
                context.Dispose();
            }
        }

        private bool TryDequeue(out SpeechRequest request)
        {
            lock (this.queueGate)
            {
                return this.pending.TryDequeue(out request);
            }
        }

        private void Execute(IPrismBackend backend, SpeechRequest request)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                if (request.Text == null)
                {
                    backend.Stop();
                }
                else
                {
                    backend.Speak(request.Text, request.Interrupt);
                }
            }
            catch (Exception ex)
            {
                this.log.Error(request.Text == null ? "Stop failed" : "Speak failed", ex);
            }

            if (timer.ElapsedMilliseconds > SlowCallMs)
            {
                this.log.Debug($"PRISM call took {timer.ElapsedMilliseconds} ms");
            }
        }

        private readonly record struct SpeechRequest(string? Text, bool Interrupt);
    }
}
