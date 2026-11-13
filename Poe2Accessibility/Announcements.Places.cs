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

        /// <summary>Unseen ground with no way on: one look from its edge shows it all.</summary>
        public static string UnexploredPocket => T("Unexplored nook", "Rincón sin explorar");

        /// <summary>
        ///     An unexplored place by how much unseen ground lies behind it, the size first so it is heard before the rest: what
        ///     soon ends, what goes on, a great part of the area. A walk to "unexplored ground" that was a dead end cost an hour
        ///     in an area of Act 2 (user 2026-11-12).
        /// </summary>
        public static string Unexplored(Services.UnseenSize size) => size switch
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

        /// <summary>The scanner's entry for the place of the last death in this area (D2R).</summary>
        public static string DeathPlace => T("Where you died", "Donde moriste");

        /// <summary>A barrier that opened on its own (scanner-style place: "The way opened, 4 steps, left").</summary>
        public static string BarrierOpened => T("The way opened", "Se abrió el paso");

        /// <summary>A way that opened and has not been walked to yet, in the scanner's objects and by its sound's name in the settings.</summary>
        public static string OpenedWay => T("Opened way", "Paso abierto");

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
