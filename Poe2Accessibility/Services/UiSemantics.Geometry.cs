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
        /// <summary>Deeper parent chains than this are broken (the root is about ten steps above any widget).</summary>
        private const int MaxLayoutDepth = 64;

        /// <summary>
        ///     An element's rect as the game lays it out before fitting it to the window: each step up the parent chain adds
        ///     the element's place (and its parent's scroll, where the game applies it) at the element's own local scale,
        ///     summed up to <paramref name="frame"/> (the root when zero) and given in the frame's units. GameHelper's
        ///     absolute rect multiplies the same sums by the window's scale; here it is left out, so the rect is the same at
        ///     every window size, aspect and UI scale, keeps its proportions at any zoom, and exists for hidden elements
        ///     too. Null for a broken chain, or a frame that is not an ancestor.
        /// </summary>
        internal (Vector2 Position, Vector2 Size)? LayoutRect(IntPtr address, IntPtr frame = default)
        {
            if (!this.elements.TryReadElement(address, out var info))
            {
                return null;
            }

            var raw = info.Raw;
            var size = new Vector2(raw.UnscaledSize.X, raw.UnscaledSize.Y) * raw.LocalScaleMultiplier;
            var position = Vector2.Zero;
            for (var depth = 0; depth < MaxLayoutDepth; depth++)
            {
                position += new Vector2(raw.RelativePosition.X, raw.RelativePosition.Y) * raw.LocalScaleMultiplier;
                if (raw.ParentPtr == IntPtr.Zero)
                {
                    return frame == IntPtr.Zero ? (position, size) : null;
                }

                if (!this.elements.TryReadElement(raw.ParentPtr, out var parent))
                {
                    return null;
                }

                if (GameOffsets.Objects.UiElement.UiElementBaseFuncs.ShouldModifyPos(raw.Flags))
                {
                    position += new Vector2(parent.Raw.PositionModifier.X, parent.Raw.PositionModifier.Y) * parent.Raw.LocalScaleMultiplier;
                }

                if (raw.ParentPtr == frame)
                {
                    var unit = parent.Raw.LocalScaleMultiplier;
                    return unit > 0f && float.IsFinite(position.X) && float.IsFinite(position.Y) ? (position / unit, size / unit) : null;
                }

                raw = parent.Raw;
            }

            return null;
        }

        /// <summary>
        ///     Research (dev command "datkeys"): dat keys (row, table file) held in the first bytes of an element, its parent
        ///     and its first children, each with the row's id: which game data a widget stands for.
        /// </summary>
        public IEnumerable<string> DatKeys(IntPtr address)
        {
            const int ScanBytes = 0x1000;
            var targets = new List<(string Who, IntPtr At)> { ("self", address) };
            if (this.elements.TryReadElement(address, out var info))
            {
                targets.Add(("parent", info.Raw.ParentPtr));
                targets.AddRange(this.elements.ReadChildren(info).Take(6).Select((child, index) => ($"child {index}", child)));
            }

            yield return $"scanning @{address:X} \"{info.Name}\" with {targets.Count - 1} more";
            foreach (var (who, at) in targets)
            {
                for (var offset = 0; at != IntPtr.Zero && offset < ScanBytes; offset += 8)
                {
                    if (this.reader.TryRead<IntPtr>(at + offset, out var row) && row != IntPtr.Zero &&
                        this.reader.TryRead<IntPtr>(at + offset + 8, out var file) && file != IntPtr.Zero &&
                        this.elements.ReadWStringStructAt(file + GameLayout.SkillSlot.FileNameOffset) is { } table &&
                        table.StartsWith("Data/", StringComparison.OrdinalIgnoreCase))
                    {
                        var id = this.reader.TryRead<IntPtr>(row, out var text) ? this.elements.ReadUtf16BufferAt(text, 64) : null;
                        yield return $"{who} @{at:X} +0x{offset:X}: {table} row {id ?? "?"}";
                    }
                }
            }
        }

        /// <summary>Research (dev command "layout"): an element's parent chain with each step's scale kind, local scale and place.</summary>
        public IEnumerable<string> LayoutChain(IntPtr address)
        {
            for (var (at, depth) = (address, 0); at != IntPtr.Zero && depth < MaxLayoutDepth && this.elements.TryReadElement(at, out var info); (at, depth) = (info.Raw.ParentPtr, depth + 1))
            {
                var raw = info.Raw;
                var drawn = PluginUiElementReflection.TryGetAbsoluteRect(at, out var position, out var size) ? $"{position.X:F1},{position.Y:F1} {size.X:F1}x{size.Y:F1}" : "none";
                yield return FormattableString.Invariant(
                    $"{depth} @{at:X} \"{info.Name}\" index {raw.ScaleIndex} scale {raw.LocalScaleMultiplier:G6} at {raw.RelativePosition.X:G6},{raw.RelativePosition.Y:G6} size {raw.UnscaledSize.X:G6}x{raw.UnscaledSize.Y:G6} ") +
                    FormattableString.Invariant(
                    $"scroll {raw.PositionModifier.X:G6},{raw.PositionModifier.Y:G6} moves {GameOffsets.Objects.UiElement.UiElementBaseFuncs.ShouldModifyPos(raw.Flags)} visible {info.IsVisibleFlag} drawn {drawn} layout {this.LayoutRect(at)?.ToString() ?? "none"}");
            }
        }

        /// <summary>
        ///     Siblings in reading order: rows top to bottom, left to right within a row. The skill-crafting grid keeps
        ///     Contagion before Skeletal Sniper in the tree although Sniper is drawn first (2026-10-23). Laid-out rects,
        ///     so a window's size or a hidden item never changes the order; tree order when any of them has none.
        /// </summary>
        private List<UiElementReader.ElementInfo> InReadingOrder(List<UiElementReader.ElementInfo> items)
        {
            var placed = new List<(UiElementReader.ElementInfo Item, Vector2 Position, Vector2 Size)>(items.Count);
            foreach (var item in items)
            {
                if (this.LayoutRect(item.Address) is not { } rect)
                {
                    return items;
                }

                placed.Add((item, rect.Position, rect.Size));
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
