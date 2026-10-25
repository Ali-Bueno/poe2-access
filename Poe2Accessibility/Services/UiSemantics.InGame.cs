namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>In-game archetypes (inventory, keyword links, panel titles) of <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Inventory grid → panel holding the title: 7 levels measured; room for deeper panels.</summary>
        private const int PanelTitleHops = 16;

        private const int MaxPanelTitleLength = 40;

        /// <summary>Grid → page → pages container → tab container (vendor stock and stash: 3 hops, 2026-10-23).</summary>
        private const int GridTabHops = 4;

        private (IntPtr Grid, List<IntPtr> Tabs) gridTabs = (IntPtr.Zero, new List<IntPtr>());

        /// <summary>Label text → cell → row (character stats); a third level would take in the whole section.</summary>
        private const int LinkRowHops = 2;

        /// <summary>Most lines a panel shows outside the focused part to be read as its notes; more is a panel of labels.</summary>
        private const int MaxPanelNotes = 3;

        /// <summary>Depth and elements of a panel looked through for its notes (Skills: the spirit line sits 2 below the panel).</summary>
        private const int PanelNoteDepth = 6;

        private const int MaxPanelNoteNodes = 600;

        /// <summary>A label and its control, alone in their holder (a check box with its caption beside it).</summary>
        private const int ControlCaptionPair = 2;

        /// <summary>The notes of the last panel, kept a moment: the focus is described every few steps within one panel.</summary>
        private const int PanelNotesKeepMs = 1000;

        private (IntPtr Panel, IReadOnlyList<string> Notes, long At) panelNotes = (IntPtr.Zero, Array.Empty<string>(), 0);

        /// <summary>Keyword link markup: "[Flask|Flasks]" shows "Flasks", "[Wells]" shows "Wells".</summary>
        private static readonly System.Text.RegularExpressions.Regex KeywordLink = new(@"\[([^\[\]|]+)(?:\|([^\[\]]+))?\]", System.Text.RegularExpressions.RegexOptions.Compiled);

        private AccessibleUiNode? DescribeInventory(in UiElementReader.ElementInfo info)
        {
            UiRole role;
            string text;
            var value = string.Empty;
            List<string> details;
            if (this.inventory.TryReadGrid(info.Address, out var column, out var row, out var hovered))
            {
                role = UiRole.GridCell;
                details = this.ItemTooltip(hovered);
                var item = hovered == IntPtr.Zero ? Announcements.EmptyCell : this.inventory.DescribeItem(hovered, Title(details)) ?? Announcements.UnknownItem;
                var inventoryName = this.inventory.InventoryOf(info.Address);
                (text, value) = Announcements.InventoryCell(inventoryName, item, column, row);
            }
            else if (this.inventory.FindItemElement(info.Address) is var item && item != IntPtr.Zero)
            {
                role = UiRole.ItemSlot;
                details = this.ItemTooltip(item);
                text = this.inventory.DescribeItem(item, Title(details)) ?? Announcements.UnknownItem;
            }
            else
            {
                return null;
            }

            if (details.Count > 0 && text.Contains(details[0], StringComparison.Ordinal))
            {
                details.RemoveAt(0); // the name the announcement already gives
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = role,
                Text = text,
                Value = value,
                IsVisible = this.elements.IsVisible(info),
                Context = role == UiRole.GridCell ? Announcements.InContext(this.PanelTitle(info), this.GridTab(info)) : this.PanelTitle(info),
                Tooltip = details,
            };
        }

        /// <summary>
        ///     The item's own tooltip (the item element owns it, built once the cursor rests on it): its name, then
        ///     "Stack Size: 3/40", "Identifies an item", ….
        /// </summary>
        private List<string> ItemTooltip(IntPtr itemElement) =>
            itemElement != IntPtr.Zero && this.elements.TryReadElement(itemElement, out var item)
                ? this.TooltipLines(item)
                : new List<string>();

        private static string Title(List<string> tooltip) => tooltip.Count > 0 ? tooltip[0] : string.Empty;

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
            var keyword = link.Groups[2].Success ? link.Groups[2].Value : link.Groups[1].Value;
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

        /// <summary>
        ///     Whether a text belongs to another group of controls than the focus: under the branch where the two part,
        ///     the text's side holds something the cursor lands on. The skill bind screen opens on a slot of the primary
        ///     set with the spirit panel, the secondary set's caption and the targeting mode beside it: those are read
        ///     when the cursor gets there, not with the prompt.
        /// </summary>
        public bool InOtherControlGroup(IntPtr text, IntPtr focus)
        {
            if (!this.elements.TryReadElement(text, out var info) || !this.elements.TryReadElement(focus, out var focused))
            {
                return false;
            }

            var focusChain = new HashSet<IntPtr>(this.elements.ReadAncestors(focused, PanelTitleHops).Select(ancestor => ancestor.Address)) { focus };
            var branch = info;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                if (focusChain.Contains(ancestor.Address))
                {
                    return branch.Address != text && this.HoldsNavigable(branch);
                }

                branch = ancestor;
            }

            return false;
        }

        private bool HoldsNavigable(in UiElementReader.ElementInfo root)
        {
            var stack = new Stack<UiElementReader.ElementInfo>();
            stack.Push(root);
            for (var budget = MaxTooltipNodes; stack.Count > 0 && budget > 0; budget--)
            {
                var node = stack.Pop();
                if (node.HasFlagBit(GameLayout.Element.NavigableFlagBit))
                {
                    return true;
                }

                foreach (var child in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(child, out var element) && element.IsVisibleFlag)
                    {
                        stack.Push(element);
                    }
                }
            }

            return false;
        }

        /// <summary>Whether an ancestor panel carries this title (a social page under the Challenges overlay does not).</summary>
        public bool IsInPanel(IntPtr address, string title)
        {
            if (!this.elements.TryReadElement(address, out var info))
            {
                return false;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                if (this.elements.ReadWStringStructAt(ancestor.Address + GameLayout.Element.PanelTitleOffset + shift)?.Trim() == title)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Title of the nearest ancestor panel (GameLayout.Element.PanelTitleOffset), judged only on a focus inside the
        ///     inventory's panels, each of which has one: the field holds where an ancestor names the panel (a shift found by
        ///     search waits for a second focus to agree).
        /// </summary>
        private string PanelTitle(in UiElementReader.ElementInfo info)
        {
            var chain = this.elements.ReadAncestors(info, PanelTitleHops);
            var ancestors = chain.Select(ancestor => ancestor.Address).ToList();
            if (ancestors.Count == 0)
            {
                return string.Empty;
            }

            var shift = chain.Any(ancestor => ancestor.Name == GameLayout.InGameUi.PanelsParentName)
                ? this.elements.Controls?.Shift(ControlGroup.PanelTitle, info.Address, at => ancestors.Any(ancestor => this.TitleAt(ancestor, at).Length > 0)) ?? 0
                : this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            return ancestors.Select(ancestor => this.TitleAt(ancestor, shift)).FirstOrDefault(title => title.Length > 0) ?? string.Empty;
        }

        /// <summary>
        ///     The few lines a titled panel shows besides what the cursor lands on, in screen order (Skills: "0 de
        ///     espíritu" over the list, the gem capacity under it; Character: the class over the stat tiles). Texts inside
        ///     navigable or interactive elements are read when focused, and a part of the panel holding more than a
        ///     handful (the stat list) is a list, not a note; empty when none are left.
        /// </summary>
        private IReadOnlyList<string> PanelNotes(in UiElementReader.ElementInfo info)
        {
            var shift = this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                var title = this.TitleAt(ancestor.Address, shift);
                if (title.Length > 0)
                {
                    return this.NotesOf(ancestor, title);
                }
            }

            return Array.Empty<string>();
        }

        private IReadOnlyList<string> NotesOf(in UiElementReader.ElementInfo panel, string title)
        {
            var now = Environment.TickCount64;
            if (this.panelNotes.Panel == panel.Address && now - this.panelNotes.At < PanelNotesKeepMs)
            {
                return this.panelNotes.Notes;
            }

            var lines = new List<(float Y, float X, string Text)>();
            var budget = MaxPanelNoteNodes;
            foreach (var childAddress in this.elements.ReadChildren(panel))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                var part = new List<(float Y, float X, string Text)>();
                this.CollectNotes(child, 0, 0, PanelNoteDepth, ref budget, part);
                if (part.Count <= MaxPanelNotes)
                {
                    lines.AddRange(part);
                }
            }

            var texts = lines.Where(line => line.Text != title).OrderBy(line => line.Y).ThenBy(line => line.X).Select(line => line.Text).Distinct().ToList();
            IReadOnlyList<string> notes = texts.Count <= MaxPanelNotes ? texts : Array.Empty<string>();
            this.panelNotes = (panel.Address, notes, now);
            return notes;
        }

        /// <summary>Texts of <paramref name="node"/> and below, placed relative to the panel, short of what the focus lands on.</summary>
        private void CollectNotes(in UiElementReader.ElementInfo node, float x, float y, int depth, ref int budget, List<(float Y, float X, string Text)> found)
        {
            if (budget-- <= 0 || node.HasFlagBit(GameLayout.Element.NavigableFlagBit) || node.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return;
            }

            var children = this.elements.ReadChildren(node);
            if (children.Length == ControlCaptionPair && children.Any(this.IsFocusable))
            {
                return; // a caption beside its control (Friends' "Mostrar desconectados" and its check box)
            }

            var nodeX = x + node.Raw.RelativePosition.X;
            var nodeY = y + node.Raw.RelativePosition.Y;
            if (this.elements.TryReadSpokenText(node.Address, out var text) && text.Any(char.IsLetter))
            {
                // A bare number is a value drawn beside its tile ("11" over DES), read with the tile.
                found.Add((nodeY, nodeX, text));
            }

            if (depth == 0)
            {
                return;
            }

            foreach (var childAddress in children)
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    this.CollectNotes(child, nodeX, nodeY, depth - 1, ref budget, found);
                }
            }
        }

        /// <summary>Whether the controller cursor lands on the element (in game: navigable; before it: interactive).</summary>
        private bool IsFocusable(IntPtr address) =>
            this.elements.TryReadElement(address, out var element) &&
            (element.HasFlagBit(GameLayout.Element.NavigableFlagBit) || element.HasFlagBit(GameLayout.Element.InteractiveFlagBit));

        /// <summary>A panel title read with the field moved by a shift: a short name with letters, no path.</summary>
        private string TitleAt(IntPtr element, int shift) =>
            this.elements.ReadWStringStructAt(element + GameLayout.Element.PanelTitleOffset + shift)?.Trim() is { Length: > 0 and <= MaxPanelTitleLength } title &&
            !title.Contains('/') && title.Any(char.IsLetter) ? title : string.Empty;

        /// <summary>
        ///     Selected tab of the tab container a grid is a page of ("tab 3" of a vendor's stock, a stash tab); empty
        ///     when the grid has no tabs. The tab buttons are looked up once per grid, their selected byte every time.
        /// </summary>
        private string GridTab(in UiElementReader.ElementInfo info)
        {
            if (info.Address != this.gridTabs.Grid)
            {
                this.gridTabs = (info.Address, this.FindGridTabs(info));
            }

            var at = this.TabSelectedOffset(this.gridTabs.Tabs);
            foreach (var button in this.gridTabs.Tabs)
            {
                if (this.IsTabSelected(button, at) && this.elements.TryReadElement(button, out var tab))
                {
                    return Announcements.TabTitle(this.FindDescendantText(tab, CaptionSearchDepth + 1, null));
                }
            }

            return string.Empty;
        }

        /// <summary>Tab buttons of the nearest ancestor holding a pages container; its pages are not searched.</summary>
        private List<IntPtr> FindGridTabs(in UiElementReader.ElementInfo info)
        {
            var tabs = new List<IntPtr>();
            foreach (var ancestor in this.elements.ReadAncestors(info, GridTabHops))
            {
                var children = this.elements.ReadChildren(ancestor);
                if (!children.Any(child => this.elements.TryReadElement(child, out var page) && page.Name == GameLayout.TabButton.PagesContainerName))
                {
                    continue;
                }

                var queue = new Queue<IntPtr>(children);
                var budget = MaxTabSearchNodes;
                while (queue.Count > 0 && budget-- > 0)
                {
                    if (!this.elements.TryReadElement(queue.Dequeue(), out var node) || !node.IsVisibleFlag ||
                        node.Name == GameLayout.TabButton.PagesContainerName)
                    {
                        continue;
                    }

                    if (node.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && this.elements.DetectRoleBySound(node.Address) == UiRole.Tab)
                    {
                        tabs.Add(node.Address);
                        continue;
                    }

                    foreach (var child in this.elements.ReadChildren(node))
                    {
                        queue.Enqueue(child);
                    }
                }

                break;
            }

            return tabs;
        }
    }
}
