namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Speaking of <see cref="ScreenNarrator"/>: held and interrupting utterances, the memory of what was just said and repeated alerts.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>A kept line is held about as long as a screen reader takes to say it, within these bounds.</summary>
        private const int MinKeepMs = 1500;
        private const int MaxKeepMs = 6000;

        /// <summary>The last narration as a set of its parts, to drop the same news reported twice.</summary>
        private (string Parts, DateTime At) lastNarrated = (string.Empty, DateTime.MinValue);

        /// <summary>What was said in the last <see cref="RecentTextsMs"/>, as parts (<see cref="JustNarrated"/>).</summary>
        private readonly List<(HashSet<string> Parts, DateTime At)> narrated = new();

        /// <summary>
        ///     Narrations of the last moment, by their sorted parts: the stash said its tab and focus twice, as its layer
        ///     opened and as it became the top layer in the same frame, with its legends between (2026-10-24).
        /// </summary>
        private readonly Dictionary<string, DateTime> recentNarrations = new(StringComparer.Ordinal);

        /// <summary>Events of one frame land within this; a tab switched there and back is slower.</summary>
        private const int SameMomentMs = 300;

        /// <summary>Recently read texts are left out of a repeat within this time.</summary>
        private const int RecentTextsMs = 1500;

        /// <summary>Said once the focus waiting for its new layer has been (the creation screen's name help).</summary>
        public void SayAfterFocus(string text) => this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, text });

        /// <summary>
        ///     Whether a line was part of a narration a moment ago (the checkpoint map's title, said as it opened, came
        ///     again as the same layer went on top).
        /// </summary>
        private bool JustNarrated(string line)
        {
            var now = DateTime.UtcNow;
            var parts = Parts(line);
            return parts.Count > 0 && this.narrated.Any(said => (now - said.At).TotalMilliseconds < RecentTextsMs && parts.All(said.Parts.Contains));
        }

        /// <summary>Keeps what was just said for <see cref="JustNarrated"/>.</summary>
        private void NoteNarrated(string text)
        {
            var now = DateTime.UtcNow;
            this.narrated.RemoveAll(said => (now - said.At).TotalMilliseconds >= RecentTextsMs);
            this.narrated.Add((new HashSet<string>(Parts(text), StringComparer.Ordinal), now));
        }

        /// <summary>A text cut at its sentences and commas ("Guild. You are not in a guild, Create Guild").</summary>
        private static List<string> Parts(string text) =>
            text.Split(new[] { ". ", ", " }, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim().TrimEnd('.')).Where(part => part.Length > 0).ToList();

        /// <summary>Said after the focus: queued behind it, or held for it while it waits for its layer to settle.</summary>
        private void AfterFocus(string reason, string text)
        {
            if (this.deferredFocus != null)
            {
                this.SayAfterFocus(text);
            }
            else
            {
                this.Speak(reason, text, false);
            }
        }

        /// <summary>The same refusal or alert shown again this soon is the same press repeated, not news.</summary>
        private const int AlertRepeatMs = 2500;

        private readonly Dictionary<string, DateTime> alertsSaid = new(StringComparer.Ordinal);

        /// <summary>A refusal or alert, unless the same one was said a moment ago.</summary>
        private bool AlertIsNews(string text)
        {
            var now = DateTime.UtcNow;
            if (this.alertsSaid.TryGetValue(text, out var at) && (now - at).TotalMilliseconds < AlertRepeatMs)
            {
                return false;
            }

            this.alertsSaid[text] = now;
            return true;
        }

        /// <summary>How long a line holds the floor against what would cut it: a lesson whole, a message for about its length.</summary>
        private enum Hold
        {
            None,
            Message,
            Lesson,

            /// <summary>What the player can ask for again (the quest page): kept for its length, but the cursor's move cuts it.</summary>
            Page,

            /// <summary>The player asked for it: it cuts even a held line, and holds nothing back itself.</summary>
            Floor,
        }

        private void Speak(string reason, string text, bool interrupt, Hold hold = Hold.None)
        {
            if (text.Length == 0)
            {
                return;
            }

            // A page still waiting for its focus goes first: what follows (the area the quest map shows) must not
            // be cut off by the page arriving a moment later.
            text = this.WithPendingPage(text);

            // The same parts in another order a moment later (a panel reported as opened, then as its texts
            // appearing: "A, empty. Secondary skill set…" after "Secondary skill set… A, empty") are not news.
            var parts = string.Join("|", text.Split(new[] { ". ", ", " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim().TrimEnd('.')).OrderBy(part => part, StringComparer.Ordinal));
            var now = DateTime.UtcNow;
            foreach (var old in this.recentNarrations.Where(pair => (now - pair.Value).TotalMilliseconds >= SameMomentMs).Select(pair => pair.Key).ToList())
            {
                this.recentNarrations.Remove(old);
            }

            if ((parts == this.lastNarrated.Parts && (now - this.lastNarrated.At).TotalMilliseconds < RecentTextsMs) || this.recentNarrations.ContainsKey(parts))
            {
                this.log.Debug($"Narrate ({reason}) skipped: same as just said");
                return;
            }

            this.lastNarrated = (parts, now);
            this.recentNarrations[parts] = now;
            this.NoteNarrated(text);
            this.log.Debug($"Narrate ({reason})");
            if (this.deferredFocus is { } deferred && text.Contains(this.lastFocusSpeech, StringComparison.Ordinal))
            {
                this.deferredFocus = null; // the popup's message carried the focus with it
            }

            if (interrupt)
            {
                this.cutInAt = now;
            }

            if (hold == Hold.Floor)
            {
                this.output.TakeFloor(text);
                return;
            }

            if (hold != Hold.None)
            {
                this.output.Keep(text, hold == Hold.Message ? SpeechFloor.MessageHoldMs(text) : Math.Clamp(SpeechFloor.SpokenMs(text), MinKeepMs, MaxKeepMs), interrupt,
                    yieldsToMoves: hold != Hold.Lesson);
                return;
            }

            var cut = interrupt || this.deferredFocus != null;
            this.output.Speak(text, cut);
            if (cut && this.cuttableHints is { } hints)
            {
                this.cuttableHints = null;
                if ((now - hints.At).TotalMilliseconds < HintsCutMs)
                {
                    this.output.Speak(hints.Text, false);
                }
            }
        }
    }
}
