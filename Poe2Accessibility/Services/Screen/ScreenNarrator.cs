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
    public sealed partial class ScreenNarrator
    {
        /// <summary>Two snapshots (~300 ms) confirm a text; one that lands this soon after a focus move belongs to it.</summary>
        private const int FocusTextMergeMs = 700;

        /// <summary>
        ///     Tab titles by control id: the panel tab bar's (2026-10-23) and the gem engraving window's category, a weapon
        ///     kind for skills and the skill for supports (LB/RB; dump 2026-10-31).
        /// </summary>
        private static readonly string[] TabTitleNames = { "label_tab_title", EngraveCategoryName };

        private const string EngraveCategoryName = "category_name_label";

        /// <summary>A choice of the gem engraving window (control id, dump 2026-10-31).</summary>
        private const string EngraveChoiceName = "new_option";

        /// <summary>A batch this large arriving with a focus move is a new page (tab switch), not a popup message.</summary>
        private const int PageChangeTexts = 6;

        /// <summary>A layer without focus showing at most this many texts is a confirmation box.</summary>
        private const int SmallBoxTexts = 4;

        /// <summary>A layer opening with this many texts is a panel to move through, not a box to read out.</summary>
        private const int LargeLayerTexts = 16;

        /// <summary>A tab switch shows up as texts appearing and then as the top layer changing: one announcement.</summary>
        private const int RepeatedTabMs = 1500;

        /// <summary>A text this short is a name or title, kept even when the focus's line mentions it (the speaker "Renly").</summary>
        private const int ShortLabelChars = 20;

        /// <summary>An item's tier numeral ("I") beside its name, which already says its level.</summary>
        private const string TierLabelName = "tier_label";

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
        private readonly TrialCounterWatch trialCounters = new();
        private readonly QuestTrackerPage questPage = new();
        private readonly CountdownWatch countdown = new();
        // Both wait for the page's own line: the account's points were cut 14 to 160 ms in by the first card (live 2026-11-10).
        private readonly ShopBalanceWatch shopBalance = new(ShopPageSettleMs);
        private readonly ShopBalanceWatch guildBalance = new(ShopPageSettleMs);

        /// <summary>When a shop tab's actions are due (<see cref="SayShopPageActions"/>), zero when none are.</summary>
        private long shopActionsDueAt;
        private readonly HonourWatch honour = new();

        /// <summary>When the focused control's value last changed in place (its actions may change with it).</summary>
        private DateTime valueChangedAt = DateTime.MinValue;
        private ScreenSnapshot? seenSnapshot;
        private IntPtr seenTop;
        private AccessibleUiNode? pendingFocus;

        /// <summary>The focus that showed nothing yet when it came (its caption is drawn a frame later).</summary>
        private IntPtr unnamedFocus;
        private DateTime lastFocusAt = DateTime.MinValue;
        private DateTime lastTabAt = DateTime.MinValue;

        /// <summary>A panel's opening and its becoming the top layer arrive in one frame (death screen): one reading.</summary>
        private (IntPtr Layer, string Text, DateTime At) lastPanel;

        /// <summary>When a panel without focus opened: what its cursor shows a moment later follows its title.</summary>
        private (IntPtr Layer, DateTime At) panelOpened;

        /// <summary>Texts just read out, so the same screen reported again a moment later (top layer) is not repeated.</summary>
        private (HashSet<string> Texts, DateTime At) recentTexts = (new HashSet<string>(), DateTime.MinValue);

        /// <summary>
        ///     A focus that lands in another layer, or right after A was pressed (a confirmation popup is built
        ///     inside the menu's own layer), waits this long for the popup's message, so "Confirm, button" is not
        ///     said and then cut off by "Exit? Confirm, button".
        /// </summary>
        private const int NewLayerFocusDelayMs = 300;

        private (string Text, DateTime At, AccessibleUiNode Node)? deferredFocus;

        /// <summary>When a narration last cut in on its own: a waiting focus said after it follows it instead of cutting it off.</summary>
        private DateTime cutInAt = DateTime.MinValue;

        /// <summary>A panel held the focus since play last showed: closing it back to play stops what it was saying.</summary>
        private bool panelShown;

        /// <summary>The layer whose tab was last said (a merchant's stock, the stash), for LB/RB that leave the focus where it is.</summary>
        private IntPtr tabbedLayer;

        /// <summary>When LB or RB was pressed in a panel; MaxValue when no tab switch is awaited.</summary>
        private DateTime tabKeyAt = DateTime.MaxValue;

        /// <summary>The game redraws the page this soon after LB/RB; a focus that moves by then says the tab itself.</summary>
        private const int TabSwitchMs = 250;

        /// <summary>A cursor in the switched grid is back by then (0.3 s after the redraw in the stash, live 2026-11-10); past it the tab is said alone.</summary>
        private const int TabFocusWaitMs = 1000;

        /// <summary>Since when nothing has held the focus with only the HUD shown; MaxValue while something does.</summary>
        private DateTime unfocusedSince = DateTime.MaxValue;

        /// <summary>The focus gone this long with only the HUD on top is play again, not a panel passing between pages.</summary>
        private const int BackToPlayMs = 250;

        /// <summary>The layer whose tab was last announced; cleared when another screen comes on top.</summary>
        private IntPtr lastTabLayer;

        /// <summary>A tab's page waiting to see whether a focus lands on it: tab, page, its texts, when.</summary>
        private (string Tab, string Page, HashSet<string> Texts, DateTime At)? pendingPage;

        /// <summary>A tutorial hint that arrived while a new layer's focus was still waiting: said after that focus.</summary>
        private string hintAfterFocus = string.Empty;
        private IntPtr lastFocusLayer;

        /// <summary>
        ///     The gem engraving window's last focused choice ("Furia I") and category (for supports the skill they go
        ///     into: the game sockets the engraved support there; the window's tab, LB/RB).
        /// </summary>
        public (string Choice, string Category) Engraving { get; private set; } = (string.Empty, string.Empty);

        /// <summary>Lines of the game menu said this session (<see cref="ReadScreenMessages"/> off): its tip never changes.</summary>
        private readonly HashSet<string> screenLinesSaid = new(StringComparer.Ordinal);

        /// <summary>A focus change this soon after A is the screen the press opened.</summary>
        private const int ConfirmOpensMs = 1000;

        /// <summary>A cursor move this soon after a direction is the player's own (our window: a few frames of reading).</summary>
        private const int DirectionMovesMs = 500;

        private bool inGame;
        private IntPtr valueOwner;
        private string lastValue = string.Empty;

        /// <summary>The text of the focused control as last said: what a change in place is told apart from.</summary>
        private string lastText = string.Empty;

        /// <summary>The focused control's place in its list as last said ("3 de 10").</summary>
        private string lastRow = string.Empty;
        private bool lastExpanded;

        /// <summary>The panel whose notes were said with the focus last (<see cref="AccessibleUiNode.PanelNotes"/>).</summary>
        private string notesContext = string.Empty;

        /// <summary>When the focused row was last closed: the rows that slide up into view after it are not news.</summary>
        private DateTime collapsedAt = DateTime.MinValue;
        private string lastLinkRow = string.Empty;
        private IReadOnlyList<string> tooltipSaid = Array.Empty<string>();

        /// <summary>
        ///     New game state: its first top layer is not a popup closing. In game only the panel holding the focus
        ///     speaks (HUD, chat and map are always shown and toggle with panels, so layer events are noise).
        /// </summary>
        public void Reset(bool inGameState)
        {
            this.lastTop = IntPtr.Zero;
            this.lastContext = string.Empty;
            this.lastGroup = string.Empty;
            this.lastSelectorPosition = string.Empty;
            this.lastSelectorChoice = null;
            this.legendsLayer = IntPtr.Zero;
            this.shortcutsSaid.Clear();
            this.inGame = inGameState;
            // The bar as last seen, said or not: a login that lasted less than the hints take to settle left its buttons
            // unknown here, and they were read at character selection as its own (live 2026-11-10).
            this.staleHints = (this.pendingHints.Text.Length > 0 ? this.pendingHints.Text : this.lastHints, DateTime.UtcNow);
            this.lastHints = string.Empty;
            this.spokenHints.Clear();
            this.pendingHints = (string.Empty, DateTime.MinValue);
            // The last state's bar is not said again as cut off: the login's came back after the character (live 2026-11-10).
            this.cuttableHints = null;
            this.lastFocusLayer = IntPtr.Zero;
            this.deferredFocus = null;
            this.pendingPage = null;
            this.hintAfterFocus = string.Empty;
            this.tabbedLayer = IntPtr.Zero;
        }

        private string lastSelectorPosition = string.Empty;

        private int? lastSelectorChoice;

        /// <summary>A layer over the focused panel with at most this many texts is a message, not a page.</summary>
        private const int MessageTexts = 2;

        /// <summary>A value that arrives this soon after its widget was named is part of naming it.</summary>
        private const int LateValueMs = 400;

        public void OnScreenChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            this.NoteTop(snapshot);
            if (this.inGame && focusLayer == IntPtr.Zero && change.Layer.Address == this.HudLayer)
            {
                // Every panel closed: its title and legends are news when it opens again.
                this.legendsLayer = IntPtr.Zero;
                this.panelLegends = (string.Empty, DateTime.MinValue);
                this.lastContext = string.Empty;
                this.notesContext = string.Empty;
            }

            this.log.Debug($"Screen change {change.Kind} layer 0x{change.Layer.Address.ToInt64():X} focus layer 0x{focusLayer.ToInt64():X} hint={change.Layer.IsHintBar} top=0x{snapshot.TopInteractive?.Address.ToInt64() ?? 0:X} justFocused={this.JustFocused()}");
            if (change.Layer.IsHintBar)
            {
                this.OnHintBarChange(change, snapshot, focusLayer);
                return;
            }

            if (this.LoadingArea)
            {
                change = change with { Texts = change.Texts.Where(text => text.IsLoadingScreen).ToList() };
                if (change.Texts.Count == 0)
                {
                    return;
                }
            }

            // Legends inside a content layer (the game menu's own hint bar) are read on request, not with the content.
            var legends = change.Texts.Where(ScreenWatcher.IsLegend).ToList();
            change = change with { Texts = change.Texts.Where(text => !ScreenWatcher.IsLegend(text)).ToList() };
            if (change.Texts.Count == 0 && change.Kind != ScreenChangeKind.TopLayerChanged)
            {
                return;
            }

            if (this.inGame && change.Kind != ScreenChangeKind.TopLayerChanged &&
                change.Texts.FirstOrDefault(text => text.Name == GameLayout.InGameUi.MessageLabelName) is { } refusal)
            {
                // The game refusing what was just pressed (socketing a second gem of a kind): whatever has the focus,
                // since the press is what the player is waiting on (it was silent and tried three times, 2026-10-24).
                if (this.AlertIsNews(refusal.Text))
                {
                    // Held: the item re-read as the focus settles cut "El objeto no tiene espacio para más mods." (2026-10-26).
                    this.Speak("game message", refusal.Text, true, Hold.Message);
                }

                return;
            }

            if (this.inGame && change.Layer.Address == this.TutorialLayer)
            {
                this.OnTutorialChange(change, snapshot, focus, legends);
                return;
            }

            if (this.inGame && change.Layer.Address == this.HudLayer)
            {
                this.OnHudChange(change, snapshot);
                return;
            }

            if (this.inGame)
            {
                this.OnInGameChange(change, snapshot, focus, focusLayer, legends);
                return;
            }

            this.OnPreGameChange(change, snapshot, focus, focusLayer);
        }

        /// <summary>Control-id suffix of a description a control shows beside itself while focused.</summary>
        private const string PopupSuffix = "_popup";

        /// <summary>The pre-game layer of notices and the connection log (login dump, 2026-10-24).</summary>
        private const string NoticeLayerName = "visible_area";
    }
}
