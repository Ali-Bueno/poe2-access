namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>In-game rules of <see cref="ScreenNarrator"/>: the focus layer, panels and popups, the objective box and new HUD texts.</summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>A panel that opened while its cursor's line still waited (<see cref="FocusPending"/>), kept this long for that line.</summary>
        private const int HeldPageMs = 1000;

        private (IntPtr Layer, IReadOnlyList<ScreenText> Texts, DateTime At)? heldPage;

        /// <summary>The focus tracker sees a grid's cursor whose line is not said yet.</summary>
        public Func<bool>? FocusPending { get; set; }

        /// <summary>
        ///     A tabbed page opening with the focus (a merchant's stock, a stash): the gamble when the game marks the stock
        ///     so, the tab, the focus, then the gold the inventory shows — after the item, never as the first line.
        /// </summary>
        private void SayPageWithFocus(IReadOnlyList<ScreenText> texts, AccessibleUiNode? focus, string pageTab)
        {
            var context = focus?.Context ?? string.Empty;
            var gamble = texts.FirstOrDefault(text => text.Name == GameLayout.InGameUi.GamblingStockLabelName) is { } stock ? Announcements.GambleStock(stock.Text) : string.Empty;
            var pageBeside = FocusDetails.PageBeside(context, pageTab, this.SidePanel?.Invoke() ?? string.Empty);
            this.Speak("page with focus", Announcements.Sentences(new[] { gamble, pageBeside, FocusDetails.PagePlace(context, pageTab), this.lastFocusSpeech }), true);
            var counter = this.GoldCounter?.Invoke() ?? IntPtr.Zero;
            if (counter != IntPtr.Zero && texts.FirstOrDefault(text => text.Address == counter) is { } gold)
            {
                this.Speak("gold after page", gold.Text, false);
            }
        }

        /// <summary>The focus of a panel held for it is due: said as the page's opening when the page has a tab; false when it is no such page.</summary>
        private bool SayHeldPage(AccessibleUiNode focus)
        {
            var held = this.heldPage;
            this.heldPage = null;
            if (held is not { } page || page.Layer != (this.FocusLayer?.Invoke() ?? IntPtr.Zero) || (DateTime.UtcNow - page.At).TotalMilliseconds >= HeldPageMs ||
                this.selectedTab(page.Layer) is not { Length: > 0 } tab)
            {
                return false;
            }

            this.tabbedLayer = page.Layer;
            this.SayPageWithFocus(page.Texts, focus, tab);
            return true;
        }

        /// <summary>In game: the focus layer speaks; panels, popups, the death screen and new texts follow the in-game rules.</summary>
        private void OnInGameChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer, System.Collections.Generic.List<ScreenText> legends)
        {
            var inShop = this.ShopShown?.Invoke() == true;
            if (inShop && this.InAnotherShopCard is { } inCard)
            {
                // The shop's cards are read as the cursor reaches them: a category opening read every card's name, without
                // prices, before its cursor showed (live 2026-11-10).
                var chosen = focus?.Address ?? IntPtr.Zero;
                change = change with { Texts = change.Texts.Where(text => !inCard(text.Address, chosen)).ToList() };
                if (change.Texts.Count == 0 && change.Kind != ScreenChangeKind.TopLayerChanged)
                {
                    return;
                }
            }

            var tabTitle = change.Texts.FirstOrDefault(text => TabTitleNames.Contains(text.Name));
            if (tabTitle == null && change.Kind == ScreenChangeKind.TopLayerChanged)
            {
                this.lastTabLayer = IntPtr.Zero; // another screen came on top: the panel's tab is news again
            }

            if (tabTitle != null && change.Kind == ScreenChangeKind.TopLayerChanged && change.Layer.Address == this.lastTabLayer &&
                this.lastContext == tabTitle.Text && !this.JustFocused())
            {
                return; // the same panel and tab reported again (an item's count on the cursor came and went)
            }

            if (tabTitle?.Name == EngraveCategoryName)
            {
                this.Engraving = (this.Engraving.Choice, tabTitle.Text);
            }

            if (tabTitle != null)
            {
                this.lastTabLayer = change.Layer.Address;
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

                // A page of a few lines besides its controls (Guild: "You are not in a guild", what guilds are for) is
                // read after the focus; a page full of labels is left to the focus that browses it.
                var focusTexts = focus == null ? new HashSet<string>() : new HashSet<string>(focus.ListItems) { focus.Text, focus.Value, focus.Group };
                var focusSpeech = withFocus || named ? this.lastFocusSpeech : focus != null ? this.Focused(focus) : string.Empty;
                var lines = change.Texts.Where(text => text != tabTitle && !text.IsControl && !IsNumber(text.Text) &&
                                                       !text.Text.Split(", ").Any(focusTexts.Contains) &&
                                                       !focusSpeech.Contains(text.Text, StringComparison.Ordinal) && !this.JustNarrated(text.Text)).ToList();
                var pageLines = lines.Count <= SmallBoxTexts ? Describe(lines) : string.Empty;
                if (named)
                {
                    if (pageLines.Length > 0)
                    {
                        this.AfterFocus("page lines after focus", pageLines);
                    }

                    this.SayShopPageActions(inShop);
                    return;
                }

                this.Speak("tab", Announcements.Sentences(new[]
                {
                    focusSpeech.Length > 0 ? Announcements.InContext(Announcements.TabTitle(tabTitle.Text), focusSpeech) : Announcements.TabTitle(tabTitle.Text),
                    pageLines,
                }.Where(part => part.Length > 0)), true);
                this.SayShopPageActions(inShop);
                return;
            }

            if (focusLayer == IntPtr.Zero && change.Layer.Address == snapshot.TopInteractive?.Address)
            {
                // A panel nothing focuses or hovers (world map): what its cursor shows is the content. Right
                // after the tab name it follows it.
                // A small box without focus (Engrave? Confirm A, Back B) is answered with its buttons: say them.
                // A bare number means nothing alone (a stash tab's title while the focus moves to the new tab).
                // Nor does a tier numeral (a gem's "I" in the tab being drawn, said alone on a stash tab switch).
                if (this.FocusPending?.Invoke() == true)
                {
                    // The cursor is on the panel already and its line waits for the item's tooltip: the panel opens with
                    // that line, as when the focus comes first (a merchant opened as "4.776 oro. Oro", live 2026-11-10).
                    this.heldPage = change.Kind == ScreenChangeKind.LayerOpened || this.heldPage == null ? (change.Layer.Address, change.Texts, DateTime.UtcNow) : this.heldPage;
                    return;
                }

                var goldChrome = this.Chrome(change.Texts);
                var content = change.Texts.Where(text => !IsNumber(text.Text) && !IsTierNumeral(text.Text) && !goldChrome.Contains(text.Address)).ToList();
                var shown = content.Count <= SmallBoxTexts && legends.Count > 0
                    ? Announcements.Sentences(new[] { Describe(content), Describe(legends) })
                    : Describe(content);
                var repeated = this.lastPanel.Layer == change.Layer.Address && this.lastPanel.Text == shown &&
                               (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < RepeatedTabMs;
                // The waypoint map's hovered place arrives 300 ms after its title: it must not cut "Teletransporte,
                // acto 1" and the "Travel to town, Y" action off (2026-10-24).
                var opening = change.Kind == ScreenChangeKind.TextsAppeared && this.panelOpened.Layer == change.Layer.Address &&
                              (DateTime.UtcNow - this.panelOpened.At).TotalMilliseconds < FocusTextMergeMs;
                if (change.Kind == ScreenChangeKind.LayerOpened)
                {
                    this.panelOpened = (change.Layer.Address, DateTime.UtcNow);
                }

                if (shown.Length > 0 && !repeated)
                {
                    this.lastPanel = (change.Layer.Address, shown, DateTime.UtcNow);
                    this.Speak("panel without focus", shown, !opening && (DateTime.UtcNow - this.lastTabAt).TotalMilliseconds >= FocusTextMergeMs);
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
            var focusIndex = snapshot.Layers.FirstOrDefault(layer => layer.Address == focusLayer)?.Index;
            if (change.Layer.Address != focusLayer && !justFocused && focusIndex < change.Layer.Index && !change.Layer.HasControls &&
                change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared && change.Texts.Count <= MessageTexts)
            {
                // A line over the focused panel that no focus move brought: the game refusing the press ("Ya tienes una
                // gema de habilidad engarzada de este tipo." over Skills was silent, tried three times, 2026-10-24).
                var line = Describe(change.Texts.Where(text => text.Text != focus?.Text && !IsNumber(text.Text)));
                if (line.Length > 0 && this.AlertIsNews(line))
                {
                    this.Speak("message over panel", line, true, Hold.Message);
                }

                return;
            }

            if (change.Layer.Address != focusLayer && !justFocused)
            {
                return;
            }

            var own = focus == null ? new HashSet<string>() : new HashSet<string>(focus.ListItems.Concat(focus.Tooltip)) { focus.Text, focus.Value, focus.Group };
            var said = (DateTime.UtcNow - this.recentTexts.At).TotalMilliseconds < RecentTextsMs ? this.recentTexts.Texts : new HashSet<string>();
            var chrome = this.Chrome(change.Texts);
            var kept = PairNumbers(change.Texts).Where(text => !own.Contains(text.Text) && !IsNumber(text.Text) && !IsTierNumeral(text.Text) && !said.Contains(text.Text) &&
                                                              !chrome.Contains(text.Address) && text.Name != TierLabelName &&
                                                              !(text.Text.Length > ShortLabelChars && focus != null &&
                                                                (focus.Text.Contains(text.Text, StringComparison.Ordinal) || focus.Value.Contains(text.Text, StringComparison.Ordinal))) &&
                                                              !FocusDetails.Names(text.Text, focus?.Text ?? string.Empty) &&
                                                              !InFocusContext(focus, text.Text)).ToList();
            var texts = Describe(kept);
            var pageTab = justFocused && this.pendingFocus == null && change.Texts.Count >= PageChangeTexts
                ? this.selectedTab(change.Layer.Address)
                : string.Empty;
            // A merchant's stock shows nothing to say but its tabs and the gold: its tab is still named before the focus
            // (Shambrin opened as "Inventario. Orbe de transmutación…" alone, live 2026-11-10).
            if (texts.Length == 0 && pageTab.Length == 0)
            {
                this.log.Debug($"Screen change with nothing to say: {change.Texts.Count} texts, no tab");
                return;
            }

            if (this.pendingFocus != null && justFocused && texts.Length > 0)
            {
                // The tooltip names what the id could not.
                this.pendingFocus = null;
                this.Speak("tooltip for unnamed focus", texts, true);
            }
            else if (pageTab.Length > 0)
            {
                // A tabbed page arrived with the focus (options opened from the game menu, an options tab switch):
                // the selected tab and the focus, as pre-game — never the page's labels. A focus outside the page (the
                // inventory beside the stash) is said in its own part.
                this.tabbedLayer = change.Layer.Address;
                this.SayPageWithFocus(change.Texts, focus, pageTab);
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
                // A tabbed panel (vendor stock): its tab, what it shows besides controls and stack counts (gold), the focus;
                // a focus outside the stock (the inventory) is said in its own part.
                var shown = Describe(change.Texts.Where(text => !text.IsControl && !IsNumber(text.Text) && !own.Contains(text.Text)));
                var place = FocusDetails.PagePlace(focus?.Context ?? string.Empty, openedTab);
                // A merchant opened with the cursor on the inventory: its first tab, then the inventory's item in its own part.
                var beside = FocusDetails.PageBeside(focus?.Context ?? string.Empty, openedTab, this.SidePanel?.Invoke() ?? string.Empty);
                this.tabbedLayer = change.Layer.Address;
                this.lastContext = place == Announcements.TabTitle(openedTab) ? openedTab : place;
                this.Speak("tabbed panel with focus", Announcements.Sentences(new[] { beside, place, shown, this.lastFocusSpeech }), true);
            }
            else if (change.Kind == ScreenChangeKind.LayerOpened && justFocused)
            {
                // A box opened on the focus (NPC dialogue: speaker, then the line and Continue the focus holds). The
                // captions of other control groups beside it are read when the cursor goes there (skill bind screen:
                // "Choose the button to bind Chaos Bolt", the primary set, the slot — not the spirit panel).
                var near = focus == null || this.InOtherControlGroup is not { } other
                    ? texts
                    : Describe(kept.Where(text => !other(text.Address, focus.Address)));
                // A panel opened beside the inventory with the focus on an inventory item (salvage bench): the panel and
                // what it says first, or the item alone sounds like the inventory (user, 2026-11-01: "se lía con el
                // inventario").
                var side = focus == null || this.InOtherControlGroup is not { } group || this.SidePanel?.Invoke() is not { Length: > 0 } title
                    ? string.Empty
                    : kept.Where(text => group(text.Address, focus.Address)).ToList() is var beside && beside.Any(text => text.Text == title)
                        ? Describe(beside)
                        : string.Empty;
                this.log.Debug($"Opened with focus: side panel \"{this.SidePanel?.Invoke()}\", said first \"{side}\"");
                this.Speak("opened with focus", Announcements.Sentences(new[] { side, near, this.lastFocusSpeech }), true);
            }
            else if (change.Kind == ScreenChangeKind.LayerOpened)
            {
                this.Speak("opened with focus", texts, true);
            }
            else if (change.Kind == ScreenChangeKind.TopLayerChanged && justFocused && change.Layer.Address == focusLayer &&
                     kept.Where(text => !text.IsControl && !this.JustNarrated(text.Text)).ToList() is { Count: > 0 and <= SmallBoxTexts } lines)
            {
                // A panel back on top with its focus (the social panel from the game menu): the few lines its frame shows
                // besides controls (the account name and area over Friends), after the focus. A panel full of labels is
                // left to the focus that browses it.
                this.AfterFocus("panel lines after focus", Describe(lines));
            }
            else if (change.Kind == ScreenChangeKind.TextsAppeared && justFocused && !FocusDetails.HeadedBy(change.Texts, focus?.Text ?? string.Empty, TierLabelName) &&
                     (change.Scrolled || this.selectedTab(change.Layer.Address).Length > 0))
            {
                // The list scrolled under the focus (the Game options page): rows coming into view, not news. On a
                // tabbed page the rows below stay drawn, clipped, so only the arrivals show; a control focused
                // there brings no text of its own (its description is its tooltip).
            }
            else if (change.Kind == ScreenChangeKind.TextsAppeared && justFocused)
            {
                // The text came with the focus move (a skill's details, an expanded definition): the focus again,
                // since this cuts it off, then the text — the name before a long description, never after it. Rows of
                // the same list coming into view (the next trade listings) are read when the cursor gets there.
                var near = focus == null || this.InSiblingRow is not { } sibling
                    ? texts
                    : Describe(kept.Where(text => !sibling(text.Address, focus.Address)));
                if (near.Length > 0)
                {
                    this.Speak("appeared with focus", Announcements.Sentences(new[] { this.lastFocusSpeech, near }), true);
                }
            }
            else if (change.Kind == ScreenChangeKind.TextsAppeared && (DateTime.UtcNow - this.collapsedAt).TotalMilliseconds >= FocusTextMergeMs)
            {
                // Cards of the same list drawn a moment after a scroll (the shop's "¡NUEVO!" flags) are read when reached.
                var news = focus == null || this.InAnotherShopCard is not { } other ? texts : Describe(kept.Where(text => !other(text.Address, focus.Address)));
                if (news.Length > 0)
                {
                    this.Speak("appeared in focus layer", news, false);
                }
            }
        }
    }
}
