namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>Focused cards (<see cref="CardText"/>): their columns' lines in on-screen order.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The game's control id of a trade listing's price block ("Precio de salida:", "1x", "Orbe de aumento").</summary>
        private const string PriceControlName = "cost";

        /// <summary>A listing nests its mods six levels under the card (item column, layout, stat group, line, keyword).</summary>
        private const int CardDepth = 6;

        private const int MaxCardLines = 80;

        private (string Label, List<string> Details)? CardOf(in UiElementReader.ElementInfo card)
        {
            var columns = new List<IReadOnlyList<CardLine>>();
            var budget = MaxCardLines;
            foreach (var childAddress in this.elements.ReadChildren(card))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    var lines = new List<CardLine>();
                    this.CollectCardLines(child, CardDepth, child.Name == PriceControlName, lines, ref budget);
                    if (lines.Count > 0)
                    {
                        columns.Add(lines);
                    }
                }
            }

            return CardText.Read(columns, MaxRowCells);
        }

        private void CollectCardLines(in UiElementReader.ElementInfo node, int depth, bool price, List<CardLine> lines, ref int budget)
        {
            if (this.elements.TryReadSpokenText(node.Address, out var text) && text.Length > 0 && budget-- > 0)
            {
                lines.Add(new CardLine(text, price));
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    this.CollectCardLines(child, depth - 1, price || child.Name == PriceControlName, lines, ref budget);
                }
            }
        }
    }
}
