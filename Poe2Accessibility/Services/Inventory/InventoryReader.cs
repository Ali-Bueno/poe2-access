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

            // A rare or unique item's own name, from the words the item keeps: known with no tooltip.
            var words = mods != null && rarity is Rarity.Rare or Rarity.Unique ? this.NameFromWords(mods.Address) : string.Empty;
            if (fromTooltip && this.NameTrust.Observe(words, tooltipTitle))
            {
                this.Report?.Missing("item names");
            }

            if (tooltipTitle.Length == 0 && rarity != Rarity.Normal)
            {
                // No tooltip up (yet): the name its tooltip gave it before, so a second look says the same line as the first.
                tooltipTitle = this.TitleOf(entity);
            }

            if (words.Length > 0 && !this.NameTrust.Lost && (tooltipTitle.Length == 0 || !fromTooltip))
            {
                // No title was read (a ground label, a move, a tooltip not up yet): a ground label is not known to hold the name.
                tooltipTitle = words;
            }

            if (name.Length == 0 || (rarity != Rarity.Normal && tooltipTitle.Length > 0))
            {
                name = tooltipTitle;
            }

            if (fromTooltip && rarity != Rarity.Normal && tooltipTitle.Length > 0)
            {
                this.RememberTitle(entity, tooltipTitle);
            }

            if (name.Length == 0 && GemIndex.Shared.ByBaseItem(ItemPath(item)) is { } gem && !gem.NameIsPattern(Localization.Language))
            {
                // A gem's base row holds no display name and nothing shows its tooltip (a move, the hand, a pick-up):
                // the game files' name of the same base item, never a pattern the game fills in.
                name = gem.Name(Localization.Language);
            }

            var count = item.TryGetComponent<Stack>(out var stack) ? stack.Count : 0;
            return Announcements.Item(name, rarity, count);
        }

        /// <summary>Titles kept for this many items; past it the oldest readings go with the rest.</summary>
        private const int MaxTitles = 1024;

        private readonly Dictionary<IntPtr, string> titles = new();

        /// <summary>
        ///     The name a magic, rare or unique item's tooltip gave it while the cursor was on it, empty when it never was: a
        ///     move names the item by it, not by its base ("Escudo torre reforzado, raro" for Égida de glifo).
        /// </summary>
        public string TitleOf(IntPtr entity) => this.titles.GetValueOrDefault(entity, string.Empty);

        /// <summary>
        ///     Whether the item's line will change once its tooltip is read: a magic, rare or unique item is named by its
        ///     tooltip's title, and none was read for this one. A normal item's line is its base name either way.
        /// </summary>
        public bool NameAwaitsTooltip(IntPtr itemElement)
        {
            var entity = this.EntityOf(itemElement);
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity) || this.TitleOf(entity).Length > 0)
            {
                return false;
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            if (!item.TryGetComponent<Mods>(out var mods) || mods.Rarity == Rarity.Normal)
            {
                return false;
            }

            // A rare or unique item's words already give the name its tooltip will show: nothing to wait for.
            return this.NameTrust.Lost || mods.Rarity == Rarity.Magic || this.NameFromWords(mods.Address).Length == 0;
        }

        /// <summary>Whether the names made from items' words still agree with the tooltip titles read.</summary>
        public ItemNameTrust NameTrust { get; } = new();

        /// <summary>Names made per Mods component, with the vector they were made from: a new vector is another item's.</summary>
        private readonly Dictionary<IntPtr, (IntPtr Words, string Name)> wordNames = new();

        /// <summary>
        ///     A rare or unique item's name from the Words.dat rows its Mods component keeps
        ///     (<see cref="GameLayout.ItemNameWords"/>); empty when the vector is not one or two keys into that table or the
        ///     words do not make a name for certain (<see cref="ItemNameWords.Compose"/>).
        /// </summary>
        private string NameFromWords(IntPtr mods)
        {
            const int Vector = GameLayout.ItemNameWords.VectorOffset;
            const int KeySize = GameLayout.ItemNameWords.KeySize;
            if (!this.reader.TryRead<IntPtr>(mods + Vector, out var begin) || !this.reader.TryRead<IntPtr>(mods + Vector + IntPtr.Size, out var end))
            {
                return string.Empty;
            }

            if (this.wordNames.TryGetValue(mods, out var known) && known.Words == begin)
            {
                return known.Name;
            }

            var name = string.Empty;
            var bytes = end.ToInt64() - begin.ToInt64();
            if (begin != IntPtr.Zero && bytes > 0 && bytes % KeySize == 0 && bytes / KeySize <= GameLayout.ItemNameWords.MaxWords && this.reader.IsValidAddress(begin))
            {
                var words = new List<ItemNameWords.Word>();
                for (var key = begin; key.ToInt64() < end.ToInt64(); key += KeySize)
                {
                    if (DatKey.Row(this.reader, this.elements, key, GameLayout.ItemNameWords.Table) is not { } row)
                    {
                        words.Clear(); // not a key into Words.dat: not this field
                        break;
                    }

                    words.Add(new ItemNameWords.Word(this.WordText(row + GameLayout.ItemNameWords.LocalTextOffset), this.WordText(row + GameLayout.ItemNameWords.InflectionOffset)));
                }

                name = ItemNameWords.Compose(words);
            }

            if (this.wordNames.Count >= MaxTitles)
            {
                this.wordNames.Clear();
            }

            this.wordNames[mods] = (begin, name);
            return name;
        }

        private string WordText(IntPtr field) =>
            this.reader.TryRead<IntPtr>(field, out var text) && text != IntPtr.Zero && this.reader.IsValidAddress(text)
                ? this.elements.ReadUtf16BufferAt(text, GameLayout.ItemNameWords.MaxTextChars) ?? string.Empty
                : string.Empty;

        /// <summary>Keeps an item's tooltip title; also for the new entity the game makes of an item that changed inventory.</summary>
        public void RememberTitle(IntPtr entity, string title)
        {
            if (this.titles.Count >= MaxTitles && !this.titles.ContainsKey(entity))
            {
                this.titles.Clear();
            }

            this.titles[entity] = title;
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
            $"item names from words {(this.NameTrust.Lost ? "LOST" : "ok")} ({this.NameTrust.Agreed} titles agree, {this.NameTrust.Differed} differ); " +
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

        /// <summary>Research (dev command "focusinfo"): the host's Mods lists of an item, to set against its tooltip (docs/research/item-mods.md).</summary>
        public string ModsReport(IntPtr entity)
        {
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity))
            {
                return "no item";
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            if (!item.TryGetComponent<Mods>(out var mods))
            {
                return $"{ItemPath(item)}: no Mods component";
            }

            static string Listed(string kind, List<(string name, (float value0, float value1) values)> list) =>
                $"{kind}[{list.Count}] " + string.Join("; ", list.Select(mod => FormattableString.Invariant($"{mod.name} {mod.values.value0}/{mod.values.value1}")));
            return $"{ItemPath(item)} rarity={mods.Rarity} {Listed("implicit", mods.ImplicitMods)} | {Listed("explicit", mods.ExplicitMods)} | {Listed("enchant", mods.EnchantMods)}";
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
