namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The cards of inspect mode (R3, "Comparar"): the inspected item's first, then the worn ones it is set against. The cursor
    ///     walks keyword links and modifier tiers across them (docs/research/ui-layers.md, "Compare view").
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The end of a modifier group's name while the advanced details show ("prefix-mods-with-reminders").</summary>
        private const string AdvancedGroupEnd = "-with-reminders";

        /// <summary>A tier is this many levels under its card at most (card, layout, group, row, holder, chip; dump 2026-11-25), with room for two more.</summary>
        private const int MaxCardDepth = 8;

        /// <summary>Levels from a card to its layer (row, holder, layer; dump 2026-11-25), so a panel's own "layout" is not taken for one.</summary>
        private const int MaxCardHopsToLayer = 4;

        private const int MaxCardNodes = 400;

        /// <summary>Parts of a modifier group's name: "explicit-stats", "prefix-mods-with-reminders", "granted-skills-from-item".</summary>
        private static readonly string[] CardModGroups = { "-stats", "-mods", "-from-item" };

        /// <summary>The requirement line's markup nests its values ("Requiere: &lt;valuedefault&gt;{Nivel &lt;valuedefault&gt;{26}…").</summary>
        private static readonly Regex NestedValues = new(@"<\w+>\{[^{}]*<\w+>\{", RegexOptions.Compiled);

        /// <summary>Whether an address lies in a card of inspect mode: those texts are the cursor's, not news of the screen.</summary>
        public bool InInspectCard(IntPtr address) =>
            this.elements.TryReadElement(address, out var info) && this.InspectCardOf(info) != null;

        /// <summary>Whether the focused card shows the advanced details (ranges, tiers, kinds); null when it has no modifiers.</summary>
        public bool? InspectAdvanced(IntPtr focused) =>
            this.elements.TryReadElement(focused, out var info) && this.InspectCardOf(info) is { } card && this.ReadInspectCard(card) is { HasMods: true } read
                ? read.Advanced
                : null;

        /// <summary>A link, a modifier's tier or a whole card under the cursor of inspect mode.</summary>
        private AccessibleUiNode? DescribeInspectCard(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame || this.InspectCardOf(info) is not { } cardElement || !this.elements.TryReadElement(cardElement.Raw.ParentPtr, out var row))
            {
                return null;
            }

            var cards = new List<InspectCard>();
            var at = -1;
            foreach (var child in this.elements.ReadChildren(row))
            {
                if (this.elements.TryReadElement(child, out var sibling) && sibling.IsVisibleFlag && this.CardLayout(sibling) != null)
                {
                    at = child == cardElement.Address ? cards.Count : at;
                    cards.Add(this.ReadInspectCard(sibling));
                }
            }

            if (at < 0)
            {
                return null;
            }

            var (address, parent, card) = (info.Address, info.Raw.ParentPtr, cards[at]);
            var cardName = Announcements.ComparedCard(card.Name, card.Worn);
            var lines = card.WornLine is { } wornLine ? card.Lines.Append(wornLine).ToList() : card.Lines;
            var line = lines.FirstOrDefault(candidate => candidate.Chip == address || candidate.Label == address || candidate.Label == parent) ??
                       lines.FirstOrDefault(candidate => this.IsUnder(candidate.Label, address));
            var link = this.DescribeLink(info);
            var isTier = link == null && line?.Chip == address;
            // Besides links and tiers the cursor stops on a granted skill's box, and on the whole card when nothing in it is a stop.
            var wholeCard = info.Name == InspectLayout.LayoutName && link == null;
            var hover = link?.Tooltip ?? (wholeCard ? Array.Empty<string>() : this.TooltipLines(info));
            var spoken = wholeCard ? string.Empty : line?.Spoken() ?? link?.Value ?? string.Empty;
            var (text, value) = wholeCard ? (cardName, string.Empty)
                : link != null ? (link.Text, spoken == link.Text ? string.Empty : spoken)
                : isTier ? (hover.Count > 0 ? hover[0] : line!.Tier, spoken)
                : (spoken.Length > 0 ? spoken : hover.Count > 0 ? hover[0] : cardName, string.Empty);
            var comparison = Comparison(cards);
            return new AccessibleUiNode
            {
                Address = address,
                Name = info.Name,
                Role = wholeCard ? UiRole.Label : UiRole.Link,
                Text = text,
                Value = value,
                Group = wholeCard ? string.Empty : cardName,
                IsVisible = this.elements.IsVisible(info),
                IsModTier = isTier,
                InInspectCard = true,
                Context = Announcements.InspectView(cards.Count > 1),
                PanelNotes = InspectNotes(cards),
                Tooltip = hover.Where(told => told != text).ToList(),
                Details = comparison.Concat(cards.Skip(1).Append(cards[0]).SelectMany(told => told.Told)).ToList(),
            };
        }

        private bool IsUnder(IntPtr address, IntPtr ancestor) =>
            this.elements.TryReadElement(address, out var info) && this.elements.ReadAncestors(info, MaxCardDepth).TakeWhile(parent => parent.Name != InspectLayout.LayoutName).Any(parent => parent.Address == ancestor);

        /// <summary>
        ///     The view's title for a link outside its cards (a definition A opened beside them): coming back to a card is then no
        ///     new panel. Empty elsewhere.
        /// </summary>
        private string InspectViewAround(in UiElementReader.ElementInfo info)
        {
            var chain = this.elements.ReadAncestors(info, MaxCardDepth + MaxCardHopsToLayer);
            var at = chain.FindIndex(ancestor => ancestor.Raw.ParentPtr == this.LayerContainer);
            return this.InGame && at >= 0 && this.InspectCardsIn(chain[at]) is { Count: > 0 } cards ? Announcements.InspectView(cards.Count > 1) : string.Empty;
        }

        /// <summary>What inspect mode opened with when nothing in its cards takes the cursor; empty when its layer holds no gear.</summary>
        public string InspectOpening(IntPtr layer)
        {
            if (!this.elements.TryReadElement(layer, out var info))
            {
                return string.Empty;
            }

            var cards = this.InspectCardsIn(info).Select(card => this.ReadInspectCard(card)).ToList();
            var notes = InspectNotes(cards);
            return notes.Count == 0 ? string.Empty : Announcements.Sentences(notes.Prepend(cards[0].Name).Prepend(Announcements.InspectView(cards.Count > 1)));
        }

        /// <summary>The comparison with the worn items; for an item alone, what the advanced details add to its tooltip.</summary>
        private static IReadOnlyList<string> InspectNotes(IReadOnlyList<InspectCard> cards) =>
            cards.Count > 1 ? Comparison(cards) : cards.Count == 1 && cards[0].Advanced ? cards[0].Body : Array.Empty<string>();

        /// <summary>The cards of a layer: siblings, the nearest level that has any.</summary>
        private List<UiElementReader.ElementInfo> InspectCardsIn(in UiElementReader.ElementInfo layer)
        {
            var level = new List<UiElementReader.ElementInfo> { layer };
            for (var depth = 0; depth < MaxCardHopsToLayer && level.Count is > 0 and < MaxCardNodes; depth++)
            {
                var below = new List<UiElementReader.ElementInfo>();
                foreach (var address in level.SelectMany(element => this.elements.ReadChildren(element)))
                {
                    if (this.elements.TryReadElement(address, out var child) && child.IsVisibleFlag)
                    {
                        below.Add(child);
                    }
                }

                if (below.Where(child => this.CardLayout(child) != null).ToList() is { Count: > 0 } cards)
                {
                    return cards;
                }

                level = below;
            }

            return new List<UiElementReader.ElementInfo>();
        }

        /// <summary>The inspected item against each worn one, the lines of a pair under its heading.</summary>
        private static List<string> Comparison(IReadOnlyList<InspectCard> cards)
        {
            var told = new List<string>();
            foreach (var worn in cards.Skip(1))
            {
                told.Add(Announcements.ComparedWith(worn.Name, worn.Worn));
                told.AddRange(Announcements.Difference(ItemComparison.Compare(cards[0].Stats, worn.Stats)));
            }

            return told;
        }

        /// <summary>The card the element is part of: an ancestor near its layer with a title and the lines' "layout".</summary>
        private UiElementReader.ElementInfo? InspectCardOf(in UiElementReader.ElementInfo info)
        {
            var chain = this.elements.ReadAncestors(info, MaxCardDepth + MaxCardHopsToLayer);
            var layer = chain.FindIndex(ancestor => ancestor.Raw.ParentPtr == this.LayerContainer);
            for (var hop = Math.Max(0, layer - MaxCardHopsToLayer); hop < layer; hop++)
            {
                if (this.CardLayout(chain[hop]) != null)
                {
                    return chain[hop];
                }
            }

            return null;
        }

        private bool cardTitleMissingLogged;

        private UiElementReader.ElementInfo? CardLayout(in UiElementReader.ElementInfo card)
        {
            foreach (var child in this.elements.ReadChildren(card).Skip(1))
            {
                if (this.elements.TryReadElement(child, out var layout) && layout.Name == InspectLayout.LayoutName && layout.IsVisibleFlag)
                {
                    if (this.CardTitle(card).Length > 0)
                    {
                        return layout;
                    }

                    if (!this.cardTitleMissingLogged)
                    {
                        this.cardTitleMissingLogged = true;
                        this.ArtLog?.Invoke("Inspect card has lines but no title in its first child: the layout may have moved");
                    }

                    return null;
                }
            }

            return null;
        }

        /// <summary>"Cuerno de armagedón, Gorro de fieltro": the text in the card's first child.</summary>
        private string CardTitle(in UiElementReader.ElementInfo card)
        {
            var children = this.elements.ReadChildren(card);
            if (children.Length == 0 || !this.elements.TryReadElement(children[0], out var head))
            {
                return string.Empty;
            }

            foreach (var child in this.elements.ReadChildren(head))
            {
                if (this.elements.TryReadElement(child, out var label) && label.IsVisibleFlag && this.elements.TryReadText(child, out var text) && text.Length > 0)
                {
                    return string.Join(", ", text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
                }
            }

            return string.Empty;
        }

        private InspectCard ReadInspectCard(in UiElementReader.ElementInfo card)
        {
            var lines = new List<InspectLine>();
            var asides = new List<string>();
            var advanced = false;
            if (this.CardLayout(card) is { } layout)
            {
                foreach (var address in this.elements.ReadChildren(layout))
                {
                    if (!this.elements.TryReadElement(address, out var child) || !child.IsVisibleFlag)
                    {
                        continue;
                    }

                    if (child.Name is InspectLayout.CostName or InspectLayout.LegendName)
                    {
                        asides.AddRange(this.LinesOf(child));
                    }
                    else if (child.Name.EndsWith(AdvancedGroupEnd, StringComparison.Ordinal))
                    {
                        advanced = true;
                        this.AdvancedLines(child, lines);
                    }
                    else
                    {
                        this.CardLines(child, CardModGroups.Any(group => child.Name.Contains(group, StringComparison.Ordinal)), lines);
                    }
                }
            }

            // A worn card ends with "Equipado" ("Equipado: Set de armas I"), after its modifiers.
            var worn = lines.Count > 1 && lines[^1] is { IsMod: false } last && !last.Text.Any(char.IsDigit) ? last : null;
            if (worn != null)
            {
                lines.Remove(worn);
            }

            return new InspectCard(this.CardTitle(card), worn, advanced, lines, asides);
        }

        /// <summary>The texts under an element, each a line; a group's are modifiers.</summary>
        private void CardLines(in UiElementReader.ElementInfo root, bool isMods, List<InspectLine> lines)
        {
            var stack = new Stack<UiElementReader.ElementInfo>();
            stack.Push(root);
            for (var budget = MaxCardNodes; stack.Count > 0 && budget > 0; budget--)
            {
                var node = stack.Pop();
                if (this.elements.TryReadText(node.Address, out var text, out var tagged) && text.Length > 0)
                {
                    var shown = ControllerGlyphs.HasGlyphs(tagged) ? ControllerGlyphs.Render(tagged) : text;
                    lines.Add(new InspectLine(node.Address, CardStats(KeywordLink.Replace(shown, ShownText)), isMods, lines.Count == 0 || NestedValues.IsMatch(tagged)));
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
        }

        /// <summary>
        ///     A group of the advanced details: rows of a modifier's text beside its tier's chip, then the group's own label
        ///     ("Prefijo"), which belongs to every row above it.
        /// </summary>
        private void AdvancedLines(in UiElementReader.ElementInfo group, List<InspectLine> lines)
        {
            var rows = new List<List<InspectLine>>();
            foreach (var rowAddress in this.elements.ReadChildren(group))
            {
                var texts = new List<InspectLine>();
                if (this.elements.TryReadElement(rowAddress, out var row) && row.IsVisibleFlag)
                {
                    this.CardLines(row, true, texts);
                }

                if (texts.Count > 0)
                {
                    rows.Add(texts);
                }
            }

            // The label stands alone in the last row, where a modifier has its tier beside it.
            var affix = rows.Count > 1 && rows[^1] is [var label] && !label.Text.Any(char.IsDigit) ? label.Text : string.Empty;
            if (affix.Length > 0)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            var mods = rows.Select(texts => texts.Count > 1 ? texts[0] with { Chip = texts[^1].Label, Tier = texts[^1].Text } : texts[0]);
            lines.AddRange(mods.Select(mod => mod with { Affix = affix, IsHead = false }));
        }

        /// <summary>A text's stats, one a line; a line in lower case goes on with the one before (layout wrapping).</summary>
        private static IReadOnlyList<string> CardStats(string text)
        {
            var stats = new List<string>();
            foreach (var line in text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (stats.Count > 0 && char.IsLower(line[0]))
                {
                    stats[^1] += " " + line;
                }
                else
                {
                    stats.Add(line);
                }
            }

            return stats;
        }

        /// <summary>A text of a card: its stats as drawn, and beside a modifier its tier ("T8") and kind ("Prefijo").</summary>
        private sealed record InspectLine(IntPtr Label, IReadOnlyList<string> Parts, bool IsMod, bool IsHead)
        {
            public IntPtr Chip { get; init; }

            public string Tier { get; init; } = string.Empty;

            public string Affix { get; init; } = string.Empty;

            public string Text => Announcements.Sentences(this.Parts);

            public string Spoken() => Announcements.CardModLine(
                Announcements.Sentences(this.Parts.Select(ItemModCheck.WithoutRange)), this.Parts.SelectMany(ItemModCheck.RangesOf).ToList(), this.Tier, this.Affix);
        }

        private sealed record InspectCard(string Name, InspectLine? WornLine, bool Advanced, IReadOnlyList<InspectLine> Lines, IReadOnlyList<string> Asides)
        {
            /// <summary>"Equipado" on a worn card, empty on another.</summary>
            public string Worn => this.WornLine?.Text ?? string.Empty;

            public bool HasMods => this.Lines.Any(line => line.IsMod);

            /// <summary>The numbers and modifiers that tell two items apart: not the kind, the requirements or the help texts.</summary>
            public IReadOnlyList<string> Stats => this.Lines.Where(line => !line.IsHead)
                .SelectMany(line => line.Parts.Select(ItemModCheck.WithoutRange).Where(part => line.IsMod || part.Any(char.IsDigit))).ToList();

            public IReadOnlyList<string> Body => this.Lines.Select(line => line.Spoken()).ToList();

            public IEnumerable<string> Told => this.Body.Prepend(Announcements.ComparedCard(this.Name, this.Worn)).Concat(this.Asides);
        }
    }
}
