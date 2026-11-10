namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Focused cards (<see cref="CardText"/>): their columns' lines in on-screen order.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The game's control id of a trade listing's price block ("Precio de salida:", "1x", "Orbe de aumento").</summary>
        private const string PriceControlName = "cost";

        /// <summary>A listing nests its mods six levels under the card (item column, layout, stat group, line, keyword).</summary>
        private const int CardDepth = 6;

        private const int MaxCardLines = 80;

        /// <summary>A shop entry's texts lie at most this deep (price: entry > buy_button > price_bar > price_label).</summary>
        private const int ShopEntryDepth = 4;

        /// <summary>
        ///     A shop card (UI dump 2026-10-29) — an item, a category ("Pestañas de alijo, 27/27"), an offer: its name, its price in
        ///     points with its other lines ("¡NUEVO!", time left, counts), and where it sits among the cards of the page (rows
        ///     of cells, one card each); null for anything else.
        /// </summary>
        private (string Label, string Value, string Position)? ShopEntryOf(in UiElementReader.ElementInfo focused)
        {
            // An item's card is focused itself; a category card through its frame; an offer card holds its captions.
            var entry = focused;
            if (focused.Name == GameLayout.InGameUi.ShopCategoryFrameName && this.elements.TryReadElement(focused.Raw.ParentPtr, out var category) &&
                category.Name == GameLayout.InGameUi.ShopCategoryName)
            {
                entry = category;
            }
            else if (focused.Name == GameLayout.InGameUi.ShopBannerName)
            {
                // A featured banner: pictures only; A opens what it shows.
                var title = this.elements.ReadChildren(focused).Select(child => this.elements.TryReadElement(child, out var picture) &&
                    picture.Name == GameLayout.InGameUi.ShopBannerPictureName ? CardText.ShopImageTitle(this.elements.ReadAnyTextureKey(picture) ?? string.Empty) : string.Empty)
                    .FirstOrDefault(text => text.Length > 0);
                return title == null ? null : (title, Announcements.ShopFeatured, this.ShopEntryPosition(focused));
            }
            else if (focused.Name != GameLayout.InGameUi.ShopEntryName &&
                     (focused.Name.Length > 0 || !this.elements.ReadChildren(focused).Any(child => this.elements.TryReadElement(child, out var caption) &&
                         caption.Name == GameLayout.InGameUi.ShopEntryNameLabel)))
            {
                return null;
            }

            var texts = new List<(string Name, string Text)>();
            this.CollectNamedTexts(focused, ShopEntryDepth, texts);
            var name = texts.FirstOrDefault(text => text.Name == GameLayout.InGameUi.ShopEntryNameLabel).Text ?? string.Empty;
            var price = texts.FirstOrDefault(text => text.Name == GameLayout.InGameUi.ShopEntryPriceLabel).Text ?? string.Empty;
            var parts = new List<string>();
            if (price.Length > 0)
            {
                parts.Add(Announcements.ShopPrice(price));
            }

            parts.AddRange(texts.Where(text => text.Name != GameLayout.InGameUi.ShopEntryNameLabel && text.Name != GameLayout.InGameUi.ShopEntryPriceLabel)
                .Select(text => text.Text));
            return name.Length > 0 ? (name, string.Join(", ", parts), this.ShopEntryPosition(entry)) : null;
        }

        private void CollectNamedTexts(in UiElementReader.ElementInfo node, int depth, List<(string Name, string Text)> texts)
        {
            if (this.elements.TryReadSpokenText(node.Address, out var text) && text.Length > 0)
            {
                texts.Add((node.Name, text));
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    this.CollectNamedTexts(child, depth - 1, texts);
                }
            }
        }

        /// <summary>"5 of 173": the entry's place counting the cells of every row before its own; empty when the page is laid out otherwise.</summary>
        private string ShopEntryPosition(in UiElementReader.ElementInfo entry)
        {
            var chain = this.elements.ReadAncestors(entry, 3);
            if (chain.Count < 3 || this.elements.ReadChildren(chain[0]).Length != 1)
            {
                return string.Empty;
            }

            var (cell, row, list) = (chain[0], chain[1], chain[2]);
            var index = this.elements.ReadChildren(row).TakeWhile(other => other != cell.Address).Count(this.HoldsShownEntry);
            var (before, total) = (0, 0);
            var reached = false;
            foreach (var rowAddress in this.elements.ReadChildren(list))
            {
                reached |= rowAddress == row.Address;
                var cells = this.elements.TryReadElement(rowAddress, out var rowInfo) ? this.elements.ReadChildren(rowInfo).Count(this.HoldsShownEntry) : 0;
                before += reached ? 0 : cells;
                total += cells;
            }

            return reached && index >= 0 ? Announcements.ListPosition(before + index + 1, total) : string.Empty;
        }

        /// <summary>A grid cell with its card shown (the last row's spare cells hold none).</summary>
        private bool HoldsShownEntry(IntPtr cell) =>
            this.elements.TryReadElement(cell, out var info) && this.elements.ReadChildren(info) is [var entry] &&
            this.elements.TryReadElement(entry, out var card) && card.IsVisibleFlag;

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
