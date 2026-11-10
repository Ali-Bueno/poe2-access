namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Pure: a table row's cells under the column headings shown above its list (Instance Manager "Players / Time Left /
    ///     Created / Realm", the ladder's "Rank / Name / Class / Level", the Currency Exchange's "Ratio / Stock"), so a bare
    ///     number says what it counts. Columns are told apart by where the headings stand: each runs from halfway to the
    ///     heading on its left to halfway to the one on its right, whatever the text alignment. A subheading under a heading
    ///     ("in this instance") and a title over several headings are no columns.
    /// </summary>
    public static class TableHeadings
    {
        /// <summary>A text drawn at a place, in one frame's units.</summary>
        public readonly record struct Box(string Text, float X, float Y, float Width, float Height)
        {
            public float Centre => this.X + (this.Width / 2f);

            public bool Spans(float x) => x >= this.X && x <= this.X + this.Width;
        }

        /// <summary>Fewer columns than this are a label and its value, not a table.</summary>
        private const int MinColumns = 2;

        /// <summary>A table has one heading per column: at most this many columns more or fewer than the row's cells.</summary>
        private const int CountSlack = 1;

        /// <summary>
        ///     Each cell with its column's heading ("Players: 3"); a cell that is its heading, or that shares a column with
        ///     an earlier cell, stays as it is. Null when the headings do not make a table over these cells (fewer than two
        ///     columns, or fewer than two cells under a heading).
        /// </summary>
        public static List<string>? Pair(IReadOnlyList<Box> headings, IReadOnlyList<Box> cells)
        {
            var columns = Columns(headings);
            if (columns.Count < MinColumns || cells.Count < MinColumns || Math.Abs(columns.Count - cells.Count) > CountSlack ||
                columns.Max(column => column.Y) - columns.Min(column => column.Y) > columns.Max(column => column.Height))
            {
                return null; // not one row of headings over this row's cells
            }

            var used = new HashSet<int>();
            var paired = new List<string>(cells.Count);
            foreach (var cell in cells)
            {
                var column = ColumnOf(columns, cell.Centre);
                if (!used.Add(column) || string.Equals(cell.Text, columns[column].Text, StringComparison.OrdinalIgnoreCase) ||
                    cell.Text.StartsWith(columns[column].Text, StringComparison.OrdinalIgnoreCase))
                {
                    paired.Add(cell.Text);
                    continue;
                }

                paired.Add(Announcements.TableCell(columns[column].Text, cell.Text));
            }

            return used.Count >= MinColumns ? paired : null;
        }

        /// <summary>The headings that are columns, left to right: subheadings and titles over several columns left out.</summary>
        private static List<Box> Columns(IReadOnlyList<Box> headings)
        {
            var titles = headings.Where(title => headings.Count(other => !other.Equals(title) && title.Spans(other.Centre) && other.Width < title.Width) >= MinColumns).ToList();
            var rest = headings.Where(heading => !titles.Contains(heading)).ToList();
            var sub = rest.Where(heading => rest.Any(other => !other.Equals(heading) && other.Y < heading.Y && (other.Spans(heading.Centre) || heading.Spans(other.Centre)))).ToList();
            return rest.Where(heading => !sub.Contains(heading)).OrderBy(heading => heading.Centre).ToList();
        }

        /// <summary>The column a point falls in: its boundaries are halfway between neighbouring headings' centres.</summary>
        private static int ColumnOf(List<Box> columns, float x)
        {
            for (var index = 0; index < columns.Count - 1; index++)
            {
                if (x < (columns[index].Centre + columns[index + 1].Centre) / 2f)
                {
                    return index;
                }
            }

            return columns.Count - 1;
        }
    }
}
