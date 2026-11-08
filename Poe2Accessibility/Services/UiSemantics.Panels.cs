namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Widgets of the panels not reached in play yet (docs/research/ui-unseen-panels.md): a control that shows only an
    ///     item's picture (the Currency Exchange's "I want" / "I have" pickers once a currency is chosen, a bench's input).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The picture of an icon-only control sits on it or one or two levels below (frame → icon).</summary>
        private const int ArtSearchDepth = 2;

        /// <summary>At most this many pictures are looked at under one control.</summary>
        private const int MaxArtKeys = 8;

        /// <summary>A row's list and the headings block before it sit at most this many wrappers above the row.</summary>
        private const int HeadingSearchLevels = 2;

        /// <summary>Column headings sit this deep in their block at most (block → column → heading, subheading).</summary>
        private const int HeadingTextDepth = 3;

        /// <summary>A block with more texts than this is a page of labels, not a heading row.</summary>
        private const int MaxHeadingTexts = 16;

        /// <summary>A heading is a word or two; a longer text is a sentence (a page's explanation).</summary>
        private const int MaxHeadingChars = 40;

        /// <summary>
        ///     A table row's cells with the column headings its list shows above it ("Players: 3, Time Left: 9 minutes"); null
        ///     when no shown heading row stands before the list. The block must be plain labels the cursor never visits (the
        ///     friends list's "Show Offline" / "PC only" check boxes are controls) and must not be hidden (the character panel's
        ///     stat lists keep an English "Name | Value" header row hidden, dump 2026-11-08).
        /// </summary>
        private List<string>? HeadedCells(in UiElementReader.ElementInfo row, IReadOnlyList<IntPtr> cellAddresses, IReadOnlyList<string> cells)
        {
            if (cellAddresses.Count != cells.Count)
            {
                return null;
            }

            var item = row;
            for (var level = 0; level < HeadingSearchLevels; level++)
            {
                if (!this.elements.TryReadElement(item.Raw.ParentPtr, out var list) || !this.elements.TryReadElement(list.Raw.ParentPtr, out var frame))
                {
                    return null;
                }

                if (this.SameClassChildren(list, item) >= 2)
                {
                    if (this.HeadingTexts(frame, list, item) is not { } headings)
                    {
                        return null;
                    }

                    var headingBoxes = this.Boxes(headings, frame.Address);
                    var cellBoxes = this.Boxes(cellAddresses.Zip(cells), frame.Address);
                    return cellBoxes.Count == cells.Count ? TableHeadings.Pair(headingBoxes, cellBoxes) : null;
                }

                item = list;
            }

            return null;
        }

        private int SameClassChildren(in UiElementReader.ElementInfo list, in UiElementReader.ElementInfo item)
        {
            var count = 0;
            foreach (var childAddress in this.elements.ReadChildren(list))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && child.Raw.Vtable == item.Raw.Vtable)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The texts of the nearest shown block of plain labels before <paramref name="list"/> in <paramref name="frame"/>.</summary>
        private List<(IntPtr Address, string Text)>? HeadingTexts(in UiElementReader.ElementInfo frame, in UiElementReader.ElementInfo list, in UiElementReader.ElementInfo item)
        {
            var siblings = this.elements.ReadChildren(frame);
            var index = Array.IndexOf(siblings, list.Address);
            for (var before = index - 1; before >= 0; before--)
            {
                if (!this.elements.TryReadElement(siblings[before], out var block) || !this.elements.IsVisible(block) ||
                    block.Raw.Vtable == list.Raw.Vtable || block.Raw.Vtable == item.Raw.Vtable)
                {
                    continue;
                }

                var texts = new List<(IntPtr Address, string Text)>();
                this.CollectHeadingTexts(block, HeadingTextDepth, texts);
                if (texts.Count == 0)
                {
                    continue; // a scroll bar's buttons, a divider
                }

                var plain = texts.Count >= 2 && texts.Count <= MaxHeadingTexts && texts.All(text => text.Text.Length <= MaxHeadingChars && text.Text.Any(char.IsLetter));
                return plain && !this.HoldsNavigable(block) && !this.HoldsControl(block, HeadingTextDepth) ? texts : null;
            }

            return null;
        }

        private void CollectHeadingTexts(in UiElementReader.ElementInfo node, int depth, List<(IntPtr Address, string Text)> texts)
        {
            if (texts.Count > MaxHeadingTexts)
            {
                return;
            }

            if (this.elements.TryReadSpokenText(node.Address, out var text) && text.Length > 0)
            {
                texts.Add((node.Address, text));
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    this.CollectHeadingTexts(child, depth - 1, texts);
                }
            }
        }

        private List<TableHeadings.Box> Boxes(IEnumerable<(IntPtr Address, string Text)> texts, IntPtr frame)
        {
            var boxes = new List<TableHeadings.Box>();
            foreach (var (address, text) in texts)
            {
                if (this.LayoutRect(address, frame) is { } rect)
                {
                    boxes.Add(new TableHeadings.Box(text, rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y));
                }
            }

            return boxes;
        }

        /// <summary>The item an icon-only control shows, named by the game's tables; empty when it shows none they name.</summary>
        private string ArtCaption(in UiElementReader.ElementInfo info)
        {
            var keys = new List<string>();
            this.CollectArt(info, ArtSearchDepth, keys);
            return ArtCaptions.Pick(keys);
        }

        private void CollectArt(in UiElementReader.ElementInfo node, int depth, List<string> keys)
        {
            if (keys.Count >= MaxArtKeys)
            {
                return;
            }

            if (this.elements.ReadTextureKey(node) is { } key)
            {
                keys.Add(key);
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var child in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(child, out var info) && info.IsVisibleFlag)
                {
                    this.CollectArt(info, depth - 1, keys);
                }
            }
        }
    }
}
