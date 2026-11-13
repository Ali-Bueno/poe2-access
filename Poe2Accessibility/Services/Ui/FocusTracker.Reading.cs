namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader.RemoteEnums;

    /// <summary>The value poll and focused-pointer reading of <see cref="FocusTracker"/>: what changed in the focused element and which element holds the focus.</summary>
    public sealed partial class FocusTracker
    {
        private void PollValue()
        {
            if (this.Current == null || (!this.Current.TooltipPending && this.sinceValuePoll.ElapsedMilliseconds < ValuePollMs))
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

            // A list the game focuses as one element: another row is a move, even onto a row with the same text.
            var sameRow = !FocusNews.RowChanged(this.Current.Position, fresh.Position);
            if (sameRow && fresh.Value == this.Current.Value && fresh.Text == this.Current.Text && fresh.IsExpanded == this.Current.IsExpanded)
            {
                if (!fresh.Tooltip.SequenceEqual(this.Current.Tooltip))
                {
                    this.Current = fresh;
                    this.TooltipChanged?.Invoke(fresh);
                }

                return;
            }

            if (sameRow && fresh.Value == this.Current.Value && fresh.IsExpanded == this.Current.IsExpanded && FocusNews.IsTick(fresh.Role, this.Current.Text, fresh.Text))
            {
                // A clock in the focused row ticking (the Instance Manager's "11 minutes 45 seconds" left, 2026-11-08) is no
                // news, as on the screen (ScreenDiffer): the row was said once, with the time it showed then. A cell's
                // count or a gem's level is the item's own number and is news (FocusNews).
                this.Current = fresh;
                return;
            }

            if (fresh.IsEmptyCell && !this.Current.IsEmptyCell && this.emptiedValue != (fresh.Text, fresh.Value))
            {
                // A grid lets go of its item as the cursor leaves it for another grid, a moment before the focus follows:
                // "Mano principal, vacío" was said of a held weapon. An empty cell is believed on its second reading.
                this.emptiedValue = (fresh.Text, fresh.Value);
                return;
            }

            this.emptiedValue = default;
            this.log.Info($"Value -> 0x{fresh.Address.ToInt64():X} \"{fresh.Text}\" = \"{fresh.Value}\"");
            this.Current = fresh;
            this.ValueChanged?.Invoke(fresh);
        }

        /// <summary>The empty cell a grid read once on a value poll, not yet said.</summary>
        private (string Text, string Value) emptiedValue;

        /// <summary>
        ///     The activation target: the element under the controller cursor, else the one under the free cursor
        ///     (passive tree). The previous-focus slot is not a fallback: after a panel closes it keeps the hidden
        ///     element, and reopening would then look unchanged.
        /// </summary>
        private (IntPtr Element, bool Hovered) ReadFocusedPointer(GameStateTypes state, IntPtr stateObject)
        {
            var manager = this.locator.Locate(state, stateObject);
            this.Manager = manager;
            this.locator.ObserveFocus(manager);
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
                this.noFocusList.Reset();
                return (current, false);
            }

            if (hovered != IntPtr.Zero && (this.hoverIsNewest || highlighted == IntPtr.Zero))
            {
                return (hovered, true);
            }

            var disabledRow = this.DisabledOptionRow();
            return disabledRow != IntPtr.Zero ? (disabledRow, false)
                : highlighted != IntPtr.Zero ? (highlighted, false)
                : (this.noFocusList.Row(this.focusLayer), false);
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
    }
}
