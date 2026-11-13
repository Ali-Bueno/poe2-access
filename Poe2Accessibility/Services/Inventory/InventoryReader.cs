namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Reads inventory widgets: the grid (cell under the controller cursor and the item there) and item
    ///     elements (the item entity behind them, read through GameHelper's components).
    /// </summary>
    public sealed partial class InventoryReader
    {
        private const string ItemPathPrefix = "Metadata/Items";

        /// <summary>Plausible grid layout: cell size in layout units (80 measured) and cells per side.</summary>
        private const float MinCellSize = 8;

        private const float MaxCellSize = 512;
        private const int MaxGridCells = 64;

        /// <summary>Items read of one grid: a quad stash tab holds 24 by 24 cells.</summary>
        private const int MaxGridItems = 576;

        /// <summary>Slot element → item element depth (slot → frame → item).</summary>
        private const int ItemSearchDepth = 2;

        private static readonly int DetailsPathOffset = (int)Marshal.OffsetOf<EntityDetails>(nameof(EntityDetails.name));

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;

        /// <summary>Where the grid and item element fields are after a game update; as measured when null.</summary>
        public ControlFields? Controls { get; set; }

        /// <summary>Where a base name that moved or was lost after a game update is reported (docs/compatibility.md).</summary>
        public CompatibilityReport? Report { get; set; }

        /// <summary>Where item base names are read, checked against tooltip titles.</summary>
        public BaseNameField BaseNames { get; } = new(NameRowOffset, DisplayNameOffset);

        private int Shift => this.Controls?.Current(ControlGroup.Inventory) ?? 0;

        /// <summary>Each local player's server inventories: a grid may show player 2's (local co-op).</summary>
        public LocalInventories? Locals { get; set; }

        public InventoryReader(HostMemoryReader reader, UiElementReader elements)
        {
            this.reader = reader;
            this.elements = elements;
        }

        /// <summary>The grid's cursor cell (zero-based) and the item element on it (zero on an empty cell).</summary>
        public bool TryReadGrid(IntPtr element, out int column, out int row, out IntPtr item)
        {
            // Only a cursor on an item proves where the fields are: an empty cell reads sensibly almost anywhere.
            var shift = this.Controls?.Shift(ControlGroup.Inventory, element, at => this.GridAt(element, at, out _, out _, out var held) && held != IntPtr.Zero) ?? 0;
            return this.GridAt(element, shift, out column, out row, out item);
        }

        private bool GridAt(IntPtr element, int shift, out int column, out int row, out IntPtr item)
        {
            column = row = 0;
            item = IntPtr.Zero;
            var block = this.reader.ReadBytes(element + GameLayout.InventoryGrid.CellSizeOffset + shift,
                GameLayout.InventoryGrid.CellOffset + (2 * sizeof(int)) - GameLayout.InventoryGrid.CellSizeOffset);
            if (block.Length == 0)
            {
                return false;
            }

            var span = block.AsSpan();
            var cellSize = MemoryMarshal.Read<float>(span);
            item = MemoryMarshal.Read<IntPtr>(span[(GameLayout.InventoryGrid.HoveredItemOffset - GameLayout.InventoryGrid.CellSizeOffset)..]);
            column = MemoryMarshal.Read<int>(span[(GameLayout.InventoryGrid.CellOffset - GameLayout.InventoryGrid.CellSizeOffset)..]);
            row = MemoryMarshal.Read<int>(span[(GameLayout.InventoryGrid.CellOffset + sizeof(int) - GameLayout.InventoryGrid.CellSizeOffset)..]);
            return cellSize is >= MinCellSize and <= MaxCellSize &&
                column is >= 0 and < MaxGridCells && row is >= 0 and < MaxGridCells &&
                (item == IntPtr.Zero || this.EntityAt(item, shift) != IntPtr.Zero);
        }

        /// <summary>Which player inventory a grid shows (main, flasks, helmet…); null when GameHelper does not list it.</summary>
        public InventoryName? InventoryOf(IntPtr grid)
        {
            if (!this.reader.TryRead<IntPtr>(grid + GameLayout.InventoryGrid.ServerInventoryOffset + this.Shift, out var inventory) || inventory == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                foreach (var seat in new[] { 1, 2 })
                {
                    foreach (var (name, address) in this.Locals?.Of(seat) ?? new Dictionary<InventoryName, IntPtr>())
                    {
                        if (address == inventory)
                        {
                            return name;
                        }
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // GameHelper rebuilt the map while we enumerated it; the next poll asks again.
            }

            return null;
        }

        /// <summary>What the panel's search makes of an item; <see cref="ItemHighlight.None"/> when the field does not read as measured.</summary>
        public ItemHighlight HighlightOf(IntPtr itemElement)
        {
            if (itemElement == IntPtr.Zero || !this.reader.TryRead<uint>(itemElement + GameLayout.InventoryItem.HighlightOffset + this.Shift, out var field))
            {
                return ItemHighlight.None;
            }

            var (state, dimmed) = ((byte)field, (byte)(field >> (8 * GameLayout.InventoryItem.DimmedAfterHighlight)));
            return (state, dimmed) switch
            {
                ((byte)ItemHighlight.Match, 0) => ItemHighlight.Match,
                ((byte)ItemHighlight.Dimmed, 1) => ItemHighlight.Dimmed,
                _ => ItemHighlight.None,
            };
        }

        /// <summary>The grid's item elements with their cell (zero-based column and row), in reading order.</summary>
        public List<(IntPtr Item, int Column, int Row)> ItemsOf(IntPtr grid)
        {
            var items = new List<(IntPtr Item, int Column, int Row)>();
            if (!this.elements.TryReadElement(grid, out var info) ||
                !this.reader.TryRead<float>(grid + GameLayout.InventoryGrid.CellSizeOffset + this.Shift, out var cell) || cell is < MinCellSize or > MaxCellSize)
            {
                return items;
            }

            foreach (var child in this.elements.ReadChildren(info).Take(MaxGridItems))
            {
                if (this.EntityOf(child) != IntPtr.Zero && this.elements.TryReadElement(child, out var item))
                {
                    items.Add((child, (int)MathF.Round(item.Raw.RelativePosition.X / cell), (int)MathF.Round(item.Raw.RelativePosition.Y / cell)));
                }
            }

            return items.OrderBy(item => item.Row).ThenBy(item => item.Column).ToList();
        }

        /// <summary>
        ///     The cell an item of the grid starts at (zero-based), read from the item itself: the grid's cursor cell lags the
        ///     item it names by a frame and lies anywhere on an item of several cells, so one item had several places.
        /// </summary>
        public bool TryReadOrigin(IntPtr grid, IntPtr item, out int column, out int row)
        {
            column = row = 0;
            if (item == IntPtr.Zero || !this.elements.TryReadElement(item, out var info) || info.Raw.ParentPtr != grid ||
                !this.reader.TryRead<float>(grid + GameLayout.InventoryGrid.CellSizeOffset + this.Shift, out var cell) || cell is < MinCellSize or > MaxCellSize)
            {
                return false;
            }

            (column, row) = ((int)MathF.Round(info.Raw.RelativePosition.X / cell), (int)MathF.Round(info.Raw.RelativePosition.Y / cell));
            return column is >= 0 and < MaxGridCells && row is >= 0 and < MaxGridCells;
        }

        /// <summary>
        ///     The item element lying over a cell, zero when none does: the grid names its hovered item late on arrival, drops it
        ///     early on leaving, and not at all while a currency is being applied to an equipment slot (2026-11-10).
        /// </summary>
        public IntPtr ItemAt(IntPtr grid, int column, int row)
        {
            if (!this.reader.TryRead<float>(grid + GameLayout.InventoryGrid.CellSizeOffset + this.Shift, out var cell))
            {
                return IntPtr.Zero;
            }

            foreach (var (item, itemColumn, itemRow) in this.ItemsOf(grid))
            {
                if (this.elements.TryReadElement(item, out var info) &&
                    column >= itemColumn && column < itemColumn + Math.Max(1, (int)MathF.Round(info.Raw.UnscaledSize.X / cell)) &&
                    row >= itemRow && row < itemRow + Math.Max(1, (int)MathF.Round(info.Raw.UnscaledSize.Y / cell)))
                {
                    return item;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The grid's item elements lying under a block of cells, in reading order: what an item in the hand would be put
        ///     on, its top left corner on the cursor's cell.
        /// </summary>
        public List<IntPtr> ItemsUnder(IntPtr grid, int column, int row, int columns, int rows)
        {
            var under = new List<IntPtr>();
            if (!this.reader.TryRead<float>(grid + GameLayout.InventoryGrid.CellSizeOffset + this.Shift, out var cell) || cell is < MinCellSize or > MaxCellSize)
            {
                return under;
            }

            foreach (var (item, itemColumn, itemRow) in this.ItemsOf(grid))
            {
                if (this.elements.TryReadElement(item, out var info) &&
                    GridBlocks.Overlap(column, row, columns, rows, itemColumn, itemRow,
                        Math.Max(1, (int)MathF.Round(info.Raw.UnscaledSize.X / cell)), Math.Max(1, (int)MathF.Round(info.Raw.UnscaledSize.Y / cell))))
                {
                    under.Add(item);
                }
            }

            return under;
        }

        /// <summary>Item element held by an equipment/flask slot (or the slot itself); zero when the slot is empty.</summary>
        public IntPtr FindItemElement(IntPtr slot, int depth = 0)
        {
            if (this.EntityOf(slot) != IntPtr.Zero)
            {
                return slot;
            }

            if (depth >= ItemSearchDepth || !this.elements.TryReadElement(slot, out var info))
            {
                return IntPtr.Zero;
            }

            foreach (var child in this.elements.ReadChildren(info))
            {
                var found = this.FindItemElement(child, depth + 1);
                if (found != IntPtr.Zero)
                {
                    return found;
                }
            }

            return IntPtr.Zero;
        }
    }
}
