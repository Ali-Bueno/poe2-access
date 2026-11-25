namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Item Inspect Mode as the player hears it (docs/research/ui-layers.md, "Item Inspect Mode"). Fed the items on
    ///     screen when the screen or the focus changes, and the clock every frame: it keeps the comparison table, says the
    ///     opening once the tooltips stop changing, answers the right stick's flicks, and names the item the game's D-pad
    ///     focus crosses into. With the table switched off only the crossings are said. Pure.
    /// </summary>
    public sealed class InspectTableWatch
    {
        /// <summary>The titles stay the same this long before the opening is said: a worn tooltip can arrive a frame late.</summary>
        public const long OpeningSettleMs = 150;

        private IReadOnlyList<InspectItem> items = Array.Empty<InspectItem>();
        private IReadOnlyList<string> labels = Array.Empty<string>();
        private InspectTable? table;
        private string mode = string.Empty;
        private string titles = string.Empty;
        private long titlesSince;
        private bool openingDue;
        private int? crossed;

        /// <summary>Gets a value indicating whether the table is on and items are on screen, so the right stick's flicks are its.</summary>
        public bool Active => this.table != null;

        /// <summary>Gets a value indicating whether the opening is still to be said: what would follow it (the game's legend) waits.</summary>
        public bool OpeningDue => this.openingDue && this.table != null;

        /// <summary>
        ///     Takes the items item Inspect Mode shows (null when it is not open), the game's own name for the mode, and
        ///     whether the table is switched on. Returns what to say now: a cell whose text changed under the cursor, or null.
        /// </summary>
        public string? Update(IReadOnlyList<InspectItem>? items, string modeLabel, bool tableOn, long nowMs)
        {
            if (items == null || items.Count == 0)
            {
                this.Clear();
                return null;
            }

            this.items = items;
            this.mode = modeLabel;
            this.labels = Labels(items);
            var titles = string.Join("\n", items.Select(item => item.Title));
            if (titles != this.titles)
            {
                // Other items: the opening waits for them to settle, and the focus has crossed into nothing yet.
                (this.titles, this.titlesSince, this.openingDue, this.crossed) = (titles, nowMs, true, null);
            }

            if (!tableOn)
            {
                (this.table, this.openingDue) = (null, false);
                return null;
            }

            var rows = InspectRows.Align(items);
            if (this.table == null)
            {
                this.table = new InspectTable(this.labels, rows);
                return null;
            }

            return this.table.Rebuild(this.labels, rows);
        }

        /// <summary>Per frame: the opening, once, when the items' titles have stayed the same for <see cref="OpeningSettleMs"/>.</summary>
        public string? Tick(long nowMs)
        {
            if (!this.openingDue || this.table == null || nowMs - this.titlesSince < OpeningSettleMs)
            {
                return null;
            }

            this.openingDue = false;
            return Announcements.InspectOpened(this.mode, this.items[0].Title, this.items.Skip(1).Select(item => item.Title).ToList());
        }

        /// <summary>What a flick of the right stick says; empty when the table is not on.</summary>
        public string Flick(PadGesture flick) => this.table?.Move(flick) ?? string.Empty;

        /// <summary>
        ///     The label of the item the game's focus is now in, when that is another item than before; null when it is the
        ///     same one, or when the focus is in no item (which forgets the last).
        /// </summary>
        public string? CrossedTo(int? item)
        {
            if (item == this.crossed)
            {
                return null;
            }

            this.crossed = item;
            return item is { } index && index >= 0 && index < this.labels.Count ? this.labels[index] : null;
        }

        /// <summary>Each item's label: the inspected one first, a single worn one as "Worn", several by their slots.</summary>
        private static IReadOnlyList<string> Labels(IReadOnlyList<InspectItem> items)
        {
            var labels = new List<string> { Announcements.InspectThisItem };
            labels.AddRange(items.Skip(1).Select(item => items.Count == 2 ? Announcements.InspectWorn : Announcements.InspectWornIn(item.Slot ?? string.Empty)));
            return labels;
        }

        private void Clear()
        {
            (this.items, this.labels, this.table) = (Array.Empty<InspectItem>(), Array.Empty<string>(), null);
            (this.titles, this.openingDue, this.crossed) = (string.Empty, false, null);
        }
    }
}
