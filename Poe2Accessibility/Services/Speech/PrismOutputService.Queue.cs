namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;

    /// <summary>The saying and queueing of <see cref="PrismOutputService"/>: what a request does to the queue and the speech in flight.</summary>
    public sealed partial class PrismOutputService
    {
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
                if (delivery is Delivery.Kept or Delivery.KeptMessage && this.floor.Echoes(text, now))
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
            if (interrupt && (now < this.urgedUntil || (now < this.urgedSoftlyUntil && delivery != Delivery.Move)))
            {
                interrupt = false; // an urged phrase is still being heard: wait behind it
            }
            else if (interrupt && delivery == Delivery.Move)
            {
                this.urgedSoftlyUntil = DateTime.MinValue; // cut by the player's move: it holds nothing more
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
            string? resumed;
            lock (this.queueGate)
            {
                switch (delivery)
                {
                    case Delivery.Kept:
                    case Delivery.KeptMessage:
                        interrupt = this.floor.Keep(text, interrupt, holdMs, now, delivery == Delivery.KeptMessage);
                        break;
                    case Delivery.Move:
                        goesOut = this.floor.Move(text, interrupt, now);
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

                resumed = delivery == Delivery.KeptMessage ? this.floor.MoveToResume : null;
                if (resumed != null)
                {
                    this.EnqueueLocked(new SpeechRequest(resumed, false));
                }
            }

            this.log.Info(goesOut
                ? $"Speech {(interrupt ? "interrupts" : "queued")}{(delivery is Delivery.Kept or Delivery.KeptMessage ? ", kept" : delivery == Delivery.Move ? ", moved" : string.Empty)}: {text}"
                : $"Speech waits for the kept line{(SpeechFloor.Replaces(interrupt, reading) ? ", replacing what waited" : string.Empty)}: {text}");
            if (resumed != null)
            {
                this.log.Info($"Speech after the kept line (the move it cut): {resumed}");
            }

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
    }
}
