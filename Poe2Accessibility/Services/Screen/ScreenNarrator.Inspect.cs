namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Item Inspect Mode for <see cref="ScreenNarrator"/> (docs/research/ui-layers.md, "Item Inspect Mode"). The
    ///     comparison table's watch is fed on every screen or focus change. While the table is on, the readings Inspect Mode
    ///     brought before it (the item cell's advanced lines as R3 opens it, the whole worn tooltip with the first link) give
    ///     way to one opening and where the focus is; the game's legend follows. Whether the table is on or not, the D-pad's
    ///     focus says the item it crosses into.
    /// </summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>An item cell's tooltip this soon after the right stick's press is the burst of advanced lines R3 brings.</summary>
        private const long StickPressBurstMs = 600;

        /// <summary>The readings of the inspect layer's opening and first focus, which the table's opening replaces.</summary>
        private static readonly HashSet<string> InspectOpeningReasons = new(StringComparer.Ordinal)
        {
            "focus in a new layer", "opened with focus", "appeared with focus", "appeared in focus layer",
        };

        /// <summary>The focus last seen, and the item label its moving into an item's tooltip gave (empty for none).</summary>
        private (IntPtr Address, string Label) inspectFocus;

        /// <summary>Gets or sets the comparison table's watch; null without one.</summary>
        public InspectTableWatch? Inspect { get; set; }

        /// <summary>Gets or sets what reads the items item Inspect Mode shows, from the focus or the screen; null when none.</summary>
        public Func<AccessibleUiNode?, IReadOnlyList<InspectItem>?>? InspectItems { get; set; }

        /// <summary>Gets or sets what tells which item's tooltip a focused element sits in.</summary>
        public Func<AccessibleUiNode, int?>? InspectItemOf { get; set; }

        /// <summary>Gets or sets the game's own name for the mode, read beside the tooltips.</summary>
        public Func<string>? InspectModeLabel { get; set; }

        /// <summary>Gets or sets whether the table is switched on (the setting).</summary>
        public Func<bool>? InspectTableOn { get; set; }

        /// <summary>Gets or sets how long ago the right stick was last pressed in, in ms.</summary>
        public Func<long>? SinceStickPress { get; set; }

        /// <summary>Feeds the watch the items on screen; a cell whose text changed under the table's cursor is said.</summary>
        private void UpdateInspect(AccessibleUiNode? focus)
        {
            if (this.Inspect is not { } watch || this.InspectItems is not { } read)
            {
                return;
            }

            var said = watch.Update(read(focus), this.InspectModeLabel?.Invoke() ?? string.Empty, this.InspectTableOn?.Invoke() == true, Environment.TickCount64);
            if (said != null)
            {
                this.Speak("inspect table", said, true);
            }
        }

        /// <summary>A new focus: the label of the item its tooltip is, when the focus crossed into another item's.</summary>
        private void FocusForInspect(AccessibleUiNode node)
        {
            this.UpdateInspect(node);
            var label = this.Inspect?.CrossedTo(this.InspectItemOf?.Invoke(node));
            this.inspectFocus = (node.Address, label ?? string.Empty);
        }

        /// <summary>Per frame: the opening once the tooltips settle, with where the focus is when it sits in one of them.</summary>
        private void TickInspect()
        {
            if (this.Inspect?.Tick(Environment.TickCount64) is not { } opening)
            {
                return;
            }

            var focus = this.inspectFocus.Address != IntPtr.Zero && this.lastFocusSpeech.Length > 0 ? this.lastFocusSpeech : string.Empty;
            this.Speak("inspect opening", Announcements.Sentences(new[] { opening, focus }.Where(part => part.Length > 0)), true);
        }

        /// <summary>A focused item's tooltip right after R3, with the table on: the burst its advanced lines were, not said.</summary>
        private bool InspectBurst(AccessibleUiNode node) =>
            this.InspectTableOn?.Invoke() == true && node.Role is UiRole.GridCell or UiRole.ItemSlot &&
            (this.SinceStickPress?.Invoke() ?? long.MaxValue) < StickPressBurstMs;

        /// <summary>A reading of the inspect layer's opening while the table speaks for it.</summary>
        private bool QuietForInspect(string reason) => this.Inspect?.Active == true && InspectOpeningReasons.Contains(reason);

        /// <summary>The focus's own words, after its item's label when it crossed into that item's tooltip.</summary>
        private string WithInspectLabel(AccessibleUiNode node, string speech) =>
            node.Address == this.inspectFocus.Address && this.inspectFocus.Label.Length > 0 ? Announcements.InspectCell(this.inspectFocus.Label, speech) : speech;
    }
}
