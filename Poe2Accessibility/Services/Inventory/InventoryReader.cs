namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.Components;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>
    ///     Reads inventory widgets: the grid (cell under the controller cursor and the item there) and item
    ///     elements (the item entity behind them, read through GameHelper's components).
    /// </summary>
    public sealed class InventoryReader
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

        /// <summary>
        ///     "Scroll of Wisdom, 3" / "Glinting Wrapped Quarterstaff, magic"; null when the element holds no readable
        ///     item. <paramref name="tooltipTitle"/> is the item's own name (magic, rare, unique) and names items whose
        ///     base row has no display name (skill gems).
        /// </summary>
        public string? DescribeItem(IntPtr itemElement, string tooltipTitle = "")
        {
            return this.Describe(this.EntityOf(itemElement), tooltipTitle, true);
        }

        /// <summary>Same for an item entity (a ground item's WorldItem points to one); null when it is not an item.</summary>
        public string? DescribeItemEntity(IntPtr entity, string tooltipTitle = "") => this.Describe(entity, tooltipTitle, false);

        /// <summary>
        ///     Only a tooltip's title checks where base names are read (<see cref="BaseNames"/>): a ground item's label
        ///     is not known to hold the base name.
        /// </summary>
        private string? Describe(IntPtr entity, string tooltipTitle, bool fromTooltip)
        {
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity))
            {
                return null;
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            var rarity = item.TryGetComponent<Mods>(out var mods) ? mods.Rarity : Rarity.Normal;
            var name = string.Empty;
            if (item.TryGetComponent<Base>(out var baseComponent))
            {
                if (fromTooltip && rarity is Rarity.Normal or Rarity.Magic)
                {
                    this.CheckBaseName(baseComponent, tooltipTitle); // their titles hold the base name
                }

                name = this.BaseName(baseComponent);
            }

            if (name.Length == 0 || (rarity != Rarity.Normal && tooltipTitle.Length > 0))
            {
                name = tooltipTitle;
            }

            var count = item.TryGetComponent<Stack>(out var stack) ? stack.Count : 0;
            return Announcements.Item(name, rarity, count);
        }

        /// <summary>The base display name of an item entity ("Broquel de cuero"); empty if unreadable.</summary>
        public string ItemBaseName(IntPtr entity) =>
            entity != IntPtr.Zero && this.IsItemEntity(entity) &&
            ((Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!)
                .TryGetComponent<Base>(out var baseComponent)
                ? this.BaseName(baseComponent)
                : string.Empty;

        private static readonly int NameRowOffset = (int)Marshal.OffsetOf<BaseOffsets>(nameof(BaseOffsets.DisplayNameRowPtr));

        /// <summary>Read from the host's own GameOffsets at run time: a const would keep the value this plugin was built against.</summary>
        private static readonly int DisplayNameOffset =
            typeof(BaseOffsets).GetField(nameof(BaseOffsets.DisplayNameOffset))?.GetRawConstantValue() is int offset ? offset : BaseOffsets.DisplayNameOffset;

        /// <summary>Research (dev command "layout"): how item base names were judged.</summary>
        public string SelfTest() =>
            "item base names " + (!this.BaseNames.Judged ? "not judged yet (needs a normal or magic item's tooltip)"
                : this.BaseNames.Lost ? "lost (titles hold no name near where measured)"
                : this.BaseNames.Moved ? $"moved (row +0x{this.BaseNames.Offsets.Row:X}, name +0x{this.BaseNames.Offsets.Name:X})"
                : "ok");

        /// <summary>
        ///     The item's base display name; GameHelper's is empty for names under 8 characters. Once tooltips show the name
        ///     elsewhere it is read there; once they show it nowhere near, none is read (callers use the title).
        /// </summary>
        private string BaseName(Base component)
        {
            if (this.BaseNames.Lost)
            {
                return string.Empty;
            }

            if (!this.BaseNames.Moved && component.BaseItemName.Length > 0)
            {
                return component.BaseItemName;
            }

            var (row, name) = this.BaseNames.Offsets;
            return this.NameAt(component.Address, row, name);
        }

        /// <summary>GameHelper's layout read as a std::wstring: short names are stored inline (SSO), not behind a pointer.</summary>
        private string NameAt(IntPtr component, int rowOffset, int nameOffset) =>
            this.reader.TryRead<IntPtr>(component + rowOffset, out var row) && row != IntPtr.Zero && this.reader.IsValidAddress(row)
                ? this.elements.ReadWStringStructAt(row + nameOffset) ?? string.Empty
                : string.Empty;

        private void CheckBaseName(Base component, string title)
        {
            if (!this.BaseNames.Observe(component.Address, title, (row, name) => this.NameAt(component.Address, row, name)))
            {
                return;
            }

            var (row, name) = this.BaseNames.Offsets;
            if (this.BaseNames.Lost)
            {
                this.Report?.Missing("item base names");
            }
            else if (!this.BaseNames.Moved)
            {
                this.Report?.Found("item base names");
            }
            else
            {
                (var from, var to) = row != NameRowOffset ? (NameRowOffset, row) : (DisplayNameOffset, name);
                this.Report?.Moved("item base names", from, to);
            }
        }

        /// <summary>
        ///     Which drop sound an item on the ground gets — "gold", "currency", "quest", "rare", "unique" or
        ///     "item", from its metadata path (Metadata/Items/Currency/…, …/QuestItems/…) and its rarity — and its worth
        ///     for the loot filter (gems are valuable too).
        /// </summary>
        public (string Kind, LootTier Tier) ItemKind(IntPtr entity)
        {
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity))
            {
                return ("item", LootTier.Valuable);
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            var path = ItemPath(item);
            if (path.Contains("Gold", StringComparison.OrdinalIgnoreCase))
            {
                return ("gold", LootTier.Valuable);
            }

            if (path.Contains("/Currency/", StringComparison.Ordinal))
            {
                return ("currency", LootTier.Valuable);
            }

            if (path.Contains("Quest", StringComparison.Ordinal))
            {
                return ("quest", LootTier.Valuable);
            }

            if (!item.TryGetComponent<Mods>(out var mods))
            {
                return ("item", LootTier.Valuable); // worth unknown: kept audible
            }

            if (mods.Rarity is Rarity.Rare or Rarity.Unique)
            {
                return (mods.Rarity == Rarity.Unique ? "unique" : "rare", LootTier.Valuable);
            }

            return ("item", BuildGems.IsGemId(path) ? LootTier.Valuable : mods.Rarity == Rarity.Magic ? LootTier.Magic : LootTier.Normal);
        }

        /// <summary>A skill or support gem, uncut ones too.</summary>
        public bool IsGem(IntPtr entity) => BuildGems.IsGemId(this.ItemBaseId(entity));

        /// <summary>The item entity an item element shows; zero when it shows none.</summary>
        public IntPtr ItemEntity(IntPtr itemElement) => itemElement == IntPtr.Zero ? IntPtr.Zero : this.EntityOf(itemElement);

        /// <summary>The item's base type id (its metadata path, "Metadata/Items/Currency/CurrencyIdentification"); empty if unreadable.</summary>
        public string ItemBaseId(IntPtr entity) =>
            entity == IntPtr.Zero || !this.IsItemEntity(entity)
                ? string.Empty
                : ItemPath((Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!);

        private static readonly FieldInfo? ComponentMap = typeof(GameReader.RemoteObjects.States.InGameStateObjects.Entity)
            .GetField("componentAddresses", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>The item's components by name (the host's own lookup), empty when it is no item.</summary>
        public IReadOnlyDictionary<string, IntPtr> ItemComponents(IntPtr entity)
        {
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity))
            {
                return new Dictionary<string, IntPtr>();
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            return ComponentMap?.GetValue(item) is IEnumerable<KeyValuePair<string, IntPtr>> map
                ? map.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, IntPtr>();
        }

        /// <summary>The entity path without the "@level" suffix some item paths carry.</summary>
        private static string ItemPath(Item item)
        {
            var path = item.Path ?? string.Empty;
            var at = path.IndexOf('@', StringComparison.Ordinal);
            return at < 0 ? path : path[..at];
        }

        /// <summary>Item entity behind an item element, zero otherwise.</summary>
        private IntPtr EntityOf(IntPtr element) => this.EntityAt(element, this.Shift);

        private IntPtr EntityAt(IntPtr element, int shift) =>
            this.reader.TryRead<IntPtr>(element + GameLayout.InventoryItem.EntityOffset + shift, out var entity) && this.IsItemEntity(entity)
                ? entity
                : IntPtr.Zero;

        /// <summary>An entity whose details path is under Metadata/Items.</summary>
        private bool IsItemEntity(IntPtr entity)
        {
            if (!this.reader.IsValidAddress(entity) ||
                !this.reader.TryRead<ItemStruct>(entity, out var item) ||
                !this.reader.IsValidAddress(item.EntityDetailsPtr))
            {
                return false;
            }

            var path = this.elements.ReadWStringStructAt(item.EntityDetailsPtr + GameReader.Utils.HostStructs.Follow<EntityDetails>(DetailsPathOffset));
            return path != null && path.StartsWith(ItemPathPrefix, StringComparison.Ordinal);
        }
    }
}
