namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using GameOffsets.Natives;

    /// <summary>Tooltips: each owner keeps its tooltip element outside its children (GameLayout.Element.TooltipsOffset).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>A rich tooltip (skill gem: title, tags, level, cost, damage, description) stays under this many nodes.</summary>
        private const int MaxTooltipNodes = 200;

        private static string ShownText(Match link) => link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value;

        /// <summary>
        ///     Lines of the tooltip the game shows for this element, in reading order; a text's own children (keyword
        ///     link boxes) are skipped and hidden lines ("Account-bound") left out.
        /// </summary>
        private List<string> TooltipLines(in UiElementReader.ElementInfo owner)
        {
            var lines = new List<string>();
            if (!this.reader.TryRead<StdVector>(owner.Address + GameLayout.Element.TooltipsOffset, out var tooltips) ||
                tooltips.Last.ToInt64() - tooltips.First.ToInt64() < GameLayout.Element.TooltipEntrySize ||
                !this.reader.TryRead<IntPtr>(tooltips.First, out var root) ||
                !this.elements.TryReadElement(root, out var rootInfo) ||
                rootInfo.Raw.ParentPtr != owner.Address)
            {
                return lines;
            }

            var stack = new Stack<UiElementReader.ElementInfo>();
            stack.Push(rootInfo);
            var budget = MaxTooltipNodes;
            var glyph = string.Empty;
            var currency = string.Empty;
            var priced = false;
            var unmet = new List<string>();
            while (stack.Count > 0 && budget-- > 0)
            {
                var node = stack.Pop();
                var key = this.elements.ReadTextureKey(node);
                if (key != null && ControllerGlyphs.FromTextureKey(key) is { Length: > 0 } button)
                {
                    glyph = button; // an action line's button image ("Unequip" beside its glyph): spoken with the next line
                    continue;
                }

                if (key != null && CurrencyIcons.FromTextureKey(key) is { } unit)
                {
                    currency = unit; // a price: the coin icon, then the amount
                    continue;
                }

                if (this.elements.TryReadText(node.Address, out var text, out var tagged) && text.Length > 0)
                {
                    if (currency.Length > 0)
                    {
                        // Said right after the item's name: what it costs matters before its stats.
                        lines.Insert(Math.Min(1, lines.Count), Announcements.Amount(text, currency, this.elements.IsTextRed(node.Address)));
                        currency = string.Empty;
                        priced = true;
                        continue;
                    }

                    if (ControllerGlyphs.HasGlyphs(tagged))
                    {
                        // "<<xbox_button_a>> to use": the stripped copy drops the button; speak it in place, line by line.
                        text = string.Join('\n', tagged.Split('\n').Select(line => KeywordLink.Replace(ControllerGlyphs.Render(line), ShownText)));
                    }

                    // Tooltip texts carry the layout's line wrapping; a one-text tooltip starts with its title line.
                    var wrapped = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Count == 0 && wrapped.Length > 1)
                    {
                        lines.Add(wrapped[0]);
                        wrapped = wrapped[1..];
                    }

                    var line = Announcements.WithButton(Announcements.JoinLines(wrapped), glyph);
                    if (line.Any(char.IsDigit) && this.elements.IsTextRed(node.Address))
                    {
                        // A requirement drawn in red is one the character does not meet (D2R "cannot equip" reasons):
                        // gathered and said right after the name (and price); red lines without numbers (Corrupted,
                        // Unidentified) are states, not requirements.
                        glyph = string.Empty;
                        if (!unmet.Contains(line))
                        {
                            unmet.Add(line);
                        }

                        continue;
                    }

                    glyph = string.Empty;
                    if (line.Length > 0 && !lines.Contains(line))
                    {
                        lines.Add(line);
                    }

                    continue;
                }

                var children = this.elements.ReadChildren(node);
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    if (this.elements.TryReadElement(children[i], out var child) && child.IsVisibleFlag)
                    {
                        stack.Push(child);
                    }
                }
            }

            if (glyph.Length > 0 && lines.Count > 0)
            {
                lines[^1] = Announcements.WithButton(lines[^1], glyph);
            }

            if (unmet.Count > 0)
            {
                lines.Insert(Math.Min(priced ? 2 : 1, lines.Count), Announcements.Unmet(string.Join(", ", unmet)));
            }

            return lines;
        }
    }
}
