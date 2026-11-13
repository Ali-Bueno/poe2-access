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

        /// <summary>The part of a collapsible row shown while it is open (Challenges; the game's spelling, 0.5.5e).</summary>
        private const string CollapsibleFrameName = "collapsable_frame";

        /// <summary>How deep under a marked row its collapsible part is looked for (row → frame → collapsable_frame).</summary>
        private const int CollapsibleFrameDepth = 3;

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
        /// <summary>Whether a player other than the character's own is in the area under that name; set by the plugin.</summary>
        public Func<string, bool>? IsPlayerNamed { get; set; }

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
                Context = Announcements.ButtonMenuContext(header, this.IsPlayerNamed),
            };
        }

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
