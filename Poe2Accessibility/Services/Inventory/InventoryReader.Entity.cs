namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.States.InGameState;

    /// <summary>Item entity reading of <see cref="InventoryReader"/>: base names, kind and tier, components and mods.</summary>
    public sealed partial class InventoryReader
    {
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

        /// <summary>Research (dev command "itemmods"): the host's Mods lists of an item, to set against its tooltip (docs/research/item-mods.md).</summary>
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

            // Each entry "Id v/v/…" with every value it holds, "Id -" for none.
            static string Listed(string kind, List<(string name, int[] values)> list) =>
                $"{kind}[{list.Count}] " + string.Join("; ", list.Select(mod => $"{mod.name} {(mod.values.Length == 0 ? "-" : string.Join('/', mod.values))}"));
            return $"{ItemPath(item)} rarity={mods.Rarity} {Listed("implicit", mods.ImplicitModValues)} | {Listed("explicit", mods.ExplicitModValues)} | " +
                   $"{Listed("enchant", mods.EnchantModValues)} | {Listed("other", mods.HellscapeModValues)}";
        }

        /// <summary>
        ///     The item's modifiers as the host's Mods component holds them (GameHelper2's layout, set against nine tooltips:
        ///     docs/research/item-mods.md); null when it is no item or has no such component.
        /// </summary>
        public ItemModLists? ItemMods(IntPtr entity)
        {
            if (entity == IntPtr.Zero || !this.IsItemEntity(entity))
            {
                return null;
            }

            var item = (Item)Activator.CreateInstance(typeof(Item), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { entity }, null)!;
            if (!item.TryGetComponent<Mods>(out var mods))
            {
                return null;
            }

            static List<ItemMod> Listed(List<(string name, int[] values)> list) => list.Select(mod => new ItemMod(mod.name, mod.values)).ToList();
            return new ItemModLists(Listed(mods.ImplicitModValues), Listed(mods.ExplicitModValues), Listed(mods.EnchantModValues), Listed(mods.HellscapeModValues));
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
