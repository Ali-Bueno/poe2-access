namespace Poe2Accessibility
{
    using System.Linq;

    /// <summary>Items and inventory: grid cells, equipment slots, weapon sets, search results, gold and amounts.</summary>
    public static partial class Announcements
    {
        /// <summary>A weapon-set toggle, named as the game's tooltips do ("Set de armas I").</summary>
        public static string WeaponSet(int number)
        {
            var numeral = number switch { 1 => "I", 2 => "II", _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            return T($"weapon set {numeral}", $"set de armas {numeral}");
        }

        /// <summary>A value shown for one weapon set ("set de armas II: 30 de espíritu").</summary>
        public static string WeaponSetValue(int number, string value) => $"{WeaponSet(number)}: {value}";

        public static string EmptyCell => T("empty", "vacío");

        /// <summary>A slot of a window the mod has no table for, with no item in it beside slots that hold one.</summary>
        public static string EmptySlot => T("empty slot", "ranura vacía");
        public static string UnknownItem => T("unknown item", "objeto desconocido");

        /// <summary>
        ///     Item text and position for a grid cell: equipment slots name the slot ("Gloves, empty") and have no
        ///     position, flasks say their slot number, the main inventory and other grids (stash, vendor) column and row.
        /// </summary>
        public static (string Text, string Value) InventoryCell(GameReader.RemoteEnums.InventoryName? inventory, string item, int column, int row)
        {
            if (inventory == GameReader.RemoteEnums.InventoryName.Flask1)
            {
                return (item, T($"flask slot {column + 1}", $"ranura de frasco {column + 1}"));
            }

            // Stash tabs are not player inventories, yet GameHelper's map can name one after an unrelated slot.
            return inventory is { } slot && SlotName(slot) is { } name ? ($"{name}, {item}", string.Empty) : (item, GridPosition(column, row));
        }

        /// <summary>Whether a grid's cells are said by column and row (<see cref="InventoryCell"/>): not the flasks' row nor an equipment slot.</summary>
        public static bool IsCellGrid(GameReader.RemoteEnums.InventoryName? inventory) =>
            inventory != GameReader.RemoteEnums.InventoryName.Flask1 && (inventory is not { } slot || SlotName(slot) is null);

        /// <summary>
        ///     A grid cell with an item in the hand: what lies where that item would go — "vacío", "sobre Mirada de odio,
        ///     raro" (one item: A swaps them), "sobre 3 objetos: …" (it cannot be put there).
        /// </summary>
        public static string UnderHand(System.Collections.Generic.IReadOnlyList<string> items) => items.Count switch
        {
            0 => EmptyCell,
            1 => T($"over {items[0]}", $"sobre {items[0]}"),
            _ => T($"over {items.Count} items: ", $"sobre {items.Count} objetos: ") + string.Join("; ", items),
        };

        public static string? SlotName(GameReader.RemoteEnums.InventoryName inventory) => inventory switch
        {
            GameReader.RemoteEnums.InventoryName.BodyArmour1 => T("Body armour", "Armadura corporal"),
            GameReader.RemoteEnums.InventoryName.Weapon1 => T("Main hand", "Mano principal"),
            GameReader.RemoteEnums.InventoryName.Offhand1 => T("Off hand", "Mano secundaria"),
            GameReader.RemoteEnums.InventoryName.Weapon2 => T("Second weapon set, main hand", "Segundo set de armas, mano principal"),
            GameReader.RemoteEnums.InventoryName.Offhand2 => T("Second weapon set, off hand", "Segundo set de armas, mano secundaria"),
            GameReader.RemoteEnums.InventoryName.Helm1 => T("Helmet", "Casco"),
            GameReader.RemoteEnums.InventoryName.Amulet1 => T("Amulet", "Amuleto"),
            GameReader.RemoteEnums.InventoryName.Ring1 => T("Left ring", "Anillo izquierdo"),
            GameReader.RemoteEnums.InventoryName.Ring2 => T("Right ring", "Anillo derecho"),
            GameReader.RemoteEnums.InventoryName.Gloves1 => T("Gloves", "Guantes"),
            GameReader.RemoteEnums.InventoryName.Boots1 => T("Boots", "Botas"),
            GameReader.RemoteEnums.InventoryName.Belt1 => T("Belt", "Cinturón"),
            _ => null,
        };

        /// <summary>
        ///     Where a moved item went: an equipment slot, or the belt's row of flasks and charms (one inventory, whose cells
        ///     the cursor names itself) — a flask put on it with X said nothing (live 2026-11-10).
        /// </summary>
        public static string? MoveSlotName(GameReader.RemoteEnums.InventoryName inventory) =>
            inventory == GameReader.RemoteEnums.InventoryName.Flask1 ? T("Flasks and charms", "Frascos y viales") : SlotName(inventory);

        /// <summary>The box that splits a stack (hold A on one): the stick sets how many are taken, A takes them.</summary>
        public static string Unstack => T("Unstack", "Desapilar");

        /// <summary>What the splitter would do now: "2, 4 stay".</summary>
        public static string UnstackAmounts(int taken, int kept) => T($"{taken}, {kept} stay", $"{taken}, quedan {kept}");

        /// <summary>One-based cell of the inventory grid, read after the item.</summary>
        public static string GridPosition(int column, int row) => T($"column {column + 1}, row {row + 1}", $"columna {column + 1}, fila {row + 1}");

        /// <summary>An item the panel's search lights, under the cursor: "Vendajes de lino, mágico, resaltado".</summary>
        public static string HighlightedItem(string item) => T($"{item}, highlighted", $"{item}, resaltado");

        /// <summary>What a search lights: "3 resaltados: Vendajes de lino, mágico, columna 3, fila 1. …"; the ones not named are counted.</summary>
        public static string SearchMatches(int count, System.Collections.Generic.IEnumerable<string> named)
        {
            var list = named.ToList();
            if (count == 0)
            {
                return T("Nothing highlighted", "Nada resaltado");
            }

            var head = count == 1 ? T("1 highlighted", "1 resaltado") : T($"{count} highlighted", $"{count} resaltados");
            var rest = count > list.Count ? T($". And {count - list.Count} more", $". Y {count - list.Count} más") : string.Empty;
            return $"{head}: {string.Join(". ", list)}{rest}";
        }

        public static string SearchCleared => T("Search cleared", "Búsqueda borrada");

        /// <summary>Base name, stack size when more than one, rarity unless normal.</summary>
        public static string Item(string name, GameReader.RemoteEnums.Rarity rarity, int count)
        {
            var parts = new System.Collections.Generic.List<string> { name.Length > 0 ? name : UnknownItem };
            if (count > 1)
            {
                parts.Add(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (rarity != GameReader.RemoteEnums.Rarity.Normal)
            {
                parts.Add(RarityName(rarity));
            }

            return string.Join(", ", parts);
        }

        public static string Gold => T("gold", "oro");

        /// <summary>An amount shown beside a currency icon ("128 gold"); a price drawn red is one the player cannot pay.</summary>
        public static string Amount(string amount, string currency, bool cannotAfford) =>
            cannotAfford ? T($"{amount} {currency}, not enough", $"{amount} {currency}, no alcanza") : $"{amount} {currency}";
    }
}
