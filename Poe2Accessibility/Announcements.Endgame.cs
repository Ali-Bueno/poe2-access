namespace Poe2Accessibility
{
    /// <summary>Spoken strings of the endgame (docs/research/endgame.md); the game's own words come from catalog/endgame.json.</summary>
    public static partial class Announcements
    {
        /// <summary>A node of the Atlas whose name the data does not hold (its map is drawn at random) and whose popup is not built yet.</summary>
        public static string AtlasNode => T("Atlas node", "Nodo del Atlas");

        /// <summary>"In a tower's reach, up left": the towers whose tablets work on this node's map, by the side each lies on.</summary>
        public static string AtlasTowerReach(System.Collections.Generic.IEnumerable<string> sides)
        {
            var list = string.Join("; ", sides);
            return T($"In a tower's reach: {list}", $"Al alcance de una torre: {list}");
        }

        /// <summary>"Reaches 26 maps": a tower node, and how many maps its tablets work on.</summary>
        public static string AtlasTowerReaches(int count) => T($"Reaches {count} maps", $"Alcanza {count} mapas");

        /// <summary>"Holds: Breach, Map Boss" — the contents an Atlas node shows as icons, in the game's words.</summary>
        public static string AtlasContents(System.Collections.Generic.IEnumerable<string> names)
        {
            var list = string.Join(", ", names);
            return T($"Holds: {list}", $"Contiene: {list}");
        }
    }
}
