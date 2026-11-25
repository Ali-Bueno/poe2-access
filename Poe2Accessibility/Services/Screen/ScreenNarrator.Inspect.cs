namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Item Inspect Mode for <see cref="ScreenNarrator"/> (docs/research/ui-layers.md, "Item Inspect Mode"). The
    ///     comparison table's watch is fed on every screen or focus change. While the table is on, the readings Inspect Mode
    ///     brought before it give way to one opening: the item cell's advanced lines as R3 opens it wait for the table and
    ///     are said only when none comes, and the first link's reading is left out, its item named on the cursor's first
    ///     move. The game's legend follows. Whether the table is on or not, the D-pad's focus says the item it crosses into.
    /// </summary>
    public sealed partial class ScreenNarrator
    {
        /// <summary>An item cell's tooltip this soon after the right stick's press is the burst of advanced lines R3 brings.</summary>
        private const long StickPressBurstMs = 600;

        /// <summary>A cell whose text changed under the table's cursor, said after the focus's own reading of the same change.</summary>
        private string? inspectCellDue;

        /// <summary>Gets or sets the comparison table's watch; null without one.</summary>
        public InspectTableWatch? Inspect { get; set; }

        /// <summary>Gets or sets what reads the items item Inspect Mode shows, from the focus or the screen; null when none.</summary>
        public Func<AccessibleUiNode?, IReadOnlyList<InspectItem>?>? InspectItems { get; set; }

        /// <summary>Gets or sets the game's own name for the mode, read beside the tooltips.</summary>
        public Func<string>? InspectModeLabel { get; set; }

        /// <summary>Gets or sets whether the table is switched on (its setting, and the mod's controller commands).</summary>
        public Func<bool>? InspectTableOn { get; set; }

        /// <summary>Gets or sets how long ago the right stick was last pressed in, in ms.</summary>
        public Func<long>? SinceStickPress { get; set; }

        /// <summary>
        ///     A change of the screen, every one the core takes from the watcher, whether or not it is spoken: the table follows
        ///     the tooltips (advanced info toggled, Inspect Mode closed) even with appearing texts not announced.
        /// </summary>
        public void ObserveInspect(AccessibleUiNode? focus) => this.UpdateInspect(focus);

        /// <summary>Feeds the watch the items on screen; a cell whose text changed under the table's cursor is due.</summary>
        private void UpdateInspect(AccessibleUiNode? focus)
        {
            if (this.Inspect is not { } watch || this.InspectItems is not { } read)
            {
                return;
            }

            IReadOnlyList<InspectItem>? items;
            try
            {
                items = read(focus);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or IndexOutOfRangeException or NullReferenceException)
            {
                // A layer read halfway through its rebuild: today's reading, not a lost focus or frame.
                this.log.Debug($"Inspect Mode not read: {error.Message}");
                items = null;
            }

            var said = watch.Update(items, this.InspectModeLabel?.Invoke() ?? string.Empty, this.InspectTableOn?.Invoke() == true, Environment.TickCount64);
            this.inspectCellDue = said ?? this.inspectCellDue;
        }

        /// <summary>A new focus: the table follows the tooltips it sits in.</summary>
        private void FocusForInspect(AccessibleUiNode node) => this.UpdateInspect(node);

        /// <summary>
        ///     Per frame: R3's burst when no table came, the opening once the tooltips settle, and a cell that changed under
        ///     the cursor, after what the focus said of the same change.
        /// </summary>
        private void TickInspect()
        {
            if (this.Inspect is not { } watch)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (watch.HeldBurst(now) is { } burst)
            {
                this.Speak("tooltip of focus", burst, false);
            }

            if (watch.Tick(now) is { } opening)
            {
                // The opening alone: where the game's cursor landed (a flask's whole help paragraph) is said on its first move
                // (user, 2026-11-25).
                this.Speak("inspect opening", opening, true);
                this.inspectCellDue = null;
            }

            if (this.inspectCellDue is { } cell && !watch.OpeningDue)
            {
                this.inspectCellDue = null;
                this.Speak("inspect table", cell, false, repeat: true); // a toggle twice in a row says the same row twice
            }
        }

        /// <summary>
        ///     A focused item's tooltip right after R3 with the table on: the burst of its advanced lines waits for the table,
        ///     whose opening speaks for the item; said after all when no table comes.
        /// </summary>
        private bool HoldInspectBurst(AccessibleUiNode node, IReadOnlyList<string> fresh)
        {
            if (this.Inspect is not { } watch || this.InspectTableOn?.Invoke() != true || node.Role is not (UiRole.GridCell or UiRole.ItemSlot) ||
                (this.SinceStickPress?.Invoke() ?? long.MaxValue) >= StickPressBurstMs)
            {
                return false;
            }

            watch.HoldBurst(Announcements.Sentences(fresh), Environment.TickCount64);
            return true;
        }

        /// <summary>
        ///     A reading of the inspect layer's first focus, which the table's opening leaves out. Its card (the focus's group)
        ///     is then forgotten, so the cursor's next move names it.
        /// </summary>
        private bool QuietForInspect(string reason) => this.Forgets(this.Inspect?.Quiets(reason, Environment.TickCount64) == true);

        /// <summary>The game's cursor put on a rebuilt card by an advanced info toggle: the table says the row instead.</summary>
        private bool QuietForInspectToggle(AccessibleUiNode node) =>
            node.InInspectCard && this.Forgets(this.Inspect?.QuietsToggledFocus(Environment.TickCount64) == true);

        /// <summary>
        ///     When a focus reading is left out, its card and its line are not the ones said: the next move names the card and
        ///     says its line, even on the same line ("Thorns, link" alone after a toggle, 2026-11-25).
        /// </summary>
        private bool Forgets(bool quiet)
        {
            (this.lastGroup, this.lastLinkRow) = quiet ? (string.Empty, string.Empty) : (this.lastGroup, this.lastLinkRow);
            return quiet;
        }
    }
}
