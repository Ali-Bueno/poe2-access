namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Reading texts aloud in <see cref="ScreenNarrator"/>: the screen on request, pairing numbers with labels and describing a set of texts.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>The longest label a zoom factor is joined to ("Zoom"; any language's word for it).</summary>
        private const int ZoomLabelChars = 16;

        /// <summary>A panel chosen in the radial menu opens this long after its name was said.</summary>
        private const int ChosenPanelMs = 2500;

        /// <summary>Longest one-word label a bare number before it is paired with ("DEX", "Strength").</summary>
        private const int MaxPairedLabel = 12;

        /// <summary>
        ///     Everything the screen shows where the player is: the focused control's layer, else the top panel
        ///     (character stats, support gem capacity, skill points) — legends excluded, they have their own command.
        /// </summary>
        public void SpeakScreen(ScreenSnapshot snapshot, IntPtr focusLayer, bool withHints = false)
        {
            var layer = snapshot.Find(focusLayer) ?? snapshot.TopInteractive;
            var texts = layer?.Texts.Where(text => !ScreenWatcher.IsLegend(text) && !IsNumber(text.Text)).ToList() ?? new List<ScreenText>();
            var hints = withHints ? snapshot.Layers.SelectMany(each => each.Texts).Where(ScreenWatcher.IsLegend).ToList() : new List<ScreenText>();
            var counters = layer?.Address == snapshot.TopInteractive?.Address ? this.TrialMapCounters(snapshot) : string.Empty;
            var spoken = Announcements.Sentences(new[] { Describe(texts), counters, Describe(hints) }.Where(part => part.Length > 0));
            this.Speak("screen", spoken.Length == 0 ? Announcements.NothingToRead : spoken, true);
        }

        /// <summary>The focus announcement already named it: a stash tab's title "3" is the context's "tab 3".</summary>
        private static bool InFocusContext(AccessibleUiNode? focus, string text) =>
            focus != null && focus.Context.Length > 0 && focus.Context.Split(". ").Contains(Announcements.TabTitle(text));

        /// <summary>Whether the radial menu named this panel a moment ago (its option is the panel's title).</summary>
        private bool JustChosen(string title) =>
            this.lastPanel.Text == title && (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < ChosenPanelMs;

        /// <summary>The tab and page waiting, then the text (a focus): one utterance; its texts count as just said.</summary>
        private string WithPendingPage(string text)
        {
            if (this.pendingPage is not { } pending)
            {
                return text;
            }

            this.pendingPage = null;
            this.recentTexts = (pending.Texts, DateTime.UtcNow);
            return Announcements.Sentences(new[] { pending.Tab, pending.Page, text }.Where(part => part.Length > 0));
        }

        /// <summary>
        ///     A bare number drawn before its one-word label reads as one text ("11", "DEX" → "DEX 11"); a label with
        ///     spaces or punctuation ("A, empty") is not a caption of the number before it.
        /// </summary>
        private static List<ScreenText> PairNumbers(IReadOnlyList<ScreenText> texts)
        {
            var paired = new List<ScreenText>(texts.Count);
            for (var i = 0; i < texts.Count; i++)
            {
                if (i + 1 < texts.Count && IsNumber(texts[i].Text) && texts[i].Text.Trim().Length > 0 &&
                    texts[i + 1].Text.Length is > 0 and <= MaxPairedLabel && texts[i + 1].Text.All(char.IsLetter))
                {
                    paired.Add(texts[i + 1] with { Text = $"{texts[i + 1].Text} {texts[i].Text.Trim()}" });
                    i++;
                    continue;
                }

                paired.Add(texts[i]);
            }

            return paired;
        }

        /// <summary>
        ///     A support gem's tier drawn on its icon ("I", "II"): a stash page with such gems said a bare "I" as it was
        ///     shown (2026-11-10); the gem's name says its tier.
        /// </summary>
        internal static bool IsTierNumeral(string text) => text.Length is > 0 and <= 4 && text.All(c => c is 'I' or 'V' or 'X');

        private static bool IsNumber(string text) => text.All(c => char.IsDigit(c) || c is '.' or ',' or ':' or '%' or '/' or '+' or '-' or ' '); // "6", "25%", "0:08"

        /// <summary>Texts in reading order without repeats; a disabled control is marked unavailable.</summary>
        public static string Describe(IEnumerable<ScreenText> texts)
        {
            var parts = new List<string>();
            foreach (var text in texts)
            {
                if (ScreenDiffer.ZoomFactor.IsMatch(text.Text) && parts.Count > 0 && parts[^1].Length <= ZoomLabelChars && !parts[^1].Any(char.IsDigit))
                {
                    parts[^1] = $"{parts[^1]} {text.Text}"; // the label beside it ("Zoom x1,9"), whatever the language calls it
                    continue;
                }

                var labelled = ScreenDiffer.ZoomFactor.IsMatch(text.Text)
                    ? Announcements.Zoom(text.Text)
                    : Announcements.WithButton(Announcements.Capitalised(text.Text), text.Button);
                var spoken = text.IsControl && !text.IsEnabled ? Announcements.Unavailable(labelled) : labelled;
                if (!parts.Contains(spoken))
                {
                    parts.Add(spoken);
                }
            }

            return Announcements.Sentences(parts);
        }

        private bool JustFocused() => (DateTime.UtcNow - this.lastFocusAt).TotalMilliseconds < FocusTextMergeMs;

        private static bool IsBehind(ScreenLayer layer, ScreenLayer? top) => top != null && layer.Index < top.Index;
    }
}
