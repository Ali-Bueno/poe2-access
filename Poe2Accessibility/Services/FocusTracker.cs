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

        /// <summary>Highlight markers are looked for among this many elements of the screen (a pre-game screen has ~4500).</summary>
        private const int MaxMarkerScan = 8000;

        /// <summary>
        ///     Child names that mark their parent as the highlighted control while visible: the creation screen's
        ///     Continue after the name field (create_button > "hightlight", the game's spelling) has nothing in the UI
        ///     manager's focus or hover (2026-10-23).
        /// </summary>
        private static readonly string[] HighlightMarkers = { "hightlight", "highlight" };

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
        private IntPtr lastFocused;
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

        /// <summary>The focused control's value changed without the focus moving (new value only is spoken).</summary>
        public event Action<AccessibleUiNode>? ValueChanged;

        /// <summary>Same element, same value, but its tooltip appeared or changed (built after the cursor landed).</summary>
        public event Action<AccessibleUiNode>? TooltipChanged;

        public AccessibleUiNode? Current { get; private set; }

        /// <summary>UI root of the current screen (from the manager), zero when unknown.</summary>
        public IntPtr CurrentRoot { get; private set; }

        /// <summary>Index of the root child that contains the focused element (which panel of the screen is active).</summary>
        public int CurrentPanelIndex { get; private set; } = -1;

        /// <summary>Screen layer (child of LayerContainer, or of the root) that contains the focused element; zero when unknown.</summary>
        public IntPtr CurrentLayer { get; private set; }

        /// <summary>Element whose children are the screen layers; in game that is not the root (set by the plugin each tick).</summary>
        public IntPtr LayerContainer { get; set; }

        /// <summary>The layer container <see cref="CurrentLayer"/> was found under.</summary>
        private IntPtr layerFoundUnder;

        /// <summary>Forget the last focus so the next tick re-announces (screen change, user request).</summary>
        /// <summary>The active state's UI manager (zero when none is found); the pad state lives there too.</summary>
        public IntPtr Manager { get; private set; }

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
            this.sinceValuePoll.Restart();
            this.log.Info($"Focus -> 0x{focused.ToInt64():X} panel={this.CurrentPanelIndex} name=\"{node.Name}\" role={node.Role} text=\"{node.Text}\" value=\"{node.Value}\"");
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

            if (fresh.Value == this.Current.Value && fresh.Text == this.Current.Text)
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
            if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.CurrentOffset, out var current) && this.elements.IsElement(current))
            {
                return (current, false);
            }

            if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.HoveredOffset, out var hovered) && this.elements.IsElement(hovered))
            {
                return (hovered, true);
            }

            // Pre-game only: in game nothing focused is the usual case (playing), and the HUD is large.
            return state != GameStateTypes.InGameState ? (this.HighlightedControl(), false) : (IntPtr.Zero, false);
        }

        /// <summary>The parent of a visible highlight marker under the current root, or zero.</summary>
        private IntPtr HighlightedControl()
        {
            if (this.CurrentRoot == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (this.markersRoot != this.CurrentRoot || (this.markers.Count == 0 && this.sinceMarkerScan.ElapsedMilliseconds >= MarkerRescanMs))
            {
                this.markersRoot = this.CurrentRoot;
                this.markers = this.FindMarkers(this.CurrentRoot);
                this.sinceMarkerScan.Restart();
            }

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

                foreach (var child in this.elements.ReadChildren(info))
                {
                    queue.Enqueue(child);
                }
            }

            return found;
        }
    }
}
