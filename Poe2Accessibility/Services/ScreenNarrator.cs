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

        /// <summary>A text this short is a name or title, kept even when the focus's line mentions it (the speaker "Renly").</summary>
        private const int ShortLabelChars = 20;

        /// <summary>An item's tier numeral ("I") beside its name, which already says its level.</summary>
        private const string TierLabelName = "tier_label";

        /// <summary>A kept line is held about as long as a screen reader takes to say it, within these bounds.</summary>
        private const int SpokenMsPerChar = 45;
        private const int MinKeepMs = 1500;
        private const int MaxKeepMs = 6000;

        /// <summary>A tutorial hint shown this soon after the HUD is back on top is the box reappearing, not news.</summary>
        private const int HudBackMs = 3000;

        /// <summary>
        ///     A tutorial hint shown again during play is said again after this long: the low-mana lesson came back 28 s
        ///     after it was said and held the character until the D-pad right flask, in silence (2026-10-24).
        /// </summary>
        private const int TutorialRepeatMs = 5000;

        /// <summary>The HUD read in this long after the host started inside an area was already there, not news.</summary>
        private const int ResumedHudMs = 2000;

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
        /// <summary>Tutorial hints said in this area and when.</summary>
        private readonly Dictionary<string, DateTime> tutorialSpoken = new();

        /// <summary>When the HUD last became the top layer again (a panel, the radial menu or a respawn screen closed).</summary>
        private DateTime hudBackAt = DateTime.MinValue;

        /// <summary>Until then the HUD's texts are only noted (the host restarted with the game in an area).</summary>
        private DateTime hudQuietUntil = DateTime.MinValue;

        /// <summary>When a ground item's name was said: the rest of its tooltip follows in a second step.</summary>
        private DateTime groundItemAt = DateTime.MinValue;

        /// <summary>When the focused control's value last changed in place (its actions may change with it).</summary>
        private DateTime valueChangedAt = DateTime.MinValue;
        private ScreenSnapshot? seenSnapshot;
        private IntPtr seenTop;
        private AccessibleUiNode? pendingFocus;
        private DateTime lastFocusAt = DateTime.MinValue;
        private DateTime lastTabAt = DateTime.MinValue;

        /// <summary>A panel's opening and its becoming the top layer arrive in one frame (death screen): one reading.</summary>
        private (IntPtr Layer, string Text, DateTime At) lastPanel;

        /// <summary>When a panel without focus opened: what its cursor shows a moment later follows its title.</summary>
        private (IntPtr Layer, DateTime At) panelOpened;

        /// <summary>Texts just read out, so the same screen reported again a moment later (top layer) is not repeated.</summary>
        private (HashSet<string> Texts, DateTime At) recentTexts = (new HashSet<string>(), DateTime.MinValue);

        /// <summary>Pre-game button hints: the last spoken, and a new set waiting to settle.</summary>
        private string lastHints = string.Empty;

        /// <summary>The previous screen's hints and when that screen closed: the bar can show them for a moment longer.</summary>
        private (string Text, DateTime Since) staleHints = (string.Empty, DateTime.MinValue);
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

        /// <summary>The layer whose tab was last announced; cleared when another screen comes on top.</summary>
        private IntPtr lastTabLayer;

        /// <summary>A tab's page waiting to see whether a focus lands on it: tab, page, its texts, when.</summary>
        private (string Tab, string Page, HashSet<string> Texts, DateTime At)? pendingPage;

        /// <summary>A tutorial hint that arrived while a new layer's focus was still waiting: said after that focus.</summary>
        private string hintAfterFocus = string.Empty;
        private IntPtr lastFocusLayer;

        /// <summary>The layer holding the focus (FocusTracker), to notice a focus that moved into a popup.</summary>
        public Func<IntPtr>? FocusLayer { get; set; }

        /// <summary>The focused node described again (its panel may have been built since); null when gone.</summary>
        public Func<AccessibleUiNode, AccessibleUiNode?>? Refresh { get; set; }

        /// <summary>Whether a text lies inside the panel with the given title; null reads every text.</summary>
        public Func<IntPtr, string, bool>? InPanel { get; set; }

        /// <summary>Whether a text (first) belongs to another group of controls than the focus (second).</summary>
        public Func<IntPtr, IntPtr, bool>? InOtherControlGroup { get; set; }

        /// <summary>Read a screen's message with its first focus (delete character); off for the game menu.</summary>
        public bool ReadScreenMessages { get; set; } = true;

        /// <summary>Milliseconds since the confirm button (A) was last pressed.</summary>
        public Func<long>? SinceConfirm { get; set; }

        /// <summary>A focus change this soon after A is the screen the press opened.</summary>
        private const int ConfirmOpensMs = 1000;

        /// <summary>The last narration as a set of its parts, to drop the same news reported twice.</summary>
        private (string Parts, DateTime At) lastNarrated = (string.Empty, DateTime.MinValue);

        /// <summary>
        ///     Narrations of the last moment, by their sorted parts: the stash said its tab and focus twice, as its layer
        ///     opened and as it became the top layer in the same frame, with its legends between (2026-10-24).
        /// </summary>
        private readonly Dictionary<string, DateTime> recentNarrations = new(StringComparer.Ordinal);

        /// <summary>Events of one frame land within this; a tab switched there and back is slower.</summary>
        private const int SameMomentMs = 300;

        /// <summary>Recently read texts are left out of a repeat within this time.</summary>
        private const int RecentTextsMs = 1500;

        /// <summary>The previous screen's hint bar seen this soon after a state change is the game redrawing late.</summary>
        private const int StaleHintsMs = 2000;

        /// <summary>A new set of pre-game hints must stay this long before it is read (screens rebuild in steps).</summary>
        private const int HintsSettleMs = 400;

        /// <summary>
        ///     A screen's own text that interrupts its button bar this soon after the bar began cut it off: the login
        ///     screen's "Conectarse…" silenced "Presiona para jugar, A" 50 ms in (2026-10-24). The bar is said again after.
        /// </summary>
        private const int HintsCutMs = 1500;

        /// <summary>The button bar last said, while an interrupting screen text could still cut it.</summary>
        private (string Text, DateTime At)? cuttableHints;
        private bool inGame;
        private IntPtr valueOwner;
        private string lastValue = string.Empty;
        private bool lastExpanded;
        private string lastLinkRow = string.Empty;
        private IReadOnlyList<string> tooltipSaid = Array.Empty<string>();

        /// <summary>
        ///     A new area (its name changed): the HUD's and the tutorial box's texts are news again. Not on every game
        ///     state change — the game menu closing and a respawn in the same area would repeat the quest tracker,
        ///     "Nuevo punto de control" and the last tutorial hint.
        /// </summary>
        /// <summary>A hint never said in this area; or said long ago and shown again while playing (the HUD on top, no panel just closed).</summary>
        /// <summary>
        ///     The inventory's gold amount and its header (the unit word alone, "Oro"): a reward or vendor panel opens
        ///     beside the inventory and read "20 oro. Oro." first.
        /// </summary>
        private HashSet<IntPtr> Chrome(IReadOnlyList<ScreenText> texts)
        {
            var chrome = new HashSet<IntPtr>();
            var counter = this.GoldCounter?.Invoke() ?? IntPtr.Zero;
            if (counter == IntPtr.Zero || texts.FirstOrDefault(text => text.Address == counter) is not { } amount)
            {
                return chrome;
            }

            chrome.Add(counter);
            var unit = amount.Text.TrimStart('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ',', ' ');
            foreach (var text in texts.Where(text => unit.Length > 0 && string.Equals(text.Text, unit, StringComparison.CurrentCultureIgnoreCase)))
            {
                chrome.Add(text.Address);
            }

            return chrome;
        }

        /// <summary>Notes the HUD coming back on top, once per snapshot.</summary>
        private void NoteTop(ScreenSnapshot snapshot)
        {
            if (ReferenceEquals(snapshot, this.seenSnapshot))
            {
                return;
            }

            var top = snapshot.TopInteractive?.Address ?? IntPtr.Zero;
            if (this.seenSnapshot != null && top != this.seenTop && top == this.HudLayer && top != IntPtr.Zero)
            {
                this.hudBackAt = DateTime.UtcNow;
            }

            this.seenSnapshot = snapshot;
            this.seenTop = top;
        }

        /// <summary>A new area; resumed when the host started inside it, so what the HUD shows now was already said.</summary>
        public void NewArea(bool resumed)
        {
            this.hudSpoken.Clear();
            this.tutorialSpoken.Clear();
            this.hudQuietUntil = resumed ? DateTime.UtcNow.AddMilliseconds(ResumedHudMs) : DateTime.MinValue;
            this.hudBaseline = resumed;
        }

        /// <summary>
        ///     A host restart: the HUD's first texts were already said, even when a panel hid it past the quiet time
        ///     (the whole quest tracker was read on closing the inventory, 2026-10-24).
        /// </summary>
        private bool hudBaseline;

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
            this.inGame = inGameState;
            this.staleHints = (this.lastHints, DateTime.UtcNow);
            this.lastHints = string.Empty;
            this.spokenHints.Clear();
            this.pendingHints = (string.Empty, DateTime.MinValue);
            this.lastFocusLayer = IntPtr.Zero;
            this.deferredFocus = null;
            this.pendingPage = null;
            this.hintAfterFocus = string.Empty;
        }

        /// <summary>Read long tooltips with the focus (setting); short ones always are.</summary>
        public bool ReadTooltips { get; set; }

        /// <summary>The in-game objective / tutorial box: whatever it shows next is read, whoever has the focus.</summary>
        public IntPtr TutorialLayer { get; set; }

        /// <summary>A HUD text read elsewhere (the target display's name, TargetNarrator), left out of the HUD news.</summary>
        public Func<IntPtr>? IgnoredHudText { get; set; }

        /// <summary>The inventory's gold amount text (GoldWatch): with its unit word as a header, chrome of any panel beside the inventory.</summary>
        public Func<IntPtr>? GoldCounter { get; set; }

        /// <summary>
        ///     The spoken name of the ground item whose tooltip the HUD shows, from its texts (empty for gold); null when the
        ///     texts are not an item's. Such a tooltip says only the name and the quality: the whole text is read in the
        ///     inventory (user, 2026-10-24); a shrine's or a portal's is read whole.
        /// </summary>
        public Func<IEnumerable<string>, string?>? GroundItemName { get; set; }

        /// <summary>The quality line of an item tooltip ("Calidad: +10%"): the only property with a signed percentage.</summary>
        private static readonly System.Text.RegularExpressions.Regex QualityLine = new(@":\s*\+\d+\s*%\s*$", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>"4 de 6" for a selector without focus in a layer (the class choice), else null.</summary>
        public Func<IntPtr, string?>? SelectorPosition { get; set; }

        private string lastSelectorPosition = string.Empty;

        /// <summary>The choice a selector without focus holds in a layer (the class id), else null.</summary>
        public Func<IntPtr, int?>? SelectorChoice { get; set; }

        private int? lastSelectorChoice;

        /// <summary>Said once the focus waiting for its new layer has been (the creation screen's name help).</summary>
        public void SayAfterFocus(string text) => this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, text });

        /// <summary>The in-game HUD: new texts (quest tracker, ground item) are read after the rest, never as a page.</summary>
        public IntPtr HudLayer { get; set; }

        /// <summary>
        ///     A HUD text in the skill bar: its alert line ("Sin maná", "Inventario lleno", "En tiempo de recarga"), said
        ///     each time it shows rather than once per area (a second "Inventario lleno" was silent, 2026-10-24).
        /// </summary>
        public Func<IntPtr, bool>? IsSkillBarText { get; set; }

        /// <summary>The panel whose button legends were said (<see cref="legendsSaid"/>), each once per opening.</summary>
        private IntPtr legendsLayer;

        private readonly HashSet<string> legendsSaid = new(StringComparer.Ordinal);

        /// <summary>Legends of a panel without focus, said once its title and content have been.</summary>
        private (string Text, DateTime At) panelLegends = (string.Empty, DateTime.MinValue);

        /// <summary>A layer over the focused panel with at most this many texts is a message, not a page.</summary>
        private const int MessageTexts = 2;

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
            this.lastExpanded = node.IsExpanded;
            this.tooltipSaid = this.ReadTooltips ? node.Tooltip : Array.Empty<string>();
            var focused = this.Focused(node);
            var layer = this.FocusLayer?.Invoke() ?? IntPtr.Zero;
            if (layer != this.lastFocusLayer)
            {
                this.lastGroup = string.Empty; // another screen: its section is news
            }

            if (node.Group.Length > 0 && node.Group != this.lastGroup)
            {
                focused = Announcements.InContext(node.Group, focused);
            }

            if (node.Group.Length > 0)
            {
                // A control without a group of its own (the FPS limit's number beside its check box) does not leave
                // the section: coming back is not news.
                this.lastGroup = node.Group;
            }

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
                // Only what was not said yet: a passive node's links come first, its description a moment later; a
                // title the focus announcement already holds is not news either.
                var said = this.tooltipSaid;
                this.tooltipSaid = node.Tooltip;
                var fresh = node.Tooltip.Where(line => !said.Contains(line) && !this.lastFocusSpeech.Contains(line, StringComparison.Ordinal)).ToList();
                if (fresh.Count > 0)
                {
                    this.Speak("tooltip of focus", Announcements.Sentences(fresh), false);
                }
            }
        }

        private string Focused(AccessibleUiNode node)
        {
            if (node.Role != UiRole.Link)
            {
                this.lastLinkRow = string.Empty;
                return Announcements.Focused(node, this.ReadTooltips);
            }

            // Moving between the links of one item in inspect mode ("Cetro", "Espíritu", "Set de armas") re-read the
            // whole item each time: its text is said when the focus comes to it, then only the links.
            var newRow = node.Value != this.lastLinkRow;
            this.lastLinkRow = node.Value;
            return Announcements.Link(node, newRow);
        }

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

            if (this.deferredFocus == null && this.hintAfterFocus.Length > 0)
            {
                // The waiting focus was said (here or with a popup's message): the hint now.
                this.Speak("objective after focus", this.hintAfterFocus, false);
                this.hintAfterFocus = string.Empty;
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

            if (this.panelLegends.Text.Length > 0 && (DateTime.UtcNow - this.panelLegends.At).TotalMilliseconds >= FocusTextMergeMs)
            {
                this.Speak("panel actions", this.panelLegends.Text, false);
                this.panelLegends = (string.Empty, DateTime.MinValue);
            }
        }

        /// <summary>Only the new value; a text field echoes just the typed or deleted characters.</summary>
        public void OnValueChanged(AccessibleUiNode node)
        {
            var before = node.Address == this.valueOwner ? this.lastValue : string.Empty;
            var wasExpanded = node.Address == this.valueOwner && this.lastExpanded;
            this.lastExpanded = node.IsExpanded;
            this.valueOwner = node.Address;
            this.lastValue = node.Value;
            if (this.deferredFocus is { } waiting && waiting.Node.Address == node.Address)
            {
                // The focus moved before its new layer settled (a vendor opening: the cursor jumps from an empty cell
                // to the first item): the waiting readout says where it is now, not where it was.
                var now = this.Focused(node);
                this.lastFocusSpeech = now;
                this.deferredFocus = (node.Context.Length > 0 && waiting.Text.StartsWith(node.Context, StringComparison.Ordinal)
                    ? Announcements.InContext(node.Context, now)
                    : now, waiting.At, node);
                return;
            }

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

            if (node.Role == UiRole.ComboBox && node.IsExpanded != wasExpanded && node.Value == before)
            {
                // Opened or closed in place, the value unchanged: A on an in-game option, A again to keep it.
                this.output.Speak(node.IsExpanded
                    ? Announcements.ComboOpened(Announcements.ValueAt(node.Value, node.ValuePosition))
                    : Announcements.ComboClosed(node.Value), true);
                return;
            }

            var edit = node.Role == UiRole.TextField ? TextEdit.Between(before, node.Value) : null;
            this.valueChangedAt = DateTime.UtcNow;
            this.output.Speak(edit is { } typed ? Announcements.Typed(typed.Added, typed.Removed) : Announcements.ValueAt(Announcements.ValueOrEmpty(node.Value), node.ValuePosition), true);
        }

        public void OnScreenChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            this.NoteTop(snapshot);
            if (this.inGame && focusLayer == IntPtr.Zero && change.Layer.Address == this.HudLayer)
            {
                // Every panel closed: its title and legends are news when it opens again.
                this.legendsLayer = IntPtr.Zero;
                this.panelLegends = (string.Empty, DateTime.MinValue);
                this.lastContext = string.Empty;
            }

            this.log.Debug($"Screen change {change.Kind} layer 0x{change.Layer.Address.ToInt64():X} focus layer 0x{focusLayer.ToInt64():X} hint={change.Layer.IsHintBar} top=0x{snapshot.TopInteractive?.Address.ToInt64() ?? 0:X} justFocused={this.JustFocused()}");
            if (change.Layer.IsHintBar)
            {
                this.OnHintBarChange(change, snapshot, focus, focusLayer);
                return;
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
                    this.Speak("game message", refusal.Text, true);
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
                this.OnHudChange(change);
                return;
            }

            if (this.inGame)
            {
                this.OnInGameChange(change, snapshot, focus, focusLayer, legends);
                return;
            }

            this.OnPreGameChange(change, snapshot, focus, focusLayer);
        }

        /// <summary>The controller legends of the hint bar, said after the focus that waits for them.</summary>
        private void OnHintBarChange(ScreenChange change, ScreenSnapshot snapshot, AccessibleUiNode? focus, IntPtr focusLayer)
        {
            if (this.inGame && focusLayer == IntPtr.Zero && change.Kind == ScreenChangeKind.TextsAppeared &&
                (DateTime.UtcNow - this.lastPanel.At).TotalMilliseconds < FocusTextMergeMs)
            {
                // What the map cursor's place offers ("Instance Manager, A" on a town): after the place.
                this.Speak("action of panel without focus", Describe(change.Texts), false);
            }
            else if (this.inGame && change.Kind == ScreenChangeKind.TextsAppeared &&
                     (DateTime.UtcNow - this.valueChangedAt).TotalMilliseconds < FocusTextMergeMs)
            {
                // The control changed in place and offers other actions: A on a bound skill picks it up, its button
                // reads "empty" and the bar "Place, A. Cancel, B" (skill bind screen, 2026-10-24).
                this.Speak("actions after value change", Describe(change.Texts), false);
            }
            else if (this.inGame && focusLayer != IntPtr.Zero && change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared)
            {
                // What a panel's buttons do, each once per opening and after the focus: the inventory said "Equip, X"
                // from the item but never "Drop, Y" from this bar (user, 2026-10-24).
                if (focusLayer != this.legendsLayer)
                {
                    this.legendsLayer = focusLayer;
                    this.legendsSaid.Clear();
                }

                var fresh = change.Texts.Where(text => this.legendsSaid.Add(Describe(new[] { text }))).ToList();
                if (fresh.Count > 0 && this.deferredFocus != null)
                {
                    this.hintAfterFocus = Announcements.Sentences(new[] { this.hintAfterFocus, Describe(fresh) });
                }
                else if (fresh.Count > 0)
                {
                    this.Speak("panel actions", Describe(fresh), false);
                }
            }
            else if (this.inGame && focusLayer == IntPtr.Zero && snapshot.TopInteractive is { } panel && panel.Address != this.HudLayer &&
                     change.Kind is ScreenChangeKind.LayerOpened or ScreenChangeKind.TextsAppeared)
            {
                // A panel nothing focuses (the world map: "Show quests, X"): its legends once per opening, after the
                // title and the place it shows, which come a moment later (Tick).
                if (panel.Address != this.legendsLayer)
                {
                    this.legendsLayer = panel.Address;
                    this.legendsSaid.Clear();
                }

                var fresh = change.Texts.Where(text => this.legendsSaid.Add(Describe(new[] { text }))).ToList();
                if (fresh.Count > 0)
                {
                    this.panelLegends = (Announcements.Sentences(new[] { this.panelLegends.Text, Describe(fresh) }), DateTime.UtcNow);
                }
            }
        }

        /// <summary>Control-id suffix of a description a control shows beside itself while focused.</summary>
        private const string PopupSuffix = "_popup";

        /// <summary>The pre-game layer of notices and the connection log (login dump, 2026-10-24).</summary>
        private const string NoticeLayerName = "visible_area";

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

        private void Speak(string reason, string text, bool interrupt, bool keep = false)
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
            this.log.Debug($"Narrate ({reason})");
            if (this.deferredFocus is { } deferred && text.Contains(this.lastFocusSpeech, StringComparison.Ordinal))
            {
                this.deferredFocus = null; // the popup's message carried the focus with it
            }

            if (keep && !interrupt && this.deferredFocus == null)
            {
                this.output.Keep(text, Math.Clamp(text.Length * SpokenMsPerChar, MinKeepMs, MaxKeepMs));
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
