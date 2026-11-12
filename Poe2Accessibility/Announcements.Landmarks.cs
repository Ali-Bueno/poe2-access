namespace Poe2Accessibility
{
    /// <summary>Radar landmark labels: the notes players wrote in English, said in the player's language.</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     GameHelper Radar's labels are notes players wrote in English ("Stairs Up", "Crop Circle (Skill Gem Level 4)"):
        ///     in other languages their common words and reward notes are translated; names stay as written.
        /// </summary>
        public static string LandmarkLabel(string label)
        {
            if (Localization.Language == "en")
            {
                return label;
            }

            var trimmed = label.Trim();
            if (LandmarkWord(trimmed) is { } whole)
            {
                return whole;
            }

            var replacements = LandmarkNoteTexts;
            for (var i = 0; i < LandmarkNotes.Length; i++)
            {
                trimmed = LandmarkNotes[i].Replace(trimmed, replacements[i]);
            }

            return trimmed;
        }

        /// <summary>A whole label that is a note of the radar table's, in the player's language; null for any other, and in English.</summary>
        public static string? LandmarkWhole(string label) => Localization.Language == "en" ? null : LandmarkWord(label.Trim());

        private static string? LandmarkWord(string label) => label.ToLowerInvariant() switch
        {
            "boss" => T("Boss", "Jefe"),
            "bosses" => T("Bosses", "Jefes"),
            "boss stairs" => T("Boss stairs", "Escaleras del jefe"),
            "stairs" => T("Stairs", "Escaleras"),
            "stairs up" => T("Stairs Up", "Escaleras hacia arriba"),
            "stairs down" => T("Stairs Down", "Escaleras hacia abajo"),
            "entrance" => T("Entrance", "Entrada"),
            "way back" => T("Way back", "Camino de vuelta"),
            "transition" => T("Transition", "Paso a otra zona"),
            "beacon" => T("Beacon", "Baliza"),
            "ancient seal" => T("Ancient Seal", "Sello antiguo"),
            "looted vault" => T("Looted Vault", "Cámara saqueada"),
            "rare fight" => T("Rare Fight", "Combate con un monstruo raro"),
            "medallion" => T("Medallion", "Medallón"),
            "bell (rewards)" => T("Bell (Rewards)", "Campana (recompensas)"),
            "flask buff - need key" => T("Flask buff - Need key", "Mejora de frascos, necesita una llave"),
            "pillars - buff choice" => T("Pillars - Buff choice", "Pilares, elige una mejora"),
            "rhoa - no reward?" => T("Rhoa - No reward?", "Rhoa, quizá sin recompensa"),
            "to devourer" => T("To Devourer", "Hacia el Devorador"),
            "arena / hunting grounds" => T("Arena / Hunting Grounds", "Arena o coto de caza"),
            "forgotten corpses" => T("Forgotten Corpses", "Cadáveres olvidados"),
            "forgotten riches" => T("Forgotten Riches", "Riquezas olvidadas"),
            // The table's notes with no name of the game's in them, the mod's own words (sweep of 2026-11-12).
            "boss stronghold" => T("Boss stronghold", "Fortaleza del jefe"),
            "corruption altar" => T("Corruption Altar", "Altar de corrupción"),
            "dungeon entrance down" => T("Dungeon entrance down", "Entrada a la mazmorra, hacia abajo"),
            "egg room" => T("Egg Room", "Sala de los huevos"),
            "farmlands" => T("Farmlands", "Tierras de cultivo"),
            "forge entrance" => T("Forge Entrance", "Entrada de la forja"),
            "generator" => T("Generator", "Generador"),
            "hall of the dead" => T("Hall of the dead", "Salón de los muertos"),
            "medicine lady" => T("Medicine lady", "Curandera"),
            "mini town" => T("Mini Town", "Pueblo pequeño"),
            "pay respects at end" => T("Pay respects at end", "Presenta tus respetos al final"),
            "warden office" => T("Warden office", "Oficina del alcaide"),
            "waterways lever" => T("Waterways Lever", "Palanca de los canales"),
            "stair place" => T("Stair Place", "Lugar de las escaleras"),
            "bell - currency" => T("Bell - Currency", "Campana, monedas"),
            "loot - ship explodes" => T("Loot - SHIP EXPLODES", "Botín, el barco explota"),
            "shrine - no loot?" => T("Shrine - No loot?", "Santuario, quizá sin botín"),
            "rare - good rewards" => T("Rare - Good rewards", "Raro, buenas recompensas"),
            "soul ferryman - sells runes" => T("Soul Ferryman - Sells runes", "Barquero de almas, vende runas"),
            "quest - map piece" => T("Quest - Map piece", "Misión, fragmento de mapa"),
            "quest - treasure map?" => T("Quest - Treasure map?", "Misión, quizá un mapa del tesoro"),
            "quest - deadman chest" => T("Quest - deadman chest", "Misión, cofre del muerto"),
            "boss - shark" => T("Boss - Shark", "Jefe, tiburón"),
            "boss - siren" => T("Boss - Siren", "Jefe, sirena"),
            "2 seal locations - loot" => T("2 Seal locations - Loot", "2 lugares con sello, botín"),
            "ritual site (level 4 skill gem)" => T("Ritual Site (Level 4 Skill Gem)", "Lugar del ritual (gema de habilidad de nivel 4)"),
            _ => null,
        };

        private static readonly System.Text.RegularExpressions.Regex[] LandmarkNotes =
        {
            new(@"Support [Gg]em Level (\d+) and Flasks"),
            new(@"Support [Gg]em Level (\d+)"),
            new(@"Support [Gg]em"),
            new(@"Skill Gem Level (\d+)"),
            new(@"Spirit Gem Level (\d+)"),
            new(@"(\d+) Spirit"),
            new(@"\+(\d+) Max Life"),
            new(@"\+(\d+) Passives?"),
            new(@"(\d+)% Cold Res"),
            new(@"(\d+)% Fire Res"),
            new(@"(\d+)% Lightning Res"),
            new(@"(\d+)% Chaos Res"),
            new(@"Res Ring"),
            new(@"\(waypoint\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new(@"Salvaging Bench"),
            new(@"weapons vendor", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new(@"armour vendor", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new(@"\(Level (\d+)\)"),
            new(@" - "),
        };

        /// <summary>The replacement of each <see cref="LandmarkNotes"/> pattern, in the same order.</summary>
        private static string[] LandmarkNoteTexts => new[]
        {
            T("support gem level $1 and flasks", "gema de apoyo de nivel $1 y frascos"),
            T("support gem level $1", "gema de apoyo de nivel $1"),
            T("support gem", "gema de apoyo"),
            T("skill gem level $1", "gema de habilidad de nivel $1"),
            T("spirit gem level $1", "gema de espíritu de nivel $1"),
            T("$1 spirit", "$1 de espíritu"),
            T("+$1 maximum life", "+$1 de vida máxima"),
            T("+$1 passive points", "+$1 puntos de pasiva"),
            T("$1% cold resistance", "$1% de resistencia al frío"),
            T("$1% fire resistance", "$1% de resistencia al fuego"),
            T("$1% lightning resistance", "$1% de resistencia al rayo"),
            T("$1% chaos resistance", "$1% de resistencia al caos"),
            T("resistance ring", "anillo de resistencia"),
            T("(waypoint)", "(punto de ruta)"),
            T("salvaging bench", "mesa de reciclaje"),
            T("weapons vendor", "vendedor de armas"),
            T("armour vendor", "vendedor de armaduras"),
            T("(level $1)", "(nivel $1)"),
            ", ",
        };
    }
}
