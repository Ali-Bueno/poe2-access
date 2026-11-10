namespace Poe2Accessibility.Services
{
    using System;
    using Poe2Accessibility.Models;

    /// <summary>Research on an item's modifiers as the host reads them, for <see cref="UiSemantics"/> (docs/research/item-mods.md).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Research (dev command "focusinfo"): the focused item's modifiers as the host reads them, beside its tooltip's lines.</summary>
        public string FocusedItemMods(AccessibleUiNode focused)
        {
            var element = this.inventory.TryReadGrid(focused.Address, out var column, out var row, out var hovered)
                ? hovered != IntPtr.Zero ? hovered : this.inventory.ItemAt(focused.Address, column, row)
                : this.inventory.FindItemElement(focused.Address);
            return $"{this.inventory.ModsReport(this.inventory.ItemEntity(element))} || tooltip: {string.Join(" | ", this.ItemTooltip(element))}";
        }
    }
}
