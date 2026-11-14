namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of a game update: what it added and the layout parts the mod could not find again.</summary>
    public static partial class Announcements
    {
        /// <summary>
        ///     What an update added, as table, count and how many of them the mod cannot classify yet; null with nothing a
        ///     player would meet (<see cref="Services.GameNews"/>).
        /// </summary>
        public static string? GameNews(IEnumerable<(string Table, int Added, int Unclassified)> news)
        {
            var parts = news.Where(item => item.Added > 0).Select(item => (Noun: NewsNoun(item.Table), item.Added, item.Unclassified))
                .Where(item => item.Noun != null)
                .Select(item => $"{item.Noun} {item.Added}, {(item.Unclassified == 0 ? T("all covered", "todo cubierto") : T($"{item.Unclassified} unclassified", $"{item.Unclassified} sin clasificar"))}")
                .ToList();
            if (parts.Count == 0)
            {
                return null;
            }

            var list = string.Join("; ", parts);
            return T($"New in this game update: {list}. The list is in the file game-news.txt", $"Novedades de esta actualización del juego: {list}. La lista está en el archivo game-news.txt");
        }

        private static string? NewsNoun(string table) => table switch
        {
            "buffdefinitions" => T("effects", "efectos"),
            "worldareas" => T("areas", "zonas"),
            "monstervarieties" => T("monsters", "monstruos"),
            "activeskills" => T("skills", "habilidades"),
            "minimapicons" => T("map icons", "iconos del mapa"),
            "chests" => T("chests and objects", "cofres y objetos"),
            "shrines" => T("shrines", "santuarios"),
            "npcs" => T("characters", "personajes"),
            "npctalkcategory" => T("kinds of dialogue", "tipos de diálogo"),
            "groundeffecttypes" => T("ground effects", "efectos de suelo"),
            "questrewardtype" => T("quest rewards", "recompensas de misión"),
            "screens" => T("screens", "pantallas"),
            _ => null,
        };

        public static string GameUpdateSurvived => T("The game was updated: the mod found everything again", "El juego se ha actualizado: el mod lo ha encontrado todo");

        /// <summary>The layout parts not found after a game update, in one line.</summary>
        public static string CompatibilityMissing(IReadOnlyList<string> parts)
        {
            // Parts carry their own "so …" clause and commas: joined by "and" two of them ran into one clause ("los controles de la
            // interfaz, así que los menús no se leerán y dónde están los controles…", heard 2026-11-08), so each is its own item.
            var list = string.Join("; ", parts.Select(CompatibilityPart).Distinct());
            return T(
                $"Compatibility warning: cannot find {list}. The game may have been updated; those readings may fail",
                $"Aviso de compatibilidad: no encuentro {list}. Puede que el juego se haya actualizado; esas lecturas pueden fallar");
        }

        private static string CompatibilityPart(string part) => part switch
        {
            "element names" => T("the interface's control names", "los nombres de la interfaz"),
            "element texts" => T("the interface's texts", "los textos de la interfaz"),
            "entity names" => T("the names of characters and monsters", "los nombres de personajes y monstruos"),
            "local player" => T("your character in the world", "tu personaje en el mundo"),
            "entity lists" => T("what is around you, so enemies, items and exits stay silent", "lo que hay a tu alrededor, así que enemigos, objetos y salidas no sonarán"),
            "terrain" or "terrain tiles" or "terrain cells" => T("the terrain, so the radar stays silent", "el terreno, así que el radar no sonará"),
            "element tree" or "UI focus" => T("the interface's controls, so menus stay silent", "los controles de la interfaz, así que los menús no se leerán"),
            "element rects" or "element flags" or "window cull" => T("where the interface's controls are and which show", "dónde están los controles de la interfaz y cuáles se ven"),
            "entity path" or "entity components" or "component lookup" => T("what each thing around you is, so enemies, items and exits stay silent", "qué es cada cosa a tu alrededor, así que enemigos, objetos y salidas no sonarán"),
            "area level" => T("the area's level", "el nivel del área"),
            "life" => T("life, mana and energy shield", "la vida, el maná y el escudo de energía"),
            "Atlas catalog" => T("the Atlas's data, so its nodes read from the game's own popup", "los datos del Atlas, así que sus nodos se leerán solo por su ventana"),
            "buffs" => T("buffs and harmful states", "los efectos y estados"),
            "skills" => T("your skills and cooldowns", "tus habilidades y enfriamientos"),
            "stats" => T("your stats", "tus estadísticas"),
            "game data" => T("part of the game's data after its update, so some warnings may be out of date", "parte de los datos del juego tras su actualización, así que algunos avisos pueden estar desfasados"),
            "cooldowns" => T("when your skills are ready", "cuándo están listas tus habilidades"),
            "poise" => T("when an enemy is about to be stunned, so that warning stays silent", "cuándo un enemigo está a punto de aturdirse, así que ese aviso no sonará"),
            "walkable" => T("where you can walk, so the guide and the radar may fail", "por dónde se puede andar, así que el guía y el radar pueden fallar"),
            "code fields" => T("some of the game's data, so some readings may be wrong", "parte de los datos del juego, así que alguna lectura puede ser errónea"),
            "effect names" => T("some effects by their name, so they stay silent", "algunos efectos por su nombre, así que no sonarán"),
            "reaction" => T("who is an enemy and who is not, so enemies may stay silent", "quién es enemigo y quién no, así que los enemigos pueden no sonar"),
            "animations" => T("what enemies are doing, so dodge and shield warnings stay silent", "lo que hacen los enemigos, así que los avisos de esquivar y de escudo no sonarán"),
            "positions" => T("where things stand, so sounds lose their side", "dónde está cada cosa, así que los sonidos pierden su lado"),
            "camera" => T("the camera, so enemies on screen are not told apart", "la cámara, así que no sé qué enemigos hay en pantalla"),
            "targetable flag" => T("which monsters and characters are in play", "qué monstruos y personajes están en juego"),
            "HUD" or "in-game layer roots" => T("the HUD", "el HUD"),
            "inventories" => T("your inventories, so pick-ups, flasks and salvage stay silent", "tus inventarios, así que recogidas, frascos y desguace no se dirán"),
            "ground items" => T("the items on the ground, so loot stays silent", "los objetos del suelo, así que el botín no sonará"),
            "death screen" => T("the death screen", "la pantalla de muerte"),
            "inventory panel" => T("the inventory", "el inventario"),
            "chat" => T("the chat", "el chat"),
            "map overlay" => T("the map", "el mapa"),
            "objective box" => T("the objective box", "la caja de objetivos"),
            "skill bar" => T("the skill bar", "la barra de habilidades"),
            "area name" => T("the area names", "los nombres de las zonas"),
            "combo boxes" => T("the drop-down lists", "las listas desplegables"),
            "image textures" => T("the button and currency icons", "los iconos de botones y monedas"),
            "tile grid" => T("the places on the map", "los lugares del mapa"),
            "pad buttons" => T("the controller's buttons", "los botones del mando"),
            "passive tree links" or "passive tree allocations" => T("the passive tree's links and allocated nodes", "las conexiones y los nodos asignados del árbol de pasivas"),
            "passive attribute choices" => T("the attribute chosen on each passive node", "el atributo elegido en cada nodo de pasivas"),
            "quest marker" => T("which characters have a quest", "qué personajes tienen misión"),
            "trial map links" => T("the ways between the rooms of the Trial's map", "los caminos entre las salas del mapa de la Prueba"),
            "interactive flag" or "text input flag" or "navigable flag" or "enabled flag" => T("the kinds of interface controls", "los tipos de controles de la interfaz"),
            "game states" or "game state stack" or "game state list" or "in-game state" or "area loading state" => T("the game's state", "el estado del juego"),
            // A part with no wording of its own is an internal name ("no encuentro EntityOffsets" was heard, 2026-11-08): the log has it.
            _ => T("some of the game's data, so some readings may be wrong", "parte de los datos del juego, así que alguna lectura puede ser errónea"),
        };
    }
}
