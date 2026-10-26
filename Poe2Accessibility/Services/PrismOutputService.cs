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

        /// <summary>
        ///     How long a mentioned warning may still be unheard: "Vida 50 por ciento" queued behind a line was dropped
        ///     by "Aturdido" 80 ms later (2026-10-24); about the time to say such a line.
        /// </summary>
        private const int MentionMs = 1500;

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

        /// <summary>A kept line's hold and what waits behind it; guarded by <see cref="queueGate"/>.</summary>
        private readonly SpeechFloor floor = new();

        /// <summary>The last <see cref="Mention"/>ed phrase and when: an interrupting phrase soon after says it first.</summary>
        private (string Text, DateTime At)? mentioned;

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

        public void Speak(string text, bool interrupt = false) => this.Say(text, interrupt, Delivery.Normal, 0);

        public void Urge(string text, int holdMs = 1500)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // An urgent phrase still being heard keeps the floor: this one waits behind it ("Life 25 percent" was cut
            // off by "Burning" 70 ms later, 2026-10-24), then holds in turn.
            var until = DateTime.UtcNow.AddMilliseconds(holdMs);
            this.Say(text, true, Delivery.Urgent, 0);
            this.urgedUntil = until > this.urgedUntil ? until : this.urgedUntil;
        }

        public void Mention(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            this.Speak(text, false);
            this.mentioned = (text, DateTime.UtcNow);
        }

        public void Keep(string text, int holdMs, bool interrupt = false) => this.Say(text, interrupt, Delivery.Kept, holdMs);

        public void Repeat()
        {
            if (string.IsNullOrEmpty(this.lastText))
            {
                this.Speak(Announcements.NothingToRepeat, true);
                return;
            }

            this.Speak(this.lastText, true);
        }

        public void Stop()
        {
            lock (this.queueGate)
            {
                this.floor.Clear(DateTime.UtcNow);
            }

            this.Enqueue(new SpeechRequest(null, true));
        }

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

        private void Say(string text, bool interrupt, Delivery delivery, int holdMs)
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

            if (interrupt && this.mentioned is { } news && (now - news.At).TotalMilliseconds < MentionMs)
            {
                text = $"{news.Text}. {text}"; // the warning this cuts off is said first, not lost
            }

            if (interrupt)
            {
                this.mentioned = null;
            }

            this.lastText = text;
            this.lastSpokenAt = now;
            var goesOut = true;
            lock (this.queueGate)
            {
                switch (delivery)
                {
                    case Delivery.Kept:
                        interrupt = this.floor.Keep(text, interrupt, holdMs, now);
                        break;
                    case Delivery.Urgent:
                        interrupt &= !this.floor.Held(now); // right behind the lesson being kept, never dropped with what waits
                        this.floor.Sent(text, interrupt, now);
                        break;
                    default:
                        goesOut = this.floor.Admit(text, interrupt, now);
                        break;
                }

                if (goesOut)
                {
                    this.EnqueueLocked(new SpeechRequest(text, interrupt));
                }
            }

            this.log.Info(goesOut
                ? $"Speech {(interrupt ? "interrupts" : "queued")}{(delivery == Delivery.Kept ? ", kept" : string.Empty)}: {text}"
                : $"Speech waits for the kept line{(interrupt ? ", replacing what waited" : string.Empty)}: {text}");
            this.pendingSignal.Set();
        }

        /// <summary>An interrupt drops everything still waiting; a null text is a stop request.</summary>
        private void Enqueue(SpeechRequest request)
        {
            lock (this.queueGate)
            {
                this.EnqueueLocked(request);
            }

            this.pendingSignal.Set();
        }

        private void EnqueueLocked(SpeechRequest request)
        {
            if (request.Interrupt)
            {
                this.pending.Clear();
            }

            this.pending.Enqueue(request);
        }

        /// <summary>Queues what waited for a kept line once it is said; returns how long to sleep until the next is due.</summary>
        private int ReleaseWaiting()
        {
            IReadOnlyList<string> due;
            TimeSpan? next;
            lock (this.queueGate)
            {
                var now = DateTime.UtcNow;
                due = this.floor.Release(now);
                foreach (var text in due)
                {
                    this.EnqueueLocked(new SpeechRequest(text, false));
                }

                next = this.floor.DueIn(now);
            }

            foreach (var text in due)
            {
                this.log.Info($"Speech after the kept line: {text}");
            }

            return next is { } wait ? Math.Max(1, (int)Math.Ceiling(wait.TotalMilliseconds)) : Timeout.Infinite;
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
                var waitMs = Timeout.Infinite;
                while (!this.stopping)
                {
                    this.pendingSignal.WaitOne(waitMs);
                    waitMs = this.ReleaseWaiting();
                    while (!this.stopping && this.TryDequeue(out var request))
                    {
                        this.Execute(backend, request);
                    }
                }
            }
            finally
            {
                // An exception escaping this thread would end the host process.
                try
                {
                    backend.Dispose();
                    context.Dispose();
                }
                catch (Exception ex)
                {
                    this.log.Error("PRISM shutdown failed", ex);
                }
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

        private enum Delivery
        {
            Normal,
            Urgent,
            Kept,
        }

        private readonly record struct SpeechRequest(string? Text, bool Interrupt);
    }
}
