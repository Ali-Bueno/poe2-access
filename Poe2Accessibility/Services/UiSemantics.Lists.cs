namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>List, same-class and tab position logic of <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Control → row → list: two levels up finds a settings list; three leaves room for wrappers.</summary>
        private const int ListSearchLevels = 3;

        /// <summary>In-game option control → cell → label/control pair → row: the rows are four levels up.</summary>
        private const int SameClassSearchLevels = 5; // the controller deadzone slider sits one wrapper deeper than its row's other sliders

        /// <summary>List container → scroll wrapper → section: where a section's title sits.</summary>
        private const int GroupLabelHops = 2;

        /// <summary>A list with a single captioned item needs this many items to count as one (skill rows).</summary>
        private const int SparseListItems = 3;

        /// <summary>How deep a list item may hold its control (row → column → control).</summary>
        private const int ListItemDepth = 2;

        /// <summary>List widget → entry holder: the character list keeps its entries one level down.</summary>
        private const int ListHolderDepth = 2;

        /// <summary>Entry → frame → label: how deep an entry's texts sit.</summary>
        private const int EntryTextDepth = 3;

        /// <summary>Breadth-first budget when looking for the selected tab of a page (options: ~800 visible nodes).</summary>
        private const int MaxTabSearchNodes = 2000;

        /// <summary>A tab bar of fewer tabs is not judged on: on two tabs a hover byte fits as well as the selected one.</summary>
        private const int MinJudgedTabs = 3;

        private const int MaxTablessLayers = 64;

        private readonly HashSet<IntPtr> tablessLayers = new();

        /// <summary>A visible frame whose control name starts so marks the chosen row of a list that keeps the focus.</summary>
        private const string SelectionMarkerPrefix = "selected_";

        /// <summary>Challenges: focused tabs_container → row 5 levels down → its marker.</summary>
        private const int MarkerSearchDepth = 8;

        private const int MaxMarkerNodes = 400;

        /// <summary>
        ///     A non-control container whose entry holder (child or grandchild) lists controls, with a valid selected
        ///     index: the selected entry is read with all its texts in screen order, then its position.
        /// </summary>
        private AccessibleUiNode? DescribeSelectableList(in UiElementReader.ElementInfo info)
        {
            if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return null;
            }

            var entries = this.FindEntryHolder(info, ListHolderDepth);
            var address = info.Address;
            var at = GameLayout.SelectableList.SelectedIndexOffset + (entries.Count < 2 ? this.elements.Controls?.Current(ControlGroup.SelectableList) ?? 0
                : this.elements.Controls?.Changing(ControlGroup.SelectableList, address, GameLayout.SelectableList.SelectedIndexOffset, sizeof(int),
                    (offset, length) => this.reader.ReadBytes(address + offset, length), value => value >= 0 && value < entries.Count) ?? 0);
            if (entries.Count == 0 || !this.reader.TryRead<int>(info.Address + at, out var selected) || selected < 0 || selected >= entries.Count)
            {
                return null;
            }

            var text = this.ScreenOrderText(entries[selected]);
            // A grey entry among coloured ones is dimmed (a dialogue topic already heard); a list drawn all grey is its style.
            var dimmed = this.EntryGrey(entries[selected]) == true && entries.Any(entry => this.EntryGrey(entry) == false);
            return text.Length == 0 ? null : new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.ListItem,
                Text = text,
                Value = dimmed ? Announcements.Dimmed : string.Empty,
                Position = Announcements.ListPosition(selected + 1, entries.Count),
                IsVisible = this.elements.IsVisible(info),
                Context = this.InGame ? this.PanelTitle(info) : string.Empty,
            };
        }

        /// <summary>
        ///     How deep a button menu's texts sit: its buttons two levels down, the player's name six (layout → header
        ///     frame → label stack → name row → frame → text).
        /// </summary>
        private const int ButtonMenuDepth = 7;

        /// <summary>
        ///     A menu that keeps the focus while the D-pad moves a chosen button (<see cref="GameLayout.ButtonMenu"/>):
        ///     the button and its place, with the texts outside the buttons (the player's name, class and league) as
        ///     its context.
        /// </summary>
        private AccessibleUiNode? DescribeButtonMenu(in UiElementReader.ElementInfo info)
        {
            if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) ||
                (this.elements.TryReadText(info.Address, out var own) && own.Length > 0) ||
                !this.reader.TryRead<int>(info.Address + GameLayout.ButtonMenu.SelectedIndexOffset, out var selected) || selected < 0)
            {
                return null;
            }

            var buttons = new List<string>();
            var header = new List<string>();
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((info, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                    {
                        continue;
                    }

                    if (child.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                    {
                        buttons.Add(this.ScreenOrderText(child));
                    }
                    else if (this.elements.TryReadText(child.Address, out var text) && text.Length > 0)
                    {
                        header.Add(text);
                    }
                    else if (depth + 1 < ButtonMenuDepth)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            // Every button labelled, and the index one of them: anything else is not this widget.
            if (buttons.Count < 2 || selected >= buttons.Count || buttons.Any(text => text.Length == 0))
            {
                return null;
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.ListItem,
                Text = buttons[selected],
                Position = Announcements.ListPosition(selected + 1, buttons.Count),
                IsVisible = this.elements.IsVisible(info),
                Context = string.Join(", ", header),
            };
        }

        /// <summary>
        ///     A container that keeps the focus while a marker frame shows the chosen row ("selected_challenge_frame"
        ///     in Challenges, moved by the stick): the marked row with all its texts, then its place among the rows.
        /// </summary>
        private AccessibleUiNode? DescribeMarkedRow(in UiElementReader.ElementInfo info)
        {
            if (this.elements.TryReadText(info.Address, out var own) && own.Length > 0)
            {
                return null;
            }

            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((info, 0));
            var budget = MaxMarkerNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                    {
                        continue;
                    }

                    if (child.Name.StartsWith(SelectionMarkerPrefix, StringComparison.Ordinal) && node.Address != info.Address)
                    {
                        return this.MarkedRow(info, node);
                    }

                    if (depth < MarkerSearchDepth)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return null;
        }

        private AccessibleUiNode? MarkedRow(in UiElementReader.ElementInfo owner, in UiElementReader.ElementInfo row)
        {
            var text = this.ScreenOrderText(row);
            if (text.Length == 0)
            {
                return null;
            }

            var position = string.Empty;
            if (this.elements.TryReadElement(row.Raw.ParentPtr, out var list))
            {
                var rows = new List<IntPtr>();
                foreach (var childAddress in this.elements.ReadChildren(list))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                    {
                        rows.Add(childAddress);
                    }
                }

                var index = rows.IndexOf(row.Address);
                position = index < 0 ? string.Empty : Announcements.ListPosition(index + 1, rows.Count);
            }

            return new AccessibleUiNode
            {
                Address = owner.Address,
                Name = owner.Name,
                Role = UiRole.ListItem,
                Text = text,
                Position = position,
                IsVisible = this.elements.IsVisible(owner),
                Context = this.InGame ? this.PanelTitle(owner) : string.Empty,
            };
        }

        /// <summary>Visible children of the first descendant whose visible children are all controls.</summary>
        private List<UiElementReader.ElementInfo> FindEntryHolder(in UiElementReader.ElementInfo node, int depth)
        {
            var visible = new List<UiElementReader.ElementInfo>();
            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    visible.Add(child);
                }
            }

            if (visible.Count > 0 && visible.TrueForAll(child => child.HasFlagBit(GameLayout.Element.InteractiveFlagBit)))
            {
                return visible;
            }

            if (depth > 0)
            {
                foreach (var child in visible)
                {
                    var found = this.FindEntryHolder(child, depth - 1);
                    if (found.Count > 0)
                    {
                        return found;
                    }
                }
            }

            return new List<UiElementReader.ElementInfo>();
        }

        /// <summary>Whether the entry's first text (the entry itself when it is a label) is grey; null when it has none.</summary>
        private bool? EntryGrey(in UiElementReader.ElementInfo entry)
        {
            var stack = new Stack<(UiElementReader.ElementInfo Node, int Depth)>();
            stack.Push((entry, 0));
            while (stack.Count > 0)
            {
                var (node, depth) = stack.Pop();
                if (this.elements.TryReadText(node.Address, out var text) && text.Length > 0)
                {
                    return this.elements.IsTextGrey(node.Address);
                }

                if (depth > EntryTextDepth)
                {
                    continue;
                }

                var children = this.elements.ReadChildren(node);
                for (var i = children.Length - 1; i >= 0; i--)
                {
                    if (this.elements.TryReadElement(children[i], out var child) && child.IsVisibleFlag)
                    {
                        stack.Push((child, depth + 1));
                    }
                }
            }

            return null;
        }

        /// <summary>Every visible text under an entry, top to bottom then left to right ("Yarkidius, Level 1 Witch, Act 1: …").</summary>
        private string ScreenOrderText(in UiElementReader.ElementInfo entry)
        {
            var cells = new List<(float Y, float X, string Text)>();
            this.CollectPositionedTexts(entry, 0, 0, EntryTextDepth, cells);
            return Announcements.Cells(cells.OrderBy(cell => cell.Y).ThenBy(cell => cell.X).Select(cell => cell.Text).Distinct());
        }

        private void CollectPositionedTexts(in UiElementReader.ElementInfo node, float x, float y, int depth, List<(float Y, float X, string Text)> found)
        {
            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                var childX = x + child.Raw.RelativePosition.X;
                var childY = y + child.Raw.RelativePosition.Y;
                if (this.elements.TryReadText(childAddress, out var text) && text.Length > 0)
                {
                    found.Add((childY, childX, text));
                }

                if (depth > 0)
                {
                    this.CollectPositionedTexts(child, childX, childY, depth - 1, found);
                }
            }
        }

        /// <summary>Label of the selected tab button in a layer; empty when the layer has no tab bar.</summary>
        public string SelectedTab(IntPtr layer)
        {
            if (!this.elements.TryReadElement(layer, out var root))
            {
                return string.Empty;
            }

            // Until a bar proves the selected byte every control's click sound is read: a layer found without tabs is
            // then only read through the byte (the cheap way) until the bar is proved.
            var proved = this.elements.Controls?.IsKnown(ControlGroup.Tabs) ?? true;
            if (!proved && this.tablessLayers.Contains(layer))
            {
                proved = true;
            }

            var queue = new Queue<UiElementReader.ElementInfo>();
            queue.Enqueue(root);
            var bars = new HashSet<IntPtr>();
            var budget = MaxTabSearchNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                // Once the selected byte is proved, it is read before the costlier click-sound scan.
                var node = queue.Dequeue();
                if (node.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && !bars.Contains(node.Raw.ParentPtr) &&
                    (!proved || this.IsTabSelected(node.Address, this.TabSelectedOffset(null))) &&
                    this.elements.DetectRoleBySound(node.Address) == UiRole.Tab && this.SiblingTabs(node) is { Count: >= 2 } tabs)
                {
                    bars.Add(node.Raw.ParentPtr);
                    var at = this.TabSelectedOffset(tabs);
                    if (tabs.FirstOrDefault(tab => this.IsTabSelected(tab, at)) is var selected && selected != IntPtr.Zero &&
                        this.elements.TryReadElement(selected, out var tab))
                    {
                        return this.FindDescendantText(tab, CaptionSearchDepth + 1, null);
                    }
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            if (!proved && bars.Count == 0)
            {
                if (this.tablessLayers.Count >= MaxTablessLayers)
                {
                    this.tablessLayers.Clear();
                }

                this.tablessLayers.Add(layer);
            }

            return string.Empty;
        }

        /// <summary>The tabs of a tab's bar, itself included; a lone control with the tab click sound is something else.</summary>
        private List<IntPtr> SiblingTabs(in UiElementReader.ElementInfo tab)
        {
            var tabs = new List<IntPtr>();
            var own = tab.Address;
            if (this.elements.TryReadElement(tab.Raw.ParentPtr, out var parent))
            {
                tabs.AddRange(this.elements.ReadChildren(parent).Where(sibling => sibling == own || this.elements.DetectRoleBySound(sibling) == UiRole.Tab));
            }

            return tabs;
        }

        /// <summary>The tab buttons' selected byte, judged on a bar of them when one is given (exactly one tab is set).</summary>
        private int TabSelectedOffset(IReadOnlyList<IntPtr>? tabs)
        {
            var shift = tabs is { Count: >= MinJudgedTabs }
                ? this.elements.Controls?.Shift(ControlGroup.Tabs, tabs[0], at => this.OneTabSelected(tabs, GameLayout.TabButton.SelectedOffset + at))
                : this.elements.Controls?.Current(ControlGroup.Tabs);
            return GameLayout.TabButton.SelectedOffset + (shift ?? 0);
        }

        private bool OneTabSelected(IReadOnlyList<IntPtr> tabs, int offset)
        {
            var selected = 0;
            foreach (var tab in tabs)
            {
                if (!this.reader.TryRead<byte>(tab + offset, out var value) || value > 1)
                {
                    return false;
                }

                selected += value;
            }

            return selected == 1;
        }

        private bool IsTabSelected(IntPtr tab, int offset) => this.reader.TryRead<byte>(tab + offset, out var selected) && selected != 0;

        /// <summary>
        ///     Position among sibling controls: the first ancestor level whose visible children hold several controls
        ///     is the list (menu buttons are siblings; a settings control sits in a row, the rows are the list), counted
        ///     in reading order (the "spawn in town" confirmation holds OK before Cancel but draws Cancel on the left).
        /// </summary>
        private (string Position, IReadOnlyList<string> Items) ListPosition(in UiElementReader.ElementInfo info)
        {
            var item = info;
            for (var level = 0; level < ListSearchLevels && item.Raw.ParentPtr != IntPtr.Zero; level++)
            {
                if (!this.elements.TryReadElement(item.Raw.ParentPtr, out var parent))
                {
                    break;
                }

                var items = new List<UiElementReader.ElementInfo>();
                foreach (var childAddress in this.elements.ReadChildren(parent))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && this.HoldsControl(child, ListItemDepth))
                    {
                        items.Add(child);
                    }
                }

                var own = item.Address;
                var index = InReadingOrder(items).FindIndex(child => child.Address == own);
                var count = items.Count;
                var kinds = items.Select(child => child.Raw.Vtable).ToHashSet();

                // A check box beside the number it enables ("Límite de FPS en primer plano" | 240) is one row of the
                // section, not a list of two: controls of different kinds side by side are a compound row (the number
                // sits in a wrapper, so its pair shows one level up).
                if (kinds.Count > 1 && (level == 0 || count == 2))
                {
                    item = parent;
                    continue;
                }

                if (count >= 2 && index >= 0)
                {
                    return (Announcements.ListPosition(index + 1, count), Array.Empty<string>());
                }

                item = parent;
            }

            return (string.Empty, Array.Empty<string>());
        }

        /// <summary>
        ///     In game controls carry no interactive flag, but a list's items are siblings of one class (same vtable).
        ///     The list is the first ancestor level with two captioned same-class siblings (context menus; an
        ///     options row's label/control pair has only one caption, so the rows above it win); failing that, the
        ///     first level of three or more with one caption (skill rows below "LVL: 1, Punch" are empty).
        /// </summary>
        /// <summary>A shown value, not a name: digits and signs only ("100%", "+ 0.0", "240").</summary>
        private static bool IsValueText(string caption) => caption.Length > 0 && !caption.Any(char.IsLetter);

        private (string Position, IReadOnlyList<string> Items, string Group) SameClassPosition(in UiElementReader.ElementInfo info)
        {
            (string Position, IReadOnlyList<string> Items, string Group)? sparse = null;
            var item = info;
            for (var level = 0; level < SameClassSearchLevels && item.Raw.ParentPtr != IntPtr.Zero; level++)
            {
                if (!this.elements.TryReadElement(item.Raw.ParentPtr, out var parent))
                {
                    break;
                }

                var siblings = new List<UiElementReader.ElementInfo>();
                foreach (var childAddress in this.elements.ReadChildren(parent))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && child.Raw.Vtable == item.Raw.Vtable)
                    {
                        siblings.Add(child);
                    }
                }

                siblings = InReadingOrder(siblings);
                var index = siblings.FindIndex(sibling => sibling.Address == item.Address);
                if (siblings.Count >= 2 && index >= 0)
                {
                    var captions = siblings.Select(sibling => this.FindDescendantText(sibling, CaptionSearchDepth + 1, null)).ToList();
                    var captioned = captions.Count(caption => caption.Length > 0);
                    if (siblings.Count == 2 && captions.Count(IsValueText) == 1)
                    {
                        // A label beside its control whose value is drawn next to it ("Transparencia" | slider "100%"):
                        // a row, not a list of two.
                        captioned = 1;
                    }
                    var found = (Announcements.ListPosition(index + 1, siblings.Count), (IReadOnlyList<string>)captions, this.GroupLabel(parent));
                    if (captioned >= 2)
                    {
                        return found;
                    }

                    if (sparse == null && captioned == 1 && siblings.Count >= SparseListItems)
                    {
                        sparse = found;
                    }
                }

                item = parent;
            }

            return sparse ?? (string.Empty, Array.Empty<string>(), string.Empty);
        }

        /// <summary>
        ///     The section a list belongs to: the nearest text before the list container, at most two wrappers up
        ///     ("UI Scaling" is the section's first child, the rows sit in its scroll wrapper; in-game options).
        /// </summary>
        private string GroupLabel(in UiElementReader.ElementInfo list)
        {
            var node = list;
            for (var level = 0; level < GroupLabelHops && this.elements.TryReadElement(node.Raw.ParentPtr, out var parent); level++)
            {
                var children = this.elements.ReadChildren(parent);
                for (var i = Array.IndexOf(children, node.Address) - 1; i >= 0; i--)
                {
                    if (this.elements.TryReadElement(children[i], out var sibling) && sibling.IsVisibleFlag &&
                        this.elements.TryReadText(children[i], out var text) && text.Length > 0)
                    {
                        return text;
                    }
                }

                node = parent;
            }

            return string.Empty;
        }

        private bool HoldsControl(in UiElementReader.ElementInfo node, int depth)
        {
            if (node.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return true;
            }

            if (depth == 0)
            {
                return false;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && this.HoldsControl(child, depth - 1))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
