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

        /// <summary>
        ///     A reading (a life percentage, a count of steps) that waited this long behind a kept line is no longer said:
        ///     the number it holds was true when asked, and the next change or the player's own question gives the present one.
        /// </summary>
        public const int ReadingFreshMs = 2000;

        private readonly List<(string Text, bool Interrupt, DateTime At, bool Reading)> held = new();

        /// <summary>The readings the last <see cref="Release"/> left unsaid for their age.</summary>
        public IReadOnlyList<string> Dropped { get; private set; } = Array.Empty<string>();
        private DateTime speechEndsAt = DateTime.MinValue;

        /// <summary>When the last line that is not a passing one is estimated said: past it, only passing lines are in flight.</summary>
        private DateTime plainEndsAt = DateTime.MinValue;

        /// <summary>The passing line waiting for the others to be said; the next takes its place.</summary>
        private (string Text, DateTime At)? waitingPassing;

        /// <summary>A passing line that waited this long is no longer said: who appeared then is where the sounds say now.</summary>
        public const int PassingFreshMs = 3000;

        /// <summary>The least time before a waiting passing line is looked at again (an urged line can hold it past the estimate).</summary>
        private const int PassingPollMs = 50;
        private DateTime keptUntil = DateTime.MinValue;

        /// <summary>The part of the hold the player's own move does not end (a lesson's).</summary>
        private DateTime lessonUntil = DateTime.MinValue;

        /// <summary>The player's last move, until something cuts it.</summary>
        private (string Text, DateTime At)? lastMove;

        /// <summary>A line cut this soon after it was sent was not heard: at most its first words.</summary>
        public const int UnheardMs = 1000;

        /// <summary>
        ///     The move the last <see cref="Keep"/> cut before it was heard, to be said again behind the kept line: the cursor
        ///     had gone on as "Movido a Alijo: …" came, and nothing said where it stood (user 2026-11-11).
        /// </summary>
        public string? MoveToResume { get; private set; }

        /// <summary>The line <see cref="lessonUntil"/> holds for.</summary>
        private string lessonText = string.Empty;

        /// <summary>The last kept line and when it is estimated said, whatever move ended its hold.</summary>
        private (string Text, DateTime Until) lastKept = (string.Empty, DateTime.MinValue);

        public static int SpokenMs(string text) => text.Length * SpokenMsPerChar;

        /// <summary>
        ///     How long a message the game or the player's own action produced ("Inventario lleno", "Vendido: …", "Menú del
        ///     juego") keeps the floor: about its spoken length, never so long that the cursor's next stop lags behind. The
        ///     focus the game lands meanwhile follows it instead of cutting it off (user 2026-10-28).
        /// </summary>
        public static int MessageHoldMs(string text) => Math.Clamp(SpokenMs(text), MinMessageHoldMs, MaxMessageHoldMs);

        private const int MinMessageHoldMs = 600;
        private const int MaxMessageHoldMs = 2500;

        /// <summary>Whether a kept line has the floor now.</summary>
        public bool Held(DateTime now) => now < this.keptUntil;

        /// <summary>Whether nothing sent is estimated still being said: a phrase sent now is heard at once.</summary>
        public bool Quiet(DateTime now) => this.speechEndsAt <= now;

        /// <summary>
        ///     The kept line being said already holds this message: "+10% a la resistencia al hielo" on screen as the chat says
        ///     "malgdhler ha recibido +10% a la resistencia al hielo." (2026-11-06). The copy can show in the hint box, where it
        ///     is kept as a lesson: "+20 a la vida máxima" held the cursor back for 3 s (user 2026-11-11).
        /// </summary>
        public bool Echoes(string text, DateTime now) => now < this.lastKept.Until && this.lastKept.Text.Contains(text, StringComparison.Ordinal);

        /// <summary>Whether all that is still being said are passing lines, which a warning cuts.</summary>
        public bool OnlyPassing(DateTime now) => !this.Held(now) && !this.Quiet(now) && this.plainEndsAt <= now;

        /// <summary>
        ///     A passing line: out now, cutting the passing one still being said, or (false) waiting for the other lines to be
        ///     said, in the place of the one that waited.
        /// </summary>
        /// <param name="mayGo">False while an urged line holds the floor.</param>
        public (bool GoesOut, bool Interrupt) Pass(string text, DateTime now, bool mayGo = true)
        {
            this.waitingPassing = null;
            if (!mayGo || this.Held(now) || this.plainEndsAt > now)
            {
                this.waitingPassing = (text, now);
                return (false, false);
            }

            var cuts = !this.Quiet(now);
            this.SentPassing(text, now);
            return (true, cuts);
        }

        /// <summary>The waiting passing line once the others are said (null: none, or not yet), or dropped for its age.</summary>
        public string? ReleasePassing(DateTime now, bool mayGo, out string? dropped)
        {
            dropped = null;
            if (this.waitingPassing is not { } waiting)
            {
                return null;
            }

            if ((now - waiting.At).TotalMilliseconds > PassingFreshMs)
            {
                dropped = waiting.Text;
                this.waitingPassing = null;
                return null;
            }

            if (!mayGo || this.Held(now) || this.held.Count > 0 || this.plainEndsAt > now)
            {
                return null;
            }

            this.waitingPassing = null;
            this.SentPassing(waiting.Text, now);
            return waiting.Text;
        }

        /// <summary>A passing line starts now: it cut the passing one being said, or there was none.</summary>
        private void SentPassing(string text, DateTime now)
        {
            this.speechEndsAt = now.AddMilliseconds(SpokenMs(text));
            this.lastMove = null;
        }

        /// <summary>True when the phrase goes out now; false when it waits for the kept line (an interrupt drops what waited).</summary>
        /// <param name="reading">The phrase says a value that changes by itself, not something that happened.</param>
        public bool Admit(string text, bool interrupt, DateTime now, bool reading = false)
        {
            if (!this.Held(now))
            {
                this.Sent(text, interrupt, now);
                return true;
            }

            if (Replaces(interrupt, reading))
            {
                this.held.Clear();
            }

            if (!this.held.Exists(phrase => phrase.Text == text))
            {
                this.held.Add((text, interrupt, now, reading)); // one waiting already keeps its turn and is said once
            }

            return false;
        }

        /// <summary>
        ///     Whether a phrase that must wait takes the place of what already waits: an interrupt does, but not a reading,
        ///     which may itself go unsaid for its age and would take a pick-up or a refusal with it.
        /// </summary>
        public static bool Replaces(bool interrupt, bool reading) => interrupt && !reading;

        /// <summary>A waiting phrase that went out another way (a warning joined to the urgent line after it) waits no more.</summary>
        public void Withdraw(string text) => this.held.RemoveAll(phrase => phrase.Text == text);

        /// <summary>
        ///     A kept line going out now: cutting in, or after what is being said; returns whether it may interrupt (never
        ///     another kept line still holding). A message (<paramref name="yieldsToMoves"/>) stops holding once the player moves.
        /// </summary>
        public bool Keep(string text, bool interrupt, int holdMs, DateTime now, bool yieldsToMoves = false)
        {
            if (yieldsToMoves && now < this.lessonUntil && this.lessonText.Length > 0 && text.Contains(this.lessonText, StringComparison.Ordinal))
            {
                // The line kept as a lesson was this notice's copy in the hint box: it yields to moves as the notice does.
                this.lessonUntil = now;
            }

            interrupt &= !this.Held(now);
            var ahead = this.speechEndsAt - now;
            var startsAt = interrupt || ahead <= TimeSpan.Zero ? now : now + TimeSpan.FromMilliseconds(Math.Min(ahead.TotalMilliseconds, MaxAheadMs));
            // Behind a message only: a page is too long for the cursor's place to wait for.
            var cut = interrupt && yieldsToMoves && holdMs <= MaxMessageHoldMs && this.lastMove is { } move &&
                      (now - move.At).TotalMilliseconds < UnheardMs ? move.Text : null;
            this.Sent(text, interrupt, now);
            var until = startsAt.AddMilliseconds(holdMs);
            this.keptUntil = until > this.keptUntil ? until : this.keptUntil;
            this.lastKept = (text, until);
            this.MoveToResume = cut;
            if (cut != null)
            {
                this.Sent(cut, false, now);
                this.lastMove = (cut, now); // still unheard: another message cutting in is followed by it too
            }

            if (!yieldsToMoves && until > this.lessonUntil)
            {
                this.lessonUntil = until;
                this.lessonText = text;
            }

            return interrupt;
        }

        /// <summary>
        ///     The player's own move (the cursor to another cell) cuts a message or a page being kept, as it cuts anything
        ///     else: behind "Movido a Alijo: …" or "Vendido: …" it left the player waiting to hear where they were (user
        ///     2026-11-11). A lesson still holds. Of what waited, a line that was to cut in is stale; what happened
        ///     meanwhile ("Recogido 5 de oro") follows the move.
        /// </summary>
        public bool Move(string text, bool interrupt, DateTime now)
        {
            if (now >= this.lessonUntil)
            {
                this.keptUntil = this.lessonUntil;
                this.held.RemoveAll(phrase => phrase.Interrupt);
            }

            var goesOut = this.Admit(text, interrupt, now);
            if (goesOut)
            {
                this.lastMove = (text, now);
            }

            return goesOut;
        }

        /// <summary>What the player just asked for: no kept line holds it back, and what waited is stale.</summary>
        public void Take(string text, DateTime now)
        {
            this.keptUntil = this.lessonUntil = now;
            this.held.Clear();
            this.waitingPassing = null;
            this.Sent(text, true, now);
        }

        /// <summary>A phrase that goes out regardless of the floor (urgent warnings queue right behind the kept line).</summary>
        public void Sent(string text, bool interrupt, DateTime now)
        {
            var start = interrupt || this.speechEndsAt < now ? now : this.speechEndsAt;
            this.plainEndsAt = this.speechEndsAt = start.AddMilliseconds(SpokenMs(text));
            if (interrupt)
            {
                this.lastMove = null;
            }
        }

        /// <summary>
        ///     What waited, once the floor is free: queued behind whatever is still being said. Readings older than
        ///     <see cref="ReadingFreshMs"/> are left out (<see cref="Dropped"/>); what happened is said however late.
        /// </summary>
        public IReadOnlyList<string> Release(DateTime now)
        {
            this.Dropped = Array.Empty<string>();
            if (this.held.Count == 0 || this.Held(now))
            {
                return Array.Empty<string>();
            }

            bool Stale((string Text, bool Interrupt, DateTime At, bool Reading) phrase) => phrase.Reading && (now - phrase.At).TotalMilliseconds > ReadingFreshMs;
            this.Dropped = this.held.Where(Stale).Select(phrase => phrase.Text).ToList();
            var due = this.held.Where(phrase => !Stale(phrase)).Select(phrase => phrase.Text).ToList();
            this.held.Clear();
            foreach (var text in due)
            {
                this.Sent(text, false, now);
            }

            return due;
        }

        /// <summary>How long until what waits is due; null when nothing waits.</summary>
        public TimeSpan? DueIn(DateTime now)
        {
            var due = this.held.Count == 0 ? (TimeSpan?)null : this.Held(now) ? this.keptUntil - now : TimeSpan.Zero;
            if (this.waitingPassing is not { } waiting)
            {
                return due;
            }

            // Looked at again when the others should be said, or to be dropped for its age.
            var free = new[] { this.keptUntil, this.plainEndsAt, now.AddMilliseconds(PassingPollMs) }.Max();
            var stale = waiting.At.AddMilliseconds(PassingFreshMs + 1);
            var passing = (free < stale ? free : stale) - now;
            passing = passing > TimeSpan.Zero ? passing : TimeSpan.Zero;
            return due is { } first && first < passing ? first : passing;
        }

        /// <summary>Speech stopped: nothing is being said, nothing waits.</summary>
        public void Clear(DateTime now)
        {
            this.held.Clear();
            this.waitingPassing = null;
            this.keptUntil = DateTime.MinValue;
            this.lessonUntil = DateTime.MinValue;
            this.lastMove = null;
            this.lastKept = (string.Empty, DateTime.MinValue);
            this.plainEndsAt = this.speechEndsAt = now;
        }
    }
}
