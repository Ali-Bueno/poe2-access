namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Pre-game rules of <see cref="ScreenNarrator"/>: popups, the top layer, selectors and the screen's hints.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>Before the game: popups, the top layer, closing popups and selectors.</summary>
        private void OnPreGameChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            var top = snapshot.TopInteractive;
            var selected = this.SelectorChoice?.Invoke(change.Layer.Address);
            var choiceSwapped = selected != null && this.lastSelectorChoice != null && selected != this.lastSelectorChoice;
            this.lastSelectorChoice = selected ?? this.lastSelectorChoice;
            switch (change.Kind)
            {
                case ScreenChangeKind.TopLayerChanged:
                    var returning = this.lastTop != IntPtr.Zero;
                    this.lastTop = change.Layer.Address;
                    if (focus != null && focusLayer == change.Layer.Address && this.ReadScreenMessages && (this.JustFocused() || this.deferredFocus != null))
                    {
                        // A screen opened with its focus (delete character: "Are you sure…? yarkey…", then Cancel):
                        // its message, then the focus.
                        var focusTexts = new HashSet<string>(focus.ListItems) { focus.Text, focus.Value };
                        // A caption ending in a colon names a field ("League filter:"), it is not the screen's message.
                        var screenMessage = Describe(change.Texts.Where(text => !text.IsControl && !focusTexts.Contains(text.Text) &&
                                                                                !text.Text.TrimEnd().EndsWith(':')));
                        if (screenMessage.Length > 0)
                        {
                            this.Speak("screen with focus", Announcements.Sentences(new[] { screenMessage, this.lastFocusSpeech }), true);
                        }
                    }
                    else if (focus == null || focusLayer != change.Layer.Address)
                    {
                        // The same screen reported again right after its texts were read (class choice: the class and
                        // its quote, then the screen with its title): only what was not said yet.
                        var recent = (DateTime.UtcNow - this.recentTexts.At).TotalMilliseconds < RecentTextsMs ? this.recentTexts.Texts : new HashSet<string>();
                        var unsaid = change.Texts.Where(text => !recent.Contains(text.Text)).ToList();
                        if (unsaid.Count > 0)
                        {
                            this.Speak("top layer", Describe(unsaid), unsaid.Count == change.Texts.Count);
                        }
                    }
                    else if (returning && !this.JustFocused())
                    {
                        // A popup closed: the focus underneath did not move, so the tracker stays silent.
                        this.Speak("top layer, focus", this.Focused(focus), true);
                    }

                    break;
                case ScreenChangeKind.LayerOpened:
                    if (change.Layer.Address != top?.Address && !change.Layer.HasControls && !IsBehind(change.Layer, top))
                    {
                        this.Speak("layer", Describe(change.Texts), false);
                    }

                    break;
                case ScreenChangeKind.TextsAppeared when change.Layer.Address == focusLayer && this.JustFocused() && change.Texts.Count >= PageChangeTexts:
                    // A new page in the same layer (options tab switch): the tab, then the focus — never the page's labels.
                    var tab = this.selectedTab(change.Layer.Address);
                    this.Speak("page with focus", tab.Length > 0 ? Announcements.InContext(Announcements.TabTitle(tab), this.lastFocusSpeech) : this.lastFocusSpeech, true);
                    break;
                case ScreenChangeKind.TextsAppeared when change.Layer.Address == focusLayer && this.JustFocused() &&
                                                         change.Texts.All(text => text.Name.EndsWith(PopupSuffix, StringComparison.Ordinal)):
                    // A description the focused control shows beside itself (the league selector's league_combobox_popup):
                    // after the control, like a tooltip.
                    this.Speak("popup of focus", Describe(change.Texts), false);
                    break;
                case ScreenChangeKind.TextsAppeared when change.Layer.Address == focusLayer && this.JustFocused():
                    // A popup built inside the focused layer (exit confirmation): its message, then the focused button.
                    var items = new HashSet<string>(focus?.ListItems ?? Array.Empty<string>());
                    var message = Describe(change.Texts.Where(text => !text.IsControl && text.Text != focus?.Text && !items.Contains(text.Text) &&
                                                                     !(focus?.Value.Contains(text.Text, StringComparison.Ordinal) ?? false)));
                    if (message.Length > 0)
                    {
                        this.Speak("message with focus", Announcements.Sentences(new[] { message, this.lastFocusSpeech }), true);
                    }
                    else if (choiceSwapped)
                    {
                        // X in the name step swaps the class for its variant and focuses the name field: the card the
                        // variant brings (a control's caption) is the news, the field was just said.
                        this.Speak("choice swapped", Describe(change.Texts.Where(text => text.Text != focus?.Text &&
                                                                                        !(focus?.Value.Contains(text.Text, StringComparison.Ordinal) ?? false))), true);
                    }

                    break;
                case ScreenChangeKind.TextsAppeared when !this.inGame && change.Layer.Name == NoticeLayerName && IsBehind(change.Layer, top):
                    // The pre-game notice corner sits under the screen it reports on ("Input method set to Controller"
                    // on a fresh install's first button press, 2026-10-24); its connection log ("> Connecting…") is
                    // the state's own announcement.
                    var notices = change.Texts.Where(text => !text.Text.StartsWith('>')).ToList();
                    if (notices.Count > 0 && this.deferredFocus != null)
                    {
                        // The press that switched the input also moved the focus: the focus first, the notice after.
                        this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, Describe(notices) }.Where(part => part.Length > 0));
                    }
                    else if (notices.Count > 0)
                    {
                        this.Speak("notice", Describe(notices), false);
                    }

                    break;
                case ScreenChangeKind.TextsAppeared:
                    if (IsBehind(change.Layer, top) && change.Layer.Address != focusLayer)
                    {
                        break;
                    }

                    var own = focus == null ? new HashSet<string>() : new HashSet<string> { focus.Text, focus.Value };
                    var fresh = change.Texts.Where(text => !own.Contains(text.Text)).ToList();
                    var navigatingWithoutFocus = change.Layer.Address == top?.Address && focusLayer != change.Layer.Address;
                    var freshTexts = new HashSet<string>(fresh.Select(text => text.Text));
                    if (fresh.Count > 0 && navigatingWithoutFocus && this.SelectorPosition?.Invoke(change.Layer.Address) is { } position &&
                        position != this.lastSelectorPosition)
                    {
                        // A choice moved with the stick (the class of character creation): where it is, after its name.
                        this.lastSelectorPosition = position;
                        // The name sits before its quote, the longest text; the screen's title can come first ("Elige tu clase").
                        var quote = fresh.IndexOf(fresh.MaxBy(text => text.Text.Length)!);
                        var named = Math.Max(0, quote - 1);
                        fresh[named] = fresh[named] with { Text = $"{fresh[named].Text}, {position}" };
                    }

                    if (fresh.Count > 0)
                    {
                        this.Speak("appeared", Describe(fresh), navigatingWithoutFocus);
                        // As shown, not with the position: the screen's title arriving next must find the class said.
                        this.recentTexts = (freshTexts, DateTime.UtcNow);
                    }

                    break;
            }
        }

        /// <summary>
        ///     Per frame before the game (login, character selection and creation): the controller hints are how a
        ///     player learns these screens ("Change class, left stick"; "Next class information, D-pad left and
        ///     right"), so a new set is read after what is being said, once it has settled.
        /// </summary>
        public void OnPreGameHints(ScreenSnapshot snapshot)
        {
            if (this.FocusLayer?.Invoke() is { } focusLayer && focusLayer != IntPtr.Zero &&
                snapshot.Find(focusLayer)?.Texts.Any(text => text.Name == GameLayout.InGameUi.MessageLabelName) == true)
            {
                // A message box holds the focus ("Disconnected… OK"): the bar still shows the screen behind it ("Press
                // to play, A"), which is not what A does now (2026-10-24).
                return;
            }

            var legends = snapshot.Layers.Where(layer => layer.IsHintBar).SelectMany(layer => layer.Texts).ToList();
            var hints = Describe(legends);
            if (hints.Length > 0 && hints == this.staleHints.Text && (DateTime.UtcNow - this.staleHints.Since).TotalMilliseconds < StaleHintsMs)
            {
                // The bar still showed the login's buttons as character selection opened: they were read after the
                // character as if they were its own (2026-10-25).
                return;
            }

            if (hints == this.lastHints)
            {
                this.pendingHints = (hints, DateTime.MinValue);
                return;
            }

            if (hints != this.pendingHints.Text)
            {
                this.pendingHints = (hints, DateTime.UtcNow);
                return;
            }

            if ((DateTime.UtcNow - this.pendingHints.Since).TotalMilliseconds >= HintsSettleMs)
            {
                // A new screen reads all its hints; a focus move on the same screen only the ones it added
                // ("Start in town, LT plus A" on a character), never the whole bar again.
                var fresh = legends.Where(legend => !this.spokenHints.Contains(Describe(new[] { legend }))).ToList();
                this.lastHints = hints;
                foreach (var legend in legends)
                {
                    this.spokenHints.Add(Describe(new[] { legend }));
                }

                if (this.deferredFocus != null)
                {
                    // The focus waiting for its screen goes first and the bar after it: back at character selection
                    // the buttons were said first and cut off by the character 60 ms later (2026-10-24).
                    this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, Describe(fresh) }.Where(part => part.Length > 0));
                }
                else
                {
                    this.Speak("pre-game hints", Describe(fresh), false);
                    this.cuttableHints = (Describe(fresh), DateTime.UtcNow);
                }
            }
        }
    }
}
