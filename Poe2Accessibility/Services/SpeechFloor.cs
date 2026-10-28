namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A kept line (a tutorial lesson) holds the floor until it is estimated said; what comes meanwhile waits, with the
    ///     interrupt rule applied among it, and follows it. Pure: the speech worker supplies the clock.
    /// </summary>
    public sealed class SpeechFloor
    {
        /// <summary>About how long a screen reader takes per character at a usual rate.</summary>
        public const int SpokenMsPerChar = 45;

        /// <summary>Past this, what is still being said ahead of a kept line no longer delays its hold: the estimate is too rough.</summary>
        public const int MaxAheadMs = 6000;

        private readonly List<(string Text, bool Interrupt)> held = new();
        private DateTime speechEndsAt = DateTime.MinValue;
        private DateTime keptUntil = DateTime.MinValue;

        /// <summary>The part of the hold the player's own move does not end (a lesson's).</summary>
        private DateTime lessonUntil = DateTime.MinValue;

        public static int SpokenMs(string text) => text.Length * SpokenMsPerChar;

        /// <summary>
        ///     How long a message the game or the player's own action produced ("Inventario lleno", "Vendido: …", "Menú del
        ///     juego") keeps the floor: about its spoken length, never so long that the cursor's next stop lags behind. The
        ///     focus that lands meanwhile follows it instead of cutting it off (user 2026-10-28).
        /// </summary>
        public static int MessageHoldMs(string text) => Math.Clamp(SpokenMs(text), MinMessageHoldMs, MaxMessageHoldMs);

        private const int MinMessageHoldMs = 600;
        private const int MaxMessageHoldMs = 2500;

        /// <summary>Whether a kept line has the floor now.</summary>
        public bool Held(DateTime now) => now < this.keptUntil;

        /// <summary>True when the phrase goes out now; false when it waits for the kept line (an interrupt drops what waited).</summary>
        public bool Admit(string text, bool interrupt, DateTime now)
        {
            if (!this.Held(now))
            {
                this.Sent(text, interrupt, now);
                return true;
            }

            if (interrupt)
            {
                this.held.Clear();
            }

            this.held.Add((text, interrupt));
            return false;
        }

        /// <summary>
        ///     A kept line going out now: cutting in, or after what is being said; returns whether it may interrupt (never
        ///     another kept line still holding). A message (<paramref name="yieldsToMoves"/>) stops holding once the player moves.
        /// </summary>
        public bool Keep(string text, bool interrupt, int holdMs, DateTime now, bool yieldsToMoves = false)
        {
            interrupt &= !this.Held(now);
            var ahead = this.speechEndsAt - now;
            var startsAt = interrupt || ahead <= TimeSpan.Zero ? now : now + TimeSpan.FromMilliseconds(Math.Min(ahead.TotalMilliseconds, MaxAheadMs));
            this.Sent(text, interrupt, now);
            var until = startsAt.AddMilliseconds(holdMs);
            this.keptUntil = until > this.keptUntil ? until : this.keptUntil;
            if (!yieldsToMoves && until > this.lessonUntil)
            {
                this.lessonUntil = until;
            }

            return interrupt;
        }

        /// <summary>
        ///     The player's own move (the cursor to another cell): a message no longer holds it back, it follows what is being
        ///     said instead of waiting out the estimate (user 2026-10-28, selling); a lesson still holds. What waited is stale.
        /// </summary>
        public bool Move(string text, ref bool interrupt, DateTime now)
        {
            if (now < this.lessonUntil)
            {
                return this.Admit(text, interrupt, now);
            }

            if (this.Held(now))
            {
                this.keptUntil = this.lessonUntil;
                interrupt = false;
            }

            this.held.Clear();
            return this.Admit(text, interrupt, now);
        }

        /// <summary>A phrase that goes out regardless of the floor (urgent warnings queue right behind the kept line).</summary>
        public void Sent(string text, bool interrupt, DateTime now)
        {
            var start = interrupt || this.speechEndsAt < now ? now : this.speechEndsAt;
            this.speechEndsAt = start.AddMilliseconds(SpokenMs(text));
        }

        /// <summary>What waited, once the floor is free: queued behind whatever is still being said.</summary>
        public IReadOnlyList<string> Release(DateTime now)
        {
            if (this.held.Count == 0 || this.Held(now))
            {
                return Array.Empty<string>();
            }

            var due = this.held.Select(phrase => phrase.Text).ToList();
            this.held.Clear();
            foreach (var text in due)
            {
                this.Sent(text, false, now);
            }

            return due;
        }

        /// <summary>How long until what waits is due; null when nothing waits.</summary>
        public TimeSpan? DueIn(DateTime now) => this.held.Count == 0 ? null : this.Held(now) ? this.keptUntil - now : TimeSpan.Zero;

        /// <summary>Speech stopped: nothing is being said, nothing waits.</summary>
        public void Clear(DateTime now)
        {
            this.held.Clear();
            this.keptUntil = DateTime.MinValue;
            this.lessonUntil = DateTime.MinValue;
            this.speechEndsAt = now;
        }
    }
}
