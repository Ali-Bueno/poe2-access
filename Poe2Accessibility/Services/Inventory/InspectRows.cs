namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>One line of one item in Inspect Mode's table: what is said, and the game's main text it is matched by.</summary>
    /// <param name="Spoken">The line as said: the game's text, with a modifier's tier and group label ("+19(10-19) to maximum Life, T13, Prefix").</param>
    /// <param name="Match">The game's main text alone ("+19(10-19) to maximum Life"), which <see cref="InspectRows.Key"/> reads.</param>
    public sealed record InspectLine(string Spoken, string Match);

    /// <summary>An item Inspect Mode shows: its title, its lines (the first is the title row), and for a worn item its slot.</summary>
    /// <param name="Slot">The worn slot's spoken name, which tells two worn items apart; null for the inspected item.</param>
    public sealed record InspectItem(string Title, IReadOnlyList<InspectLine> Lines, bool Worn, string? Slot);

    /// <summary>One row of the table: what its lines are matched by, the how-manyth line with that key it is in an item, and each item's line.</summary>
    /// <param name="Cells">One for each item, in the items' order; null where the item has no such line.</param>
    public sealed record InspectRow(string Key, int Occurrence, IReadOnlyList<InspectLine?> Cells);

    /// <summary>
    ///     Lines up the tooltip lines of the items Inspect Mode shows, so that a row means the same thing on each item
    ///     (docs/research/ui-layers.md, "Item Inspect Mode"). Both tooltips are in the game's
    ///     language, so lines are matched by their text: a property line by its label ("Energy Shield:"), any other line
    ///     with its numbers taken out ("+# to maximum Life"). Nothing is computed from the numbers. Pure.
    /// </summary>
    public static class InspectRows
    {
        /// <summary>The key of the first row, every item's title.</summary>
        public const string TitleKey = "title";

        /// <summary>The match text of an item's sockets line, which no tooltip line has (the sockets are art in the game).</summary>
        public const string SocketsMatch = "\u0001sockets";

        private static readonly Regex Number = new(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        /// <summary>A minus sign before a number: a negative roll of a stat is the same stat ("-10% to Cold Resistance").</summary>
        private static readonly Regex MinusSign = new(@"(?<![\w)])-(?=\d)", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

        /// <summary>What a line is matched by: a property line's label with its colon, else the text with every number a '#'.</summary>
        public static string Key(string match)
        {
            var text = match.Replace('：', ':');
            var colon = text.IndexOf(':');
            if (colon > 0)
            {
                return text[..(colon + 1)].Trim();
            }

            text = Number.Replace(MinusSign.Replace(ItemModCheck.RolledRange.Replace(text, string.Empty), "+"), "#");
            return Spaces.Replace(text, " ").Trim();
        }

        /// <summary>Whether a key is a property line's label ("Requires:").</summary>
        public static bool IsLabel(string key) => key.EndsWith(':');

        /// <summary>A line's words without numbers, for the item that lacks it: "to maximum Life", "Requires".</summary>
        public static string Words(string match)
        {
            var key = Key(match);
            if (IsLabel(key))
            {
                return key[..^1].Trim();
            }

            var words = key.Replace("#", string.Empty).Replace("+", string.Empty).Replace("%", string.Empty);
            return Spaces.Replace(words, " ").Trim(' ', ',', '.');
        }

        /// <summary>
        ///     The items' lines as rows: the titles first, then each item folded into the rows in turn. The longest run of
        ///     lines two lists have in common, in order, shares rows; between those, the lines of only one side keep their
        ///     own order, property lines first, so that a requirement one item has stays near the top.
        /// </summary>
        public static IReadOnlyList<InspectRow> Align(IReadOnlyList<InspectItem> items)
        {
            var count = items.Count;
            var title = new InspectLine?[count];
            for (var item = 0; item < count; item++)
            {
                title[item] = items[item].Lines.Count > 0 ? items[item].Lines[0] : null;
            }

            var body = new List<Row>();
            for (var item = 0; item < count; item++)
            {
                var lines = Numbered(items[item].Lines.Skip(1)).Select(line => new Row(line.Key, line.Occurrence, count, item, line.Line)).ToList();
                if (item == 0)
                {
                    body = lines;
                    continue;
                }

                var merged = new List<Row>();
                var (row, line) = (0, 0);
                foreach (var (matchedRow, matchedLine) in CommonRun(body, lines).Append((body.Count, lines.Count)))
                {
                    merged.AddRange(Merge(body.GetRange(row, matchedRow - row), lines.GetRange(line, matchedLine - line)));
                    if (matchedRow < body.Count)
                    {
                        body[matchedRow].Cells[item] = lines[matchedLine].Cells[item];
                        merged.Add(body[matchedRow]);
                    }

                    (row, line) = (matchedRow + 1, matchedLine + 1);
                }

                body = merged;
            }

            var rows = new List<InspectRow> { new(TitleKey, 1, title) };
            rows.AddRange(body.Select(entry => new InspectRow(entry.Key, entry.Occurrence, entry.Cells)));
            return rows;
        }

        /// <summary>Each line with its key and the how-manyth line with that key it is, from 1.</summary>
        private static IEnumerable<(string Key, int Occurrence, InspectLine Line)> Numbered(IEnumerable<InspectLine> lines)
        {
            var seen = new Dictionary<string, int>();
            foreach (var line in lines)
            {
                var key = Key(line.Match);
                seen[key] = seen.GetValueOrDefault(key) + 1;
                yield return (key, seen[key], line);
            }
        }

        /// <summary>The longest run of rows and lines with the same key and occurrence, in order, as (row, line) index pairs.</summary>
        private static List<(int Row, int Line)> CommonRun(List<Row> rows, List<Row> lines)
        {
            var longest = new int[rows.Count + 1, lines.Count + 1];
            for (var row = rows.Count - 1; row >= 0; row--)
            {
                for (var line = lines.Count - 1; line >= 0; line--)
                {
                    longest[row, line] = rows[row].SameAs(lines[line])
                        ? longest[row + 1, line + 1] + 1
                        : System.Math.Max(longest[row + 1, line], longest[row, line + 1]);
                }
            }

            var pairs = new List<(int Row, int Line)>();
            var (r, l) = (0, 0);
            while (r < rows.Count && l < lines.Count)
            {
                if (rows[r].SameAs(lines[l]))
                {
                    pairs.Add((r, l));
                    (r, l) = (r + 1, l + 1);
                }
                else if (longest[r + 1, l] >= longest[r, l + 1])
                {
                    r++;
                }
                else
                {
                    l++;
                }
            }

            return pairs;
        }

        /// <summary>The rows only the earlier items have and the lines only this item has, each in its own order, property lines first.</summary>
        private static IEnumerable<Row> Merge(List<Row> earlier, List<Row> added)
        {
            var (a, b) = (0, 0);
            while (a < earlier.Count && b < added.Count)
            {
                if (IsLabel(earlier[a].Key) || !IsLabel(added[b].Key))
                {
                    yield return earlier[a++];
                }
                else
                {
                    yield return added[b++];
                }
            }

            while (a < earlier.Count)
            {
                yield return earlier[a++];
            }

            while (b < added.Count)
            {
                yield return added[b++];
            }
        }

        /// <summary>A row while it is built: its cells fill in as items are folded in.</summary>
        private sealed class Row
        {
            public Row(string key, int occurrence, int items, int item, InspectLine line)
            {
                (this.Key, this.Occurrence, this.Cells) = (key, occurrence, new InspectLine?[items]);
                this.Cells[item] = line;
            }

            public string Key { get; }

            public int Occurrence { get; }

            public InspectLine?[] Cells { get; }

            public bool SameAs(Row other) => this.Key == other.Key && this.Occurrence == other.Occurrence;
        }
    }
}
