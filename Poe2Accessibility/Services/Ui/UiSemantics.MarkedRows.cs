namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Marked rows of <see cref="UiSemantics"/>: rows chosen by a marker, collapsible frames, entry holders and positioned texts.</summary>
    public sealed partial class UiSemantics
    {
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

            var collapsible = this.CollapsibleFrame(row);
            return new AccessibleUiNode
            {
                Address = owner.Address,
                Name = owner.Name,
                Role = UiRole.ListItem,
                Text = text,
                Position = position,
                IsCollapsible = collapsible != null,
                IsExpanded = collapsible?.IsVisibleFlag == true,
                IsVisible = this.elements.IsVisible(owner),
                Context = this.InGame ? this.PanelTitle(owner) : string.Empty,
            };
        }

        /// <summary>The part of <paramref name="row"/> that shows only while A has it open; null for a row that does not open.</summary>
        private UiElementReader.ElementInfo? CollapsibleFrame(in UiElementReader.ElementInfo row)
        {
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((row, 0));
            while (queue.Count > 0)
            {
                var (node, depth) = queue.Dequeue();
                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (!this.elements.TryReadElement(childAddress, out var child))
                    {
                        continue;
                    }

                    if (child.Name == CollapsibleFrameName)
                    {
                        return child;
                    }

                    if (depth + 1 < CollapsibleFrameDepth)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return null;
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
    }
}
