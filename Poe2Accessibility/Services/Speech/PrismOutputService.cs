namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     PRISM screen-reader output through the mod's own binding (<see cref="PrismLibrary"/>) of the prism.dll shipped
    ///     next to the plugin. Every PRISM call runs on one worker thread because a backend speak can block for hundreds of ms.
    /// </summary>
    public sealed class PrismOutputService : IAccessibilityOutputService
    {
        /// <summary>
        ///     How long a mentioned warning may still be unheard: "Vida 50 por ciento" queued behind a line was dropped
        ///     by "Aturdido" 80 ms later (2026-10-24); about the time to say such a line.
        /// </summary>
        private const int MentionMs = 1500;

        /// <summary>A backend call slower than this is logged, to measure how long speech blocks.</summary>
        private const int SlowCallMs = 100;

        /// <summary>How long Dispose waits for the worker to release PRISM before giving up.</summary>
        private const int ShutdownWaitMs = 2000;

        private readonly PluginLog log;
        private readonly object queueGate = new();
        private readonly Queue<SpeechRequest> pending = new();
        private readonly AutoResetEvent pendingSignal = new(false);
        private Thread? worker;
        private volatile bool stopping;
        private readonly SpeechRepeats repeats = new();
        private string lastText = string.Empty;

        /// <summary>The phrases asked for in the last SaidKeptMs (at most SaidKept), for <see cref="SaidSince"/>.</summary>
        private readonly Queue<(DateTime At, string Text)> said = new();

        private const int SaidKept = 64;

        private const int SaidKeptMs = 10000;

        private PrismOutputService(PluginLog log)
        {
            this.log = log;
        }

        public string BackendName { get; private set; } = "none";

        /// <summary>Until then interrupting phrases queue (see <see cref="Urge"/>).</summary>
        private DateTime urgedUntil = DateTime.MinValue;

        /// <summary>A kept line's hold and what waits behind it; guarded by <see cref="queueGate"/>.</summary>
        private readonly SpeechFloor floor = new();

        /// <summary>How long a kept line outlives a stop: the time from a window's last notice to the mod seeing it closed.</summary>
        private const int KeptThroughStopMs = 1200;

        /// <summary>What went out lately and when, for <see cref="StopBefore"/>.</summary>
        private readonly List<(string Text, DateTime At, bool Kept)> sent = new();

        /// <summary>How far back <see cref="sent"/> reaches.</summary>
        private const int SentKeptMs = 3000;

        /// <summary>
        ///     The last <see cref="Mention"/>ed phrase, when, and, when it went out with nothing estimated still being said
        ///     ahead of it, the <see cref="enqueued"/> count it left: while that count stands it is being heard with nothing
        ///     after it, so an interrupting phrase soon after waits behind it; otherwise it may still wait behind another
        ///     line, and the interrupting phrase says it first (-1).
        /// </summary>
        private (string Text, DateTime At, long HeardAt)? mentioned;

        /// <summary>Requests handed to <see cref="pending"/> so far; guarded by <see cref="queueGate"/>.</summary>
        private long enqueued;

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

        public bool SaidSince(string fragment, DateTime since)
        {
            lock (this.said)
            {
                foreach (var (at, text) in this.said)
                {
                    if (at >= since && text.Contains(fragment, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
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
            this.Say(text, true, Delivery.Urgent, 0);
            this.urgedUntil = until > this.urgedUntil ? until : this.urgedUntil;
        }

        public void Mention(string text) => this.Mention(text, reading: true);

        public void MentionNews(string text) => this.Mention(text, reading: false);

        private void Mention(string text, bool reading)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            long before;
            bool quiet;
            lock (this.queueGate)
            {
                before = this.enqueued;
                quiet = this.floor.Quiet(DateTime.UtcNow);
            }

            if (!this.Say(text, false, Delivery.Normal, 0, reading))
            {
                return; // held back as a repeat: it was just heard
            }

            lock (this.queueGate)
            {
                // Said again before an interrupting phrase although it had been heard: "Frasco de vida menor, 5 usos" and
                // then "Frasco de vida menor, 5 usos. Remolino" 270 ms later (user 2026-11-09).
                this.mentioned = (text, DateTime.UtcNow, quiet && this.enqueued == before + 1 ? this.enqueued : -1);
            }
        }

        public void Keep(string text, int holdMs, bool interrupt = false, bool yieldsToMoves = false) =>
            this.Say(text, interrupt, yieldsToMoves ? Delivery.KeptMessage : Delivery.Kept, holdMs);

        public void Moved(string text) => this.Say(text, true, Delivery.Move, 0);

        public void SpeakReading(string text, bool interrupt = false) => this.Say(text, interrupt, Delivery.Normal, 0, reading: true);

        public void TakeFloor(string text) => this.Say(text, true, Delivery.Floor, 0);

        public void Repeat()
        {
            if (string.IsNullOrEmpty(this.lastText))
            {
                this.Speak(Announcements.NothingToRepeat, true);
                return;
            }

            this.Speak(this.lastText, true);
        }

        public void StopBefore(DateTime since)
        {
            var again = new List<string>();
            lock (this.queueGate)
            {
                foreach (var line in this.sent)
                {
                    // A kept line can arrive as the window it belongs to closes, a moment before the close is seen.
                    if (line.At >= since || (line.Kept && (DateTime.UtcNow - line.At).TotalMilliseconds < KeptThroughStopMs))
                    {
                        again.Add(line.Text);
                    }
                }

                this.floor.Clear(DateTime.UtcNow);
                this.EnqueueLocked(new SpeechRequest(null, true));
                foreach (var text in again)
                {
                    this.EnqueueLocked(new SpeechRequest(text, false));
                }
            }

            this.log.Info(again.Count == 0 ? "Speech stopped" : $"Speech stopped, said again: {string.Join(" | ", again)}");
            this.pendingSignal.Set();
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

        /// <summary>False when the phrase is held back (empty, a repeat, or already in the kept line).</summary>
        private bool Say(string text, bool interrupt, Delivery delivery, int holdMs, bool reading = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            lock (this.queueGate)
            {
                if (delivery == Delivery.KeptMessage && this.floor.Echoes(text, now))
                {
                    this.log.Info($"Speech held back (in the kept line): {text}");
                    return false;
                }
            }

            if ((!interrupt || delivery == Delivery.Urgent) && this.repeats.IsRepeat(text, now))
            {
                this.log.Info($"Speech held back (duplicate): {text}");
                return false;
            }

            this.repeats.Note(text, now);
            if (interrupt && now < this.urgedUntil)
            {
                interrupt = false; // an urgent phrase is still being heard: wait behind it
            }

            if (interrupt && this.mentioned is { } news)
            {
                this.mentioned = null;
                if ((now - news.At).TotalMilliseconds < MentionMs)
                {
                    bool heard;
                    lock (this.queueGate)
                    {
                        heard = news.HeardAt == this.enqueued;
                        if (!heard)
                        {
                            // Said with this phrase now: left waiting behind a kept line it would be said a second time.
                            this.floor.Withdraw(news.Text);
                        }
                    }

                    if (heard)
                    {
                        interrupt = false; // the warning is being heard and nothing came after it: wait behind it
                    }
                    else
                    {
                        text = $"{news.Text}. {text}"; // the warning this cuts off is said first, not lost
                    }
                }
            }

            this.lastText = text;
            lock (this.said)
            {
                this.said.Enqueue((now, text));
                while (this.said.Count > SaidKept || (this.said.Count > 0 && (now - this.said.Peek().At).TotalMilliseconds > SaidKeptMs))
                {
                    this.said.Dequeue();
                }
            }

            var goesOut = true;
            lock (this.queueGate)
            {
                switch (delivery)
                {
                    case Delivery.Kept:
                    case Delivery.KeptMessage:
                        interrupt = this.floor.Keep(text, interrupt, holdMs, now, delivery == Delivery.KeptMessage);
                        break;
                    case Delivery.Move:
                        goesOut = this.floor.Move(text, ref interrupt, now);
                        break;
                    case Delivery.Floor:
                        this.floor.Take(text, now);
                        break;
                    case Delivery.Urgent:
                        interrupt &= !this.floor.Held(now); // right behind the lesson being kept, never dropped with what waits
                        this.floor.Sent(text, interrupt, now);
                        break;
                    default:
                        goesOut = this.floor.Admit(text, interrupt, now, reading);
                        break;
                }

                if (goesOut)
                {
                    this.EnqueueLocked(new SpeechRequest(text, interrupt));
                    this.sent.RemoveAll(line => (now - line.At).TotalMilliseconds > SentKeptMs);
                    this.sent.Add((text, now, delivery is Delivery.Kept or Delivery.KeptMessage));
                }
            }

            this.log.Info(goesOut
                ? $"Speech {(interrupt ? "interrupts" : "queued")}{(delivery is Delivery.Kept or Delivery.KeptMessage ? ", kept" : delivery == Delivery.Move ? ", moved" : string.Empty)}: {text}"
                : $"Speech waits for the kept line{(SpeechFloor.Replaces(interrupt, reading) ? ", replacing what waited" : string.Empty)}: {text}");
            this.pendingSignal.Set();
            return true;
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
            this.enqueued++;
        }

        /// <summary>Queues what waited for a kept line once it is said; returns how long to sleep until the next is due.</summary>
        private int ReleaseWaiting()
        {
            IReadOnlyList<string> due;
            IReadOnlyList<string> dropped;
            TimeSpan? next;
            lock (this.queueGate)
            {
                var now = DateTime.UtcNow;
                due = this.floor.Release(now);
                dropped = this.floor.Dropped;
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

            foreach (var text in dropped)
            {
                this.log.Info($"Speech not said (a reading that waited too long): {text}");
            }

            return next is { } wait ? Math.Max(1, (int)Math.Ceiling(wait.TotalMilliseconds)) : Timeout.Infinite;
        }

        private void Run(string pluginDirectory, TaskCompletionSource<bool> ready)
        {
            PrismLibrary prism;
            try
            {
                prism = PrismLibrary.Load(Path.Join(pluginDirectory, PrismLibrary.FileName));
                var failure = prism.Start(message => this.log.Info(message));
                foreach (var (name, priority) in prism.Backends())
                {
                    this.log.Debug($"PRISM backend registered: {name} (priority {priority})");
                }

                if (prism.LibraryConfigVersion != PrismLibrary.ConfigVersion)
                {
                    this.log.Warn($"prism.dll {prism.Version} writes config version {prism.LibraryConfigVersion}; the binding matches {PrismLibrary.ConfigVersion}");
                }

                if (failure != null)
                {
                    this.log.Error($"PRISM {prism.Version}: {failure}");
                    prism.Dispose();
                    ready.SetResult(false);
                    return;
                }

                this.BackendName = prism.BackendName;
                var wine = PrismLibrary.WineVersion();
                this.log.Info($"PRISM {prism.Version} initialised, backend: {prism.BackendName}" + (wine == null ? string.Empty : $" (under Wine {wine})"));
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
                        this.Execute(prism, request);
                    }
                }
            }
            finally
            {
                // An exception escaping this thread would end the host process.
                try
                {
                    prism.Dispose();
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

        private void Execute(PrismLibrary prism, SpeechRequest request)
        {
            var timer = Stopwatch.StartNew();
            try
            {
                var error = request.Text == null ? prism.Stop() : prism.Speak(request.Text, request.Interrupt);
                if (error != 0 && request.Text != null)
                {
                    this.log.Error($"Speak failed: {prism.ErrorText(error)}");
                }
                else if (error != 0)
                {
                    this.log.Debug($"Stop: {prism.ErrorText(error)}"); // some backends call a stop with nothing to stop an error
                }
            }
            catch (Exception ex)
            {
                // An exception escaping this thread would end the host process.
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
            KeptMessage,
            Move,
            Floor,
        }

        private readonly record struct SpeechRequest(string? Text, bool Interrupt);
    }
}
