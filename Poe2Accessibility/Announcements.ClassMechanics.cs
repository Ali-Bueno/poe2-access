namespace Poe2Accessibility
{
    using System.Globalization;

    /// <summary>Spoken strings of the class resources the game's tables name (same translatable layer; docs/research/class-mechanics.md).</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     The name of a counted stat after "current_": the game's own wording for the ones the mod knows (KeywordPopups,
        ///     BuffDefinitions 2026-11-08), the stat's words for any other, so a resource a patch adds is still named.
        /// </summary>
        public static string ResourceName(string statName) => statName switch
        {
            "infernal_flame" => T("Infernal Flame", "Llama infernal"),
            "demon_form_stacks" => T("Demonflame", "Fuego demoníaco"),
            "number_of_crossbow_heat_stacks" => T("Heat", "Calor"),
            "fury_charges" => T("Fury", "Furia salvaje"),
            "fire_infusion_stacks" => T("Fire Infusions", "Infusiones de fuego"),
            "cold_infusion_stacks" => T("Cold Infusions", "Infusiones de hielo"),
            "lightning_infusion_stacks" => T("Lightning Infusions", "Infusiones de rayo"),
            "verisium_infusion_stacks" => T("Verisium Infusions", "Infusiones de verisio"),
            "instilling_chaos_stacks" => T("Chaos Surges", "Sobrecargas de caos"),
            "spirit_charges" => T("Spirit Charges", "Cargas de espíritu"),
            "focus_lost" => T("Focus lost", "Concentración perdida"),
            _ => Words(statName),
        };

        /// <summary>The name of a limit after "number_of_active_": totems, minions and offerings by their game words.</summary>
        public static string LimitName(string limitName) => limitName switch
        {
            "totems" => T("Totems", "Tótems"),
            "skeletons" => T("Skeletons", "Esqueletos"),
            "zombies" => T("Zombies", "Zombis"),
            "raging_spirits" => T("Raging Spirits", "Espíritus furiosos"),
            "spectres" => T("Spectres", "Espectros"),
            "wolves" => T("Wolves", "Lobos"),
            "beast_minions" => T("Beasts", "Bestias"),
            "infernal_familiars" => T("Infernal Hounds", "Sabuesos infernales"),
            "tempest_bells" => T("Tempest Bells", "Campanas de tempestad"),
            "generic_limited_minions" => T("Minions", "Esbirros"),
            _ => limitName.EndsWith("_offerings", System.StringComparison.Ordinal) ? T("Offerings", "Ofrendas") : Words(limitName),
        };

        /// <summary>"Infernal Flame 120 of 400", "Jade: 7".</summary>
        public static string ResourceCount(string name, int count, int max) =>
            max > 0 ? T($"{name} {count} of {max}", $"{name} {count} de {max}") : $"{name}: {count}";

        /// <summary>A pool at its maximum: "Infernal Flame full, 400".</summary>
        public static string ResourceFull(string name, int count) => T($"{name} full, {count}", $"{name} al máximo, {count}");

        /// <summary>A pool run out or a limit emptied: "no Totems".</summary>
        public static string ResourceEmpty(string name) => T($"no {name}", $"sin {name}");

        /// <summary>A named buff gone: "Unbound ended".</summary>
        public static string ResourceEnded(string name) => T($"{name} ended", $"{name} se acabó");

        /// <summary>A stat's or limit's id as words: "crossbow heat stacks".</summary>
        private static string Words(string id)
        {
            var words = id.Replace('_', ' ').Trim();
            return words.Length > 0 ? char.ToUpper(words[0], CultureInfo.CurrentCulture) + words[1..] : id;
        }
    }
}
