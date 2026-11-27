namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The place phrases of <see cref="Announcements"/>: unexplored ground, barriers, trial rooms and keys, league devices, boss rooms and signs.</summary>
    public static partial class Announcements
    {
        /// <summary>A scanner entry of the unexplored category: where unseen ground begins.</summary>
        public static string UnexploredArea => T("Unexplored ground", "Zona sin explorar");

        /// <summary>Said to a player long in an area, before the route to where the way to the objective leaves the ground seen.</summary>
        public static string WayOnHint => T("Hint: the way goes on", "Pista: el camino sigue");

        /// <summary>Said after "ya estuviste aquí" while going round in circles, before the way to it.</summary>
        public static string NearestUnexplored => T("Nearest unexplored ground", "Zona sin explorar más cercana");

        /// <summary>Unseen ground with no way on: one look from its edge shows it all.</summary>
        public static string UnexploredPocket => T("Unexplored nook", "Rincón sin explorar");

        /// <summary>
        ///     An unexplored place by how much unseen ground lies behind it, the size first so it is heard before the rest: what
        ///     soon ends, what goes on, a great part of the area. A walk to "unexplored ground" that was a dead end cost an hour
        ///     in an area of Act 2 (user 2026-11-12).
        /// </summary>
        public static string Unexplored(Services.UnseenSize size, IReadOnlyList<string> leadsTo) =>
            string.Join(", ", leadsTo.Prepend(Unexplored(size)));

        /// <summary>The known place a way leads to: "hacia El valle rojo", "hacia El valle rojo, tu misión".</summary>
        public static string WayTo(string name, bool quest) => quest ? QuestPlace(T($"toward {name}", $"hacia {name}")) : T($"toward {name}", $"hacia {name}");

        /// <summary>A place the quest asks for, or the exit straight to it: "Nueva salida: El valle rojo, tu misión".</summary>
        public static string QuestPlace(string place) => T($"{place}, your quest", $"{place}, tu misión");

        private static string Unexplored(Services.UnseenSize size) => size switch
        {
            Services.UnseenSize.Pocket => UnexploredPocket,
            Services.UnseenSize.Small => T("Small unexplored ground", "Zona pequeña sin explorar"),
            Services.UnseenSize.Large => T("Large unexplored ground", "Zona grande sin explorar"),
            _ => UnexploredArea,
        };

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

        /// <summary>The room of a Trial's floor the player arrives in, by the kind its map gave it: "Room: Escape Trial".</summary>
        public static string TrialRoomEntered(string kind) => T($"Room: {kind}", $"Sala: {kind}");

        /// <summary>Carried to ground of the same area no walk joins, as down a tomb's stairs: only what lies here is told now.</summary>
        public static string EnteredGroundApart => T("Separate zone: only what is here is told", "Zona aparte: solo se dice lo que hay aquí");

        /// <summary>Carried back from ground apart to the area's main ground.</summary>
        public static string BackToMainGround => T("Back on the main map", "De vuelta al mapa principal");

        /// <summary>A quest goal on other ground of the area, under the quest: "Misión. Antes, pasa por Escaleras, 26 pasos…".</summary>
        public static string GoThroughFirst(string way) => T($"first go through {way}", $"antes, pasa por {way}");

        /// <summary>The way between two grounds of the area when its exit is not listed: where the player was carried from.</summary>
        public static string WhereCarried => T("the passage you came by", "el paso por el que viniste");

        /// <summary>The part of the map just walked into and how it stands: "Top right corner. Explored 4 percent".</summary>
        public static string MapPartEntered(int across, int down, Services.MapPart? standing) => Sentences(new[]
        {
            CornerOfMap(across, down),
            standing is not { } part ? string.Empty : part.Explored ? T("Already explored", "Ya explorado") : Explored(part.Percent),
        });

        /// <summary>Parts of the map just finished, then those still with ground left: "Done: Right edge. Left: Top left corner, Left edge".</summary>
        public static string MapPartsDone(IReadOnlyList<Services.MapPart> done, IReadOnlyList<Services.MapPart> left)
        {
            var finished = string.Join(", ", done.Select(part => CornerOfMap(part.Across, part.Down)));
            var rest = string.Join(", ", left.Select(part => CornerOfMap(part.Across, part.Down)));
            return Sentences(new[]
            {
                T($"Done: {finished}", $"Completado: {finished}"),
                rest.Length > 0 ? T($"Left: {rest}", $"Falta: {rest}") : T("The whole map is explored", "Todo el mapa explorado"),
            });
        }

        /// <summary>
        ///     A look at the map in words: the explored share, the part the player is in, the parts with ground left and the
        ///     nearest unexplored stretches. A map too small for parts says none.
        /// </summary>
        public static string MapGlance(int? percent, (int Across, int Down) here, IReadOnlyList<Services.MapPart> parts, IReadOnlyList<string> stretches, IReadOnlyList<string>? trails = null, string layout = "")
        {
            var place = CornerOfMap(here.Across, here.Down);
            var left = string.Join("; ", parts.Where(part => !part.Explored).Select(part =>
            {
                var name = CornerOfMap(part.Across, part.Down);
                return T($"{name}, {part.Percent} percent", $"{name}, {part.Percent} por ciento");
            }));
            var lines = new List<string>
            {
                percent is { } share ? Explored(share) : string.Empty,
                layout,
                parts.Count > 1 ? T($"You are at: {place}", $"Estás en: {place}") : string.Empty,
                left.Length > 0 ? T($"Left to explore: {left}", $"Por explorar: {left}") : string.Empty,
            };
            lines.AddRange(trails ?? new List<string>());
            lines.AddRange(stretches);
            if (stretches.Count == 0)
            {
                lines.Add(T("No unexplored ground in reach", "Sin zonas por explorar a tu alcance"));
            }

            return Sentences(lines);
        }

        /// <summary>The game has one layout for the area: it is the same in every instance.</summary>
        public static string FixedMap => T("Fixed map", "Mapa fijo");

        /// <summary>The game has several layouts for the area; how many instances in a row, up to now, had this one.</summary>
        /// <param name="varies">Met differing; otherwise it only may, by the files.</param>
        public static string VariableMap(bool varies, int same, bool metBefore) => same switch
        {
            0 when !varies => T("The map may change", "El mapa puede cambiar"),
            1 when !varies => T("The map may change, the same as last time", "El mapa puede cambiar, igual que la última vez"),
            _ when !varies => T($"The map may change, the same as the last {same} times", $"El mapa puede cambiar, igual que las últimas {same} veces"),
            0 when metBefore => T("Variable map, different from last time", "Mapa variable, distinto de la última vez"),
            0 => T("Variable map", "Mapa variable"),
            1 => T("Variable map, the same as last time", "Mapa variable, igual que la última vez"),
            _ => T($"Variable map, the same as the last {same} times", $"Mapa variable, igual que las últimas {same} veces"),
        };

        /// <summary>The scanner's entry for the place of the last death in this area (D2R).</summary>
        public static string DeathPlace => T("Where you died", "Donde moriste");

        /// <summary>A barrier that opened on its own (scanner-style place: "The way opened, 4 steps, left").</summary>
        public static string BarrierOpened => T("The way opened", "Se abrió el paso");

        /// <summary>A way that opened and has not been walked to yet, in the scanner's objects and by its sound's name in the settings.</summary>
        public static string OpenedWay => T("Opened way", "Paso abierto");

        /// <summary>A door seen shut and not opened yet, among the scanner's unexplored: the ground behind it is still to see.</summary>
        public static string PowderBarrel => T("Powder barrel", "Barril de pólvora");

        public static string LitPowderBarrel => T("Powder barrel about to explode", "Barril de pólvora a punto de explotar");

        public static string UnopenedDoor => T("Unopened door", "Puerta sin abrir");

        /// <summary>A closed barrier in the scanner's objects (a tutorial beam, a gate); it has no name of its own.</summary>
        public static string ClosedBarrier => T("Closed barrier", "Barrera cerrada");

        /// <summary>Unseen ground past a closed barrier, among the scanner's unexplored: "Large unexplored ground behind a closed barrier".</summary>
        public static string UnexploredBehindBarrier(Services.UnseenSize size)
        {
            var ground = Unexplored(size);
            return T($"{ground} behind a closed barrier", $"{ground} tras una barrera cerrada");
        }

        /// <summary>
        ///     The guide at a barrier still shut, with the boss alive on the player's side when there is one (it may be what
        ///     keeps the gate shut): "Still closed. The Crowbell is alive in here".
        /// </summary>
        public static string StillClosed(string? boss) => Sentences(new[]
        {
            T("Still closed", "Sigue cerrada"),
            boss is { Length: > 0 } ? T($"{boss} is alive in here", $"{boss} sigue con vida aquí dentro") : string.Empty,
        });

        /// <summary>After a boss's defeat, the barrier whose ground is still to explore: "The closed barrier is 42 steps away".</summary>
        public static string BarrierAfterDefeat(int steps)
        {
            var away = StepsAway(steps);
            return T($"The closed barrier is {away}", $"La barrera cerrada está {away}");
        }

        /// <summary>A room of the Trial's map not revealed yet: the map shows only a few rooms ahead.</summary>
        public static string HiddenRoom => T("Hidden room", "Sala oculta");

        /// <summary>A cell of the Temple Console's map with no room or path on it (the game names it nothing).</summary>
        public static string TempleFreeCell => T("Free", "Libre");

        /// <summary>Said once on the first cell after A on a room card: the card held, to place with A or put back with B.</summary>
        public static string TemplePlacing(string card, int spots) => spots == 1
            ? T($"Holding {card}, 1 place to put it", $"Sujetando {card}, 1 sitio donde colocarla")
            : T($"Holding {card}, {spots} places to put it", $"Sujetando {card}, {spots} sitios donde colocarla");

        /// <summary>The card held, read again in the Room Cards list.</summary>
        public static string TempleCardInHand => T("in hand", "en la mano");

        /// <summary>The room cards left to place, said when the Room Cards list is first read and when the count changes.</summary>
        public static string TempleCardsLeft(int count) => count == 1
            ? T("1 card left", "queda 1 carta")
            : T($"{count} cards left", $"quedan {count} cartas");

        /// <summary>
        ///     A cell's place on the Temple Console's map (`TempleGrid`, from 0): rows from the Vestibule toward Atziri, columns
        ///     across, then what stands beside it by the pad's direction ("arriba Camino, derecha Cuartel").
        /// </summary>
        /// <param name="cut">No path leads to it from the Vestibule: said right after the place.</param>
        /// <param name="here">Inside the temple, the player stands in it.</param>
        /// <param name="done">Inside the temple, its room is completed.</param>
        public static string TempleCellPlace(int row, int column, IReadOnlyList<(Services.TempleGrid.Side Side, string Room)> neighbours, bool cut = false,
            bool here = false, bool done = false)
        {
            var place = T($"row {row + 1}, column {column + 1}", $"fila {row + 1}, columna {column + 1}");
            place = cut ? $"{place}, {T("not connected", "sin conectar")}" : place;
            place = here ? $"{place}, {T("you are here", "estás aquí")}" : place;
            place = done ? $"{place}, {TempleRoomDone}" : place;
            if (neighbours.Count == 0)
            {
                return $"{place}, {T("nothing beside it", "nada al lado")}";
            }

            return $"{place}, {string.Join(", ", neighbours.Select(each => $"{TempleSide(each.Side)} {each.Room}"))}";
        }

        /// <summary>A temple room walked into: its name, and whether it is done.</summary>
        public static string TempleRoomEntered(string room, bool done) => done ? $"{room}, {TempleRoomDone}" : room;

        /// <summary>The room to do next, said with its way once a room is completed: "Próxima sala: Armería".</summary>
        public static string TempleNextRoom(string room) => T($"Next room: {room}", $"Próxima sala: {room}");

        private static string TempleRoomDone => T("completed", "completada");

        private static string TempleSide(Services.TempleGrid.Side side) => side switch
        {
            Services.TempleGrid.Side.Up => T("up", "arriba"),
            Services.TempleGrid.Side.Right => T("right", "derecha"),
            Services.TempleGrid.Side.Down => T("down", "abajo"),
            _ => T("left", "izquierda"),
        };

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

        /// <summary>A piece of a Trial of Chaos round the game shows no name for.</summary>
        public static string ChaosPieceName(Services.ChaosPiece piece) => piece switch
        {
            Services.ChaosPiece.Statue => T("Statue to escort", "Estatua que escoltar"),
            Services.ChaosPiece.BloodCircle => T("Blood circle", "Círculo de sangre"),
            Services.ChaosPiece.BloodAltar => T("Blood altar", "Altar de sangre"),
            Services.ChaosPiece.BossLift => T("Lift to the Trialmaster", "Ascensor al Maestro de pruebas"),
            Services.ChaosPiece.LiftLever => T("Lift lever", "Palanca del ascensor"),
            Services.ChaosPiece.Selector => T("Challenge switch", "Interruptor del desafío"),
            Services.ChaosPiece.Lift => T("Lift", "Ascensor"),
            Services.ChaosPiece.StoneAltar => T("Stone Altar", "Altar de piedra"),
            _ => T("Vaal Soul Core", "Núcleo de alma vaal"),
        };

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
            Services.TrialTrap.StormRune => T("Stormcaller rune", "Runa llamatormentas"),
            Services.TrialTrap.BloodMist => T("Blood mist", "Niebla sangrienta"),
            _ => T("Petrifying statue", "Estatua petrificadora"),
        };

        /// <summary>"Honor 474 de 513", in the game's word for it.</summary>
        public static string HonourAmount(string label, int now, int most) => T($"{label} {now} of {most}", $"{label} {now} de {most}");

        /// <summary>"Agua sagrada 39, más 7": the second number is the HUD's "(+7)".</summary>
        public static string SacredWater(string label, int water, int toCome) =>
            toCome > 0 ? T($"{label} {water}, plus {toCome}", $"{label} {water}, más {toCome}") : T($"{label} {water}", $"{label} {water}");

        /// <summary>A price drawn as a number beside the currency's picture: "200 de Agua sagrada".</summary>
        public static string AmountOf(string amount, string currency) => T($"{amount} {currency}", $"{amount} de {currency}");

        /// <summary>The water a Trial window shows: "Agua sagrada 269" on opening, "Agua sagrada 49, menos 220" after a purchase.</summary>
        public static string WaterInWindow(string label, int now, int change) => change switch
        {
            < 0 => T($"{label} {now}, minus {-change}", $"{label} {now}, menos {-change}"),
            > 0 => T($"{label} {now}, plus {change}", $"{label} {now}, más {change}"),
            _ => SacredWater(label, now, 0),
        };

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

        /// <summary>The Trial's honour restored, by a shrine or a fountain: "Honor recuperado, 80 por ciento".</summary>
        public static string HonourRestored(string label, int percent) => T($"{label} restored, {percent} percent", $"{label} recuperado, {percent} por ciento");

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

        /// <summary>
        ///     The crystal with what its script says it holds (Monolith.ot: <c>num_essences</c>, <c>is_corrupted</c>): "Esencia: Cretino
        ///     de sangre, 2 esencias, corrupta". One essence, the usual, is not counted aloud.
        /// </summary>
        public static string Essence(string monster, long essences, bool corrupted) => Essence(monster, essences, corrupted, System.Array.Empty<string>());

        /// <summary>The same with the essences by name when they read ("Esencia: Cretino de sangre. Esencia de llamas, Esencia del cuerpo"): the count then says nothing more.</summary>
        public static string Essence(string monster, long essences, bool corrupted, System.Collections.Generic.IReadOnlyList<string> named)
        {
            var name = Essence(monster);
            name = named.Count > 0 ? $"{name}. {string.Join(", ", named)}"
                : essences > 1 ? T($"{name}, {essences} essences", $"{name}, {essences} esencias") : name;
            return corrupted ? T($"{name}, corrupted", $"{name}, corrupta") : name;
        }

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
    }
}
