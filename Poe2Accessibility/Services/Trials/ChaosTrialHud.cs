namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     What the Trial of Chaos writes on the screen, found by the names the game gives its elements: the exe of 0.5.5f holds
    ///     "UltimatumTitle", "UltimatumLabel", "UltimatumProgressBarLabel", "UltimatumTimerLabel", "UltimatumReturnToRingLabel",
    ///     "UltimatumPopupLabel" and "UltimatumVoteCountLabel", and the round's objective, its timer and the "Return to the Ring"
    ///     banner are in no table a reader can reach. Whatever is visible under such a name is read as text; nothing is assumed of
    ///     where it hangs in the tree.
    /// </summary>
    public sealed class ChaosTrialHudReader
    {
        /// <summary>The start of the names of the Trial's elements.</summary>
        public const string NamePrefix = "Ultimatum";

        /// <summary>The whole interface is walked, hidden branches left out: this bounds a walk gone wrong after a game patch.</summary>
        private const int MaxNodes = 6000;

        private readonly UiElementReader elements;

        public ChaosTrialHudReader(UiElementReader elements)
        {
            this.elements = elements;
        }

        /// <summary>The visible elements of the Trial with their text, in tree order; none outside one.</summary>
        public IReadOnlyList<(string Name, string Text)> Visible(IntPtr container)
        {
            var found = new List<(string Name, string Text)>();
            if (container == IntPtr.Zero)
            {
                return found;
            }

            var stack = new Stack<IntPtr>();
            stack.Push(container);
            for (var visited = 0; stack.Count > 0 && visited < MaxNodes; visited++)
            {
                if (!this.elements.TryReadElement(stack.Pop(), out var info) || !info.IsVisibleFlag)
                {
                    continue;
                }

                if (info.Name is { Length: > 0 } name && name.StartsWith(NamePrefix, StringComparison.Ordinal) &&
                    this.elements.TryReadText(info.Address, out var text) && text.Trim() is { Length: > 0 } line)
                {
                    found.Add((name, line));
                }

                var children = this.elements.ReadChildren(info);
                for (var index = children.Length - 1; index >= 0; index--)
                {
                    stack.Push(children[index]);
                }
            }

            return found;
        }
    }
}
