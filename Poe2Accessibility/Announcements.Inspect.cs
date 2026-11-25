namespace Poe2Accessibility
{
    using System.Collections.Generic;

    /// <summary>
    ///     Inspect Mode's comparison table for <see cref="Announcements"/>: the opening, the items' labels and the cells an
    ///     item lacks. The cells themselves are the game's own lines (docs/research/ui-layers.md, "Item Inspect Mode").
    /// </summary>
    public static partial class Announcements
    {
        /// <summary>The controls help's sentence on the table, after the menus' (settings menu, help).</summary>
        public static string InspectHelp => T(
            "In Inspect Mode on an item, flick the right stick: up and down go through its lines, left and right switch between the item and what you wear",
            "En el modo inspección de un objeto, mueve el stick derecho: arriba y abajo recorren sus líneas, izquierda y derecha cambian entre el objeto y lo que llevas puesto");

        /// <summary>The label of the item the player inspected, said when the table switches to it.</summary>
        public static string InspectThisItem => T("This item", "Este objeto");

        /// <summary>The label of the one worn item beside it.</summary>
        public static string InspectWorn => T("Worn", "Equipado");

        /// <summary>The label of one of two worn items, by its slot: "Worn, left ring".</summary>
        public static string InspectWornIn(string slot) => T($"Worn, {slot}", $"Equipado, {slot}");

        /// <summary>A row an item lacks, in the row's words without numbers: "to maximum Life, none".</summary>
        public static string InspectNone(string words) => T($"{words}, none", $"{words}, ninguno");

        /// <summary>The sockets row of an item without sockets.</summary>
        public static string InspectNoSockets => T("no sockets", "sin engarces");

        /// <summary>A cell said with the item it belongs to, when the table switches item: "Worn. Energy Shield: 43".</summary>
        public static string InspectCell(string label, string cell) => $"{label}. {cell}";

        /// <summary>
        ///     Inspect Mode opened on an item, in the game's word for the mode: "Inspect Mode. Hale Tattered Robe of the
        ///     Pupil, against worn Protective Feathered Robe of the Cloud"; the item alone when nothing worn is beside it.
        /// </summary>
        public static string InspectOpened(string mode, string item, IReadOnlyList<string> worn)
        {
            if (worn.Count == 0)
            {
                return $"{mode}. {item}";
            }

            var joined = string.Join(", ", worn);
            return T($"{mode}. {item}, against worn {joined}", $"{mode}. {item}, frente a lo equipado: {joined}");
        }
    }
}
