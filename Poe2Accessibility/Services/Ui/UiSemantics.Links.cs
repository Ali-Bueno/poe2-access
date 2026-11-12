namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Models;

    /// <summary>Keyword links of <see cref="UiSemantics"/>: the markup, the table row a link sits in and its hover definition.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Label text → cell → row (character stats); a third level would take in the whole section.</summary>
        private const int LinkRowHops = 2;

        /// <summary>Keyword link markup: "[Flask|Flasks]" shows "Flasks", "[Wells]" shows "Wells".</summary>
        private static readonly System.Text.RegularExpressions.Regex KeywordLink = new(@"\[([^\[\]|]+)(?:\|([^\[\]]+))?\]", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        ///     A keyword link is an empty box child of the text holding it; the k-th box is the k-th "[key|shown]"
        ///     of the parent's tagged text (inspect tooltip, 2026-10-23).
        /// </summary>
        private AccessibleUiNode? DescribeLink(in UiElementReader.ElementInfo info)
        {
            if (info.Raw.ParentPtr == IntPtr.Zero || this.elements.ReadChildren(info).Length > 0 ||
                this.elements.TryReadText(info.Address, out var own) && own.Length > 0 ||
                !this.elements.TryReadText(info.Raw.ParentPtr, out var sentence, out var tagged) ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                return null;
            }

            var links = KeywordLink.Matches(tagged);
            var index = Array.IndexOf(this.elements.ReadChildren(parent), info.Address);
            if (index < 0 || index >= links.Count)
            {
                return null;
            }

            var link = links[index];
            var keyword = ShownText(link);
            var row = this.LinkRow(parent, sentence, keyword, out var rowElement);
            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Link,
                Text = keyword,
                Value = row,
                // The stat list's section ("Escudo de energía" over its rows): said as the links move into it.
                Group = rowElement is { } inRow && this.elements.TryReadElement(inRow.Raw.ParentPtr, out var rows) ? this.GroupLabel(rows) : string.Empty,
                IsVisible = this.elements.IsVisible(info),
                Context = this.InGame ? this.PanelTitle(info) : string.Empty,
                Tooltip = this.Definition(info),
            };
        }

        /// <summary>
        ///     Where the link sits: its table row when the text is a row's label ("Evasion Rating, 7" in the character
        ///     stats), else the text itself; empty when the text is just the keyword.
        /// </summary>
        private string LinkRow(in UiElementReader.ElementInfo text, string sentence, string keyword, out UiElementReader.ElementInfo? rowElement)
        {
            var node = text;
            for (var level = 0; level < LinkRowHops && this.elements.TryReadElement(node.Raw.ParentPtr, out var parent); level++)
            {
                var row = this.RowText(parent);
                if (row.Length > sentence.Length && row.Contains(sentence, StringComparison.Ordinal))
                {
                    rowElement = parent;
                    return row;
                }

                node = parent;
            }

            rowElement = null;
            return sentence == keyword ? string.Empty : sentence;
        }

        /// <summary>A keyword link's hover tooltip: the keyword as title (dropped), then its definition (character stats).</summary>
        private List<string> Definition(in UiElementReader.ElementInfo info)
        {
            var lines = this.TooltipLines(info);
            if (lines.Count > 1)
            {
                lines.RemoveAt(0);
            }

            return lines;
        }
    }
}
