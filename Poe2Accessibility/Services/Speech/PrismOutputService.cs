namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    ///     PRISM screen-reader output through the mod's own binding (<see cref="PrismLibrary"/>) of the prism.dll shipped
    ///     next to the plugin. Every PRISM call runs on one worker thread because a backend speak can block for hundreds of ms.
    /// </summary>
    public sealed partial class PrismOutputService : IAccessibilityOutputService
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

        /// <summary>The same for an urged phrase that is not urgent news: the player's own move still cuts it.</summary>
        private DateTime urgedSoftlyUntil = DateTime.MinValue;

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

        public bool Speaking
        {
            get
            {
                lock (this.queueGate)
                {
                    return !this.floor.Quiet(DateTime.UtcNow);
                }
            }
        }

        public void Urge(string text, int holdMs = 1500, bool yieldsToMoves = false)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // An urgent phrase still being heard keeps the floor: this one waits behind it ("Life 25 percent" was cut
            // off by "Burning" 70 ms later, 2026-10-24), then holds in turn.
            var until = DateTime.UtcNow.AddMilliseconds(holdMs);
            this.Say(text, true, Delivery.Urgent, 0);
            if (yieldsToMoves)
            {
                this.urgedSoftlyUntil = until > this.urgedSoftlyUntil ? until : this.urgedSoftlyUntil;
            }
            else
            {
                this.urgedUntil = until > this.urgedUntil ? until : this.urgedUntil;
            }
        }

        public void Mention(string text) => this.Mention(text, reading: true);

        public void MentionNews(string text) => this.Mention(text, reading: false);

        private void Mention(string text, bool reading)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            text = ChangeSign.Spoken(text);
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
    }
}
