namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameHelper.Plugin;

    /// <summary>Selector widgets without a focus (the class choice of character creation) of <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Layer → selector widget → split frame → icon frame → icon grid: where the selector sits and its icons are.</summary>
        private const int SelectorSearchDepth = 4;

        /// <summary>An icon grid has at least this many children.</summary>
        private const int MinSelectorIcons = 4;

        /// <summary>Icons whose tops differ by less than this many pixels are on the same row.</summary>
        private const float SameRowPixels = 4f;

        private readonly Dictionary<IntPtr, (IntPtr Selector, IntPtr Grid)> selectors = new();

        /// <summary>
        ///     "4 de 6" for the class choice of character creation: no control holds the focus, the left stick moves a
        ///     highlight along a row of class icons (the second row holds the variants, swapped in with X).
        ///     The selector keeps the highlighted index (GameLayout.ClassSelector); the count is the icons in its row.
        ///     Null when the layer has no such selector or its index is out of range.
        /// </summary>
        public string? SelectorPosition(IntPtr layer)
        {
            if (!this.selectors.TryGetValue(layer, out var found) ||
                (found.Selector != IntPtr.Zero && !(this.elements.TryReadElement(found.Selector, out var cached) && cached.Name == GameLayout.ClassSelector.Name)))
            {
                found = this.FindSelector(layer);
                this.selectors[layer] = found;
            }

            if (found.Selector == IntPtr.Zero || !this.reader.TryRead<int>(found.Selector + GameLayout.ClassSelector.IndexOffset, out var index) ||
                !this.elements.TryReadElement(found.Grid, out var grid))
            {
                return null;
            }

            var icons = this.elements.ReadChildren(grid);
            if (index < 0 || index >= icons.Length || !PluginUiElementReflection.TryGetAbsoluteRect(icons[index], out var current, out _))
            {
                return null;
            }

            // The row the highlight is on: the classes, or their variants once X swapped one (the stick then walks those).
            var row = Enumerable.Range(0, icons.Length)
                .Where(at => PluginUiElementReflection.TryGetAbsoluteRect(icons[at], out var place, out _) && Math.Abs(place.Y - current.Y) < SameRowPixels)
                .ToList();
            return Announcements.ListPosition(row.IndexOf(index) + 1, row.Count);
        }

        /// <summary>
        ///     The class the selector shows (GameLayout.ClassSelector id): X in the name step swaps it for its variant
        ///     (Exploradora 2 → Cazadora 8, live 2026-10-24) without any focus change of its own. Null without a selector.
        /// </summary>
        public int? SelectorChoice(IntPtr layer)
        {
            if (!this.selectors.TryGetValue(layer, out var found))
            {
                found = this.FindSelector(layer);
                this.selectors[layer] = found;
            }

            return found.Selector != IntPtr.Zero && this.reader.TryRead<int>(found.Selector + GameLayout.ClassSelector.ClassIdOffset, out var id) ? id : null;
        }

        private (IntPtr Selector, IntPtr Grid) FindSelector(IntPtr layer)
        {
            if (this.FindNamed(layer, GameLayout.ClassSelector.Name, SelectorSearchDepth) is not { } selector ||
                !this.elements.TryReadElement(selector, out var info))
            {
                return (IntPtr.Zero, IntPtr.Zero);
            }

            // The icon grid is the descendant with the most children (12 icons; the class info beside it has 5).
            var best = (Address: IntPtr.Zero, Count: MinSelectorIcons - 1);
            var queue = new Queue<(UiElementReader.ElementInfo Info, int Depth)>();
            queue.Enqueue((info, 0));
            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                var children = this.elements.ReadChildren(current);
                if (depth > 0 && children.Length > best.Count)
                {
                    best = (current.Address, children.Length);
                }

                if (depth < SelectorSearchDepth)
                {
                    foreach (var child in children)
                    {
                        if (this.elements.TryReadElement(child, out var childInfo))
                        {
                            queue.Enqueue((childInfo, depth + 1));
                        }
                    }
                }
            }

            return best.Address == IntPtr.Zero ? (IntPtr.Zero, IntPtr.Zero) : (selector, best.Address);
        }

        /// <summary>The first element with this control name under <paramref name="root"/>, breadth first.</summary>
        private IntPtr? FindNamed(IntPtr root, string name, int maxDepth)
        {
            var queue = new Queue<(IntPtr Address, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (address, depth) = queue.Dequeue();
                if (!this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (info.Name == name)
                {
                    return address;
                }

                if (depth < maxDepth)
                {
                    foreach (var child in this.elements.ReadChildren(info))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return null;
        }
    }
}
