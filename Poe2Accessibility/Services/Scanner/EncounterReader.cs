namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The league encounter tracker on the HUD (<see cref="GameLayout.EncounterTracker"/>), for the vitals readout:
    ///     "Encuentro de ritual. 100 Tributo. 0 rituales restantes" — the tribute and any other league counter shown.
    /// </summary>
    public sealed class EncounterReader
    {
        /// <summary>The tracker is a handful of elements; this bounds a walk gone wrong after a game patch.</summary>
        private const int MaxNodes = 200;

        private readonly UiElementReader elements;

        public EncounterReader(UiElementReader elements)
        {
            this.elements = elements;
        }

        /// <summary>The tracker's visible lines in screen order; empty when the HUD shows none.</summary>
        public IReadOnlyList<string> Lines(IntPtr hudLayer)
        {
            var lines = new List<string>();
            if (hudLayer == IntPtr.Zero || this.elements.FindNamed(hudLayer, GameLayout.EncounterTracker.ColumnName, GameLayout.EncounterTracker.SearchDepth) is not { } column || !this.elements.IsVisible(column))
            {
                return lines;
            }

            var stack = new Stack<IntPtr>(Enumerable.Reverse(this.elements.ReadChildren(column)));
            for (var visited = 0; stack.Count > 0 && visited < MaxNodes; visited++)
            {
                if (!this.elements.TryReadElement(stack.Pop(), out var info) || !info.IsVisibleFlag ||
                    GameLayout.EncounterTracker.SkippedNames.Contains(info.Name))
                {
                    continue;
                }

                if (this.elements.TryReadText(info.Address, out var text) && text.Trim() is { Length: > 0 } line && !lines.Contains(line))
                {
                    lines.Add(line);
                }

                foreach (var child in Enumerable.Reverse(this.elements.ReadChildren(info)))
                {
                    stack.Push(child);
                }
            }

            return lines;
        }
    }
}
