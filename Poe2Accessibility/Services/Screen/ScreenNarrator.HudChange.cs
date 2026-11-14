namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The tutorial and HUD change rules of <see cref="ScreenNarrator"/>, with the in-menu test and the quest page that waits for it.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>The objective / tutorial box: new texts read, lessons that hold the character kept whole.</summary>
        private void OnTutorialChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, System.Collections.Generic.List<ScreenText> legends)
        {
            // The box shows the same hint again whenever a panel or the radial menu closes: not news, until a reminder
            // is due. Shown again later during play it can be the game waiting for that very action — the dodge lesson
            // holds the character stunned until B (Riverbank boss, 2026-10-24, silent for minutes): said again then.
            var hints = change.Kind == ScreenChangeKind.TopLayerChanged
                ? new List<ScreenText>()
                : change.Texts.Where(text => this.TutorialIsNews(text.Text, focus == null && snapshot.TopInteractive?.Address == this.HudLayer)).ToList();
            if (hints.Count > 0)
            {
                // Its button line is the lesson's instruction: "You received a passive point. View to open the menus"
                // holds the character until View (2026-10-24: only the first line was said, the player stood still).
                hints.AddRange(legends);
            }
            if (hints.Count > 0)
            {
                // A lesson is said whole and what would cut it waits ("Before going on, engrave your uncut skill gem" cut by
                // the beacon, 2026-10-24; "Navigate to your passive tree" by inventory moves, 2026-10-26). In a menu it
                // cuts in, then the focus it cut off; in play the objectives' speech setting says whether it waits.
                var inMenu = focus != null;
                var cutFocus = inMenu && this.deferredFocus == null &&
                               (DateTime.UtcNow - this.lastFocusAt).TotalMilliseconds < SpeechFloor.SpokenMs(this.lastFocusSpeech);
                this.Speak("objective", Describe(hints), inMenu || SpeechPriority.Interrupts(SpeechKind.Quest), Hold.Lesson);
                if (cutFocus)
                {
                    this.output.Speak(this.lastFocusSpeech);
                }
            }
        }

        /// <summary>A panel with no focus covers the game (<see cref="ScreenSnapshot.PanelOver"/>).</summary>
        public Func<bool>? PanelOverGame { get; set; }

        /// <summary>A quest page that waited for a menu or for the area's line can be said now.</summary>
        public bool QuestPageDue => this.questPage.Waiting && !this.InMenu && !this.AreaUnnamed;

        private bool AreaUnnamed => this.AreaLinePending?.Invoke() ?? false;

        /// <summary>The character selection was reached: what was said of the last character's quests says nothing of the next one's.</summary>
        public void CharacterChanged() => this.questPage.Reset();

        /// <summary>The player is in a menu: the focus is in a panel, or a panel without focus covers the game.</summary>
        private bool InMenu
        {
            get
            {
                var layer = this.FocusLayer?.Invoke() ?? IntPtr.Zero;
                return (layer != IntPtr.Zero && layer != this.HudLayer) || this.PanelOverGame?.Invoke() == true;
            }
        }

        private void OnHudChange(ScreenChange change, ScreenSnapshot snapshot)
        {
            this.hudSpoken.UnionWith(this.CheckQuestPage(snapshot).Select(text => ScreenDiffer.Shape(text.Text)));
            if (this.hudBaseline)
            {
                this.hudBaseline = false;
                this.hudSpoken.UnionWith(change.Texts.Select(text => ScreenDiffer.Shape(text.Text)));
                return;
            }

            // Closing every panel makes the HUD the top layer: that is not news. What appears on it is, once per
            // area — the quest tracker reappears whenever a panel closes; bare numbers are flask charges and timers.
            if (change.Kind != ScreenChangeKind.TopLayerChanged)
            {
                var ignored = this.IgnoredHudText?.Invoke() ?? IntPtr.Zero;
                // The alert line keeps its last text after it fades: the HUD reopening lists it again, so only an alert
                // that appears while the HUD is up is news ("Inventario lleno" on every panel close, 2026-10-27).
                var alerts = change.Texts.Where(text => !IsNumber(text.Text) && (this.IsSkillBarText?.Invoke(text.Address) ?? false)).ToList();
                var alertNews = change.Kind == ScreenChangeKind.TextsAppeared ? alerts.Where(text => this.AlertIsNews(text.Text)).ToList() : new List<ScreenText>();
                if (alertNews.Count > 0)
                {
                    // Held: "Ya no llevas ningún objeto" cut "Inventario lleno" off 120 ms in (2026-10-26).
                    this.Speak("alert", Describe(alertNews), false, Hold.Message);
                }

                // By its shape: the mana orb's "Maná, 108/223" was news at every panel closed, with other numbers each time.
                var news = change.Texts.Except(alerts).Where(text => text.Address != ignored && !IsNumber(text.Text) && !GaugeLine.IsMatch(text.Text) &&
                                                                     this.hudSpoken.Add(ScreenDiffer.Shape(text.Text))).ToList();
                // A tooltip that shows again while the HUD is up is another item's, or the same one aimed at again: news
                // each time, whatever was said of that title before (a second stack of a currency was silent). The HUD
                // coming back from behind a panel shows again what it showed before, which is none.
                var shownAgain = news.Count == 0 && change.Kind == ScreenChangeKind.TextsAppeared && !this.InMenu &&
                                 (DateTime.UtcNow - this.hudBackAt).TotalMilliseconds >= HudBackMs &&
                                 change.Texts.Any(text => !IsNumber(text.Text) && !PropertyLine.IsMatch(text.Text));
                var itemTarget = (news.Count > 0 || shownAgain) && this.ItemTargetNamed?.Invoke() == true;
                var item = news.Count > 0 || shownAgain ? this.GroundItemName?.Invoke(change.Texts.Select(text => text.Text)) : null;
                if (item != null || (itemTarget && news.Count > 0 && change.Texts.Any(text => PropertyLine.IsMatch(text.Text))))
                {
                    var quality = change.Texts.FirstOrDefault(text => QualityLine.IsMatch(text.Text));
                    // The game shows this tooltip for what A uses, already named with the button: only its quality is new.
                    var said = itemTarget ? quality?.Text ?? string.Empty : quality == null ? item ?? string.Empty : $"{item}, {quality.Text}";
                    var settling = said == this.groundItemSaid && (DateTime.UtcNow - this.groundItemAt).TotalMilliseconds < FocusTextMergeMs;
                    if (item is not "" && !settling && DateTime.UtcNow >= this.hudQuietUntil && this.QuietGroundItems?.Invoke() != true)
                    {
                        this.Speak("ground item", said, false);
                        this.groundItemSaid = said;
                    }

                    this.groundItemAt = DateTime.UtcNow; // the stats that follow ("Attacks per Second: 1.40") are its tooltip
                }
                else if (news.Where(text => !PropertyLine.IsMatch(text.Text)).ToList() is { Count: > 0 } lines && DateTime.UtcNow >= this.hudQuietUntil &&
                         (DateTime.UtcNow - this.groundItemAt).TotalMilliseconds >= FocusTextMergeMs)
                {
                    this.Speak("hud", Describe(lines), false);
                }
            }
        }

        private bool TutorialIsNews(string text, bool playing)
        {
            var now = DateTime.UtcNow;
            var boxBack = !playing || (now - this.hudBackAt).TotalMilliseconds < HudBackMs;
            if (this.tutorialSpoken.TryGetValue(text, out var said) && !LessonReminder.IsDue(now - said, boxBack))
            {
                return false;
            }

            this.tutorialSpoken[text] = now;
            return true;
        }

        /// <summary>
        ///     The player pushes the stick and the character stands still: the lesson on screen, if any, is what holds it
        ///     ("You have low mana. Press D-pad right…" held the Monk in silence, 2026-10-24). Said again, kept whole.
        /// </summary>
        public void RepeatLesson()
        {
            if (!this.inGame || this.seenSnapshot?.Find(this.TutorialLayer) is not { Texts.Count: > 0 } lesson)
            {
                return;
            }

            this.Speak("lesson held", Describe(lesson.Texts), false, Hold.Lesson);
        }
    }
}
