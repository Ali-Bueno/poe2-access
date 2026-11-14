namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>In-game archetypes (inventory, keyword links, panel titles) of <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Inventory grid → panel holding the title: 7 levels measured; room for deeper panels.</summary>
        private const int PanelTitleHops = 16;

        /// <summary>
        ///     What was learnt of the focused part of the interface by its elements' addresses (the map's own marker, the depth
        ///     toggle, a grid's tabs, what is no Atlas node) is forgotten: the focus left its layer or went away, and the
        ///     game gives an address to another element once a panel is rebuilt. Called by the focus tracker.
        /// </summary>
        public void ForgetFocusMemos()
        {
            this.gridTabs = (IntPtr.Zero, new List<IntPtr>(), 0);
            this.tooltipSettle.Forget();
            this.mapHere = default;
            this.depthToggle = default;
            this.notEndgame.Clear();
        }

        private AccessibleUiNode? DescribeInventory(in UiElementReader.ElementInfo info)
        {
            UiRole role;
            string text;
            var value = string.Empty;
            var emptyCell = false;
            List<string> details;
            var gear = (First: new List<string>(), Last: new List<string>(), Headed: false);
            var filling = false;
            if (this.inventory.TryReadGrid(info.Address, out var column, out var row, out var hovered) &&
                Announcements.IsCellGrid(this.inventory.InventoryOf(info.Address)) && this.HandSpan() is { Columns: > 0 } hand)
            {
                // An item in the hand: the cursor's cell is where its top left corner would go, and the item the grid
                // names is any one of those under it, so the place read as another item's and jumped about; each stop also
                // read that item whole, with its place in the build (user 2026-11-11, arranging the bag). Said instead: what
                // lies under the item held, and the cell.
                role = UiRole.GridCell;
                var under = this.inventory.ItemsUnder(info.Address, column, row, hand.Columns, hand.Rows);
                emptyCell = under.Count == 0;
                hovered = IntPtr.Zero;
                details = new List<string>();
                text = Announcements.UnderHand(under.Select(item => this.inventory.DescribeItem(item) ?? Announcements.UnknownItem).ToList());
                value = Announcements.GridPosition(column, row);
            }
            else if (this.inventory.TryReadGrid(info.Address, out column, out row, out hovered))
            {
                role = UiRole.GridCell;
                hovered = hovered != IntPtr.Zero ? hovered : this.inventory.ItemAt(info.Address, column, row);
                emptyCell = hovered == IntPtr.Zero;
                if (this.inventory.TryReadOrigin(info.Address, hovered, out var itemColumn, out var itemRow))
                {
                    // The item's own first cell, so the place is the item's and one item has one place.
                    (column, row) = (itemColumn, itemRow);
                }

                details = this.ItemTooltip(hovered);
                filling = this.tooltipSettle.Filling(
                    hovered,
                    details.Count,
                    details.Aggregate(0, (hash, line) => HashCode.Combine(hash, line)),
                    Environment.TickCount64,
                    details.Count == 0 && this.inventory.NameAwaitsTooltip(hovered));
                var item = hovered == IntPtr.Zero ? Announcements.EmptyCell : this.inventory.DescribeItem(hovered, Title(details)) ?? Announcements.UnknownItem;
                item = this.inventory.HighlightOf(hovered) == ItemHighlight.Match ? Announcements.HighlightedItem(item) : item;
                var inventoryName = this.inventory.InventoryOf(info.Address);
                (text, value) = Announcements.InventoryCell(inventoryName, item, column, row);
                gear = this.BuildGearLines(info.Address, inventoryName, column, this.inventory.ItemEntity(hovered), details.ToList());
                if (!filling)
                {
                    this.CheckItemMods(this.inventory.ItemEntity(hovered), item, details);
                }
            }
            else if (this.inventory.FindItemElement(info.Address) is var item && item != IntPtr.Zero)
            {
                role = UiRole.ItemSlot;
                details = this.ItemTooltip(item);
                text = this.inventory.DescribeItem(item, Title(details)) ?? Announcements.UnknownItem;
            }
            else
            {
                return null;
            }

            if (details.Count > 0 && text.Contains(details[0], StringComparison.Ordinal))
            {
                details.RemoveAt(0); // the name the announcement already gives
            }

            // A gem: its place in the Build Planner's active build, before what it does.
            details.InsertRange(0, this.GemItemLines(this.inventory.ItemEntity(role == UiRole.GridCell ? hovered : this.inventory.FindItemElement(info.Address))));

            // A piece of gear: how it fits the build's slot before its own lines, each wanted modifier after them.
            // A line the build judges is said once, with its verdict, where the item shows it (docs/research/sweep-2026-11-13.md).
            var after = gear.Last;
            if (!gear.Headed)
            {
                (details, after) = BuildGear.JudgedInPlace(details, gear.Last);
            }

            details.InsertRange(0, gear.First);
            details.AddRange(after);

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = role,
                Text = text,
                Value = value,
                IsVisible = this.elements.IsVisible(info),
                Context = role == UiRole.GridCell ? Announcements.InContext(this.PanelTitle(info), this.GridTab(info)) : this.PanelTitle(info),
                Tooltip = details,
                IsEmptyCell = emptyCell,
                TooltipFilling = filling,
            };
        }

        /// <summary>
        ///     Dev aid (`gridtrace`): the focused grid as the game holds it — the cursor's own cell, the item the grid names
        ///     with its first cell and size, the item lying over the cursor's cell, and how many items the hand holds.
        /// </summary>
        public string GridState(IntPtr address)
        {
            if (!this.inventory.TryReadGrid(address, out var column, out var row, out var hovered))
            {
                return "no grid";
            }

            string Name(IntPtr item) => item == IntPtr.Zero ? "none" : this.inventory.DescribeItem(item) ?? "?";
            var origin = this.inventory.TryReadOrigin(address, hovered, out var itemColumn, out var itemRow) ? $"{itemColumn},{itemRow}" : "-";
            var size = hovered != IntPtr.Zero && this.elements.TryReadElement(hovered, out var info) ? $"{info.Raw.UnscaledSize.X:0}x{info.Raw.UnscaledSize.Y:0}" : "-";
            return $"cell={column},{row} hovered={Name(hovered)}@{origin} size={size} lying={Name(this.inventory.ItemAt(address, column, row))} " +
                   $"inventory={this.inventory.InventoryOf(address)} hand={this.HandSpan()}";
        }

        /// <summary>The cells the item in the asking player's hand spans, columns and rows; (0, 0) with an empty hand.</summary>
        private (int Columns, int Rows) HandSpan()
        {
            try
            {
                return this.inventory.Locals?.Of(Seats.Input) is { } inventories && inventories.TryGetValue(GameReader.RemoteEnums.InventoryName.Cursor1, out var cursor)
                    ? ServerInventory.FirstSpan(this.reader, cursor)
                    : (0, 0);
            }
            catch (InvalidOperationException)
            {
                return (0, 0); // the host rebuilt its inventory list meanwhile
            }
        }

        private readonly TooltipSettle tooltipSettle = new();

        /// <summary>
        ///     The item's own tooltip (the item element owns it, built once the cursor rests on it): its name, then
        ///     "Stack Size: 3/40", "Identifies an item", ….
        /// </summary>
        private List<string> ItemTooltip(IntPtr itemElement) =>
            itemElement != IntPtr.Zero && this.elements.TryReadElement(itemElement, out var item)
                ? this.TooltipLines(item)
                : new List<string>();

        private static string Title(List<string> tooltip) => tooltip.Count > 0 ? tooltip[0] : string.Empty;
    }
}
