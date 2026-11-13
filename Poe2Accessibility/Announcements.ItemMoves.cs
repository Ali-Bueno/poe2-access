namespace Poe2Accessibility
{
    using System.Linq;

    /// <summary>The item and trade phrases of <see cref="Announcements"/>: holding, equipping, weapon sets, buying, selling, salvage and moves between panels.</summary>
    public static partial class Announcements
    {
        /// <summary>"Recogido: Traje de ermitaño"; a stack that grew: "Recogido 2 más: Pergamino de sabiduría, 5".</summary>
        /// <summary>The cursor took an item (D2R "item in hand"): what, and what to do with it.</summary>
        public static string InHand(string name) =>
            T($"In hand: {name}. Choose a cell or a matching equipment slot to place it",
              $"En la mano: {name}. Elige una celda o una ranura de equipo compatible para colocarlo");

        /// <summary>An item now in an equipment slot (by the cursor or the equip button): what and where.</summary>
        public static string Equipped(string name, string slot) => T($"Equipped: {name}, {slot}", $"Equipado: {name}, {slot}");

        public static string Unequipped(string name) => T($"Unequipped: {name}", $"Desequipado: {name}");

        /// <summary>The weapon set swapped to and what it holds (D2R "Armas 2: Arco largo, Carcaj").</summary>
        public static string WeaponSet(int number, System.Collections.Generic.IReadOnlyList<string> held) =>
            held.Count == 0
                ? T($"Weapon set {number}, empty", $"Set de armas {number}, vacío")
                : T($"Weapon set {number}: ", $"Set de armas {number}: ") + string.Join(", ", held);

        /// <summary>
        ///     What happened to an item moved with a panel open (<see cref="Services.ItemMoves"/>), said as the deed: "Vendido:
        ///     Brazales de gamuza, por 19 de oro", "Movido a Alijo: …" (the panel's own title), "Tirado: …".
        /// </summary>
        public static string Sold(string names, long gold) => T($"Sold: {names}, for {gold} gold", $"Vendido: {names}, por {gold} de oro");

        public static string Bought(string names, long gold) => T($"Bought: {names}, for {gold} gold", $"Comprado: {names}, por {gold} de oro");

        /// <summary>The game's Skills panel title (ClientStrings SkillGemPanelTitle), to know it beside the inventory.</summary>
        public static string SkillGemPanel => T("Skills", "Habilidades");

        /// <summary>The game's salvage bench title (ClientStrings SalvageBenchTitle), to know it beside the inventory.</summary>
        public static string SalvageBenchPanel => T("Salvage Bench", "Mesa de reciclaje");

        /// <summary>"Reciclado: Corona con cuernos, raro. Recibes: Chatarra de armero, 2".</summary>
        public static string Salvaged(Services.SalvageOutcome outcome)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (outcome.Salvaged.Count > 0)
            {
                var names = string.Join(", ", outcome.Salvaged);
                lines.Add(T($"Salvaged: {names}", $"Reciclado: {names}"));
            }

            if (outcome.Received.Count > 0)
            {
                var items = string.Join("; ", outcome.Received.Select(item => Item(item.Title, GameReader.RemoteEnums.Rarity.Normal, item.Count)));
                lines.Add(T($"Received: {items}", $"Recibes: {items}"));
            }

            return string.Join(". ", lines);
        }

        /// <summary>"Furia I, engarzada en Campana de tempestad".</summary>
        public static string SupportEngraved(string support, string skill) => T($"{support}, socketed in {skill}", $"{support}, engarzada en {skill}");

        public static string MovedTo(string name, string panel) => T($"Moved to {panel}: {name}", $"Movido a {panel}: {name}");

        public static string TakenFrom(string name, string panel) => T($"Taken from {panel}: {name}", $"Sacado de {panel}: {name}");

        public static string Obtained(string name) => T($"Obtained: {name}", $"Obtenido: {name}");

        public static string Dropped(string name) => T($"Dropped: {name}", $"Tirado: {name}");

        /// <summary>What a trade took, a disenchanted item among them.</summary>
        public static string HandedOver(string names) => T($"Handed over: {names}", $"Entregado: {names}");

        public static string UsedUp(string name) => T($"Used: {name}", $"Usado: {name}");

        public static string HandEmpty => T("You no longer hold an item", "Ya no llevas ningún objeto");
    }
}
