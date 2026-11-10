namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Row reading of <see cref="UiSemantics"/>: a focused row's cells said in drawing order.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>An interactive control with this many texts under it is a row of cells, not a button and its caption.</summary>
        private const int MinButtonCells = 3;

        private const int RowCellDepth = 2;

        /// <summary>The caption of a control, or every cell of a focused table row ("Maximum Life, 42").</summary>
        private string RowText(in UiElementReader.ElementInfo info) => this.RowText(info, out _, out _);

        /// <summary>
        ///     Same, with the length of its longest cell (a row of short cells is no paragraph, however long together) and
        ///     whether it is an interactive row of several cells.
        /// </summary>
        private string RowText(in UiElementReader.ElementInfo info, out int longestCell, out bool cellRow)
        {
            cellRow = false;
            if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                var caption = this.FindDescendantText(info, CaptionSearchDepth, null);
                if (this.InGame && caption.Length > 0)
                {
                    // A choice drawn as a row of cells: the Instance Manager's "Enter the instance | Sao Paulo (BR) | Created:
                    // 12:28:45 | 18 minutes 34 seconds" said only its tooltip's "No players" (2026-11-08); all its texts, in screen order.
                    var rowCells = new RowWalk();
                    this.CollectTexts(info, 0, RowCellDepth, rowCells);
                    if (rowCells.Texts.Count is >= MinButtonCells and <= MaxRowCells)
                    {
                        cellRow = true;
                        longestCell = rowCells.Texts.Max(text => text.Text.Length);
                        return this.ScreenOrderText(info);
                    }
                }

                longestCell = caption.Length;
                return caption;
            }

            var walk = new RowWalk();
            this.CollectTexts(info, 0, RowCellDepth, walk);
            var sets = walk.Icons.Count > 0 ? WeaponSetRows.Pair(walk.Parents, walk.Indices, walk.Icons, walk.Texts.Select(text => text.Node)) : null;
            var cells = walk.Texts.Select(text => sets != null && sets.TryGetValue(text.Node, out var set) ? Announcements.WeaponSetValue(set, text.Text) : text.Text).ToList();
            if (this.InGame && cells.Count is > 1 and <= MaxHeadedRowCells && this.HeadedCells(info, walk.TextAddresses, cells) is { } headed)
            {
                longestCell = headed.Max(cell => cell.Length);
                return Announcements.Cells(headed); // a table row under shown column headings ("Players: 3, Time Left: 9 minutes")
            }

            if (cells.Count is > 0 and <= MaxRowCells)
            {
                longestCell = cells.Max(cell => cell.Length);
                return Announcements.Cells(cells);
            }

            var text = this.FindDescendantText(info, CaptionSearchDepth, null);
            longestCell = text.Length;
            return text;
        }

        private void CollectTexts(in UiElementReader.ElementInfo node, int nodeId, int depth, RowWalk walk)
        {
            if (depth < 0 || walk.Texts.Count > MaxHeadedRowCells)
            {
                return;
            }

            var children = this.elements.ReadChildren(node);
            for (var index = 0; index < children.Length; index++)
            {
                if (!this.elements.TryReadElement(children[index], out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                var id = walk.Add(nodeId, index);
                if (this.elements.TryReadSpokenText(children[index], out var text) && text.Length > 0)
                {
                    walk.Texts.Add((id, this.WithUnitPicture(child, text)));
                    walk.TextAddresses.Add(children[index]);
                }
                else if (this.elements.ReadTextureKey(child) is { } key && WeaponSetRows.FromTextureKey(key) is { } set)
                {
                    walk.Icons.Add((id, set));
                }

                this.CollectTexts(child, id, depth - 1, walk);
            }
        }

        /// <summary>A number with a unit picture as its child ("30" and the spirit icon) said with the unit (<see cref="InlineIcons.WithPicture"/>).</summary>
        private string WithUnitPicture(in UiElementReader.ElementInfo holder, string text)
        {
            var pictures = this.elements.ReadChildren(holder);
            if (pictures.Length is 0 or > 2)
            {
                return text;
            }

            foreach (var address in pictures)
            {
                if (this.elements.TryReadElement(address, out var picture) && picture.IsVisibleFlag && this.elements.ReadTextureKey(picture) is { } art &&
                    InlineIcons.WithPicture(text, art) is var said && said != text)
                {
                    return said;
                }
            }

            return text;
        }

        /// <summary>A row's texts in drawing order and the weapon-set icons among them, as a tree for <see cref="WeaponSetRows" />.</summary>
        private sealed class RowWalk
        {
            public List<int> Parents { get; } = new() { -1 };

            public List<int> Indices { get; } = new() { 0 };

            public List<(int Node, int Set)> Icons { get; } = new();

            public List<(int Node, string Text)> Texts { get; } = new();

            /// <summary>The element of each of <see cref="Texts"/>, in the same order (where it is drawn, for column headings).</summary>
            public List<IntPtr> TextAddresses { get; } = new();

            public int Add(int parent, int index)
            {
                this.Parents.Add(parent);
                this.Indices.Add(index);
                return this.Parents.Count - 1;
            }
        }
    }
}
