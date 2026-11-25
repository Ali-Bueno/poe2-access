namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Item Inspect Mode as the player hears it (docs/research/ui-layers.md, "Item Inspect Mode"). Fed the items on
    ///     screen when the screen or the focus changes, and the clock every frame: it keeps the comparison table, says the
    ///     opening once the tooltips stop changing, and answers the right stick's flicks. The D-pad's item names are the
    ///     Compare view's (`UiSemantics.InspectCards`). With the table switched off nothing is said here. Pure.
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

        /// <summary>Each item's class line, as said, at the last update: advanced info adds or takes away its item level.</summary>
        private string classes = string.Empty;
        private long titlesSince;
        private bool openingDue;

        /// <summary>Gets a value indicating whether the table is on and items are on screen, so the right stick's flicks are its.</summary>
        public bool Active => this.table != null;

        /// <summary>How long after advanced info was toggled what appears on screen is still the toggle's: the screen can show it a poll later.</summary>
        public const long ToggleWindowMs = 500;

        /// <summary>When advanced info was last toggled under the table; null since it opened.</summary>
        private long? toggledAt;

        /// <summary>
        ///     Whether advanced info was toggled under the table a moment ago (the same items, each class line changed: "Bow:
        ///     Item Level 5" and "Bow"): what appeared on screen is the table's to say, which says the row under its cursor. A
        ///     line changing on its own (a flask's charges) is no toggle.
        /// </summary>
        public bool AdvancedInfoToggled(long nowMs) => this.table != null && this.toggledAt is { } at && nowMs - at < ToggleWindowMs;

        /// <summary>
        ///     Whether the game's cursor just moved because advanced info was toggled (it rebuilds the tooltips and puts the
        ///     cursor on a new link): left out, as the opening leaves out where it landed.
        /// </summary>
        public bool QuietsToggledFocus(long nowMs) => this.AdvancedInfoToggled(nowMs);

        /// <summary>Gets a value indicating whether the opening is still to be said: what would follow it (the game's legend) waits.</summary>
        public bool OpeningDue => this.openingDue && this.table != null;

        /// <summary>How long R3's burst of the item's advanced lines waits for the table before it is said after all.</summary>
        public const long BurstWaitMs = 400;

        /// <summary>The readings of the inspect layer's first focus, which the opening replaces while it is due.</summary>
        private static readonly HashSet<string> OpeningReasons = new(StringComparer.Ordinal) { "focus in a new layer", "opened with focus" };

        private (string Text, long At)? burst;

        /// <summary>Whether a flick of the right stick is the table's: it is on, and Inspect Mode still shows (a close not yet noticed is gameplay again).</summary>
        public bool TakesFlicks(bool shown) => this.Active && shown;

        /// <summary>
        ///     How long after the opening the inspect layer's first focus may still be read: the narrator waits for a new
        ///     layer to settle, and its reading came 170 ms after the opening (2026-11-25).
        /// </summary>
        public const long LateFocusMs = 700;

        /// <summary>When the opening was said; null before it.</summary>
        private long? openedAt;

        /// <summary>
        ///     Whether a reading is the inspect layer's first focus, which the opening leaves out: before the opening, or just
        ///     after it.
        /// </summary>
        public bool Quiets(string reason, long nowMs) =>
            OpeningReasons.Contains(reason) && (this.OpeningDue || this.table != null && this.openedAt is { } at && nowMs - at < LateFocusMs);

        /// <summary>Keeps R3's burst of the item's advanced lines until the table comes, or does not.</summary>
        public void HoldBurst(string text, long nowMs) => this.burst = (text, nowMs);

        /// <summary>The burst held, once, when no table came within <see cref="BurstWaitMs"/>; dropped when one came (its opening speaks for the item).</summary>
        public string? HeldBurst(long nowMs)
        {
            if (this.burst is not { } held)
            {
                return null;
            }

            if (this.Active)
            {
                this.burst = null;
                return null;
            }

            if (nowMs - held.At < BurstWaitMs)
            {
                return null;
            }

            this.burst = null;
            return held.Text;
        }

        /// <summary>
        ///     Takes the items item Inspect Mode shows (null when it is not open), the game's own name for the mode, and
        ///     whether the table is switched on. Returns what to say now: the cursor's cell when it changed or advanced info was
        ///     toggled, or null.
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
            var sameItems = titles == this.titles;
            if (!sameItems)
            {
                // Other items: the opening waits for them to settle.
                (this.titles, this.titlesSince, this.openingDue) = (titles, nowMs, true);
            }

            if (!tableOn)
            {
                (this.table, this.openingDue) = (null, false);
                return null;
            }

            var classes = string.Join("\n", items.Select(item => item.Lines.FirstOrDefault(line => line.Key == InspectRows.ClassKey)?.Spoken ?? string.Empty));
            var toggled = this.table != null && sameItems && classes != this.classes;
            this.toggledAt = toggled ? nowMs : this.toggledAt;
            this.classes = classes;
            var rows = InspectRows.Align(items);
            if (this.table == null)
            {
                this.table = new InspectTable(this.labels, rows);
                return null;
            }

            // Advanced info toggled: the row under the cursor is said even when it reads the same, so the toggle is heard.
            return this.table.Rebuild(this.labels, rows) ?? (toggled ? this.table.Again() : null);
        }

        /// <summary>
        ///     Per frame: the opening, once, when the items' titles have stayed the same for <see cref="OpeningSettleMs"/>. It
        ///     does not say where the game's cursor is, so the cursor's next move names the item it is in.
        /// </summary>
        public string? Tick(long nowMs)
        {
            if (!this.openingDue || this.table == null || nowMs - this.titlesSince < OpeningSettleMs)
            {
                return null;
            }

            (this.openingDue, this.openedAt) = (false, nowMs);
            return Announcements.InspectOpened(this.mode, this.items[0].Title, this.items.Skip(1).Select(item => item.Title).ToList());
        }

        /// <summary>What a flick of the right stick says; empty when the table is not on.</summary>
        public string Flick(PadGesture flick) => this.table?.Move(flick) ?? string.Empty;

        /// <summary>Each item's label: the inspected one first, a single worn one as "Worn", several by their slots where known.</summary>
        private static IReadOnlyList<string> Labels(IReadOnlyList<InspectItem> items)
        {
            var labels = new List<string> { Announcements.InspectThisItem };
            labels.AddRange(items.Skip(1).Select(item => items.Count > 2 && item.Slot is { Length: > 0 } slot ? Announcements.InspectWornIn(slot) : Announcements.InspectWorn));
            return labels;
        }

        private void Clear()
        {
            (this.items, this.labels, this.table) = (Array.Empty<InspectItem>(), Array.Empty<string>(), null);
            (this.titles, this.classes, this.toggledAt, this.openingDue, this.openedAt) = (string.Empty, string.Empty, null, false, null);
        }
    }
}
