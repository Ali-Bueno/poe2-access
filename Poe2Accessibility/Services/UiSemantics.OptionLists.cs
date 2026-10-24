namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Option lists of <see cref="UiSemantics"/>: scrolling lists that keep the focus while the confirm glyph moves
    ///     over their entries (NPC dialogue topics and services). The chosen entry is read alone, counted over every
    ///     option list shown beside it (Renly's dialogue, focus-research.log 2026-10-23 17:32–17:33).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Layout id of a scrolling list's entry frame (its other children are hidden scroll bars).</summary>
        private const string ListFrameName = "main_frame";

        /// <summary>List → topic column → dialogue body: the level from which the sibling option lists are gathered.</summary>
        private const int OptionGroupHops = 2;

        /// <summary>Group root → body → column → list: how deep the option lists sit below the group root.</summary>
        private const int OptionGroupDepth = 3;

        /// <summary>Entry frame → entry → slot → glyph: how deep the confirm glyph is looked for.</summary>
        private const int OptionGlyphDepth = 4;

        /// <summary>Glyph file of the confirm button in every controller family (xbox_button_a in ControllerGlyphs).</summary>
        private const string ConfirmGlyphFile = "Button4";

        /// <summary>
        ///     A scrolling list beside at least one other (a lone one is the dialogue line box, read whole): the entry
        ///     the confirm glyph marks, its position among all options, and every option for the narrator to skip.
        /// </summary>
        private AccessibleUiNode? DescribeOptionList(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame || info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) || this.ListFrame(info) is not { } frame)
            {
                return null;
            }

            var lists = this.OptionListsAround(info);
            if (lists.Count < 2)
            {
                return null;
            }

            var options = new List<string>();
            var rows = new List<UiElementReader.ElementInfo>();
            var chosen = -1;
            foreach (var (list, listFrame) in lists)
            {
                var entries = this.OptionEntries(listFrame);
                if (list.Address == info.Address)
                {
                    var index = this.ChosenEntry(frame, entries);
                    if (index < 0)
                    {
                        return null;
                    }

                    chosen = options.Count + index;
                }

                options.AddRange(entries.Select(entry => entry.Text));
                rows.AddRange(entries.Select(entry => entry.Entry));
            }

            // A topic already heard is drawn grey among the new ones (Una's "Presentación", 2026-10-24).
            var heard = chosen >= 0 && this.EntryGrey(rows[chosen]) == true && rows.Any(row => this.EntryGrey(row) == false);
            return chosen < 0 ? null : new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.ListItem,
                Text = options[chosen],
                Value = heard ? Announcements.Dimmed : string.Empty,
                Position = Announcements.ListPosition(chosen + 1, options.Count),
                ListItems = options,
                IsVisible = this.elements.IsVisible(info),
                Context = this.PanelTitle(info),
            };
        }

        /// <summary>The visible entry frame of a scrolling list; null when the element is not one.</summary>
        private UiElementReader.ElementInfo? ListFrame(in UiElementReader.ElementInfo list)
        {
            foreach (var childAddress in this.elements.ReadChildren(list))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && child.Name == ListFrameName)
                {
                    return child;
                }
            }

            return null;
        }

        /// <summary>Visible scrolling lists under the group root, in tree order (the order the options are drawn and navigated).</summary>
        private List<(UiElementReader.ElementInfo List, UiElementReader.ElementInfo Frame)> OptionListsAround(in UiElementReader.ElementInfo info)
        {
            var found = new List<(UiElementReader.ElementInfo List, UiElementReader.ElementInfo Frame)>();
            var ancestors = this.elements.ReadAncestors(info, OptionGroupHops);
            if (ancestors.Count == OptionGroupHops)
            {
                this.CollectOptionLists(ancestors[^1], OptionGroupDepth, found);
            }

            var address = info.Address;
            return found.Exists(list => list.List.Address == address) ? found : new();
        }

        private void CollectOptionLists(in UiElementReader.ElementInfo node, int depth, List<(UiElementReader.ElementInfo List, UiElementReader.ElementInfo Frame)> found)
        {
            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                if (this.ListFrame(child) is { } frame)
                {
                    found.Add((child, frame));
                }
                else if (depth > 1)
                {
                    this.CollectOptionLists(child, depth - 1, found);
                }
            }
        }

        /// <summary>Visible entries of a list frame that show text, with that text.</summary>
        private List<(UiElementReader.ElementInfo Entry, string Text)> OptionEntries(in UiElementReader.ElementInfo frame)
        {
            var entries = new List<(UiElementReader.ElementInfo Entry, string Text)>();
            foreach (var childAddress in this.elements.ReadChildren(frame))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                // The entry is its label, or a row holding it.
                var text = this.elements.TryReadText(childAddress, out var own) && own.Length > 0 ? own : this.ScreenOrderText(child);
                if (text.Length > 0)
                {
                    entries.Add((child, text));
                }
            }

            return entries;
        }

        /// <summary>
        ///     Index among <paramref name="entries"/> of the one the confirm glyph marks: the entry holding the glyph, else
        ///     (a glyph drawn beside the entries) the one nearest to it on screen; -1 when no glyph is shown.
        /// </summary>
        private int ChosenEntry(in UiElementReader.ElementInfo frame, List<(UiElementReader.ElementInfo Entry, string Text)> entries)
        {
            var queue = new Queue<(UiElementReader.ElementInfo Node, IntPtr Owner, int Depth)>();
            foreach (var childAddress in this.elements.ReadChildren(frame))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    queue.Enqueue((child, childAddress, 1));
                }
            }

            while (queue.Count > 0)
            {
                var (node, owner, depth) = queue.Dequeue();
                if (this.IsConfirmGlyph(node))
                {
                    var holder = entries.FindIndex(entry => entry.Entry.Address == owner);
                    return holder >= 0 ? holder : this.NearestEntry(node, entries);
                }

                if (depth == OptionGlyphDepth)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                    {
                        queue.Enqueue((child, owner, depth + 1));
                    }
                }
            }

            return -1;
        }

        private int NearestEntry(in UiElementReader.ElementInfo glyph, List<(UiElementReader.ElementInfo Entry, string Text)> entries)
        {
            if (!PluginUiElementReflection.TryGetAbsoluteRect(glyph.Address, out var position, out var size))
            {
                return -1;
            }

            var rects = new List<(Vector2 Position, Vector2 Size)>(entries.Count);
            foreach (var (entry, _) in entries)
            {
                if (!PluginUiElementReflection.TryGetAbsoluteRect(entry.Address, out var entryPosition, out var entrySize))
                {
                    return -1;
                }

                rects.Add((entryPosition, entrySize));
            }

            return NearestRect(rects, position + (size / 2));
        }

        /// <summary>A controller glyph of the confirm button, drawn as an image or as a markup-only label (as ScreenWatcher pairs them).</summary>
        private bool IsConfirmGlyph(in UiElementReader.ElementInfo node)
        {
            var hasText = this.elements.TryReadText(node.Address, out var text, out var tagged);
            if (hasText && text.Length > 0)
            {
                return false;
            }

            var button = hasText ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
            if (button.Length == 0 && this.elements.ReadTextureKey(node) is { } key)
            {
                button = ControllerGlyphs.FromTextureKey(key) ?? string.Empty;
            }

            return button.Length > 0 && button == Announcements.ButtonName(ConfirmGlyphFile, ControllerGlyphs.PlayStationSeen);
        }
    }
}
