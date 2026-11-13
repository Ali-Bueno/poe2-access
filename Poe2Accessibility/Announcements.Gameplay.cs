namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of the guide beacon, vitals, target and text fields (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>A resistance as the sheet gives it: "27%", and what it would be without its cap when that differs.</summary>
        public static string Resistance(int value, int uncapped) =>
            uncapped == value ? $"{value}%" : T($"{value}%, uncapped {uncapped}%", $"{value}%, sin tope {uncapped}%");

        /// <summary>A skill bar set when the game gives no title for it.</summary>
        public static string SkillSetTitle(int set) =>
            set == 0 ? T("Primary skill set", "Set de habilidades principal") : T("Secondary skill set", "Set de habilidades secundario");

        /// <summary>
        ///     One set of the skill bar in screen order, the free buttons together at the end:
        ///     "Primary skill set: RT, Fragmentation Rounds. X, Basic Bolt. Empty: LB, Y".
        /// </summary>
        public static string SkillSet(string title, IEnumerable<(string Button, string Bound)> bindings)
        {
            var bound = new List<string>();
            var free = new List<string>();
            foreach (var (button, name) in bindings)
            {
                if (name.Length > 0)
                {
                    bound.Add($"{button}, {name}");
                }
                else
                {
                    free.Add(button);
                }
            }

            if (free.Count > 0)
            {
                bound.Add($"{T("Empty", "Vacío")}: {string.Join(", ", free)}");
            }

            return $"{title}: {string.Join(". ", bound)}";
        }

        /// <summary>How much of the area's reachable ground has been seen, for the vitals readout (D2R): "Explorado 40 por ciento".</summary>
        public static string Explored(int percent) => T($"Explored {percent} percent", $"Explorado {percent} por ciento");

        /// <summary>The text field closed: what it holds now, so the player knows editing ended.</summary>
        public static string EditFinished(string value) => value.Length > 0
            ? T($"{value}, editing done", $"{value}, edición terminada")
            : T("Editing done, field empty", "Edición terminada, campo vacío");

        /// <summary>The creation screen opens with no class highlighted: A does nothing before the stick moves.</summary>
        public static string CreationClassHelp => T(
            "Choose your class: move the left stick, then press A.",
            "Elige tu clase: mueve el stick izquierdo y pulsa A.");

        /// <summary>
        ///     Character creation's name step: the field takes typing at once and the stick only moves once B has left it;
        ///     Enter, or A on Continue, creates the character. The name's limits are the game's, measured live 2026-11-06.
        /// </summary>
        public static string CreationNameHelp => T(
            "Type the name: 3 to 23 letters or underscores, no numbers or spaces. Enter creates the character. B leaves the field; the left stick then moves between name, league and Continue, and A on the name types again.",
            "Escribe el nombre: de 3 a 23 letras o guiones bajos, sin números ni espacios. Intro crea el personaje. B sale del campo; luego el stick izquierdo pasa por nombre, liga y Continuar, y A en el nombre vuelve a escribir.");

        /// <summary>Character selection's filter: only a stick glyph beside it says how to reach it.</summary>
        public static string LeagueFilterHint => T("League filter, left stick click", "Filtro de liga, clic del stick izquierdo");

        /// <summary>The login's globe button: only a glyph beside it shows its button.</summary>
        public static string PatchNotesHint => T("Patch notes in the web browser, LB", "Notas del parche en el navegador, LB");

        /// <summary>Why a press on the name step did nothing; the game says none of it.</summary>
        public static string ForCreation(Services.CreationAdvice advice) => advice switch
        {
            Services.CreationAdvice.TypeOnTheName => T("Not typing: go to the name and press A.", "No estás escribiendo: ve al nombre y pulsa A."),
            Services.CreationAdvice.CharacterRefused => T("Not allowed: letters and underscore only.", "No admitido: solo letras y guion bajo."),
            Services.CreationAdvice.NameFull => T("Name full: 23 characters at most.", "Nombre completo: máximo 23 caracteres."),
            Services.CreationAdvice.NameTooShort => T("Name too short: at least 3 letters. Continue stays off until then.", "Nombre demasiado corto: mínimo 3 letras. Continuar sigue desactivado hasta entonces."),
            Services.CreationAdvice.HowToFinish => T("To create the character: Enter, or B and then A on Continue.", "Para crear el personaje: Intro, o B y luego A en Continuar."),
            _ => string.Empty,
        };

        /// <summary>A world map pin's monster level, when the game's own words are not in the table (they normally are).</summary>
        public static string PinLevel(int level) => T($"Monster level: {level}", $"Nivel de monstruos: {level}");

        /// <summary>A town pin's kind, when the game's own word is not in the table.</summary>
        public static string PinTown => T("Town", "Pueblo");

        /// <summary>An exit's own hint: holding the interact button offers its options (a new instance).</summary>
        public static string HoldForOptions(string button) => T($"hold {button} for options", $"mantén {button} para opciones");

        /// <summary>The chat panel shown, with its hint ("Hold LT to chat").</summary>
        public static string ChatShown(string hint) => T($"Chat. {hint}", $"Chat. {hint}");

        public static string ChatHidden => T("Chat hidden", "Chat oculto");

        /// <summary>The chat channel chosen in its list (the game's own channel name).</summary>
        public static string ChatChannel(string name) => T($"Channel: {name}", $"Canal: {name}");

        /// <summary>The large map over the world shown, with its hint ("Hold D-pad down to interact with the map").</summary>
        public static string MapShown(string hint) => hint.Length == 0 ? T("Map", "Mapa") : T($"Map. {hint}", $"Mapa. {hint}");

        public static string MapHidden => T("Map hidden", "Mapa oculto");

        /// <summary>The eight sides a map hint can go, by <see cref="Services.StickDirection"/> octant: "sigue a la derecha".</summary>
        public static string[] HintSides => new[]
        {
            T("to the right", "a la derecha"), T("up right", "arriba a la derecha"), T("up", "arriba"), T("up left", "arriba a la izquierda"),
            T("to the left", "a la izquierda"), T("down left", "abajo a la izquierda"), T("down", "abajo"), T("down right", "abajo a la derecha"),
        };

        /// <summary>A line and the side it goes, when known: "Letrero: Pueblo de Ogham, a la derecha".</summary>
        public static string WithSide(string line, string side) => side.Length > 0 ? $"{line}, {side}" : line;

        /// <summary>A cue GGG placed on the map, as its look tells where it goes (<see cref="Services.MapHintRow.Hint"/>); side empty when not known.</summary>
        public static string MapHint(string hint, string side) => (hint, side.Length > 0) switch
        {
            ("cattle", true) => T($"Dead cattle, the blood trail goes {side}", $"Reses muertas, el rastro de sangre sigue {side}"),
            ("cattle", false) => T("Dead cattle", "Reses muertas"),
            ("mushrooms", true) => T($"Mushrooms, thicker {side}", $"Setas, más espesas {side}"),
            ("mushrooms", false) => T("Mushrooms", "Setas"),
            ("roots", true) => T($"Glowing roots, running {side}", $"Raíces luminosas, siguen {side}"),
            ("roots", false) => T("Glowing roots", "Raíces luminosas"),
            ("river", true) => T($"River, upstream {side}", $"Río, corriente arriba {side}"),
            ("river", false) => T("River", "Río"),
            ("camp", _) => T("Explorer camp", "Campamento de exploradores"),
            _ => hint,
        };

        /// <summary>An NPC with the quest indicator over its head.</summary>
        public static string NpcWithQuest(string name) => T($"{name}, has a quest", $"{name}, con misión");

        public static string GoldPickedUp(long amount) => T($"Picked up {amount} gold", $"Recogido {amount} de oro");

        public static string PickedUp(string name, int added) => added > 0
            ? T($"Picked up {added} more: {name}", $"Recogido {added} más: {name}")
            : T($"Picked up: {name}", $"Recogido: {name}");

        /// <summary>The rarity word that ends a unique monster's scanner name ("El molinero hinchado, único").</summary>
        public static string UniqueRarityWord => RarityName(GameReader.RemoteEnums.Rarity.Unique);

        public static string RareRarityWord => RarityName(GameReader.RemoteEnums.Rarity.Rare);

        /// <summary>Enemies coming into view, biggest pack first: "Ahogado, 3, derecha. Lobo podrido, raro, arriba".</summary>
        public static string EnemiesAppeared(IEnumerable<(string Name, int Count, int Direction)> groups, bool directions = true) =>
            string.Join(". ", groups.Select(group =>
            {
                var named = group.Count > 1 ? $"{group.Name}, {group.Count}" : group.Name;
                return directions ? $"{named}, {StickDirections[group.Direction]}" : named;
            }));

        /// <summary>
        ///     The death screen read on its own: "Has muerto. resucitar en el punto de control"; hidden, it says so and that Y
        ///     (the game's hint "toggle death screen visibility [Y]") shows it.
        /// </summary>
        public static string DeathScreen(IReadOnlyList<string> texts, bool shown)
        {
            var read = texts.Count > 0 ? string.Join(". ", texts) : T("You have died", "Has muerto");
            return shown ? read : $"{read}. {DeathScreenHidden}";
        }

        /// <summary>A trade's other side with nothing on it.</summary>
        public static string Nothing => T("nothing", "nada");

        public static string DeathScreenHidden => T("The death screen is hidden: Y shows it", "La pantalla de muerte está oculta: Y la muestra");

        /// <summary>A place on a map by what it is beside: "cerca de Campamento de Sierraclara".</summary>
        public static string NearPlace(string name) => T($"near {name}", $"cerca de {name}");

        /// <summary>A straight-line distance in scanner steps: "a 35 pasos".</summary>
        public static string StepsAway(int steps) => T($"{Steps(steps)} away", $"a {Steps(steps)}");

        /// <summary>Something and where it lies on the screen (a <see cref="StickDirections"/> octant): "Nueva salida: Sierraclara, arriba a la izquierda".</summary>
        public static string Toward(string what, int octant) => $"{what}, {StickDirections[octant]}";

        /// <summary>A first sighting: "Nueva salida: Sierraclara", "Nuevo punto de control", "Jefe a la vista: El molinero hinchado".</summary>
        public static string Discovered(Services.ScanEntry entry, string near = "") => entry.Kind switch
        {
            "checkpoint" when near.Length > 0 => T($"New checkpoint, {NearPlace(near)}", $"Nuevo punto de control, {NearPlace(near)}"),
            "checkpoint" => T("New checkpoint", "Nuevo punto de control"),
            "waypoint" => T($"New waypoint: {entry.Name}", $"Nuevo punto de ruta: {entry.Name}"),
            "portal" => T($"Portal: {entry.Name}", $"Portal: {entry.Name}"),
            _ when entry.Category == Services.ScanCategory.PointsOfInterest => T($"On the map: {entry.Name}", $"En el mapa: {entry.Name}"),
            _ when entry.Category == Services.ScanCategory.Enemies =>
                T($"Boss in sight: {entry.Name[..entry.Name.LastIndexOf(',')]}", $"Jefe a la vista: {entry.Name[..entry.Name.LastIndexOf(',')]}"),
            _ => T($"New exit: {entry.Name}", $"Nueva salida: {entry.Name}"),
        };

        /// <summary>
        ///     Requirements the character does not meet (drawn red), said after the item's name (D2R "cannot equip"):
        ///     "No puedes usarlo: Requiere Nivel 5".
        /// </summary>
        public static string Unmet(string requirements) => T($"Cannot use: {requirements}", $"No puedes usarlo: {requirements}");

        /// <summary>A named map piece or prop of the terrain files (catalog/places.json kinds).</summary>
        public static string PlaceName(string kind) => kind switch
        {
            "bridge" => T("bridge", "puente"),
            "stairs" => T("stairs", "escaleras"),
            "door" => T("door", "puerta"),
            "gate" => T("gate", "portón"),
            "ruins" => T("ruins", "ruinas"),
            "church" => T("church", "iglesia"),
            "graveyard" => T("graveyard", "cementerio"),
            "hut" => T("hut", "cabaña"),
            "tower" => T("tower", "torre"),
            "temple" => T("temple", "templo"),
            "arena" => T("arena", "arena"),
            "entrance" => T("entrance", "entrada"),
            "dock" => T("dock", "muelle"),
            "waterfall" => T("waterfall", "cascada"),
            "encampment" => T("encampment", "campamento"),
            "courtyard" => T("courtyard", "patio"),
            "alley" => T("alley", "callejón"),
            "ramp" => T("ramp", "rampa"),
            "lake" => T("lake", "lago"),
            "corridor" => T("corridor", "pasillo"),
            "tomb" => T("tomb", "tumba"),
            "forge" => T("forge", "forja"),
            "statue" => T("statue", "estatua"),
            "well" => T("well", "pozo"),
            "boat" => T("boat", "barca"),
            "market" => T("market", "mercado"),
            "garden" => T("garden", "jardín"),
            "library" => T("library", "biblioteca"),
            "cart" => T("cart", "carro"),
            "campfire" => T("campfire", "fogata"),
            "anvil" => T("anvil", "yunque"),
            "noticeboard" => T("notice board", "tablón de anuncios"),
            "waterwheel" => T("water wheel", "rueda del molino"),
            "tent" => T("tent", "tienda de campaña"),
            "stall" => T("stall", "puesto"),
            "altar" => T("altar", "altar"),
            "fountain" => T("fountain", "fuente"),
            "throne" => T("throne", "trono"),
            _ => kind,
        };

        /// <summary>A monster's modifiers after its name and rarity, in the game's words and order.</summary>
        public static string WithMods(string name, System.Collections.Generic.IReadOnlyList<string> mods) =>
            mods.Count == 0 ? name : $"{name}, {string.Join(", ", mods)}";

        /// <summary>The thing the right stick aims at: "Drowned", "Bloated Miller, unique, 40 percent".</summary>
        public static string Target(string name, int? lifePercent) => lifePercent is { } life and < 100
            ? T($"{name}, {life} percent", $"{name}, {life} por ciento")
            : name;
    }
}
