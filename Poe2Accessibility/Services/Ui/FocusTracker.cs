namespace Poe2Accessibility.Services
{
    using System;
    using System.Diagnostics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Watches the game's own focused-element pointer and raises FocusChanged only when it moves, and
    ///     ValueChanged when the focused control's value changes in place (slider dragged, box toggled).
    ///     The game is the source of truth: nothing here remembers where focus "should" be.
    /// </summary>
    public sealed partial class FocusTracker
    {
        /// <summary>While the focused element is not yet visible (screen still fading in), re-check at this pace.</summary>
        private const int PendingRecheckMs = 250;

        /// <summary>How long a cell that reads empty as the cursor arrives is given to name its item (it did within 170 ms, 2026-11-10).</summary>
        private const int EmptyRecheckMs = 200;

        /// <summary>The grid whose cell read empty on arrival, read once more before it is said.</summary>
        private IntPtr emptyOnArrival;

        /// <summary>The grid whose item's tooltip was still filling, read again sooner than a hidden screen.</summary>
        private IntPtr fillingTooltip;

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
        private readonly NoFocusList noFocusList;

        /// <summary>The semantics' per-address memos are kept through this long without a focus (a cursor between two controls).</summary>
        private const int MemoKeepMs = 500;

        /// <summary>The layer the semantics' per-address memos were learnt in, and since when nothing has the focus (0: something has).</summary>
        private IntPtr memoLayer;
        private long noFocusSince;

        /// <summary>The layer of the last focus: a popup it opens without a focus is looked for there.</summary>
        private IntPtr focusLayer;
        private System.Collections.Generic.List<(IntPtr Address, bool Frame)> markers = new();
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

        /// <summary>When the pointer last moved to the element it is on: how long a free cursor has rested there.</summary>
        private long hoverSince;
        private bool pendingVisibility;

        /// <summary>A grid's cursor is seen and waits to be said (its item's first tooltip filling, a cell empty on arrival): its line comes within moments.</summary>
        public bool ItemPending => this.pendingVisibility && this.lastFocused != IntPtr.Zero && (this.lastFocused == this.fillingTooltip || this.lastFocused == this.emptyOnArrival);
        private GameStateTypes lastState = GameStateTypes.GameNotLoaded;

        public FocusTracker(HostMemoryReader reader, UiElementReader elements, UiManagerLocator locator, UiSemantics semantics, PluginLog log)
        {
            this.reader = reader;
            this.elements = elements;
            this.locator = locator;
            this.semantics = semantics;
            this.log = log;
            this.noFocusList = new NoFocusList(reader, elements);
        }

        public event Action<AccessibleUiNode>? FocusChanged;

        /// <summary>Whose cursor is read in local co-op, 1 or 2 (<see cref="InputSeat"/>).</summary>
        public int Seat { get; set; } = 1;

        /// <summary>The focused control's value changed without the focus moving (new value only is spoken).</summary>
        public event Action<AccessibleUiNode>? ValueChanged;

        /// <summary>Same element, same value, but its tooltip appeared or changed (built after the cursor landed).</summary>
        public event Action<AccessibleUiNode>? TooltipChanged;

        public AccessibleUiNode? Current { get; private set; }

        /// <summary>Gets when the focus came to the element it is on (Environment.TickCount64).</summary>
        public long CurrentSince { get; private set; }

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
                this.markersRoot = IntPtr.Zero; // the root outlives its screen: a second visit rebuilds the controls under it
                this.semantics.ForgetFocusMemos();
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

            this.ForgetMemosWithoutFocus(focused);
            var changed = focused != this.lastFocused;
            var waitedFor = focused == this.emptyOnArrival ? EmptyRecheckMs : focused == this.fillingTooltip ? TooltipSettle.HoldMs : PendingRecheckMs;
            var recheck = this.pendingVisibility && this.sincePendingCheck.ElapsedMilliseconds >= waitedFor;
            if (!changed && !recheck)
            {
                this.PollValue();
                return;
            }

            this.lastFocused = focused;
            this.pendingVisibility = false;
            this.emptyOnArrival = changed ? IntPtr.Zero : this.emptyOnArrival;
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

            if (changed)
            {
                this.hoverSince = Environment.TickCount64;
            }

            var bare = hovered && node.IsVisible && node.Tooltip.Count == 0 && !node.TooltipPending;
            if (bare && changed)
            {
                this.log.Debug($"Hover without tooltip 0x{focused.ToInt64():X}: name=\"{node.Name}\" role={node.Role} text=\"{node.Text}\" value=\"{node.Value}\" byId={node.NamedByControlId}");
                this.NoteUnnamed(focused, node); // a hover nothing names is never spoken: the Info line is how it gets fixed
            }

            if (!node.IsVisible || (bare && !BareHover.IsSaid(state == GameStateTypes.InGameState, node.Role, node.Text, node.NamedByControlId, Environment.TickCount64 - this.hoverSince)))
            {
                // Focus already points at the control while the screen is still hidden: say it once it shows. A hovered
                // node is described by the tooltip the game builds a frame later; one that never gets one is said by its
                // own caption once the cursor rests on it (BareHover), else stays silent.
                this.pendingVisibility = true;
                this.sincePendingCheck.Restart();
                return;
            }

            if (node.IsEmptyCell && focused != this.emptyOnArrival)
            {
                // The grid names its item a moment after the cursor arrives: "Amuleto, vacío" was said, then the amulet.
                this.emptyOnArrival = focused;
                this.pendingVisibility = true;
                this.sincePendingCheck.Restart();
                return;
            }

            this.fillingTooltip = node.TooltipFilling ? focused : IntPtr.Zero;
            if (node.TooltipFilling)
            {
                // An item's first tooltip, still being filled: read again once it holds still (TooltipSettle).
                this.log.Debug($"Focus 0x{focused.ToInt64():X} waits for its item's first tooltip: {node.Tooltip.Count} lines");
                this.pendingVisibility = true;
                this.sincePendingCheck.Restart();
                return;
            }

            this.emptyOnArrival = IntPtr.Zero;
            this.Current = node;
            this.CurrentSince = Environment.TickCount64;
            this.CurrentPanelIndex = this.PanelIndexOf(focused);
            if (this.CurrentLayer != this.memoLayer)
            {
                // Another layer: what the semantics kept by address was the last one's (the map's own marker, a grid's tabs).
                // This focus is read again without them, so what it teaches (the marker a map opened on) is this layer's.
                this.memoLayer = this.CurrentLayer;
                this.semantics.ForgetFocusMemos();
                node = this.semantics.Describe(focused, hovered) ?? node;
                this.Current = node;
            }

            this.focusLayer = this.CurrentLayer;
            this.optionSections = this.OptionSectionsOf(focused);
            this.sinceValuePoll.Restart();
            this.log.Info($"Focus -> 0x{focused.ToInt64():X} panel={this.CurrentPanelIndex} name=\"{node.Name}\" role={node.Role} text=\"{node.Text}\" value=\"{node.Value}\" context=\"{node.Context}\" group=\"{node.Group}\" notes=\"{string.Join(". ", node.PanelNotes)}\"");
            this.NoteUnnamed(focused, node);
            this.FocusChanged?.Invoke(node);
        }

        /// <summary>A panel closed and opened again shows no layer change: the memos also go once nothing has had the focus for a moment.</summary>
        private void ForgetMemosWithoutFocus(IntPtr focused)
        {
            if (focused != IntPtr.Zero)
            {
                this.noFocusSince = 0;
                return;
            }

            var now = Environment.TickCount64;
            if (this.noFocusSince == 0)
            {
                this.noFocusSince = now;
            }
            else if (this.noFocusSince > 0 && now - this.noFocusSince >= MemoKeepMs)
            {
                this.noFocusSince = -1; // forgotten once per stretch without focus
                this.memoLayer = IntPtr.Zero;
                this.semantics.ForgetFocusMemos();
            }
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
    }
}
