namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using GameReader.RemoteEnums;
    using GameReader.RemoteObjects.Components;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.Components;

    /// <summary>Item description of <see cref="InventoryReader"/>: the item readout, its titles and the name built from the words of its mods.</summary>
    public sealed partial class InventoryReader
    {
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
        public ItemNameTrust NameTrust { get; private set; } = new();

        /// <summary>A new game process, perhaps a patched one: the names made and their standing are the last one's.</summary>
        public void ResetNameWords()
        {
            this.NameTrust = new ItemNameTrust();
            this.wordNames.Clear();
            this.titles.Clear();
        }

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
    }
}
