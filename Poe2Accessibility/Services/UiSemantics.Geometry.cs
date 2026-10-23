namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper.Plugin;

    /// <summary>Screen geometry for <see cref="UiSemantics"/>: values drawn over a widget, reading order.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Value box → its text: the attribute circles keep the number one level down.</summary>
        private const int OverlayTextDepth = 2;

        /// <summary>
        ///     A widget whose value a sibling draws on top of it or just above it: the attribute circles hold only
        ///     "STR" while "7" is a sibling inside the circle; a resistance icon has the "0%" sibling right above it
        ///     (character panel, 2026-10-23). The text whose centre falls in the widget's rect, extended upwards by
        ///     half its height, is the value.
        /// </summary>
        private string OverlayValue(in UiElementReader.ElementInfo info, string label)
        {
            if (!PluginUiElementReflection.TryGetAbsoluteRect(info.Address, out var position, out var size) ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
            {
                return string.Empty;
            }

            var top = position.Y - (size.Y / 2);
            foreach (var siblingAddress in this.elements.ReadChildren(parent))
            {
                if (siblingAddress == info.Address || !this.elements.TryReadElement(siblingAddress, out var sibling) || !sibling.IsVisibleFlag)
                {
                    continue;
                }

                var text = this.TextInside(sibling, OverlayTextDepth, position.X, top, position.X + size.X, position.Y + size.Y);
                if (text.Length > 0 && text != label)
                {
                    return text;
                }
            }

            return string.Empty;
        }

        private string TextInside(in UiElementReader.ElementInfo node, int depth, float left, float top, float right, float bottom)
        {
            if (this.elements.TryReadText(node.Address, out var text) && text.Length > 0)
            {
                return PluginUiElementReflection.TryGetAbsoluteRect(node.Address, out var position, out var size) &&
                       Contains(left, top, right, bottom, position + (size / 2))
                    ? text
                    : string.Empty;
            }

            if (depth == 0)
            {
                return string.Empty;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                {
                    var found = this.TextInside(child, depth - 1, left, top, right, bottom);
                    if (found.Length > 0)
                    {
                        return found;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     Siblings in reading order: rows top to bottom, left to right within a row. The skill-crafting grid keeps
        ///     Contagion before Skeletal Sniper in the tree although Sniper is drawn first (2026-10-23). Tree order
        ///     when any of them has no rect.
        /// </summary>
        private static List<UiElementReader.ElementInfo> InReadingOrder(List<UiElementReader.ElementInfo> items)
        {
            var placed = new List<(UiElementReader.ElementInfo Item, Vector2 Position, Vector2 Size)>(items.Count);
            foreach (var item in items)
            {
                if (!PluginUiElementReflection.TryGetAbsoluteRect(item.Address, out var position, out var size))
                {
                    return items;
                }

                placed.Add((item, position, size));
            }

            placed.Sort((a, b) => a.Position.Y.CompareTo(b.Position.Y));
            var rows = new List<List<(UiElementReader.ElementInfo Item, Vector2 Position, Vector2 Size)>>();
            var rowTop = 0f;
            foreach (var entry in placed)
            {
                if (rows.Count == 0 || entry.Position.Y - rowTop > entry.Size.Y / 2)
                {
                    rows.Add(new());
                    rowTop = entry.Position.Y;
                }

                rows[^1].Add(entry);
            }

            return rows.SelectMany(row => row.OrderBy(entry => entry.Position.X)).Select(entry => entry.Item).ToList();
        }

        private static bool Contains(float left, float top, float right, float bottom, Vector2 point) =>
            point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;
    }
}
