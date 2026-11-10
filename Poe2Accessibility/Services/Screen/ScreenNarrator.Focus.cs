namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Focus rules of <see cref="ScreenNarrator"/>: the cursor's move, its late tooltip, a changed value, a tab switch, and the per-frame waits behind them.</summary>
    public sealed partial class ScreenNarrator
    {
        public void OnFocusChanged(AccessibleUiNode node)
        {
            this.lastFocusAt = DateTime.UtcNow;
            if (node.Name == EngraveChoiceName && node.Text.Length > 0)
            {
                this.Engraving = (node.Text, this.Engraving.Category);
            }

            this.unnamedFocus = node.Text.Length == 0 && node.Value.Length == 0 ? node.Address : IntPtr.Zero;
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
            this.lastText = node.Text;
            this.lastRow = node.Position;
            this.lastExpanded = node.IsExpanded;
            this.tooltipSaid = this.ReadTooltips || node.IsHovered ? node.Tooltip : Array.Empty<string>();
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
            else if (node.Position.Length > 0)
            {
                // A list of its own without a heading (the Character panel's stat tiles): coming back to the stat list
                // names its section again.
                this.lastGroup = string.Empty;
            }

            var text = focused;
            var notes = node.Context == this.notesContext ? new List<string>() : node.PanelNotes.Where(note => !focused.Contains(note.TrimEnd('.'), StringComparison.Ordinal)).ToList();
            if (notes.Count > 0)
            {
                // Entering a panel: the lines it shows besides the part the focus is in, before the focus (kept due
                // until said: a focus waiting for its layer can be overtaken by the next), unless the focus says them
                // (Party's "You are not in a party" heads its buttons).
                text = Announcements.InContext(Announcements.Sentences(notes), text);
            }

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

            this.notesContext = node.Context;
            this.deferredFocus = null;
            this.NoteNarrated(text);
            this.SayMove(this.WithPendingPage(text));
        }

        /// <summary>The cursor's new place: the player's own move ends a message's hold, the game's refresh does not.</summary>
        private void SayMove(string text)
        {
            if ((this.SinceDirection?.Invoke() ?? long.MaxValue) < DirectionMovesMs)
            {
                this.output.Moved(text);
            }
            else
            {
                this.output.Speak(text, true);
            }
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

            if ((this.ReadTooltips || node.IsHovered) && node.Tooltip.Count > 0 && node.Address == this.valueOwner)
            {
                // Only what was not said yet: a passive node is named before its tooltip is built, its stats and
                // links follow; a title the focus announcement already holds is not news either.
                var said = this.tooltipSaid;
                this.tooltipSaid = node.Tooltip;
                var fresh = FocusDetails.NewTooltipLines(said, node.Tooltip, this.lastFocusSpeech);
                if (fresh.Count > 0)
                {
                    this.Speak("tooltip of focus", Announcements.Sentences(fresh), FocusDetails.LineSwapped(said, node.Tooltip));
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
            this.TickBackToPlay();
            this.TickTabKey();
            if (this.deferredFocus is { } deferred && (DateTime.UtcNow - deferred.At).TotalMilliseconds >= NewLayerFocusDelayMs)
            {
                this.deferredFocus = null;
                this.notesContext = deferred.Node.Context;
                var text = deferred.Text;
                if (deferred.Node.Context.Length == 0 && this.Refresh?.Invoke(deferred.Node) is { Context.Length: > 0 } fresh &&
                    fresh.Context != this.lastContext)
                {
                    // The panel finished building after its focus arrived (Skills beside the inventory): its title now.
                    text = Announcements.InContext(fresh.Context, text);
                    this.lastContext = fresh.Context;
                }

                // A message that cut in while the focus waited is not cut off by it: "Waiting for the player to accept
                // the trade invitation…" over the party panel the focus went back to was cut by the panel's lines
                // (user 2026-10-28).
                this.Speak("focus in a new layer", this.WithPendingPage(text), this.cutInAt < deferred.At);
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

        /// <summary>
        ///     Back to play from a panel or an NPC (the focus gone, only the HUD on top): what the panel was saying, a long
        ///     tooltip, stops with it (user 2026-10-28); what the world says from then on follows. The HUD stays on top
        ///     under an NPC dialogue, so no screen change marks its closing: the focus does. A free-cursor panel (the
        ///     world map) is on top itself and keeps talking.
        /// </summary>
        private void TickBackToPlay()
        {
            var focused = this.inGame && (this.FocusLayer?.Invoke() ?? IntPtr.Zero) != IntPtr.Zero;
            if (!this.inGame || focused || !(this.seenTop == IntPtr.Zero || this.seenTop == this.HudLayer))
            {
                this.panelShown |= focused;
                this.unfocusedSince = DateTime.MaxValue;
                return;
            }

            var now = DateTime.UtcNow;
            if (!this.panelShown)
            {
                return;
            }

            if (this.unfocusedSince == DateTime.MaxValue)
            {
                this.unfocusedSince = now;
            }
            else if ((now - this.unfocusedSince).TotalMilliseconds >= BackToPlayMs)
            {
                this.panelShown = false;
                this.tabbedLayer = IntPtr.Zero;
                this.deferredFocus = null;
                this.pendingPage = null;
                this.hintAfterFocus = string.Empty;
                this.output.StopBefore(this.unfocusedSince);
                this.log.Debug("Back to play: what the panel was saying stopped");
            }
        }

        /// <summary>LB or RB pressed (the game's own pad state): a tab switch that leaves the focus in place is said in Tick.</summary>
        public void OnTabKey()
        {
            if (this.inGame && (this.FocusLayer?.Invoke() ?? IntPtr.Zero) != IntPtr.Zero)
            {
                this.tabKeyAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        ///     LB/RB switched a merchant's or the stash's tab while the focus stayed in the inventory beside it: nothing
        ///     on screen moves the focus, so the new tab was silent (2026-10-28). Read once the page is redrawn, unless a
        ///     focus that moved already said it.
        /// </summary>
        private void TickTabKey()
        {
            if (this.tabKeyAt == DateTime.MaxValue || (DateTime.UtcNow - this.tabKeyAt).TotalMilliseconds < TabSwitchMs)
            {
                return;
            }

            var pressed = this.tabKeyAt;
            this.tabKeyAt = DateTime.MaxValue;
            if (this.lastFocusAt >= pressed || this.deferredFocus != null)
            {
                return;
            }

            // A merchant that opened on the inventory's side never named its tabs' layer: they lie in the focus's own.
            var tab = this.selectedTab(this.tabbedLayer != IntPtr.Zero ? this.tabbedLayer : this.FocusLayer?.Invoke() ?? IntPtr.Zero);
            if (tab.Length > 0 && tab != this.lastContext)
            {
                this.lastContext = tab;
                this.Speak("tab without focus move", Announcements.TabTitle(tab), true);
            }
        }

        /// <summary>
        ///     The cursor on another cell or row of the control it was in: said as a move, and the new place's tooltip is what
        ///     counts as said from now on (a line the last cell's item shared was left out of the next one's late tooltip).
        /// </summary>
        private void MovedWithin(AccessibleUiNode node, string whole)
        {
            this.lastFocusSpeech = whole;
            this.tooltipSaid = this.ReadTooltips || node.IsHovered ? node.Tooltip : Array.Empty<string>();
            this.SayMove(whole);
        }

        /// <summary>Only the new value; a text field echoes just the typed or deleted characters.</summary>
        public void OnValueChanged(AccessibleUiNode node)
        {
            var before = node.Address == this.valueOwner ? this.lastValue : string.Empty;
            if (before.Length == 0 && node.Address == this.valueOwner && node.Role == UiRole.Panel && node.Value.Length > 0 &&
                (DateTime.UtcNow - this.lastFocusAt).TotalMilliseconds < LateValueMs)
            {
                // A sheet's number comes with its widget's tooltip markup, which the game can finish a frame after the
                // tooltip's text (a resistance read "-1%" after its whole description, 2026-11-07): the widget again, whole.
                this.OnFocusChanged(node);
                return;
            }

            var sameOwner = node.Address == this.valueOwner;
            var wasExpanded = sameOwner && this.lastExpanded;
            var textBefore = this.lastText;
            var rowChanged = sameOwner && FocusNews.RowChanged(this.lastRow, node.Position);
            this.lastExpanded = node.IsExpanded;
            this.valueOwner = node.Address;
            this.lastValue = node.Value;
            this.lastText = node.Text;
            this.lastRow = node.Position;
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

            if (this.pendingFocus?.Address == node.Address || (this.unnamedFocus == node.Address && node.Text.Length > 0))
            {
                // Its caption arrived after the focus, which only a control id or nothing named: the whole focus, once.
                this.OnFocusChanged(node);
                return;
            }

            if (rowChanged && node.Role is not (UiRole.GridCell or UiRole.ItemSlot or UiRole.ListItem))
            {
                // Another row of a list focused as one element, with the same text or none ("vacío, 3 de 10"): a move.
                this.MovedWithin(node, this.Focused(node));
                return;
            }

            if (node.IsCollapsible && node.IsExpanded != wasExpanded)
            {
                // A opened or closed the row: its state; the lines it shows follow as the screen reports them.
                this.collapsedAt = node.IsExpanded ? DateTime.MinValue : DateTime.UtcNow;
                this.output.Speak(node.IsExpanded ? Announcements.Expanded : Announcements.Collapsed, true);
                return;
            }

            if (node.Role is UiRole.GridCell or UiRole.ItemSlot or UiRole.ListItem)
            {
                var whole = this.Focused(node);
                this.lastFocusSpeech = whole;
                if (sameOwner && !rowChanged && node.Value == before && FocusNews.ChangedNumbers(node.Role, textBefore, node.Text) is { } number)
                {
                    // The item under a resting cursor changed its own number (a stack used, a gem levelled): that part alone.
                    this.output.Speak(number, true);
                    return;
                }

                // The cursor moved inside the grid: a new item and cell, not a value of the same control.
                this.MovedWithin(node, whole);
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
    }
}
