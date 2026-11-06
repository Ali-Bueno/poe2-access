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

        /// <summary>The English name a localised client prints under a tooltip's title ("Escudo de energía", "(Energy Shield)").</summary>
        private static readonly Regex EnglishName = new(@"^\([A-Za-z' -]+\)$", RegexOptions.Compiled);

        private const int TooltipRadius = 0x80;

        private bool tooltipsRead;

        /// <summary>Told where the tooltips moved, from and to.</summary>
        public Action<int, int>? TooltipsMoved { get; set; }

        private static string ShownText(Match link) => link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value;

        /// <summary>
        ///     The root of the element's tooltip. The field follows the element names' shift, but it can move alone, and every item,
        ///     skill and passive would fall silent with nothing said: until a tooltip has been read, the bytes around it are searched.
        /// </summary>
        private UiElementReader.ElementInfo? TooltipRoot(in UiElementReader.ElementInfo owner)
        {
            if (this.TooltipRootAt(owner, GameLayout.Element.TooltipsOffset) is { } root)
            {
                this.tooltipsRead = true;
                return root;
            }

            if (this.tooltipsRead)
            {
                return null;
            }

            var element = owner;
            if (OffsetSearch.First(GameLayout.Element.TooltipsOffset, TooltipRadius, IntPtr.Size, at => this.TooltipRootAt(element, at) != null) is not { } found)
            {
                return null;
            }

            this.TooltipsMoved?.Invoke(GameLayout.Element.TooltipsOffset, found);
            GameLayout.Element.TooltipsOffset = found;
            this.tooltipsRead = true;
            return this.TooltipRootAt(owner, found);
        }

        /// <summary>A vector of tooltip entries at this offset: the first one's element has the owner as parent, yet is not its child.</summary>
        private UiElementReader.ElementInfo? TooltipRootAt(in UiElementReader.ElementInfo owner, int offset)
        {
            if (!this.reader.TryRead<StdVector>(owner.Address + offset, out var tooltips))
            {
                return null;
            }

            var bytes = tooltips.Last.ToInt64() - tooltips.First.ToInt64();
            if (bytes < GameLayout.Element.TooltipEntrySize || bytes % GameLayout.Element.TooltipEntrySize != 0 ||
                !this.reader.TryRead<IntPtr>(tooltips.First, out var root) ||
                !this.elements.TryReadElement(root, out var rootInfo) ||
                rootInfo.Raw.ParentPtr != owner.Address ||
                this.elements.ReadChildren(owner).Contains(root))
            {
                return null;
            }

            return rootInfo;
        }

        /// <summary>
        ///     Lines of the tooltip the game shows for this element, in reading order; a text's own children (keyword
        ///     link boxes) are skipped and hidden lines ("Account-bound") left out.
        /// </summary>
        private List<string> TooltipLines(in UiElementReader.ElementInfo owner)
        {
            var lines = new List<string>();
            if (this.TooltipRoot(owner) is not { } rootInfo)
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

                    var line = Announcements.WithButton(Announcements.Flatten(string.Join('\n', wrapped)), glyph);
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

            if (lines.Count > 1 && EnglishName.IsMatch(lines[1]))
            {
                lines.RemoveAt(1);
            }

            return lines;
        }
    }
}
