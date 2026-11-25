namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The cursor of Inspect Mode's comparison table: up and down change the row (one stat), left and right the item.
    ///     Each move says one line of one item, the item's label first when the item changed. A move past an edge says the
    ///     same thing again, so silence never means nothing happened. Nothing is compared or computed. Pure.
    /// </summary>
    public sealed class InspectTable
    {
        private IReadOnlyList<string> labels;
        private IReadOnlyList<InspectRow> rows;

        /// <param name="labels">Each item's label, in the rows' cell order: the inspected item's first.</param>
        public InspectTable(IReadOnlyList<string> labels, IReadOnlyList<InspectRow> rows)
        {
            (this.labels, this.rows) = (labels, rows);
        }

        /// <summary>Gets the row the cursor is on; -1 until the first flick, above the first row.</summary>
        public int Row { get; private set; } = -1;

        /// <summary>Gets the item the cursor is on; 0, the inspected item, at the start.</summary>
        public int Item { get; private set; }

        /// <summary>What a flick of the right stick says; empty for any other gesture or an empty table.</summary>
        public string Move(PadGesture flick)
        {
            if (this.rows.Count == 0 || this.labels.Count == 0)
            {
                return string.Empty;
            }

            switch (flick)
            {
                case PadGesture.FlickUp or PadGesture.FlickDown:
                    if (this.Row == -1)
                    {
                        this.Row = 0;
                        return this.Cell();
                    }

                    var row = this.Row + (flick == PadGesture.FlickDown ? 1 : -1);
                    if (row >= 0 && row < this.rows.Count)
                    {
                        this.Row = row;
                    }

                    return this.Cell();
                case PadGesture.FlickLeft or PadGesture.FlickRight:
                    this.Row = Math.Max(this.Row, 0);
                    var item = this.Item + (flick == PadGesture.FlickRight ? 1 : -1);
                    if (item >= 0 && item < this.labels.Count)
                    {
                        this.Item = item;
                    }

                    return Announcements.InspectCell(this.labels[this.Item], this.Cell());
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        ///     Takes the rows again (the tooltip filled in, advanced info was toggled): the cursor stays on the same row, or
        ///     on the row now at its place when that one is gone. Returns the cell when its text changed, else null.
        /// </summary>
        public string? Rebuild(IReadOnlyList<string> labels, IReadOnlyList<InspectRow> rows)
        {
            var before = this.Row >= 0 ? this.Cell() : null;
            var on = this.Row >= 0 ? this.rows[this.Row] : null;
            (this.labels, this.rows) = (labels, rows);
            this.Item = Math.Clamp(this.Item, 0, Math.Max(labels.Count - 1, 0));
            if (this.Row < 0 || rows.Count == 0 || labels.Count == 0)
            {
                this.Row = rows.Count == 0 ? -1 : this.Row;
                return null;
            }

            var same = on == null ? -1 : rows.ToList().FindIndex(row => row.Key == on.Key && row.Occurrence == on.Occurrence);
            this.Row = same >= 0 ? same : Math.Min(this.Row, rows.Count - 1);
            var now = this.Cell();
            return now == before ? null : now;
        }

        /// <summary>The cursor's cell: the item's line, or what the item lacks in the row's own words.</summary>
        private string Cell()
        {
            var row = this.rows[this.Row];
            if (this.Item < row.Cells.Count && row.Cells[this.Item] is { } line)
            {
                return line.Spoken;
            }

            if (row.Key == InspectRows.SocketsMatch)
            {
                return Announcements.InspectNoSockets;
            }

            return Announcements.InspectNone(InspectRows.Words(row.Cells.FirstOrDefault(cell => cell != null)?.Match ?? string.Empty));
        }
    }
}
