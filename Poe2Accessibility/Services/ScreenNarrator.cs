namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Decides what the focus tracker and the screen watcher say. Rules (from the D2R and LeAccess mods):
    ///     a context change interrupts; the layer that owns input is the only one that speaks; a layer with no
    ///     focus inside is read as text (title, message, controls); closing a popup re-reads the focus.
    /// </summary>
    public sealed class ScreenNarrator
    {
        /// <summary>Two snapshots (~300 ms) confirm a text; one that lands this soon after a focus move belongs to it.</summary>
        private const int FocusTextMergeMs = 700;

        /// <summary>The panel tab bar's title label (control id in the game layout, 2026-10-23).</summary>
        private const string TabTitleName = "label_tab_title";

        /// <summary>A batch this large arriving with a focus move is a new page (tab switch), not a popup message.</summary>
        private const int PageChangeTexts = 6;

        /// <summary>A layer without focus showing at most this many texts is a confirmation box.</summary>
        private const int SmallBoxTexts = 4;

        /// <summary>A layer opening with this many texts is a panel to move through, not a box to read out.</summary>
        private const int LargeLayerTexts = 16;

        /// <summary>A tab switch shows up as texts appearing and then as the top layer changing: one announcement.</summary>
        private const int RepeatedTabMs = 1500;

        private readonly IAccessibilityOutputService output;
        private readonly Func<IntPtr, string> selectedTab;
        private readonly PluginLog log;

        /// <param name="selectedTab">Label of the selected tab in a layer (empty when none); read only on page changes.</param>
        public ScreenNarrator(IAccessibilityOutputService output, PluginLog log, Func<IntPtr, string> selectedTab)
        {
            this.output = output;
            this.log = log;
            this.selectedTab = selectedTab;
        }

        private IntPtr lastTop;
        private string lastContext = string.Empty;
        private string lastFocusSpeech = string.Empty;
        private string lastGroup = string.Empty;
        private readonly HashSet<string> hudSpoken = new();
        private readonly HashSet<string> tutorialSpoken = new();
        private AccessibleUiNode? pendingFocus;
        private DateTime lastFocusAt = DateTime.MinValue;
        private DateTime lastTabAt = DateTime.MinValue;

        /// <summary>A panel's opening and its becoming the top layer arrive in one frame (death screen): one reading.</summary>
        private (IntPtr Layer, string Text, DateTime At) lastPanel;

        /// <summary>Texts just read out, so the same screen reported again a moment later (top layer) is not repeated.</summary>
        private (HashSet<string> Texts, DateTime At) recentTexts = (new HashSet<string>(), DateTime.MinValue);

        /// <summary>Pre-game button hints: the last spoken, and a new set waiting to settle.</summary>
        private string lastHints = string.Empty;
        private readonly HashSet<string> spokenHints = new();
        private (string Text, DateTime Since) pendingHints = (string.Empty, DateTime.MinValue);

        /// <summary>
        ///     A focus that lands in another layer, or right after A was pressed (a confirmation popup is built
        ///     inside the menu's own layer), waits this long for the popup's message, so "Confirm, button" is not
        ///     said and then cut off by "Exit? Confirm, button".
        /// </summary>
        private const int NewLayerFocusDelayMs = 300;

        /// <summary>A panel chosen in the radial menu opens this long after its name was said.</summary>
        private const int ChosenPanelMs = 2500;

        /// <summary>Longest one-word label a bare number before it is paired with ("DEX", "Strength").</summary>
        private const int MaxPairedLabel = 12;

        private (string Text, DateTime At, AccessibleUiNode Node)? deferredFocus;

        /// <summary>A tab's page waiting to see whether a focus lands on it: tab, page, its texts, when.</summary>
        private (string Tab, string Page, HashSet<string> Texts, DateTime At)? pendingPage;
        private IntPtr lastFocusLayer;

        /// <summary>The layer holding the focus (FocusTracker), to notice a focus that moved into a popup.</summary>
        public Func<IntPtr>? FocusLayer { get; set; }

        /// <summary>The focused node described again (its panel may have been built since); null when gone.</summary>
        public Func<AccessibleUiNode, AccessibleUiNode?>? Refresh { get; set; }

        /// <summary>Whether a text lies inside the panel with the given title; null reads every text.</summary>
        public Func<IntPtr, string, bool>? InPanel { get; set; }

        /// <summary>Read a screen's message with its first focus (delete character); off for the game menu.</summary>
        public bool ReadScreenMessages { get; set; } = true;

        /// <summary>Milliseconds since the confirm button (A) was last pressed.</summary>
        public Func<long>? SinceConfirm { get; set; }

        /// <summary>A focus change this soon after A is the screen the press opened.</summary>
        private const int ConfirmOpensMs = 1000;

        /// <summary>The last narration as a set of its parts, to drop the same news reported twice.</summary>
        private (string Parts, DateTime At) lastNarrated = (string.Empty, DateTime.MinValue);

        /// <summary>Recently read texts are left out of a repeat within this time.</summary>
        private const int RecentTextsMs = 1500;

        /// <summary>A new set of pre-game hints must stay this long before it is read (screens rebuild in steps).</summary>
        private const int HintsSettleMs = 400;
        private bool inGame;
        private IntPtr valueOwner;
        private string lastValue = string.Empty;

        /// <summary>
        ///     New game state: its first top layer is not a popup closing. In game only the panel holding the focus
        ///     speaks (HUD, chat and map are always shown and toggle with panels, so layer events are noise).
        /// </summary>
        public void Reset(bool inGameState)
        {
            this.lastTop = IntPtr.Zero;
            this.lastContext = string.Empty;
            this.lastGroup = string.Empty;
            this.hudSpoken.Clear();
            this.tutorialSpoken.Clear();
            this.inGame = inGameState;
            this.lastHints = string.Empty;
            this.spokenHints.Clear();
            this.pendingHints = (string.Empty, DateTime.MinValue);
            this.lastFocusLayer = IntPtr.Zero;
            this.deferredFocus = null;
            this.pendingPage = null;
        }

        /// <summary>
        ///     Per frame before the game (login, character selection and creation): the controller hints are how a
        ///     player learns these screens ("Change class, left stick"; "Next class information, D-pad left and
        ///     right"), so a new set is read after what is being said, once it has settled.
        /// </summary>
        public void OnPreGameHints(ScreenSnapshot snapshot)
        {
            var legends = snapshot.Layers.Where(layer => layer.IsHintBar).SelectMany(layer => layer.Texts).ToList();
            var hints = Describe(legends);
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

                this.Speak("pre-game hints", Describe(fresh), false);
            }
        }

        /// <summary>Read long tooltips with the focus (setting); short ones always are.</summary>
        public bool ReadTooltips { get; set; }

        /// <summary>The in-game objective / tutorial box: whatever it shows next is read, whoever has the focus.</summary>
        public IntPtr TutorialLayer { get; set; }

        /// <summary>A HUD text read elsewhere (the target display's name, TargetNarrator), left out of the HUD news.</summary>
        public Func<IntPtr>? IgnoredHudText { get; set; }

        /// <summary>The in-game HUD: new texts (quest tracker, ground item) are read after the rest, never as a page.</summary>
        public IntPtr HudLayer { get; set; }

        public void OnFocusChanged(AccessibleUiNode node)
        {
            this.lastFocusAt = DateTime.UtcNow;
            if (this.inGame && node.NamedByControlId && node.Role is UiRole.Panel or UiRole.Unknown)
            {
                // Only a control id names it ("skillgem socket", inspect "layout"): a tooltip that appears now says
                // more, so wait for one and fall back to the id in Tick.
                this.pendingFocus = node;
                this.lastFocusSpeech = this.Focused(node);
                return;
            }

            this.pendingFocus = null;

            this.valueOwner = node.Address;
            this.lastValue = node.Value;
            var focused = this.Focused(node);
            if (node.Group.Length > 0 && node.Group != this.lastGroup)
            {
                focused = Announcements.InContext(node.Group, focused);
            }

            this.lastGroup = node.Group;
            var text = focused;
            if (node.Context.Length > 0 && node.Context != this.lastContext)
            {
                if (!this.JustChosen(node.Context))
                {
                    text = Announcements.InContext(node.Context, text);
                }

                this.lastContext = node.Context;
            }

            this.lastFocusSpeech = focused;
            var layer = this.FocusLayer?.Invoke() ?? IntPtr.Zero;
            var intoNewLayer = layer != this.lastFocusLayer ||
                               (this.SinceConfirm?.Invoke() ?? long.MaxValue) < ConfirmOpensMs;
            this.lastFocusLayer = layer;
            if (intoNewLayer)
            {
                this.deferredFocus = (text, DateTime.UtcNow, node);
                return;
            }

            this.deferredFocus = null;
            this.output.Speak(this.WithPendingPage(text), true);
        }

        /// <summary>
        ///     The focused element's tooltip appeared or changed after it was announced (an item's tooltip is built a
        ///     moment after the cursor lands): read it after what is being said, or name an id-only focus with it.
        /// </summary>
        public void OnTooltipChanged(AccessibleUiNode node)
        {
            if (this.pendingFocus?.Address == node.Address)
            {
                this.OnFocusChanged(node);
                return;
            }

            if (this.ReadTooltips && node.Tooltip.Count > 0 && node.Address == this.valueOwner)
            {
                this.Speak("tooltip of focus", Announcements.Sentences(node.Tooltip), false);
            }
        }

        private string Focused(AccessibleUiNode node) => Announcements.Focused(node, this.ReadTooltips);

        /// <summary>Per frame: a focus named only by its control id is spoken once no tooltip described it in time.</summary>
        public void Tick()
        {
            if (this.deferredFocus is { } deferred && (DateTime.UtcNow - deferred.At).TotalMilliseconds >= NewLayerFocusDelayMs)
            {
                this.deferredFocus = null;
                var text = deferred.Text;
                if (deferred.Node.Context.Length == 0 && this.Refresh?.Invoke(deferred.Node) is { Context.Length: > 0 } fresh &&
                    fresh.Context != this.lastContext)
                {
                    // The panel finished building after its focus arrived (Skills beside the inventory): its title now.
                    text = Announcements.InContext(fresh.Context, text);
                    this.lastContext = fresh.Context;
                }

                this.Speak("focus in a new layer", this.WithPendingPage(text), true);
            }

            if (this.pendingPage is { } pending && this.deferredFocus == null &&
                (DateTime.UtcNow - pending.At).TotalMilliseconds >= NewLayerFocusDelayMs)
            {
                // No focus came with the page (Challenges: a free cursor): the page alone, after its tab.
                this.Speak("tab", this.WithPendingPage(string.Empty), true);
            }

            if (this.pendingFocus != null && !this.JustFocused())
            {
                this.Speak("focus by control id", this.lastFocusSpeech, true);
                this.pendingFocus = null;
            }
        }

        /// <summary>Only the new value; a text field echoes just the typed or deleted characters.</summary>
        public void OnValueChanged(AccessibleUiNode node)
        {
            var before = node.Address == this.valueOwner ? this.lastValue : string.Empty;
            this.valueOwner = node.Address;
            this.lastValue = node.Value;
            if (node.Role is UiRole.GridCell or UiRole.ItemSlot or UiRole.ListItem)
            {
                // The cursor moved inside the grid: a new item and cell, not a value of the same control.
                this.output.Speak(this.Focused(node), true);
                return;
            }

            if (node.Role != UiRole.TextField && before.Length == 0 && node.Value.Length == 0)
            {
                // Only the label changed: the next NPC dialogue line. The screen narration leaves out texts the
                // focus already holds, so the line is said here; "empty" is the blank between two lines.
                if (node.Text.Length > 0 && node.Text != Announcements.Empty)
                {
                    this.output.Speak(this.Focused(node), true);
                }

                return;
            }

            var edit = node.Role == UiRole.TextField ? TextEdit.Between(before, node.Value) : null;
            this.output.Speak(edit is { } typed ? Announcements.Typed(typed.Added, typed.Removed) : Announcements.ValueOrEmpty(node.Value), true);
        }

        public void OnScreenChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            this.log.Debug($"Screen change {change.Kind} layer 0x{change.Layer.Address.ToInt64():X} focus layer 0x{focusLayer.ToInt64():X} hint={change.Layer.IsHintBar} top=0x{snapshot.TopInteractive?.Address.ToInt64() ?? 0:X} justFocused={this.JustFocused()}");
            if (change.Layer.IsHintBar)
            {
                if (this.inGame && focusLayer == IntPtr.Zero && change.Kind == ScreenChangeKind.TextsAppeared &&
                    (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < FocusTextMergeMs)
                {
                    // What the map cursor's place offers ("Instance Manager, A" on a town): after the place.
                    this.Speak("action of panel without focus", Describe(change.Texts), false);
                }

                return;
            }

            // Legends inside a content layer (the game menu's own hint bar) are read on request, not with the content.
            var legends = change.Texts.Where(ScreenWatcher.IsLegend).ToList();
            change = change with { Texts = change.Texts.Where(text => !ScreenWatcher.IsLegend(text)).ToList() };
            if (change.Texts.Count == 0 && change.Kind != ScreenChangeKind.TopLayerChanged)
            {
                return;
            }

            if (this.inGame && change.Layer.Address == this.TutorialLayer)
            {
                // A new objective or tutorial hint: after what is being said, never instead of it; once per area, as
                // the box shows the same hint again whenever a panel or the radial menu closes.
                var hints = change.Kind == ScreenChangeKind.TopLayerChanged
                    ? new List<ScreenText>()
                    : change.Texts.Where(text => this.tutorialSpoken.Add(text.Text)).ToList();
                if (hints.Count > 0)
                {
                    this.Speak("objective", Describe(hints), false);
                }

                return;
            }

            if (this.inGame && change.Layer.Address == this.HudLayer)
            {
                // Closing every panel makes the HUD the top layer: that is not news. What appears on it is, once per
                // area — the quest tracker reappears whenever a panel closes; bare numbers are flask charges and timers.
                if (change.Kind != ScreenChangeKind.TopLayerChanged)
                {
                    var ignored = this.IgnoredHudText?.Invoke() ?? IntPtr.Zero;
                    var news = change.Texts.Where(text => text.Address != ignored && !IsNumber(text.Text) && this.hudSpoken.Add(text.Text)).ToList();
                    if (news.Count > 0)
                    {
                        this.Speak("hud", Describe(news), false);
                    }
                }

                return;
            }

            if (this.inGame)
            {
                var tabTitle = change.Texts.FirstOrDefault(text => text.Name == TabTitleName);
                if (tabTitle != null)
                {
                    // A tab switch rebuilds the whole panel: say only the tab, then where the focus is (D2R rule) — the
                    // one that moved with it, or the one that stayed (the inventory beside Skills and Character).
                    var withFocus = this.JustFocused();
                    if (this.lastContext == tabTitle.Text && (DateTime.UtcNow - this.lastTabAt).TotalMilliseconds < RepeatedTabMs)
                    {
                        return; // the same switch reported twice
                    }

                    // Already named: by the focus announcement (its panel title), or by the radial menu as it was chosen.
                    var named = (withFocus && this.lastContext == tabTitle.Text) || this.JustChosen(tabTitle.Text);
                    this.lastContext = tabTitle.Text;
                    this.lastTabAt = DateTime.UtcNow;
                    var freeCursor = (focus == null || focus.IsHovered) && change.Layer.Address == snapshot.TopInteractive?.Address &&
                                     (focusLayer == IntPtr.Zero || focusLayer == change.Layer.Address);
                    if (freeCursor)
                    {
                        // A page the free cursor browses (Challenges, the passive tree): no control holds what it shows,
                        // so its content follows the tab ("Skill points: 0", the attributes); a progress count ("0/8")
                        // stays, other bare numbers mean nothing alone. The page under an overlay page stays visible to
                        // the tree (Friends under Challenges): only the texts inside the panel the tab names.
                        var pageTexts = PairNumbers(change.Texts).Where(text => text != tabTitle && (!IsNumber(text.Text) || text.Text.Contains('/'))).ToList();
                        var inPage = this.InPanel == null ? pageTexts : pageTexts.Where(text => this.InPanel(text.Address, tabTitle.Text)).ToList();
                        var page = Describe(inPage.Count > 0 ? inPage : pageTexts);
                        var pageSaid = new HashSet<string>(change.Texts.Select(text => text.Text));
                        this.lastPanel = (change.Layer.Address, page, DateTime.UtcNow);
                        if (focus != null && this.deferredFocus == null)
                        {
                            // The hovered element was already said: the page after it.
                            this.recentTexts = (pageSaid, DateTime.UtcNow);
                            this.Speak("page after focus", page, false);
                            return;
                        }

                        // A cursor may still land on the page (the passive tree's node): one utterance with it, else alone.
                        this.pendingPage = (named ? string.Empty : tabTitle.Text, page, pageSaid, DateTime.UtcNow);
                        return;
                    }

                    if (named)
                    {
                        return;
                    }

                    var focusSpeech = withFocus ? this.lastFocusSpeech : focus != null ? this.Focused(focus) : string.Empty;
                    this.Speak("tab", focusSpeech.Length > 0 ? Announcements.InContext(tabTitle.Text, focusSpeech) : tabTitle.Text, true);
                    return;
                }

                if (focusLayer == IntPtr.Zero && change.Layer.Address == snapshot.TopInteractive?.Address)
                {
                    // A panel nothing focuses or hovers (world map): what its cursor shows is the content. Right
                    // after the tab name it follows it.
                    // A small box without focus (Engrave? Confirm A, Back B) is answered with its buttons: say them.
                    // A bare number means nothing alone (a stash tab's title while the focus moves to the new tab).
                    var content = change.Texts.Where(text => !IsNumber(text.Text)).ToList();
                    var shown = content.Count <= SmallBoxTexts && legends.Count > 0
                        ? Announcements.Sentences(new[] { Describe(content), Describe(legends) })
                        : Describe(content);
                    var repeated = this.lastPanel.Layer == change.Layer.Address && this.lastPanel.Text == shown &&
                                   (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < RepeatedTabMs;
                    if (shown.Length > 0 && !repeated)
                    {
                        this.lastPanel = (change.Layer.Address, shown, DateTime.UtcNow);
                        this.Speak("panel without focus", shown, (DateTime.UtcNow - this.lastTabAt).TotalMilliseconds >= FocusTextMergeMs);
                    }

                    return;
                }

                if (focusLayer == IntPtr.Zero && !change.Layer.IsBase && !change.Layer.HasControls && change.Texts.Count <= SmallBoxTexts &&
                    change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared)
                {
                    // A small box showing one choice at a time with nothing focused: the radial menu (hold View) shows
                    // only the option the stick points at.
                    var choice = Describe(change.Texts);
                    if (!(this.lastPanel.Layer == change.Layer.Address && this.lastPanel.Text == choice &&
                          (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < RepeatedTabMs))
                    {
                        this.lastPanel = (change.Layer.Address, choice, DateTime.UtcNow);
                        this.Speak("box without focus", choice, true);
                    }

                    return;
                }

                var justFocused = this.JustFocused();
                if (change.Layer.Address != focusLayer && !justFocused)
                {
                    return;
                }

                var own = focus == null ? new HashSet<string>() : new HashSet<string>(focus.ListItems) { focus.Text, focus.Value, focus.Group };
                var said = (DateTime.UtcNow - this.recentTexts.At).TotalMilliseconds < RecentTextsMs ? this.recentTexts.Texts : new HashSet<string>();
                var texts = Describe(PairNumbers(change.Texts).Where(text => !own.Contains(text.Text) && !IsNumber(text.Text) && !said.Contains(text.Text) &&
                                                                !(focus?.Text.Contains(text.Text, StringComparison.Ordinal) ?? false) &&
                                                                !InFocusContext(focus, text.Text)));
                if (texts.Length == 0)
                {
                    return;
                }

                var pageTab = justFocused && this.pendingFocus == null && change.Texts.Count >= PageChangeTexts
                    ? this.selectedTab(change.Layer.Address)
                    : string.Empty;
                if (this.pendingFocus != null && justFocused)
                {
                    // The tooltip names what the id could not.
                    this.pendingFocus = null;
                    this.Speak("tooltip for unnamed focus", texts, true);
                }
                else if (pageTab.Length > 0)
                {
                    // A tabbed page arrived with the focus (options opened from the game menu, an options tab switch):
                    // the selected tab and the focus, as pre-game — never the page's labels.
                    this.Speak("page with focus", Announcements.InContext(pageTab, this.lastFocusSpeech), true);
                }
                else if (change.Layer.Address != focusLayer)
                {
                    // A tooltip beside the focused control (support gem slot): after the control.
                    this.Speak("tooltip", texts, false);
                }
                else if (change.Kind == ScreenChangeKind.LayerOpened && justFocused && change.Texts.Count >= LargeLayerTexts)
                {
                    // A whole panel opened on the focus (skill crafting: 24 skills and their levels): its labels are
                    // what the focus moves through, so the focus alone, already said, stays.
                }
                else if (change.Kind == ScreenChangeKind.LayerOpened && justFocused && this.selectedTab(change.Layer.Address) is { Length: > 0 } openedTab)
                {
                    // A tabbed panel (vendor stock): its tab, what it shows besides controls and stack counts (gold), the focus.
                    var shown = Describe(change.Texts.Where(text => !text.IsControl && !IsNumber(text.Text) && !own.Contains(text.Text)));
                    this.Speak("tabbed panel with focus", Announcements.Sentences(new[] { Announcements.TabTitle(openedTab), shown, this.lastFocusSpeech }), true);
                }
                else if (change.Kind == ScreenChangeKind.LayerOpened && justFocused)
                {
                    // A box opened on the focus (NPC dialogue: speaker, then the line and Continue the focus holds).
                    this.Speak("opened with focus", Announcements.Sentences(new[] { texts, this.lastFocusSpeech }), true);
                }
                else if (change.Kind == ScreenChangeKind.LayerOpened)
                {
                    this.Speak("opened with focus", texts, true);
                }
                else if (change.Kind == ScreenChangeKind.TextsAppeared && justFocused)
                {
                    // The text came with the focus move (a skill's details, an expanded definition): the focus again,
                    // since this cuts it off, then the text — the name before a long description, never after it.
                    this.Speak("appeared with focus", Announcements.Sentences(new[] { this.lastFocusSpeech, texts }), true);
                }
                else if (change.Kind == ScreenChangeKind.TextsAppeared)
                {
                    this.Speak("appeared in focus layer", texts, false);
                }

                return;
            }

            var top = snapshot.TopInteractive;
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
                    this.Speak("page with focus", tab.Length > 0 ? Announcements.InContext(tab, this.lastFocusSpeech) : this.lastFocusSpeech, true);
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

                    break;
                case ScreenChangeKind.TextsAppeared:
                    if (IsBehind(change.Layer, top) && change.Layer.Address != focusLayer)
                    {
                        break;
                    }

                    var own = focus == null ? new HashSet<string>() : new HashSet<string> { focus.Text, focus.Value };
                    var fresh = change.Texts.Where(text => !own.Contains(text.Text)).ToList();
                    var navigatingWithoutFocus = change.Layer.Address == top?.Address && focusLayer != change.Layer.Address;
                    if (fresh.Count > 0)
                    {
                        this.Speak("appeared", Describe(fresh), navigatingWithoutFocus);
                        this.recentTexts = (new HashSet<string>(fresh.Select(text => text.Text)), DateTime.UtcNow);
                    }

                    break;
            }
        }

        /// <summary>Speaks the hint bar (controller legends) on request.</summary>
        public void SpeakHints(ScreenSnapshot snapshot)
        {
            var hints = snapshot.Layers.SelectMany(layer => layer.Texts).Where(ScreenWatcher.IsLegend).ToList();
            this.Speak("hints", hints.Count == 0 ? Announcements.NoHints : Describe(hints), true);
        }

        /// <summary>
        ///     Everything the screen shows where the player is: the focused control's layer, else the top panel
        ///     (character stats, support gem capacity, skill points) — legends excluded, they have their own command.
        /// </summary>
        public void SpeakScreen(ScreenSnapshot snapshot, IntPtr focusLayer, bool withHints = false)
        {
            var layer = snapshot.Find(focusLayer) ?? snapshot.TopInteractive;
            var texts = layer?.Texts.Where(text => !ScreenWatcher.IsLegend(text) && !IsNumber(text.Text)).ToList() ?? new List<ScreenText>();
            var hints = withHints ? snapshot.Layers.SelectMany(each => each.Texts).Where(ScreenWatcher.IsLegend).ToList() : new List<ScreenText>();
            var spoken = Announcements.Sentences(new[] { Describe(texts), Describe(hints) }.Where(part => part.Length > 0));
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

        private static bool IsNumber(string text) => text.All(c => char.IsDigit(c) || c is '.' or ',' or ':' or '%' or '/' or '+' or '-' or ' '); // "6", "25%", "0:08"

        /// <summary>Texts in reading order without repeats; a disabled control is marked unavailable.</summary>
        public static string Describe(IEnumerable<ScreenText> texts)
        {
            var parts = new List<string>();
            foreach (var text in texts)
            {
                var labelled = Announcements.WithButton(text.Text, text.Button);
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

        private void Speak(string reason, string text, bool interrupt)
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
            if (parts == this.lastNarrated.Parts && (DateTime.UtcNow - this.lastNarrated.At).TotalMilliseconds < RecentTextsMs)
            {
                this.log.Debug($"Narrate ({reason}) skipped: same as just said");
                return;
            }

            this.lastNarrated = (parts, DateTime.UtcNow);
            this.log.Debug($"Narrate ({reason})");
            if (this.deferredFocus is { } deferred && text.Contains(this.lastFocusSpeech, StringComparison.Ordinal))
            {
                this.deferredFocus = null; // the popup's message carried the focus with it
            }

            this.output.Speak(text, interrupt || this.deferredFocus != null);
        }
    }
}
