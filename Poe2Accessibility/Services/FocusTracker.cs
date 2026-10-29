namespace Poe2Accessibility.Services
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using GameHelper.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Watches the game's own focused-element pointer and raises FocusChanged only when it moves, and
    ///     ValueChanged when the focused control's value changes in place (slider dragged, box toggled).
    ///     The game is the source of truth: nothing here remembers where focus "should" be.
    /// </summary>
    public sealed class FocusTracker
    {
        /// <summary>While the focused element is not yet visible (screen still fading in), re-check at this pace.</summary>
        private const int PendingRecheckMs = 250;

        /// <summary>How often the focused control is re-read for in-place value changes (a few reads each time).</summary>
        private const int ValuePollMs = 150;

        /// <summary>A screen with no highlight marker is searched again this often (it may still be building).</summary>
        private const int MarkerRescanMs = 2000;

        /// <summary>The shop's markers are searched again this often while none shows (a new tab, entries made on scrolling).</summary>
        private const int InGameMarkerRescanMs = 300;

        /// <summary>A shop page showing no marker for long (a tab bar, a page still loading) is searched less often, up to this.</summary>
        private const int InGameMarkerRescanMaxMs = 1200;

        /// <summary>Highlight markers are looked for among this many elements of the screen (a pre-game screen has ~4500).</summary>
        private const int MaxMarkerScan = 8000;

        /// <summary>
        ///     Child names that mark their parent as the highlighted control while visible: the creation screen's
        ///     Continue after the name field (create_button > "hightlight", the game's spelling) has nothing in the UI
        ///     manager's focus or hover (2026-10-23).
        /// </summary>
        private static readonly string[] HighlightMarkers = { "hightlight", "highlight", "highlight_picture", "highlight_rect" };

        /// <summary>Hops from a focused control up to its options row (a combo box sits three below it).</summary>
        private const int OptionRowHops = 4;

        /// <summary>Fewest rows that make an options list.</summary>
        private const int MinOptionRows = 3;

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly UiManagerLocator locator;
        private readonly UiSemantics semantics;
        private readonly PluginLog log;
        private readonly Stopwatch sincePendingCheck = Stopwatch.StartNew();
        private readonly Stopwatch sinceValuePoll = Stopwatch.StartNew();
        private readonly Stopwatch sinceMarkerScan = Stopwatch.StartNew();
        private System.Collections.Generic.List<IntPtr> markers = new();
        private IntPtr markersRoot;
        private long inGameRescanMs = InGameMarkerRescanMs;
        private IntPtr lastFocused;
        private IntPtr seenCurrent;
        private IntPtr seenHovered;
        private IntPtr seenHighlighted;
        private bool hoverIsNewest;

        /// <summary>
        ///     The element holding the options sections of the last focus (<see cref="GameLayout.OptionsSection"/>): the game
        ///     highlights a disabled row without focusing it, so that row is found from its section. Zero elsewhere.
        /// </summary>
        private IntPtr optionSections;

        /// <summary>A focus the game gave as a side effect (<see cref="IgnoreNextFocus"/>), ignored until the focus moves on.</summary>
        private IntPtr ignoredFocus;

        private long ignoreFocusUntil;
        private bool pendingVisibility;
        private GameStateTypes lastState = GameStateTypes.GameNotLoaded;

        public FocusTracker(HostMemoryReader reader, UiElementReader elements, UiManagerLocator locator, UiSemantics semantics, PluginLog log)
        {
            this.reader = reader;
            this.elements = elements;
            this.locator = locator;
            this.semantics = semantics;
            this.log = log;
        }

        public event Action<AccessibleUiNode>? FocusChanged;

        /// <summary>Whose cursor is read in local co-op, 1 or 2 (<see cref="InputSeat"/>).</summary>
        public int Seat { get; set; } = 1;

        /// <summary>The focused control's value changed without the focus moving (new value only is spoken).</summary>
        public event Action<AccessibleUiNode>? ValueChanged;

        /// <summary>Same element, same value, but its tooltip appeared or changed (built after the cursor landed).</summary>
        public event Action<AccessibleUiNode>? TooltipChanged;

        public AccessibleUiNode? Current { get; private set; }

        /// <summary>UI root of the current screen (from the manager), zero when unknown.</summary>
        public IntPtr CurrentRoot { get; private set; }

        /// <summary>Index of the root child that contains the focused element (which panel of the screen is active).</summary>
        public int CurrentPanelIndex { get; private set; } = -1;

        /// <summary>
        ///     In game, a full-screen page that marks its choice with a highlight child instead of focusing it (the
        ///     microtransaction shop, <see cref="UiSemantics.ShopPage"/>); zero when none is shown. Set by the plugin.
        /// </summary>
        public Func<IntPtr>? InGameHighlightRoot { get; set; }

        /// <summary>Screen layer (child of LayerContainer, or of the root) that contains the focused element; zero when unknown.</summary>
        public IntPtr CurrentLayer { get; private set; }

        /// <summary>Element whose children are the screen layers; in game that is not the root (set by the plugin each tick).</summary>
        public IntPtr LayerContainer { get; set; }

        /// <summary>The layer container <see cref="CurrentLayer"/> was found under.</summary>
        private IntPtr layerFoundUnder;

        /// <summary>Forget the last focus so the next tick re-announces (screen change, user request).</summary>
        /// <summary>The active state's UI manager (zero when none is found); the pad state lives there too.</summary>
        public IntPtr Manager { get; private set; }

        /// <summary>
        ///     The focus the game gives within <paramref name="withinMs"/> is a side effect, not the player's move: the
        ///     guide chord's R3 puts the game's HUD cursor on the experience bar, which was read over "Guiando hacia…" and
        ///     made L3 read it again instead of the scanner (2026-10-24). Ignored until the game moves the focus on.
        /// </summary>
        public void IgnoreNextFocus(int withinMs) => this.ignoreFocusUntil = Environment.TickCount64 + withinMs;

        public void Reset()
        {
            this.lastFocused = IntPtr.Zero;
            this.pendingVisibility = false;
            this.Current = null;
            this.CurrentLayer = IntPtr.Zero;
        }

        public void Tick(GameStateTypes state, IntPtr stateObject)
        {
            if (state != this.lastState)
            {
                this.lastState = state;
                this.Reset();
            }

            var (focused, hovered) = this.ReadFocusedPointer(state, stateObject);
            if (focused != IntPtr.Zero && focused != this.lastFocused && Environment.TickCount64 < this.ignoreFocusUntil)
            {
                this.ignoreFocusUntil = 0;
                this.ignoredFocus = focused;
                this.log.Debug($"Focus 0x{focused.ToInt64():X} ignored: the game's side effect of a mod chord");
            }

            if (focused != this.ignoredFocus)
            {
                this.ignoredFocus = IntPtr.Zero;
            }
            else if (focused != IntPtr.Zero)
            {
                focused = IntPtr.Zero; // neither said nor a menu for the mod button while the game keeps it there
            }

            var changed = focused != this.lastFocused;
            var recheck = this.pendingVisibility && this.sincePendingCheck.ElapsedMilliseconds >= PendingRecheckMs;
            if (!changed && !recheck)
            {
                this.PollValue();
                return;
            }

            this.lastFocused = focused;
            this.pendingVisibility = false;
            if (focused == IntPtr.Zero)
            {
                this.Current = null;
                this.CurrentLayer = IntPtr.Zero;
                return;
            }

            var node = this.semantics.Describe(focused, hovered);
            if (node == null)
            {
                this.log.Debug($"Focused pointer 0x{focused.ToInt64():X} is not a readable element");
                return;
            }

            if (!node.IsVisible || (hovered && node.Tooltip.Count == 0))
            {
                // Focus already points at the control while the screen is still hidden: say it once it shows. A hovered
                // node is described by the tooltip the game builds a frame later; one that never gets one stays silent.
                this.pendingVisibility = true;
                this.sincePendingCheck.Restart();
                return;
            }

            this.Current = node;
            this.CurrentPanelIndex = this.PanelIndexOf(focused);
            this.optionSections = this.OptionSectionsOf(focused);
            this.sinceValuePoll.Restart();
            this.log.Info($"Focus -> 0x{focused.ToInt64():X} panel={this.CurrentPanelIndex} name=\"{node.Name}\" role={node.Role} text=\"{node.Text}\" value=\"{node.Value}\" context=\"{node.Context}\" group=\"{node.Group}\" notes=\"{string.Join(". ", node.PanelNotes)}\"");
            this.FocusChanged?.Invoke(node);
        }

        /// <summary>Which child of the root the element lives under; -1 when it cannot be climbed to the root.</summary>
        private int PanelIndexOf(IntPtr element)
        {
            if (this.CurrentRoot == IntPtr.Zero || !this.elements.TryReadElement(element, out var info))
            {
                return -1;
            }

            var chain = this.elements.ReadAncestors(info, 64);
            if (chain.Count == 0 || chain[^1].Address != this.CurrentRoot)
            {
                return -1;
            }

            var topLevel = chain.Count >= 2 ? chain[^2].Address : element;
            var layerIndex = chain.FindIndex(ancestor => ancestor.Address == this.LayerContainer);
            this.CurrentLayer = layerIndex > 0 ? chain[layerIndex - 1].Address : layerIndex == 0 ? element : topLevel;
            this.layerFoundUnder = this.LayerContainer;
            if (!this.elements.TryReadElement(this.CurrentRoot, out var root))
            {
                return -1;
            }

            return Array.IndexOf(this.elements.ReadChildren(root), topLevel);
        }

        private void PollValue()
        {
            if (this.Current == null || this.sinceValuePoll.ElapsedMilliseconds < ValuePollMs)
            {
                return;
            }

            this.sinceValuePoll.Restart();
            if (this.layerFoundUnder != this.LayerContainer)
            {
                // Focused before the in-game layer container was known (plugin started on an open panel): the
                // layer is the container itself until placed again.
                this.PanelIndexOf(this.Current.Address);
            }

            var fresh = this.semantics.Describe(this.Current.Address, this.Current.IsHovered);
            if (fresh is { IsVisible: false })
            {
                // Its panel was switched away (Cosmetics → Quests keeps the pointer): no focus until it shows again.
                this.Current = null;
                this.CurrentLayer = IntPtr.Zero;
                this.pendingVisibility = true;
                this.sincePendingCheck.Restart();
                return;
            }

            if (fresh == null)
            {
                return;
            }

            if (fresh.Value == this.Current.Value && fresh.Text == this.Current.Text && fresh.IsExpanded == this.Current.IsExpanded)
            {
                if (!fresh.Tooltip.SequenceEqual(this.Current.Tooltip))
                {
                    this.Current = fresh;
                    this.TooltipChanged?.Invoke(fresh);
                }

                return;
            }

            this.log.Info($"Value -> 0x{fresh.Address.ToInt64():X} \"{fresh.Text}\" = \"{fresh.Value}\"");
            this.Current = fresh;
            this.ValueChanged?.Invoke(fresh);
        }

        /// <summary>
        ///     The activation target: the element under the controller cursor, else the one under the free cursor
        ///     (passive tree). The previous-focus slot is not a fallback: after a panel closes it keeps the hidden
        ///     element, and reopening would then look unchanged.
        /// </summary>
        private (IntPtr Element, bool Hovered) ReadFocusedPointer(GameStateTypes state, IntPtr stateObject)
        {
            var manager = this.locator.Locate(state, stateObject);
            this.Manager = manager;
            if (manager == IntPtr.Zero)
            {
                this.CurrentRoot = IntPtr.Zero;
                return (IntPtr.Zero, false);
            }

            this.CurrentRoot = this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var root) ? root : IntPtr.Zero;
            var seat = (this.Seat - 1) * GameLayout.UiManager.PlayerStride;
            var current = this.ElementAt(manager + GameLayout.UiManager.CurrentOffset + seat);
            var hovered = this.ElementAt(manager + GameLayout.UiManager.HoveredOffset + seat);

            // Pre-game the whole screen; in game nothing focused is the usual case (playing) and the HUD is large, so only
            // a page that highlights its choice (the shop) is searched.
            var highlighted = current != IntPtr.Zero ? IntPtr.Zero
                : state != GameStateTypes.InGameState ? this.HighlightedControl(this.CurrentRoot)
                : this.HighlightedControl(this.InGameHighlightRoot?.Invoke() ?? IntPtr.Zero);
            this.TrackNewest(current, hovered, highlighted);
            if (current != IntPtr.Zero)
            {
                return (current, false);
            }

            if (hovered != IntPtr.Zero && (this.hoverIsNewest || highlighted == IntPtr.Zero))
            {
                return (hovered, true);
            }

            var disabledRow = this.DisabledOptionRow();
            return disabledRow != IntPtr.Zero ? (disabledRow, false) : (highlighted, false);
        }

        private IntPtr ElementAt(IntPtr slot) =>
            this.reader.TryRead<IntPtr>(slot, out var element) && this.elements.IsElement(element) ? element : IntPtr.Zero;

        /// <summary>
        ///     Notes whether the hovered element moved after the controller did: the creation screen keeps a class info
        ///     panel hovered while the stick highlights the name field, and the stale hover must not win.
        /// </summary>
        private void TrackNewest(IntPtr current, IntPtr hovered, IntPtr highlighted)
        {
            if (current != this.seenCurrent || highlighted != this.seenHighlighted)
            {
                this.hoverIsNewest = false;
            }

            if (hovered != this.seenHovered)
            {
                this.hoverIsNewest = true;
            }

            (this.seenCurrent, this.seenHovered, this.seenHighlighted) = (current, hovered, highlighted);
        }

        /// <summary>The holder of the options sections <paramref name="element"/> sits in (row → list → page → section → holder), or zero.</summary>
        private IntPtr OptionSectionsOf(IntPtr element)
        {
            if (!this.elements.TryReadElement(element, out var info))
            {
                return IntPtr.Zero;
            }

            var chain = this.elements.ReadAncestors(info, OptionRowHops + 4);
            chain.Insert(0, info);
            for (var hop = 0; hop <= OptionRowHops && hop + 4 < chain.Count; hop++)
            {
                var rows = this.elements.ReadChildren(chain[hop + 1]);
                var row = Array.IndexOf(rows, chain[hop].Address);
                if (rows.Length >= MinOptionRows && row >= 0)
                {
                    // The first list on the way up is the options list or none.
                    return this.elements.IsSectionRow(chain[hop + 3].Address, row) ? chain[hop + 4].Address : IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The control of the options row the active section highlights when the game focuses nothing, if that control is
        ///     disabled ("Resolución de la ventana" while the mode is windowed full screen); zero otherwise.
        /// </summary>
        private IntPtr DisabledOptionRow()
        {
            if (this.optionSections == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (!this.elements.TryReadElement(this.optionSections, out var holder) || !this.elements.IsVisible(holder))
            {
                this.optionSections = IntPtr.Zero; // the options page closed or another tab shows
                return IntPtr.Zero;
            }

            foreach (var section in this.elements.ReadChildren(holder))
            {
                if (this.elements.ReadSectionRow(section) is { } row && this.elements.TryReadElement(section, out var sectionInfo))
                {
                    var rows = this.SectionRows(sectionInfo);
                    return row < rows.Length ? this.DisabledControlIn(rows[row]) : IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>A section's rows: its grandchild with the most children (heading and list sit in a page).</summary>
        private IntPtr[] SectionRows(in UiElementReader.ElementInfo section)
        {
            var rows = Array.Empty<IntPtr>();
            foreach (var page in this.elements.ReadChildren(section))
            {
                if (!this.elements.TryReadElement(page, out var pageInfo))
                {
                    continue;
                }

                foreach (var list in this.elements.ReadChildren(pageInfo))
                {
                    if (this.elements.TryReadElement(list, out var listInfo) && this.elements.ReadChildren(listInfo) is { } children && children.Length > rows.Length)
                    {
                        rows = children;
                    }
                }
            }

            return rows;
        }

        /// <summary>The row's control (its first interactive descendant) when the game has it disabled, else zero.</summary>
        private IntPtr DisabledControlIn(IntPtr row)
        {
            var queue = new System.Collections.Generic.Queue<(IntPtr Address, int Depth)>();
            queue.Enqueue((row, 0));
            while (queue.Count > 0)
            {
                var (address, depth) = queue.Dequeue();
                if (!this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                {
                    return FlagBits.EnabledTrusted && !info.HasFlagBit(GameLayout.Element.EnabledFlagBit) ? address : IntPtr.Zero;
                }

                if (depth < OptionRowHops)
                {
                    foreach (var child in this.elements.ReadChildren(info))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The parent of a visible highlight marker under <paramref name="root"/>, or zero. The markers are searched again
        ///     when none shows (a screen still building, a shop page whose entries are made as it scrolls).
        /// </summary>
        private IntPtr HighlightedControl(IntPtr root)
        {
            if (root == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (this.markersRoot != root)
            {
                this.markersRoot = root;
                this.markers = this.FindMarkers(root);
                this.sinceMarkerScan.Restart();
                this.inGameRescanMs = InGameMarkerRescanMs;
            }

            // A pre-game screen is searched again only while it has no marker at all; the shop whenever none shows, since
            // it rebuilds its entries.
            var found = this.VisibleMarkerParent();
            var stale = root == this.CurrentRoot ? this.markers.Count == 0 : found == IntPtr.Zero;
            if (stale && this.sinceMarkerScan.ElapsedMilliseconds >= this.RescanMs(root))
            {
                this.markers = this.FindMarkers(root);
                this.sinceMarkerScan.Restart();
                found = this.VisibleMarkerParent();
                this.inGameRescanMs = found == IntPtr.Zero ? Math.Min(this.inGameRescanMs * 2, InGameMarkerRescanMaxMs) : InGameMarkerRescanMs;
            }

            return found;
        }

        /// <summary>Pre-game screens rarely change; an in-game page rebuilds its entries as it scrolls and switches tabs.</summary>
        private long RescanMs(IntPtr root) => root == this.CurrentRoot ? MarkerRescanMs : this.inGameRescanMs;

        private IntPtr VisibleMarkerParent()
        {
            foreach (var marker in this.markers)
            {
                if (this.elements.TryReadElement(marker, out var info) && info.IsVisibleFlag && info.Raw.ParentPtr != IntPtr.Zero &&
                    this.elements.TryReadElement(info.Raw.ParentPtr, out var parent) && this.elements.IsVisible(parent))
                {
                    return info.Raw.ParentPtr;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The frame of a card whose caption is <paramref name="caption"/> ("name_label"): the card's last child, unnamed
        ///     (UI dump 2026-10-29); zero for another card.
        /// </summary>
        private IntPtr CardFrame(in UiElementReader.ElementInfo caption)
        {
            if (caption.Raw.ParentPtr == IntPtr.Zero || !this.elements.TryReadElement(caption.Raw.ParentPtr, out var card))
            {
                return IntPtr.Zero;
            }

            var children = this.elements.ReadChildren(card);
            return children.Length > 1 && this.elements.TryReadElement(children[^1], out var last) && last.Name.Length == 0 ? children[^1] : IntPtr.Zero;
        }

        private System.Collections.Generic.List<IntPtr> FindMarkers(IntPtr root)
        {
            var found = new System.Collections.Generic.List<IntPtr>();
            var queue = new System.Collections.Generic.Queue<IntPtr>();
            var seen = new System.Collections.Generic.HashSet<IntPtr>();
            queue.Enqueue(root);
            while (queue.Count > 0 && seen.Count < MaxMarkerScan)
            {
                var address = queue.Dequeue();
                if (!seen.Add(address) || !this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (Array.IndexOf(HighlightMarkers, info.Name) >= 0)
                {
                    found.Add(address);
                }

                if (root != this.CurrentRoot && info.Name == GameLayout.InGameUi.ShopEntryNameLabel && this.CardFrame(info) is var frame &&
                    frame != IntPtr.Zero && !found.Contains(frame))
                {
                    found.Add(frame); // the shop's offer cards: an unnamed frame, last, shown on the chosen one
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    queue.Enqueue(child);
                }
            }

            return found;
        }
    }
}
