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

        /// <summary>"Bastille, unrevealed": a linked node still in the fog — named and placed, but no hop lands there yet.</summary>
        public static string AtlasUnrevealed(string name) => T($"{name}, unrevealed", $"{name}, sin revelar");

        /// <summary>"Caldera, 1 link up left" / "Slash, 4 steps right": a frontier entry with its distance and side.</summary>
        public static string AtlasFrontierEntry(string name, int away, string side, bool linked)
        {
            var distance = linked
                ? T(away == 1 ? "1 link" : $"{away} links", away == 1 ? "a 1 enlace" : $"a {away} enlaces")
                : T(away == 1 ? "1 step" : $"{away} steps", away == 1 ? "a 1 paso" : $"a {away} pasos");
            return side.Length > 0 ? $"{name}, {distance} {side}" : $"{name}, {distance}";
        }

        /// <summary>"From here: Caldera, 1 link up left; Pit, 3 links right. 9 accessible, not completed in sight"</summary>
        public static string AtlasFrontier(string entries, int total) =>
            entries.Length > 0
                ? T($"From here: {entries}. {total} accessible, not completed in sight", $"Desde aquí: {entries}. {total} accesibles sin completar a la vista")
                : T($"{total} accessible, not completed in sight", $"{total} accesibles sin completar a la vista");

        /// <summary>"2 links from Precursor Tower" (or steps, off the catalog graph): how far a named node lies.</summary>
        public static string AtlasAway(int away, bool linked, string name) => linked
            ? T(away == 1 ? $"1 link from {name}" : $"{away} links from {name}", away == 1 ? $"a 1 enlace de {name}" : $"a {away} enlaces de {name}")
            : T(away == 1 ? $"1 step from {name}" : $"{away} steps from {name}", away == 1 ? $"a 1 paso de {name}" : $"a {away} pasos de {name}");

        /// <summary>"Came from Marrow": the previous landing, so a hop too far has its way back said.</summary>
        public static string AtlasCameFrom(string name) => T($"Came from {name}", $"Vienes de {name}");

        /// <summary>"Last time: Marrow, 4 links from Marrow" — where the last session left off, and how far it lies.</summary>
        public static string AtlasLastTime(string name, string away) => T($"Last time: {name}, {away}", $"La vez anterior: {name}, {away}");

        /// <summary>"Nearest: right, Slash": the closest revealed nodes by direction around a row-less landing.</summary>
        public static string AtlasNearest(string parts) => T($"Nearest: {parts}", $"Cercanos: {parts}");

        /// <summary>The Atlas skill tree finder's lists, said on entering one.</summary>
        public static string AtlasTreeCategoryName(Services.AtlasTreeCategory category) => category switch
        {
            Services.AtlasTreeCategory.Notables => T("Notables", "Notables"),
            Services.AtlasTreeCategory.Clusters => T("Clusters", "Grupos"),
            _ => T("Position", "Posición"),
        };

        /// <summary>How many links a node lies from the cursor on a graph ("3 links away").</summary>
        public static string AtlasLinksAway(int links) =>
            links == 1 ? T("1 link away", "a 1 enlace") : T($"{links} links away", $"a {links} enlaces");
    }
}
