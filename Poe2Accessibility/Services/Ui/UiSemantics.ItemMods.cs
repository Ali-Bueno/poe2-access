namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>An item's modifiers as the host reads them, set against its tooltip, for <see cref="UiSemantics"/> (docs/research/item-mods.md).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The comparisons kept; past this many the oldest session's are dropped together.</summary>
        private const int MaxItemModChecks = 512;

        /// <summary>Each item whose tooltip was read, by entity: the tooltip it was compared with and how the lines made from memory stood against it.</summary>
        private readonly Dictionary<IntPtr, (int Tooltip, string Name, ItemModComparison? Result)> itemModChecks = new();

        /// <summary>Gets or sets where a difference between an item's memory and its tooltip is logged.</summary>
        public Action<string>? ItemModsLog { get; set; }

        /// <summary>Research (dev command "itemmods"): the focused item's modifiers as the host reads them, beside its tooltip's lines.</summary>
        public string FocusedItemMods(AccessibleUiNode focused)
        {
            var element = this.inventory.TryReadGrid(focused.Address, out var column, out var row, out var hovered)
                ? hovered != IntPtr.Zero ? hovered : this.inventory.ItemAt(focused.Address, column, row)
                : this.inventory.FindItemElement(focused.Address);
            return $"{this.inventory.ModsReport(this.inventory.ItemEntity(element))} || tooltip: {string.Join(" | ", this.ItemTooltip(element))}";
        }

        /// <summary>
        ///     Sets the lines made from an item's modifiers against the tooltip just read, once per item and tooltip: the
        ///     tooltip's lines are the ones used, and a difference is logged so it is seen.
        /// </summary>
        private void CheckItemMods(IntPtr entity, string name, IReadOnlyList<string> tooltip)
        {
            if (entity == IntPtr.Zero || tooltip.Count == 0 || ModStatCatalog.Shared.Count == 0)
            {
                return;
            }

            var read = tooltip.Aggregate(tooltip.Count, (hash, line) => HashCode.Combine(hash, line));
            if (this.itemModChecks.TryGetValue(entity, out var known) && known.Tooltip == read)
            {
                return;
            }

            if (this.itemModChecks.Count >= MaxItemModChecks && !this.itemModChecks.ContainsKey(entity))
            {
                this.itemModChecks.Clear();
            }

            // An item without modifiers (a rune, a currency) is kept too, so it is not read again at every look.
            var result = this.inventory.ItemMods(entity) is { } mods
                ? ItemModCheck.Compare(ItemModLines.Render(mods, Localization.Language, ModStatCatalog.Shared, ModTextCatalog.Shared), tooltip, ModTextCatalog.Shared)
                : null;
            this.itemModChecks[entity] = (read, name, result);
            if (result is { Differs: true })
            {
                this.ItemModsLog?.Invoke($"Item mods: {name} memory and tooltip differ: {result.Details}");
            }
        }
    }
}
