namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
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
        ///     Research (dev command "itemmodscheck", docs/research/item-mods.md "Live check"): every worn item and every item
        ///     of the bag of the asking player, a line each: how its lines made from memory stood against its tooltip when
        ///     that was read, else the lines themselves; then the totals.
        /// </summary>
        public IReadOnlyList<string> ItemModsCheck()
        {
            var lines = new List<string>();
            int compared = 0, equal = 0, differ = 0, uncertain = 0, unread = 0, bare = 0;
            KeyValuePair<InventoryName, IntPtr>[] inventories;
            try
            {
                inventories = this.inventory.Locals?.Of(Seats.Input)?.ToArray() ?? Array.Empty<KeyValuePair<InventoryName, IntPtr>>();
            }
            catch (InvalidOperationException)
            {
                return new[] { "Item mods check: the host rebuilt its inventory list meanwhile; ask again" };
            }

            foreach (var (inventoryName, address) in inventories.Where(pair => pair.Key == InventoryName.MainInventory1 || IsEquipment(pair.Key)).OrderBy(pair => (int)pair.Key))
            {
                foreach (var item in ServerInventory.Items(this.reader, address).OrderBy(item => item.Y).ThenBy(item => item.X))
                {
                    var named = this.inventory.DescribeItemEntity(item.Entity, this.inventory.TitleOf(item.Entity)) ?? this.inventory.ItemBaseId(item.Entity);
                    var name = $"{named} ({inventoryName} {item.X},{item.Y})";
                    if (this.inventory.ItemMods(item.Entity) is not { } mods)
                    {
                        bare++;
                        lines.Add($"Item mods check: {name}: no modifiers in memory");
                        continue;
                    }

                    if (this.itemModChecks.TryGetValue(item.Entity, out var known) && known.Result is { } result)
                    {
                        compared++;
                        (equal, differ, uncertain) = (equal + result.Equal, differ + result.MemoryOnly.Count + result.TooltipOnly.Count, uncertain + result.Uncertain.Count);
                        lines.Add($"Item mods check: {name}: {result.Counts}{(result.Details.Length > 0 ? "; " + result.Details : string.Empty)}");
                        continue;
                    }

                    unread++;
                    var made = ItemModLines.Render(mods, Localization.Language, ModStatCatalog.Shared, ModTextCatalog.Shared);
                    lines.Add($"Item mods check: {name}: tooltip not read yet, {made.Count(line => line.Certain)} lines certain, {made.Count(line => !line.Certain)} uncertain: " +
                              string.Join(" | ", made.Select(line => line.Certain ? line.Text : $"?{line.Text} ({line.From})")));
                }
            }

            lines.Add($"Item mods check: {compared} items compared: {equal} lines equal, {differ} differ, {uncertain} uncertain; {unread} with no tooltip read, {bare} without modifiers");
            return lines;
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
