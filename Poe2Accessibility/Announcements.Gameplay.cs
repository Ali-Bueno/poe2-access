namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of the guide beacon, vitals, target and text fields (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>"Guiding to Clearfell, 116 steps, down 3, then left" (the scanner's route description).</summary>
        public static string BeaconLocked(string described) => T($"Guiding to {described}", $"Guiando hacia {described}");

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

        /// <summary>The guide reached a turn: "Now up right, 12".</summary>
        public static string RouteTurn(string direction, int steps) => T($"Now {direction}, {steps}", $"Ahora {direction}, {steps}");

        public static string BeaconOff => T("Guide off", "Guía desactivada");

        public static string BeaconArrived(string name) => T($"Arrived at {name}", $"Llegaste a {name}");

        /// <summary>The guide's target (a boss, an NPC) hid, died or left view near the player: not reached, only gone.</summary>
        public static string BeaconLost(string name) => T($"{name} is no longer in sight", $"{name} ya no está a la vista");

        /// <summary>The player reached the place of a target that is no longer there; the guide lets go.</summary>
        public static string BeaconLastSeen(string name) => T($"Arrived where {name} was last seen", $"Llegaste a donde se vio por última vez a {name}");

        /// <summary>The route stops short of the target (closed gate): the guide leads as close as the way goes.</summary>
        public static string BeaconBarrierRoute(string name) =>
            T($"A closed barrier cuts the way to {name}; guiding as close as it goes", $"Una barrera cerrada corta el camino a {name}; te guío lo más cerca posible");

        /// <summary>The way to a door the game opened, whole a moment ago, is cut: it shut, and the guide leaves it.</summary>
        public static string BeaconWayShut(string name) =>
            T($"The way to {name} has closed; the guide stops", $"El camino a {name} se cerró; la guía se detiene");

        public static string BeaconPartialRoute(string name) =>
            T($"The way to {name} is closed further on; guiding as close as it goes", $"El camino a {name} está cerrado más adelante; te guío lo más cerca posible");

        /// <summary>The route search ran out of budget: the way is not known to be closed, and the guide searches again from further on.</summary>
        public static string BeaconLongWay => T("The way is long, still searching", "La ruta es larga, sigo buscando");

        /// <summary>An object no table names, outside English: the file's word kept so it can be told apart (<see cref="Services.ObjectNames.Spoken"/>).</summary>
        public static string UnnamedObject(string fileWord) => T($"Unnamed object, {fileWord}", $"Objeto sin nombre, {fileWord}");

        /// <summary>The player reached the end of a route that stops short; the way on is still shut.</summary>
        public static string BeaconAtClosest(string name) =>
            T($"As close to {name} as the way goes; it is still closed", $"Estás lo más cerca posible de {name}; el camino sigue cerrado");

        public static string BeaconNoRoute(string name) => T($"No route to {name} yet", $"Todavía no hay ruta hacia {name}");

        public static string BeaconNeedsChoice => T("Choose something with the scanner first", "Primero elige algo con el escáner");

        /// <summary>Mana fell below a threshold: "Mana 25 percent".</summary>
        public static string LowMana(int percent) => T($"Mana {percent} percent", $"Maná {percent} por ciento");

        /// <summary>Energy shield fell below a threshold: "Energy shield 50 percent".</summary>
        public static string LowEnergyShield(int percent) => T($"Energy shield {percent} percent", $"Escudo de energía {percent} por ciento");

        public static string EnergyShieldGone => T("Energy shield depleted", "Escudo de energía agotado");

        /// <summary>The player's heavy stun bar rose past a step while blocking or parrying (the game's "stun bar"): "Stun bar 80 percent".</summary>
        public static string StunBar(int percent) => T($"Stun bar {percent} percent", $"Barra de aturdimiento {percent} por ciento");

        /// <summary>
        ///     A flask as it stands, for the vitals readout and after a drink: "Frasco de vida menor, 2 usos"; by its charges
        ///     while what a drink of it costs has not been seen (<paramref name="uses"/> null): "Frasco de vida menor, 71 cargas".
        /// </summary>
        public static string Flask(string name, int charges, int? uses) => (charges, uses) switch
        {
            (<= 0, _) or (_, <= 0) => FlaskEmpty(name),
            (_, 1) => T($"{name}, 1 use", $"{name}, 1 uso"),
            (_, { } left) => T($"{name}, {left} uses", $"{name}, {left} usos"),
            (1, _) => T($"{name}, 1 charge", $"{name}, 1 carga"),
            _ => T($"{name}, {charges} charges", $"{name}, {charges} cargas"),
        };

        /// <summary>How much of the area's reachable ground has been seen, for the vitals readout (D2R): "Explorado 40 por ciento".</summary>
        public static string Explored(int percent) => T($"Explored {percent} percent", $"Explorado {percent} por ciento");

        /// <summary>A flask with no use left: "Frasco de vida menor vacío".</summary>
        public static string FlaskEmpty(string name) => T($"{name} empty", $"{name} vacío");

        /// <summary>A flask that gained a use from kills or time: "Frasco de vida menor recargado, 2 usos".</summary>
        public static string FlaskRefilled(string name, int uses) => uses == 1
            ? T($"{name} refilled, 1 use", $"{name} recargado, 1 uso")
            : T($"{name} refilled, {uses} uses", $"{name} recargado, {uses} usos");

        /// <summary>An empty flask filled again before any drink showed what one costs: by its charges.</summary>
        public static string FlaskRefilledCharges(string name, int charges) => T($"{name} refilled, {charges} charges", $"{name} recargado, {charges} cargas");

        /// <summary>A flask put on the belt during play: "Nuevo frasco: Frasco de vida mayor, 3 usos".</summary>
        public static string FlaskArrived(string flask) => T($"New flask: {flask}", $"Nuevo frasco: {flask}");

        /// <summary>"Life 80 percent, mana 40 percent, energy shield 100 percent" (energy shield only when the character has some).</summary>
        public static string Vitals(int life, int mana, int? energyShield)
        {
            var parts = new List<string>
            {
                T($"Life {life} percent", $"Vida {life} por ciento"),
                T($"mana {mana} percent", $"maná {mana} por ciento"),
            };
            if (energyShield is { } shield)
            {
                parts.Add(T($"energy shield {shield} percent", $"escudo de energía {shield} por ciento"));
            }

            return string.Join(", ", parts);
        }

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

        /// <summary>"Recogido: Traje de ermitaño"; a stack that grew: "Recogido 2 más: Pergamino de sabiduría, 5".</summary>
        /// <summary>The cursor took an item (D2R "item in hand"): what, and what to do with it.</summary>
        public static string InHand(string name) =>
            T($"In hand: {name}. Choose a cell or a matching equipment slot to place it",
              $"En la mano: {name}. Elige una celda o una ranura de equipo compatible para colocarlo");

        /// <summary>An item now in an equipment slot (by the cursor or the equip button): what and where.</summary>
        public static string Equipped(string name, string slot) => T($"Equipped: {name}, {slot}", $"Equipado: {name}, {slot}");

        public static string Unequipped(string name) => T($"Unequipped: {name}", $"Desequipado: {name}");

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

        /// <summary>The weapon set swapped to and what it holds (D2R "Armas 2: Arco largo, Carcaj").</summary>
        public static string WeaponSet(int number, System.Collections.Generic.IReadOnlyList<string> held) =>
            held.Count == 0
                ? T($"Weapon set {number}, empty", $"Set de armas {number}, vacío")
                : T($"Weapon set {number}: ", $"Set de armas {number}: ") + string.Join(", ", held);

        /// <summary>
        ///     What happened to an item moved with a panel open (<see cref="Services.ItemMoves"/>), said as the deed: "Vendido:
        ///     Brazales de gamuza, por 19 de oro", "Movido a Alijo: …" (the panel's own title), "Tirado: …".
        /// </summary>
        public static string Sold(string names, long gold) => T($"Sold: {names}, for {gold} gold", $"Vendido: {names}, por {gold} de oro");

        public static string Bought(string names, long gold) => T($"Bought: {names}, for {gold} gold", $"Comprado: {names}, por {gold} de oro");

        /// <summary>The game's Skills panel title (ClientStrings SkillGemPanelTitle), to know it beside the inventory.</summary>
        public static string SkillGemPanel => T("Skills", "Habilidades");

        /// <summary>The game's salvage bench title (ClientStrings SalvageBenchTitle), to know it beside the inventory.</summary>
        public static string SalvageBenchPanel => T("Salvage Bench", "Mesa de reciclaje");

        /// <summary>"Reciclado: Corona con cuernos, raro. Recibes: Chatarra de armero, 2".</summary>
        public static string Salvaged(Services.SalvageOutcome outcome)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (outcome.Salvaged.Count > 0)
            {
                var names = string.Join(", ", outcome.Salvaged);
                lines.Add(T($"Salvaged: {names}", $"Reciclado: {names}"));
            }

            if (outcome.Received.Count > 0)
            {
                var items = string.Join("; ", outcome.Received.Select(item => Item(item.Title, GameReader.RemoteEnums.Rarity.Normal, item.Count)));
                lines.Add(T($"Received: {items}", $"Recibes: {items}"));
            }

            return string.Join(". ", lines);
        }

        /// <summary>"Furia I, engarzada en Campana de tempestad".</summary>
        public static string SupportEngraved(string support, string skill) => T($"{support}, socketed in {skill}", $"{support}, engarzada en {skill}");

        public static string MovedTo(string name, string panel) => T($"Moved to {panel}: {name}", $"Movido a {panel}: {name}");

        public static string TakenFrom(string name, string panel) => T($"Taken from {panel}: {name}", $"Sacado de {panel}: {name}");

        public static string Obtained(string name) => T($"Obtained: {name}", $"Obtenido: {name}");

        public static string Dropped(string name) => T($"Dropped: {name}", $"Tirado: {name}");

        /// <summary>What a trade took, a disenchanted item among them.</summary>
        public static string HandedOver(string names) => T($"Handed over: {names}", $"Entregado: {names}");

        public static string UsedUp(string name) => T($"Used: {name}", $"Usado: {name}");

        public static string HandEmpty => T("You no longer hold an item", "Ya no llevas ningún objeto");

        /// <summary>A scanner entry of the unexplored category: where unseen ground begins.</summary>
        public static string UnexploredArea => T("Unexplored ground", "Zona sin explorar");

        /// <summary>Unseen ground with no way on: one look from its edge shows it all.</summary>
        public static string UnexploredPocket => T("Unexplored nook", "Rincón sin explorar");

        /// <summary>Which part of the map a place lies in, by thirds across and down (-1, 0, 1), as the screen draws it.</summary>
        public static string CornerOfMap(int across, int down) => (across, down) switch
        {
            (-1, -1) => T("Top left corner", "Esquina superior izquierda"),
            (1, -1) => T("Top right corner", "Esquina superior derecha"),
            (-1, 1) => T("Bottom left corner", "Esquina inferior izquierda"),
            (1, 1) => T("Bottom right corner", "Esquina inferior derecha"),
            (0, -1) => T("Top edge", "Borde superior"),
            (0, 1) => T("Bottom edge", "Borde inferior"),
            (-1, 0) => T("Left edge", "Borde izquierdo"),
            (1, 0) => T("Right edge", "Borde derecho"),
            _ => T("Middle of the map", "Centro del mapa"),
        };

        /// <summary>The scanner's entry for the place of the last death in this area (D2R).</summary>
        public static string DeathPlace => T("Where you died", "Donde moriste");

        /// <summary>A barrier that opened on its own (scanner-style place: "The way opened, 4 steps, left").</summary>
        public static string BarrierOpened => T("The way opened", "Se abrió el paso");

        /// <summary>A closed barrier in the scanner's objects (a tutorial beam, a gate); it has no name of its own.</summary>
        public static string ClosedBarrier => T("Closed barrier", "Barrera cerrada");

        /// <summary>A room of the Trial's map not revealed yet: the map shows only a few rooms ahead.</summary>
        public static string HiddenRoom => T("Hidden room", "Sala oculta");

        /// <summary>A room's place on the Trial's map, columns running from the entrance to the boss; to enter a room, its tooltip says so.</summary>
        /// <param name="through">With the links known: the rooms to enter that lead to this one, when there is a choice (rows from 0).</param>
        /// <param name="leadsTo">With the links known: the rooms of the next column this one leads to (rows from 0).</param>
        public static string TrialRoomPlace(
            int column, int columns, int room, int rooms, Services.TrialRoomStanding standing, IReadOnlyList<int>? through = null, IReadOnlyList<int>? leadsTo = null)
        {
            var parts = new List<string> { TrialRoomStands(column, columns, room, rooms, standing) };
            if (through is { Count: > 0 })
            {
                var list = string.Join(", ", through.Select(each => each + 1));
                parts.Add(through.Count == 1 ? T($"through room {list}", $"por la sala {list}") : T($"through rooms {list}", $"por las salas {list}"));
            }

            if (leadsTo is { Count: > 0 })
            {
                var list = string.Join(", ", leadsTo.Select(each => each + 1));
                parts.Add(leadsTo.Count == 1 ? T($"leads to room {list}", $"lleva a la sala {list}") : T($"leads to rooms {list}", $"lleva a las salas {list}"));
            }

            return string.Join(", ", parts);
        }

        private static string TrialRoomStands(int column, int columns, int room, int rooms, Services.TrialRoomStanding standing)
        {
            var place = T($"column {column} of {columns}, room {room} of {rooms}", $"columna {column} de {columns}, sala {room} de {rooms}");
            return standing switch
            {
                Services.TrialRoomStanding.Reachable => $"{place}, {T("on your way", "en tu camino")}",
                Services.TrialRoomStanding.Here => $"{place}, {T("your column", "tu columna")}",
                Services.TrialRoomStanding.Current => $"{place}, {T("your room", "tu sala")}",
                Services.TrialRoomStanding.Behind => $"{place}, {T("left behind", "ya quedó atrás")}",
                Services.TrialRoomStanding.Ahead => $"{place}, {T("not reachable yet", "aún no puedes entrar")}",
                Services.TrialRoomStanding.Beside => $"{place}, {T("no way to it from your room", "sin camino desde tu sala")}",
                Services.TrialRoomStanding.Next => $"{place}, {T("next column", "columna siguiente")}",
                _ => place,
            };
        }

        /// <summary>A trap of the Trials as the scanner and the approach name it.</summary>
        public static string TrialTrapName(Services.TrialTrap trap) => trap switch
        {
            Services.TrialTrap.Spikes => T("Spike trap", "Trampa de pinchos"),
            Services.TrialTrap.Laser => T("Laser turret", "Torreta láser"),
            Services.TrialTrap.Mine => T("Proximity mine", "Mina de proximidad"),
            Services.TrialTrap.Turret => T("Projectile turret", "Torreta de proyectiles"),
            Services.TrialTrap.Quicksand => T("Quicksand", "Arenas movedizas"),
            Services.TrialTrap.PressurePlate => T("Pressure plate", "Placa de presión"),
            Services.TrialTrap.TimeTrap => T("Time trap", "Trampa temporal"),
            Services.TrialTrap.ProjectilePortal => T("Projectile portal", "Portal de proyectiles"),
            _ => T("Petrifying statue", "Estatua petrificadora"),
        };

        /// <summary>"Honor 474 de 513", in the game's word for it.</summary>
        public static string HonourAmount(string label, int now, int most) => T($"{label} {now} of {most}", $"{label} {now} de {most}");

        /// <summary>"Agua sagrada 39, más 7": the second number is the HUD's "(+7)".</summary>
        public static string SacredWater(string label, int water, int toCome) =>
            toCome > 0 ? T($"{label} {water}, plus {toCome}", $"{label} {water}, más {toCome}") : T($"{label} {water}", $"{label} {water}");

        /// <summary>"Llaves: bronce 1, plata 0, oro 0".</summary>
        public static string TrialKeys(int bronze, int silver, int gold) =>
            T($"Keys: bronze {bronze}, silver {silver}, gold {gold}", $"Llaves: bronce {bronze}, plata {silver}, oro {gold}");

        /// <summary>A key just gained: "Llave de bronce, 1".</summary>
        public static string BronzeKey(int count) => T($"Bronze key, {count}", $"Llave de bronce, {count}");

        /// <inheritdoc cref="BronzeKey"/>
        public static string SilverKey(int count) => T($"Silver key, {count}", $"Llave de plata, {count}");

        /// <inheritdoc cref="BronzeKey"/>
        public static string GoldKey(int count) => T($"Gold key, {count}", $"Llave de oro, {count}");

        /// <summary>The Trial's honour: "Honor 25 por ciento", in the game's word for it.</summary>
        public static string HonourLeft(string label, int percent) => T($"{label} {percent} percent", $"{label} {percent} por ciento");

        /// <summary>A clock falling on the HUD: "Quedan 10 segundos".</summary>
        public static string SecondsLeft(int seconds) => T($"{seconds} seconds left", $"Quedan {seconds} segundos");

        /// <summary>What the minimap marks with the quest's icon and the game gives no name.</summary>
        public static string QuestObject => T("Quest object", "Objeto de misión");

        /// <summary>A cage to open, for which the game has no name, called after who is inside: "Jaula de Risu".</summary>
        public static string CageOf(string held) => T($"{held}'s cage", $"Jaula de {held}");

        /// <summary>A cage with nobody seen inside yet.</summary>
        public static string Cage => T("Cage", "Jaula");

        /// <summary>
        ///     A league device by its minimap icon (MinimapIcons.dat id), as the game's own strings call it (ClientStrings
        ///     "Altar de ritual {0}", "Interactúa para ver los favores"); the devices have no render name.
        /// </summary>
        /// <param name="gameName">The device's own label (minimap icon, else its league folder), empty when the game gives none.</param>
        /// <param name="boss">The altar that summons an area's boss, not one of those its quest sends the player to.</param>
        public static string LeagueDevice(string icon, bool boss, string gameName) => icon switch
        {
            Services.MinimapMarks.RitualDoneIcon => T("Ritual altar, done", "Altar de ritual, completado"),
            Services.MinimapMarks.RitualIcon when boss => T("Boss's ritual altar", "Altar de ritual del jefe"),
            Services.MinimapMarks.RitualIcon => T("Ritual altar", "Altar de ritual"),
            _ when gameName.Length > 0 => gameName,
            _ => T("League encounter", "Encuentro de liga"),
        };

        /// <summary>An essence's crystal with the monster it holds: "Essence: Blood Cretin".</summary>
        public static string Essence(string monster) => monster.Length > 0 ? T($"Essence: {monster}", $"Esencia: {monster}") : T("Essence", "Esencia");

        /// <summary>A boss's arena as its minimap icon labels it ("Sala del jefe: Corazón de la tumba"); map bosses have no label.</summary>
        public static string BossRoom(string label) => label.Length > 0
            ? T($"Boss room: {label}", $"Sala del jefe: {label}")
            : T("Boss room", "Sala del jefe");

        /// <summary>The arena of a boss already slain (its icon turned grey).</summary>
        public static string BossRoomDefeated(string label) => label.Length > 0
            ? T($"Boss room, defeated: {label}", $"Sala del jefe, derrotado: {label}")
            : T("Boss room, defeated", "Sala del jefe, derrotado");

        /// <summary>A readable sign or plaque (the game's story glyphs: "Pueblo de Ogham" by the road, "Proclamación de Asinia").</summary>
        public static string Sign(string name) => T($"Sign: {name}", $"Letrero: {name}");

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
