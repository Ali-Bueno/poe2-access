namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Control groups of <see cref="UiSemantics"/>: whether a text lies outside the focus's own group, row or shop card.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>
        ///     Whether a text belongs to another group of controls than the focus: under the branch where the two part,
        ///     the text's side holds something the cursor lands on. The skill bind screen opens on a slot of the primary
        ///     set with the spirit panel, the secondary set's caption and the targeting mode beside it: those are read
        ///     when the cursor gets there, not with the prompt.
        /// </summary>
        public bool InOtherControlGroup(IntPtr text, IntPtr focus)
        {
            if (!this.elements.TryReadElement(text, out var info) || !this.elements.TryReadElement(focus, out var focused))
            {
                return false;
            }

            var focusChain = new HashSet<IntPtr>(this.elements.ReadAncestors(focused, PanelTitleHops).Select(ancestor => ancestor.Address)) { focus };
            var branch = info;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                if (focusChain.Contains(ancestor.Address))
                {
                    return branch.Address != text && this.HoldsNavigable(branch);
                }

                branch = ancestor;
            }

            return false;
        }

        /// <summary>
        ///     Whether a text lies in another row of the list the focus is a row of (the next trade listing scrolling
        ///     in), not in a panel beside the list (a skill's details beside the engrave grid).
        /// </summary>
        /// <summary>
        ///     The text lies in another card of the shop's list than the focused one: a card drawn just after a scroll (its
        ///     "¡NUEVO!" flag came 200 ms later and was said alone), read when the cursor reaches it.
        /// </summary>
        public bool InAnotherShopCard(IntPtr text, IntPtr focus)
        {
            if (!this.elements.TryReadElement(text, out var info))
            {
                return false;
            }

            // With the cursor on no card (a category just opened, the item page over the list) every card is another one.
            foreach (var ancestor in this.elements.ReadAncestors(info, ShopEntryDepth))
            {
                if (ancestor.Name == GameLayout.InGameUi.ShopEntryName)
                {
                    return ancestor.Address != focus;
                }
            }

            return false;
        }

        public bool InSiblingRow(IntPtr text, IntPtr focus)
        {
            if (!this.elements.TryReadElement(text, out var info) || !this.elements.TryReadElement(focus, out var focused))
            {
                return false;
            }

            var list = focused.Raw.ParentPtr;
            var branch = info;
            foreach (var ancestor in this.elements.ReadAncestors(info, PanelTitleHops))
            {
                if (ancestor.Address == list)
                {
                    return branch.Address != focus && branch.Address != text;
                }

                branch = ancestor;
            }

            return false;
        }

        private bool HoldsNavigable(in UiElementReader.ElementInfo root)
        {
            var stack = new Stack<UiElementReader.ElementInfo>();
            stack.Push(root);
            for (var budget = MaxTooltipNodes; stack.Count > 0 && budget > 0; budget--)
            {
                var node = stack.Pop();
                if (node.HasFlagBit(GameLayout.Element.NavigableFlagBit))
                {
                    return true;
                }

                foreach (var child in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(child, out var element) && element.IsVisibleFlag)
                    {
                        stack.Push(element);
                    }
                }
            }

            return false;
        }
    }
}
