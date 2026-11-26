namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     The league encounter tracker on the HUD (<see cref="GameLayout.EncounterTracker"/>), for the vitals readout:
    ///     "Encuentro de ritual. 100 Tributo. 0 rituales restantes" — the tribute and any other league counter shown.
    /// </summary>
    public sealed class EncounterReader
    {
        /// <summary>The tracker is a handful of elements; this bounds a walk gone wrong after a game patch.</summary>
        private const int MaxNodes = 200;

        private readonly UiElementReader elements;
        private readonly List<EncounterLines.Node> nodes = new();
        private readonly Stack<(IntPtr Address, int Parent, int Depth)> stack = new();

        public EncounterReader(UiElementReader elements)
        {
            this.elements = elements;
        }

        /// <summary>The tracker's visible lines in screen order; empty when the HUD shows none.</summary>
        public IReadOnlyList<string> Lines(IntPtr hudLayer)
        {
            if (hudLayer == IntPtr.Zero || this.elements.FindNamed(hudLayer, GameLayout.EncounterTracker.ColumnName, GameLayout.EncounterTracker.SearchDepth) is not { } column || !this.elements.IsVisible(column))
            {
                return Array.Empty<string>();
            }

            // The whole visible tree, not only its texts: a legend's buttons are pictures beside a bare "+" ("Colocar explosivos"
            // under "Encuentro de expedición", live 2026-11-26, where only the "+" was said).
            var (nodes, stack) = (this.nodes, this.stack);
            nodes.Clear();
            stack.Clear();
            var children = this.elements.ReadChildren(column);
            for (var i = children.Length - 1; i >= 0; i--)
            {
                stack.Push((children[i], -1, 0));
            }

            while (stack.Count > 0 && nodes.Count < MaxNodes)
            {
                var (address, parent, depth) = stack.Pop();
                if (!this.elements.TryReadElement(address, out var info) || !info.IsVisibleFlag ||
                    GameLayout.EncounterTracker.SkippedNames.Contains(info.Name))
                {
                    continue;
                }

                var (text, tagged, glyph) = (string.Empty, string.Empty, string.Empty);
                if (this.elements.TryReadText(info, out var read, out var readTagged) && read.Trim().Length > 0)
                {
                    (text, tagged) = (read, readTagged);
                }
                else if (this.elements.ReadTextureKey(info) is { } key && ControllerGlyphs.FromTextureKey(key) is { } button)
                {
                    glyph = button;
                }

                var self = nodes.Count;
                nodes.Add(new EncounterLines.Node(parent, depth, text, tagged, glyph));
                var below = this.elements.ReadChildren(info);
                for (var i = below.Length - 1; i >= 0; i--)
                {
                    stack.Push((below[i], self, depth + 1));
                }
            }

            return EncounterLines.Build(nodes);
        }
    }
}
