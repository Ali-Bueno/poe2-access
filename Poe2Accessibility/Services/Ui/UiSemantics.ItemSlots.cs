namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Item slots of windows no inventory table names (a bench's inputs): the slot's place among its sibling slots, and an empty one named.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>A parent with more children than this is a page of rows, not a few slots side by side.</summary>
        private const int MaxSlotSiblings = 24;

        /// <summary>The slot's place among the visible siblings of its class ("2 of 3"), and whether any of them holds an item; empty place when it stands alone.</summary>
        private (string Position, bool AnyItem) SlotSiblings(in UiElementReader.ElementInfo slot)
        {
            if (!this.elements.TryReadElement(slot.Raw.ParentPtr, out var parent))
            {
                return (string.Empty, false);
            }

            var children = this.elements.ReadChildren(parent);
            if (children.Length > MaxSlotSiblings)
            {
                return (string.Empty, false);
            }

            var siblings = new List<UiElementReader.ElementInfo>();
            foreach (var childAddress in children)
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag && child.Raw.Vtable == slot.Raw.Vtable)
                {
                    siblings.Add(child);
                }
            }

            siblings = this.InReadingOrder(siblings);
            var address = slot.Address;
            var index = siblings.FindIndex(sibling => sibling.Address == address);
            return siblings.Count < 2 || index < 0
                ? (string.Empty, false)
                : (Announcements.ListPosition(index + 1, siblings.Count), siblings.Any(sibling => this.inventory.FindItemElement(sibling.Address) != IntPtr.Zero));
        }

        /// <summary>The place of a slot holding nothing beside slots that hold items: it is an empty slot, not an element without a name; empty otherwise.</summary>
        private string EmptySlotPlace(in UiElementReader.ElementInfo slot)
        {
            var (position, anyItem) = this.SlotSiblings(slot);
            return anyItem && this.inventory.FindItemElement(slot.Address) == IntPtr.Zero && this.FindDescendantText(slot, CaptionSearchDepth + 1, null).Length == 0
                ? position
                : string.Empty;
        }
    }
}
