namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One line of a card's column, and whether it belongs to the column's price block.</summary>
    public readonly record struct CardLine(string Text, bool Price);

    /// <summary>
    ///     A card: one focused entry whose columns hold more lines than a table row (a trade market listing: the seller's
    ///     column beside the item's). Said as a sighted player scans it: the item, its price, the other columns (seller,
    ///     online or not); the rest of the item column (properties, mods) follows as details. Pure.
    /// </summary>
    public static class CardText
    {
        /// <returns>Null when the lines fit a table row (<paramref name="rowCells"/>) or there is a single column.</returns>
        public static (string Label, List<string> Details)? Read(IReadOnlyList<IReadOnlyList<CardLine>> columns, int rowCells)
        {
            if (columns.Count < 2 || columns.Sum(column => column.Count) <= rowCells)
            {
                return null;
            }

            // The priced column is the item; without a price, the column with the most lines is.
            var main = columns.FirstOrDefault(column => column.Any(line => line.Price)) ?? columns.OrderByDescending(column => column.Count).First();
            var label = new List<string> { main[0].Text };
            var price = main.Where(line => line.Price).Select(line => line.Text).ToList();
            if (price.Count > 0)
            {
                label.Add(Announcements.Cells(price));
            }

            label.AddRange(columns.Where(column => !ReferenceEquals(column, main)).Select(column => Announcements.Cells(column.Select(line => line.Text))));
            return (string.Join(", ", label), main.Skip(1).Where(line => !line.Price).Select(line => line.Text).ToList());
        }
    }
}
