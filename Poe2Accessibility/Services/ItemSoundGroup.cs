namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>The sound key an item on the ground plays under, so each kind has its own volume (settings SoundLevels).</summary>
    public static class ItemSoundGroup
    {
        public const string Flask = "item_flask";
        public const string Equipment = "item_equipment";
        public const string Gem = "item_gem";
        public const string Other = "item";

        /// <summary>Base item folders (Metadata/Items/&lt;folder&gt;/…, catalog/item-sounds.json) of gear worn or wielded.</summary>
        private static readonly string[] EquipmentFolders = { "Armours", "Weapons", "Quivers", "Rings", "Amulets", "Belts", "Jewels" };

        /// <param name="kind">The item's scanner kind (<see cref="ScanEntry.Kind"/>: "gold", "currency", "quest", "rare", "item").</param>
        /// <param name="baseId">Its base type id, "Metadata/Items/Flasks/FlaskLife1"; empty when unread.</param>
        public static string Of(string kind, string baseId)
        {
            switch (kind)
            {
                case "gold":
                    return "gold";
                case "currency":
                    return "currency";
                case "quest":
                    return "quest_item";
            }

            var folder = Folder(baseId);
            return folder.Equals("Flasks", StringComparison.OrdinalIgnoreCase) ? Flask
                : folder.StartsWith("Gem", StringComparison.OrdinalIgnoreCase) ? Gem
                : Array.Exists(EquipmentFolders, name => name.Equals(folder, StringComparison.OrdinalIgnoreCase)) ? Equipment
                : Other;
        }

        /// <summary>The folder after Metadata/Items/ ("Gems", "Gem" and "gems" all occur), empty for another path.</summary>
        private static string Folder(string baseId)
        {
            var parts = baseId.Split('/');
            return parts.Length > 3 && parts[0].Equals("Metadata", StringComparison.OrdinalIgnoreCase) && parts[1].Equals("Items", StringComparison.OrdinalIgnoreCase)
                ? parts[2]
                : string.Empty;
        }
    }
}
