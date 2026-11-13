namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.States;

    /// <summary>The element base, layer root and focus probes of <see cref="LayoutProbe"/>.</summary>
    public sealed partial class LayoutProbe
    {
        /// <summary>
        ///     The element base fields every walk of the tree reads (self, children, parent, flags, rects), judged by the host on
        ///     the UI root (<see cref="GameReader.Utils.UiElementLayoutSearch"/>) before anything else reads through them.
        /// </summary>
        public void ProbeElementBase(IntPtr root)
        {
            // An element that is no UI root (or a root the search fails on) is tried again only after a while: the manager is
            // looked for on every frame its cache misses.
            var now = Environment.TickCount64;
            if (this.elementBaseKnown || root == IntPtr.Zero || (this.elementBaseTried.TryGetValue(root, out var tried) && now - tried < RetryMs))
            {
                return;
            }

            if (this.elementBaseTried.Count >= MaxOwnElements)
            {
                this.elementBaseTried.Clear();
            }

            this.elementBaseTried[root] = now;

            var outcomes = GameReader.Utils.OffsetHealer.JudgeUiElementBase(root);
            if (outcomes.Count == 0)
            {
                return;
            }

            // A root judged before its children exist reads the tree lost: judged again on the next roots (LayoutVerdict).
            var lost = outcomes.Any(outcome => outcome.Health == GameReader.Utils.StructHealth.Lost);
            this.elementBaseLostAt ??= lost ? now : null;
            if (!LayoutVerdict.Settled(lost, this.elementBaseLostAt ?? now, now))
            {
                return;
            }

            this.elementBaseKnown = true;
            foreach (var outcome in outcomes)
            {
                this.report.Record(outcome);
            }
        }

        /// <summary>
        ///     When none of a state object's <paramref name="pointers"/> reads as an element: looks for the UI root above them
        ///     by shape alone and judges the element base on it, so base fields a patch moved are found before any screen was
        ///     read (the screens before the game have no other way to a root). Tried again only after a while.
        /// </summary>
        /// <returns>The root found, zero when none; null when it was not looked for this time.</returns>
        public IntPtr? ProbeElementBaseAbove(IReadOnlyList<IntPtr> pointers)
        {
            var now = Environment.TickCount64;
            if (this.elementBaseKnown || pointers.Count == 0 || now < this.nextRootSearchAt)
            {
                return null;
            }

            this.nextRootSearchAt = now + RetryMs;
            var root = GameReader.Utils.OffsetHealer.FindUiRootAbove(pointers);
            this.ProbeElementBase(root);
            return root;
        }

        /// <summary>
        ///     The in-game layer containers (InGameState's mouse and gamepad UI roots): an element whose children carry the
        ///     known layer names. Taken where they read now when either holds one; else the slot of the state nearest them that
        ///     does, and both move by that shift (call once the element names are known).
        /// </summary>
        public void ProbeLayerRoots(IntPtr inGameState)
        {
            var now = Environment.TickCount64;
            if (this.layerRootsKnown || !this.namesKnown || inGameState == IntPtr.Zero || now - this.layerRootsTriedAt < RetryMs)
            {
                return;
            }

            this.layerRootsTriedAt = now;
            var (mouse, pad) = (MeasuredStateField(nameof(InGameStateOffset.UiRootStructPtr)), MeasuredStateField(nameof(InGameStateOffset.GamepadUiRootStructPtr)));
            var (mouseNow, padNow) = (GameReader.Utils.HostStructs.Follow<InGameStateOffset>(mouse), GameReader.Utils.HostStructs.Follow<InGameStateOffset>(pad));
            var slots = this.reader.ReadArray<IntPtr>(inGameState, ManagerScanBytes / IntPtr.Size);
            bool Holds(int offset) => offset >= 0 && offset / IntPtr.Size < slots.Length && this.IsLayerContainer(slots[offset / IntPtr.Size]);
            if (Holds(mouseNow) || Holds(padNow))
            {
                this.layerRootsKnown = true;
                this.layerRootMisses.Clear();
                this.Record(LayerRootsPart, mouse, mouseNow);
                return;
            }

            // A slot found elsewhere needs a second fact: its element climbs to the root its UI manager shows.
            bool HoldsLinked(int offset) => Holds(offset) && this.LeadsToManagerRoot(slots[offset / IntPtr.Size]);
            var nearPad = OffsetSearch.First(padNow, ManagerScanBytes, Step, HoldsLinked, Step);
            var nearMouse = OffsetSearch.First(mouseNow, ManagerScanBytes, Step, HoldsLinked, Step);
            if ((nearPad ?? nearMouse) is null)
            {
                this.pendingState = inGameState;
                if (this.layerRootMisses.Against(++this.looks))
                {
                    this.report.Missing(LayerRootsPart);
                }

                return;
            }

            var shift = nearPad is { } p && (nearMouse is not { } m || Math.Abs(p - padNow) <= Math.Abs(m - mouseNow)) ? p - padNow : nearMouse!.Value - mouseNow;
            GameReader.Utils.OffsetHealer.PlacePointers(typeof(InGameStateOffset), (mouse, mouseNow + shift), (pad, padNow + shift));
            this.layerRootsKnown = true;
            this.layerRootMisses.Clear();
            this.report.Moved(LayerRootsPart, mouseNow, mouseNow + shift);
        }

        /// <summary>
        ///     Watches the UI manager's focus slot while the player moves (<see cref="FocusSlotCheck"/>): once judged where it
        ///     reads, nothing changes; judged elsewhere, the last-focused and hovered slots move with it. Call every frame.
        /// </summary>
        public void ObserveFocus(IntPtr manager)
        {
            this.LookAgain(manager);
            if (this.focusKnown || manager == IntPtr.Zero)
            {
                return;
            }

            var current = GameLayout.UiManager.CurrentOffset;
            this.focusCheck ??= new FocusSlotCheck(current, GameLayout.UiManager.LastFocusedOffset - current);
            var start = Math.Max(0, current - FocusWindow);
            var slots = this.reader.ReadArray<IntPtr>(manager + start, ((current + FocusWindow - start) / IntPtr.Size) + 1);
            var held = new Dictionary<int, long>(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                held[start + (i * IntPtr.Size)] = this.IsOwnElement(slots[i], manager) ? slots[i].ToInt64() : 0;
            }

            if (this.focusCheck.See(held) is not { } found)
            {
                return;
            }

            this.focusKnown = true;
            var shift = found - current;
            GameLayout.UiManager.CurrentOffset += shift;
            GameLayout.UiManager.LastFocusedOffset += shift;
            GameLayout.UiManager.HoveredOffset += shift;
            this.Record(FocusPart, this.measuredCurrent, found);
        }

        private static int MeasuredStateField(string field) => Marshal.OffsetOf<InGameStateOffset>(field).ToInt32();

        /// <summary>An element of this manager: the focus never holds another's (a stray pointer or a closed state's).</summary>
        private bool IsOwnElement(IntPtr element, IntPtr manager)
        {
            if (element == IntPtr.Zero)
            {
                return false;
            }

            if (manager != this.ownElementsManager)
            {
                this.ownElements.Clear();
                this.ownElementsManager = manager;
            }

            if (!this.ownElements.TryGetValue(element.ToInt64(), out var own))
            {
                own = this.elements.IsElement(element) &&
                      this.reader.TryRead<IntPtr>(element + GameLayout.Element.ManagerOffset, out var owner) && owner == manager;
                if (this.ownElements.Count >= MaxOwnElements)
                {
                    this.ownElements.Clear();
                }

                this.ownElements[element.ToInt64()] = own;
            }

            return own;
        }

        /// <summary>An element whose children carry at least two of the known layer names.</summary>
        private bool IsLayerContainer(IntPtr candidate)
        {
            if (!this.reader.IsValidAddress(candidate) || !this.elements.TryReadElement(candidate, out var info))
            {
                return false;
            }

            var named = 0;
            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(child, out var layer) && KnownLayerNames.Contains(layer.Name) && ++named >= MinLayerNames)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
