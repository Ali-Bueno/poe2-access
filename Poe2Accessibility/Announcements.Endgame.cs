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

        /// <summary>The word for an Atlas node's state, said right after its name on landing.</summary>
        public static string AtlasState(Services.AtlasNodeState state) => state switch
        {
            Services.AtlasNodeState.Completed => T("Completed", "Completado"),
            Services.AtlasNodeState.Accessible => T("Accessible", "Accesible"),
            _ => T("Locked", "Bloqueado"),
        };

        /// <summary>"Steaming Springs. Accessible. Biome: Forest. Holds: … Links: …" — the landed Atlas node (Phase 1).</summary>
        public static string AtlasLanding(string name, string state, string biome, string contents, string links) =>
            Sentences(new[] { name, state, biome.Length > 0 ? T($"Biome: {biome}", $"Bioma: {biome}") : string.Empty, contents, links });
    }
}
