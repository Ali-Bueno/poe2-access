namespace Poe2Accessibility
{
    /// <summary>Spoken strings of the endgame (docs/research/endgame.md); the game's own words come from catalog/endgame.json.</summary>
    public static partial class Announcements
    {
        /// <summary>A node of the Atlas whose name the data does not hold (its map is drawn at random) and whose popup is not built yet.</summary>
        public static string AtlasNode => T("Atlas node", "Nodo del Atlas");

        /// <summary>"Holds: Breach, Map Boss" — the contents an Atlas node shows as icons, in the game's words.</summary>
        public static string AtlasContents(System.Collections.Generic.IEnumerable<string> names)
        {
            var list = string.Join(", ", names);
            return T($"Holds: {list}", $"Contiene: {list}");
        }
    }
}
