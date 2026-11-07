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
    }
}
