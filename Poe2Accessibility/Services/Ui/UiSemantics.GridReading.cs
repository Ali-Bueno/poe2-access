namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>The focused grid said whole for <see cref="UiSemantics"/> (docs/controls.md, "Hearing a grid at once").</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>
        ///     Every item of the grid the cursor is in, read from the grid itself, so the cursor stays; null when the focus
        ///     is in no grid. An item is named as far as it is known: by its tooltip's name once read, else by its base.
        /// </summary>
        /// <param name="searchLit">The panel's search lights or dims the grid's items: the game's own hold clears it.</param>
        public string? ReadGrid(AccessibleUiNode focused, out bool searchLit)
        {
            searchLit = false;
            if (focused.Role != UiRole.GridCell || !this.inventory.TryReadGrid(focused.Address, out _, out _, out _))
            {
                return null;
            }

            var inventoryName = this.inventory.InventoryOf(focused.Address);
            var active = inventoryName is { } own && IsEquipment(own) ? null : this.GearPlan();
            var worn = active == null ? null : this.WornGear().Worn;
            var items = new List<GridItem>();
            foreach (var (element, column, row) in this.inventory.ItemsOf(focused.Address))
            {
                searchLit |= this.inventory.HighlightOf(element) != ItemHighlight.None;
                var entity = this.inventory.ItemEntity(element);
                var named = this.inventory.DescribeItemEntity(entity, this.inventory.TitleOf(entity)) ?? Announcements.UnknownItem;
                var (text, place) = Announcements.InventoryCell(inventoryName, named, column, row);
                var (onBase, fit) = active is { } plan ? this.BuildFitByBase(plan, worn!, entity) : (false, new List<string>());
                items.Add(new GridItem(text, column, row, place, onBase, fit));
            }

            return GridReading.Say(items);
        }

        /// <summary>Research (dev command "focusinfo"): the base ids of the focused grid's items.</summary>
        public string GridBaseIds(AccessibleUiNode focused) =>
            string.Join(" | ", this.inventory.ItemsOf(focused.Address).Select(item => this.inventory.ItemBaseId(this.inventory.ItemEntity(item.Item))));

        /// <summary>What the build says of an item by its base alone, per slot it asks for: nothing of its modifiers, which only a tooltip gives.</summary>
        private (bool OnBase, List<string> Lines) BuildFitByBase((BuildPlan Plan, int Level) active, Dictionary<(string Slot, int X), IntPtr> worn, IntPtr entity)
        {
            var (onBase, lines, verdicts) = (false, new List<string>(), new HashSet<string>());
            GearFit Fit(BuildGearEntry entry, IntPtr item) => BuildGear.Fit(entry, this.GearItemOf(item, null), BuildGear.Bases, BuildGear.Texts, Localization.Language);
            foreach (var slot in BuildGear.SlotsFor(this.inventory.ItemBaseId(entity)))
            {
                if (BuildGear.EntryFor(active.Plan, slot.Slot, slot.X, active.Level) is not { } entry)
                {
                    continue;
                }

                var fit = Fit(entry, entity);
                var held = worn.GetValueOrDefault(slot);
                // Two slots with one verdict (both weapon sets, both rings) are said for the first: the list stays short.
                if (Announcements.BuildGearItem(fit, held == IntPtr.Zero ? null : Fit(entry, held), active.Level) is { Length: > 0 } line &&
                    verdicts.Add(line[(line.IndexOf(": ", StringComparison.Ordinal) + 1)..]))
                {
                    lines.Add(line);
                    onBase |= fit.Base == BaseFit.Same;
                }
            }

            return (onBase, lines);
        }
    }
}
