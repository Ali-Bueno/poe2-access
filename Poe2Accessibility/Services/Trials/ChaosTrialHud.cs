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
        private readonly List<IntPtr> named = new();

        public ChaosTrialHudReader(UiElementReader elements)
        {
            this.elements = elements;
        }

        /// <summary>A new area: the elements found are another screen's.</summary>
        public void Reset() => this.named.Clear();

        /// <summary>
        ///     Finds the Trial's elements, shown or not, under what is on screen: the costly walk, for every few seconds. A banner
        ///     hidden inside a shown panel is found here and then watched by <see cref="Visible"/>, which is cheap.
        /// </summary>
        public void Discover(IntPtr container)
        {
            this.named.Clear();
            if (container == IntPtr.Zero)
            {
                return;
            }

            var stack = new Stack<IntPtr>();
            stack.Push(container);
            for (var visited = 0; stack.Count > 0 && visited < MaxNodes; visited++)
            {
                if (!this.elements.TryReadElement(stack.Pop(), out var info))
                {
                    continue;
                }

                if (info.Name is { Length: > 0 } name && name.StartsWith(NamePrefix, StringComparison.Ordinal))
                {
                    this.named.Add(info.Address);
                }

                if (!info.IsVisibleFlag)
                {
                    continue; // nothing under a hidden branch is on screen
                }

                var children = this.elements.ReadChildren(info);
                for (var index = children.Length - 1; index >= 0; index--)
                {
                    stack.Push(children[index]);
                }
            }
        }

        /// <summary>The Trial's elements on screen now, with their text, of those last found; none outside a Trial.</summary>
        public IReadOnlyList<(string Name, string Text)> Visible()
        {
            var found = new List<(string Name, string Text)>();
            foreach (var address in this.named)
            {
                if (this.elements.TryReadElement(address, out var info) && info.Name is { Length: > 0 } name && name.StartsWith(NamePrefix, StringComparison.Ordinal) &&
                    this.elements.IsVisible(info) && this.elements.TryReadText(info.Address, out var text) && text.Trim() is { Length: > 0 } line)
                {
                    found.Add((name, line));
                }
            }

            return found;
        }
    }
}
