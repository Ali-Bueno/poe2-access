namespace Poe2Accessibility
{
    /// <summary>Radar-table labels: the common words a label is made of, said in the player's language.</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     A whole piece of a radar label ("Boss" in "Boss - Yama", "Chest") that is a common word, translated; null
        ///     for anything else (names stay as written, or come from the game's own tables).
        /// </summary>
        public static string? LandmarkPiece(string piece) => piece.Trim().ToLowerInvariant() switch
        {
            "boss" => T("Boss", "Jefe"),
            "quest" => T("Quest", "Misión"),
            "loot" => T("Loot", "Botín"),
            "rare" => T("Rare", "Raro"),
            "rares" => T("Rares", "Raros"),
            "chest" => T("Chest", "Cofre"),
            "rare chests" => T("Rare chests", "Cofres raros"),
            "exit" => T("Exit", "Salida"),
            "waypoint" => T("Waypoint", "Punto de ruta"),
            "town" => T("Town", "Ciudad"),
            "lever" => T("Lever", "Palanca"),
            "arena" => T("Arena", "Arena"),
            "boat" => T("Boat", "Barco"),
            "crystal" => T("Crystal", "Cristal"),
            "seal" => T("Seal", "Sello"),
            _ => null,
        };
    }
}
