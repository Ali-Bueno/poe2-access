namespace Poe2Accessibility
{
    using System;

    /// <summary>Spoken strings of the mod's settings menu: setting and group names, the sound glossary, the controls help and choice values.</summary>
    public static partial class Announcements
    {
        /// <summary>Spoken name of a setting of the settings menu, by field name.</summary>
        public static string SettingName(string field) => field switch
        {
            nameof(AccessibilitySettings.AnnounceTarget) => T("Target names", "Nombres de objetivos"),
            nameof(AccessibilitySettings.AnnounceMonsterMods) => T("Monster modifiers", "Modificadores de monstruos"),
            nameof(AccessibilitySettings.AnnounceLowLife) => T("Low life warnings", "Avisos de vida baja"),
            nameof(AccessibilitySettings.AnnounceLowMana) => T("Low mana warnings", "Avisos de maná bajo"),
            nameof(AccessibilitySettings.AnnounceEnergyShield) => T("Energy shield warnings", "Avisos de escudo de energía"),
            nameof(AccessibilitySettings.AnnounceFlasks) => T("Flask uses", "Usos de los frascos"),
            nameof(AccessibilitySettings.AnnounceBossLife) => T("Boss life", "Vida de jefes"),
            nameof(AccessibilitySettings.StatusAlerts) => T("Harmful states", "Estados alterados"),
            nameof(AccessibilitySettings.ChargeSpeech) => T("Say charges, infusions, surges and Rage", "Decir cargas, infusiones, sobrecargas y furia"),
            nameof(AccessibilitySettings.ChargeSounds) => T("Sounds of charges, infusions, surges and Rage", "Sonidos de cargas, infusiones, sobrecargas y furia"),
            nameof(AccessibilitySettings.ComboSpeech) => T("Say combo points", "Decir puntos de combo"),
            nameof(AccessibilitySettings.ComboSounds) => T("Sounds of combo points", "Sonidos de puntos de combo"),
            nameof(AccessibilitySettings.CooldownSpeech) => T("Say skills ready after their cooldown", "Decir habilidades listas tras recargarse"),
            nameof(AccessibilitySettings.CooldownSounds) => T("Sound of skills ready after their cooldown", "Sonido de habilidades listas tras recargarse"),
            nameof(AccessibilitySettings.AnnounceNearby) => T("Name nearby things", "Nombrar cosas cercanas"),
            nameof(AccessibilitySettings.AnnouncePlaces) => T("Name places you walk into", "Nombrar los lugares por los que pasas"),
            nameof(AccessibilitySettings.AnnounceInteractTarget) => T("What the interact button uses", "Lo que usa el botón de interactuar"),
            nameof(AccessibilitySettings.AnnounceChat) => T("Chat", "Chat"),
            nameof(AccessibilitySettings.AnnouncePlayerChat) => T("Global and trade chat", "Chat global y de comercio"),
            nameof(AccessibilitySettings.ReadTooltipsOnFocus) => T("Whole tooltips on focus", "Descripciones completas al enfocar"),
            nameof(AccessibilitySettings.AnnouncePickups) => T("Picked up items", "Objetos recogidos"),
            nameof(AccessibilitySettings.AnnounceDiscoveries) => T("Discoveries", "Descubrimientos"),
            nameof(AccessibilitySettings.AnnounceEnemies) => T("Enemies coming into view", "Enemigos que aparecen"),
            nameof(AccessibilitySettings.EnemyDirections) => T("Direction of enemies coming into view", "Dirección de los enemigos que aparecen"),
            nameof(AccessibilitySettings.QuietItemsInFights) => T("Silence loot, chests and altars in fights", "Silenciar objetos, cofres y altares en combate"),
            nameof(AccessibilitySettings.QuietChatInFights) => T("Silence characters' lines in fights", "Silenciar frases de personajes en combate"),
            nameof(AccessibilitySettings.AnnounceHand) => T("Item in hand", "Objeto en la mano"),
            nameof(AccessibilitySettings.AnnounceMoves) => T("Equipped and moved items", "Objetos equipados y movidos"),
            nameof(AccessibilitySettings.RoutePrompts) => T("Turn prompts", "Indicaciones de giro"),
            nameof(AccessibilitySettings.PassiveFinder) => T("Passive tree finder on the D-pad", "Buscador del árbol de pasivas en la cruceta"),
            nameof(AccessibilitySettings.PassiveGuideBrief) => T("Brief passive tree guide", "Guía breve en el árbol de pasivas"),
            nameof(AccessibilitySettings.AutoGuide) => T("Guide to the scanner's choice without confirming", "Guiar a lo elegido en el escáner sin confirmar"),
            nameof(AccessibilitySettings.AnnounceTimers) => T("Countdown timers", "Cuentas atrás"),
            nameof(AccessibilitySettings.AnnounceHonour) => T("Honour in the Trial", "Honor en la Prueba"),
            nameof(AccessibilitySettings.AnnounceTrialCounters) => T("Sacred water and keys in the Trial", "Agua sagrada y llaves en la Prueba"),
            nameof(AccessibilitySettings.TrialTrapCues) => T("Trial traps about to strike", "Trampas de la Prueba a punto de saltar"),
            nameof(AccessibilitySettings.GuideTimed) => T("Guide to each Death Crystal by itself", "Guiar solo a cada cristal de la muerte"),
            nameof(AccessibilitySettings.AnnounceTrialProgress) => T("Trial rounds, crystals left and room doors", "Rondas, cristales restantes y puertas de la Prueba"),
            nameof(AccessibilitySettings.GuideTrialDoors) => T("Guide to the room door the Trial opens", "Guiar a la puerta que abre la Prueba"),
            nameof(AccessibilitySettings.AnnounceLevelUp) => T("Level up", "Subida de nivel"),
            nameof(AccessibilitySettings.Radar) => T("Radar", "Radar"),
            nameof(AccessibilitySettings.ReadPlayer1) => T("Local co-op: read for player 1", "Cooperativo local: lectura para el jugador 1"),
            nameof(AccessibilitySettings.ReadPlayer2) => T("Local co-op: read for player 2", "Cooperativo local: lectura para el jugador 2"),
            nameof(AccessibilitySettings.CheckForUpdates) => T("Check for updates at start", "Buscar actualizaciones al iniciar"),
            nameof(AccessibilitySettings.SmallObstacles) => T("Small obstacles sound hollow", "Obstáculos pequeños con toc hueco"),
            nameof(AccessibilitySettings.RadarRooms) => T("Radar: tin at the way into another room", "Radar: ding al pasar a otra sala"),
            nameof(AccessibilitySettings.WallTones) => T("Wall tones", "Tonos de pared"),
            nameof(AccessibilitySettings.LostHint) => T("Sound hint when lost", "Pista sonora si te pierdes"),
            nameof(AccessibilitySettings.LostHintSeconds) => T("Sound hint: seconds walking without new ground", "Pista sonora: segundos andando sin terreno nuevo"),
            nameof(AccessibilitySettings.MapHints) => T("Map hints when passing them", "Pistas del mapa al pasar"),
            nameof(AccessibilitySettings.MapHintSides) => T("Map hints: say which way they go", "Pistas del mapa: decir hacia qué lado van"),
            nameof(AccessibilitySettings.UnexploredMark) => T("Radar: unexplored mark", "Radar: marca de sin explorar"),
            nameof(AccessibilitySettings.AreaCorner) => T("Say the map corner on entering an area", "Decir la esquina del mapa al entrar en un área"),
            nameof(AccessibilitySettings.SpeechStatusBegins) => T("Speech: a state begins", "Voz: empieza un estado"),
            nameof(AccessibilitySettings.SpeechStatusEnds) => T("Speech: a state ends", "Voz: termina un estado"),
            nameof(AccessibilitySettings.SpeechLifeAndMana) => T("Speech: low life and mana", "Voz: vida y maná bajos"),
            nameof(AccessibilitySettings.SpeechBossLife) => T("Speech: boss life", "Voz: vida del jefe"),
            nameof(AccessibilitySettings.SpeechEnemyNames) => T("Speech: enemy names", "Voz: nombres de enemigos"),
            nameof(AccessibilitySettings.SpeechQuest) => T("Speech: objectives", "Voz: objetivos"),
            nameof(AccessibilitySettings.SpeechPlaces) => T("Speech: places and things nearby", "Voz: lugares y cosas cercanas"),
            nameof(AccessibilitySettings.SpeechGroundItems) => T("Speech: picked-up items", "Voz: objetos recogidos"),
            nameof(AccessibilitySettings.SpeechArrivals) => T("Speech: arrivals", "Voz: llegadas"),
            nameof(AccessibilitySettings.SpeechCharges) => T("Speech: charges", "Voz: cargas"),
            nameof(AccessibilitySettings.SpeechCombo) => T("Speech: combo points", "Voz: puntos de combo"),
            nameof(AccessibilitySettings.SpeechCooldowns) => T("Speech: skills ready", "Voz: habilidades listas"),
            nameof(AccessibilitySettings.WallToneRange) => T("Wall tone range", "Alcance de los tonos de pared"),
            nameof(AccessibilitySettings.MeleeReach) => T("Reach ping", "Aviso de alcance cuerpo a cuerpo"),
            nameof(AccessibilitySettings.MeleeReachRange) => T("Reach distance", "Distancia de alcance"),
            nameof(AccessibilitySettings.EnemySounds) => T("Enemy sounds", "Sonidos de enemigos"),
            nameof(AccessibilitySettings.NpcSounds) => T("NPC sounds", "Sonidos de personajes"),
            nameof(AccessibilitySettings.PlayerSounds) => T("Other players' sounds", "Sonidos de otros jugadores"),
            nameof(AccessibilitySettings.WallBump) => T("Wall bump", "Choque con paredes"),
            nameof(AccessibilitySettings.ObjectSounds) => T("Object sounds", "Sonidos de objetos"),
            nameof(AccessibilitySettings.LootSounds) => T("Items heard and named", "Objetos que suenan y se nombran"),
            nameof(AccessibilitySettings.ItemSounds) => T("Item sound set", "Set de sonidos de objetos"),
            nameof(AccessibilitySettings.DodgeWarnings) => T("Dodge warnings", "Avisos para esquivar"),
            nameof(AccessibilitySettings.DodgeWarningsAllMonsters) => T("Dodge warnings for every monster", "Avisos para esquivar de todos los monstruos"),
            nameof(AccessibilitySettings.DodgeWarningsAllMonstersInTrials) => T("Dodge warnings for every monster in a trial", "Avisos para esquivar de todos los monstruos en las pruebas"),
            nameof(AccessibilitySettings.ParryCues) => T("Parry cue", "Aviso de Parada"),
            nameof(AccessibilitySettings.UnblockableCues) => T("Unblockable attack cue", "Aviso de ataque imbloqueable"),
            nameof(AccessibilitySettings.HazardAlarm) => T("Harmful ground alarm", "Alarma de suelo peligroso"),
            nameof(AccessibilitySettings.ParryLeadMs) => T("Parry cue lead, milliseconds", "Antelación del aviso de Parada, milisegundos"),
            nameof(AccessibilitySettings.BossWeakPoint) => T("Boss weak point heartbeat", "Latido del punto débil del jefe"),
            nameof(AccessibilitySettings.ShieldSounds) => T("Shield raised and lowered", "Escudo levantado y bajado"),
            nameof(AccessibilitySettings.AnnounceStunBar) => T("Stun bar while blocking", "Barra de aturdimiento al bloquear"),
            nameof(AccessibilitySettings.CullCue) => T("Culling Strike cue", "Aviso de golpe de gracia"),
            nameof(AccessibilitySettings.DeployedCues) => T("Sound of what your skills place", "Sonido de lo que colocan tus habilidades"),
            nameof(AccessibilitySettings.EnemyStateCues) => T("Enemy state sounds", "Sonidos de estados del enemigo"),
            nameof(AccessibilitySettings.PrimedStunCues) => T("Primed for Stun sound", "Sonido de listo para el aturdimiento"),
            nameof(AccessibilitySettings.CrosshairBeep) => T("Crosshair beep", "Pitido de mira"),
            nameof(AccessibilitySettings.LowLifeBelow) => T("Urgent life warnings below", "Avisos de vida urgentes por debajo de"),
            nameof(AccessibilitySettings.SoundVolume) => T("Master volume", "Volumen general"),
            nameof(AccessibilitySettings.RadarVolume) => T("Radar volume", "Volumen del radar"),
            nameof(AccessibilitySettings.EnemyVolume) => T("Enemy volume", "Volumen de enemigos"),
            nameof(AccessibilitySettings.DodgeVolume) => T("Dodge and parry volume", "Volumen de esquiva y Parada"),
            nameof(AccessibilitySettings.BeaconVolume) => T("Guide volume", "Volumen de la guía"),
            nameof(AccessibilitySettings.ObjectVolume) => T("Object volume", "Volumen de objetos"),
            nameof(AccessibilitySettings.Language) => T("Language", "Idioma"),
            _ => field,
        };

        /// <summary>A sound's own volume listed beside its switch, among settings that are not volumes.</summary>
        public static string SoundVolumeName(string key)
        {
            var name = SoundName(key);
            return T($"{name} volume", $"Volumen: {name}");
        }

        /// <summary>Sound glossary: the name of each of the mod's sounds.</summary>
        public static string SoundName(string key) => key switch
        {
            "beacon" => T("Guide ping", "Ping de la guía"),
            "dodge" => T("Dodge warning", "Aviso para esquivar"),
            "parry" => T("Parry cue", "Aviso de Parada"),
            "unblockable" => T("Unblockable attack", "Ataque imbloqueable"),
            "hazard" => T("Harmful ground", "Suelo peligroso"),
            "crosshair" => T("Crosshair", "Mira"),
            "weak_point" => T("Boss weak point", "Punto débil del jefe"),
            "shield_up" => T("Shield raised", "Escudo levantado"),
            "cull" => T("Culling Strike", "Golpe de gracia"),
            "charge_gained" => T("Charge gained", "Carga ganada"),
            "charge_lost" => T("Charge lost", "Carga perdida"),
            "power_charge" => T("Power charge", "Carga de poder"),
            "combo" => T("Combo point", "Punto de combo"),
            "skill_ready" => T("Skill ready", "Habilidad lista"),
            "deployed" => T("Placed by a skill", "Colocado por una habilidad"),
            "tempest_bell" => T("Tempest Bell", "Campana de tempestad"),
            "ice_crystal" => T("Ice crystal", "Cristal de hielo"),
            _ when StateOfSound(key) is { } state => EnemyStateName(state),
            "shield_down" => T("Shield lowered", "Escudo bajado"),
            "reach" => T("Reach ping", "Aviso de alcance"),
            "wall_tone" => T("Wall tone", "Tono de pared"),
            "radar_obstacle" => T("Radar: small obstacle", "Radar: obstáculo pequeño"),
            "enemy" => T("Enemy", "Enemigo"),
            "elite" => T("Magic or rare enemy", "Enemigo mágico o raro"),
            "boss" => T("Unique enemy", "Enemigo único"),
            "friend" => T("Character", "Personaje"),
            "player" => T("Player", "Jugador"),
            "player_down" => T("Player down", "Jugador caído"),
            "radar_wall" => T("Radar: wall", "Radar: pared"),
            "radar_open" => T("Radar: open way", "Radar: camino libre"),
            "radar_exit" => T("Radar: exit", "Radar: salida"),
            "radar_unexplored" => T("Radar: toward unexplored ground", "Radar: hacia terreno sin explorar"),
            Services.LostHint.LoopName => T("Hint when lost", "Pista si te pierdes"),
            "radar2_wall" => T("Player 2 radar: wall", "Radar del jugador 2: pared"),
            "radar2_open" => T("Player 2 radar: open way", "Radar del jugador 2: camino libre"),
            "radar2_exit" => T("Player 2 radar: exit", "Radar del jugador 2: salida"),
            "radar2_obstacle" => T("Player 2 radar: small obstacle", "Radar del jugador 2: obstáculo pequeño"),
            "bump" => T("Wall bump", "Choque con pared"),
            Services.ItemSoundGroup.Flask => T("Flask on the ground", "Frasco en el suelo"),
            Services.ItemSoundGroup.Equipment => T("Equipment on the ground", "Equipo en el suelo"),
            Services.ItemSoundGroup.Gem => T("Gem on the ground", "Gema en el suelo"),
            "item" => T("Other item on the ground", "Otro objeto en el suelo"),
            "item_rare" => T("Rare item", "Objeto raro"),
            "item_unique" => T("Unique item", "Objeto único"),
            "d4_magic" => T("Diablo IV: magic item", "Diablo IV: objeto mágico"),
            "d4_scroll" => T("Diablo IV: scroll", "Diablo IV: pergamino"),
            "currency" => T("Currency", "Moneda"),
            "gold" => T("Gold", "Oro"),
            "quest_item" => T("Quest item", "Objeto de misión"),
            "chest" => T("Chest", "Cofre"),
            "corpse" => T("Body to search", "Cadáver para registrar"),
            "breakable" => T("Breakable: rock, egg, urn", "Rompible: roca, huevo, urna"),
            "switch" => T("Switch or lever", "Interruptor o palanca"),
            "shrine" => T("Shrine", "Santuario"),
            "ritual" => T("Ritual altar", "Altar de ritual"),
            "exit" => T("Exit", "Salida"),
            "portal" => T("Portal", "Portal"),
            "waypoint" => T("Waypoint", "Punto de ruta"),
            "checkpoint" => T("Checkpoint", "Punto de control"),
            "npc_quest" => T("Character with a quest", "Personaje con misión"),
            Services.DiscoveryNarrator.SoundKey => T("Discovery", "Descubrimiento"),
            _ => key,
        };

        /// <summary>Sound glossary: what the sound means (player 2's radar sounds mean what player 1's do), and that left or right plays it.</summary>
        public static string SoundMeaning(string key) => key.Replace("radar2_", "radar_", StringComparison.Ordinal) switch
        {
            "beacon" => T("walk so it sounds in the centre; faster when closer", "camina para oírlo al centro; más rápido al acercarte"),
            "dodge" => T("a rare or unique monster starts an attack, dodge", "un monstruo raro o único empieza un ataque, esquiva"),
            "unblockable" => T(
                "a rare or unique monster starts an attack that cannot be blocked or parried: only dodging avoids it, the dodge warning follows",
                "un monstruo raro o único empieza un ataque que no se puede bloquear ni parar: solo esquivarlo sirve, luego suena el aviso para esquivar"),
            "hazard" => T(
                "repeats while you stand in harmful ground or an enemy's damaging zone: move until it stops",
                "se repite mientras pisas suelo peligroso o una zona dañina de un enemigo: muévete hasta que pare"),
            "parry" => T(
                "a blow you can parry is coming, from its side: press Parry as it stops dead (only with the Parry skill)",
                "viene un golpe que puedes parar, de su lado: pulsa Parada cuando se corte en seco (solo con la habilidad Parada)"),
            "crosshair" => T("the game's target lands on a living enemy", "el objetivo del juego cae en un enemigo vivo"),
            "shield_up" => T("you raised your shield (Raise Shield, held)", "levantaste el escudo (Levantar escudo, mantenido)"),
            "shield_down" => T("your shield came down", "el escudo bajó"),
            "cull" => T(
                "an enemy on that side is low enough for a Culling Strike: 35% life normal, 20% magic, 10% rare, 5% unique",
                "un enemigo de ese lado ya cae con un golpe de gracia: 35% de vida si es normal, 20% mágico, 10% raro, 5% único"),
            "charge_gained" => T(
                "you gained a charge, an infusion or a surge: one tick for each you hold, highest for spirit charges, lowest for endurance; Rage ticks as it fills",
                "ganaste una carga, una infusión o una sobrecarga: un toque por cada una que tienes, más agudo para las de espíritu, más grave para las de aguante; la furia suena al llenarse"),
            "power_charge" => T("you gained a power charge", "ganaste una carga de poder"),
            "combo" => T(
                "Tempest Bell's combo grew: a semitone higher for each point, the first on C; at 4 the bell can ring",
                "subió el combo de la campana de tempestad: un semitono más agudo por cada punto, el primero en do; con 4 ya puede sonar la campana"),
            "charge_lost" => T("you spent one or it ran out; Rage ticks this way as it empties", "gastaste una o se te acabó; la furia suena así al vaciarse"),
            "skill_ready" => T(
                "a skill of yours finished its cooldown and can be used again; its name is said too",
                "una de tus habilidades terminó de recargarse y ya puedes volver a usarla; también se dice su nombre"),
            "deployed" or "tempest_bell" or "ice_crystal" => T(
                "something a skill of yours placed and left standing, such as a bell, a crystal or a totem, from where it is; more often as you get closer",
                "algo que una de tus habilidades colocó y dejó en el suelo, como una campana, un cristal o un tótem, desde donde está; más seguido cuanto más cerca"),
            "state_PrimedForStun" => T(
                "an enemy on that side has enough stun buildup that a heavy hit stuns it (Boneshatter): 40% normal, 50% magic, 60% rare, 70% unique",
                "un enemigo de ese lado ya acumula aturdimiento suficiente para que un golpe fuerte lo aturda (Destrozahuesos): 40% si es normal, 50% mágico, 60% raro, 70% único"),
            _ when StateOfSound(key) is not null => T(
                "an enemy on that side has just been put in this state; a rare or unique one is named too",
                "un enemigo de ese lado acaba de quedar en este estado; si es raro o único, también se nombra"),
            "weak_point" => T(
                "from the side of the boss's head, not its tail; faster as you near it, slow and soft at the head, silent while it is buried",
                "del lado de la cabeza del jefe, no de la cola; más rápido al acercarte, lento y suave junto a la cabeza, callado mientras está enterrada"),
            "reach" => T(
                "an enemy is close enough to hit in melee, on its side; again once every enemy has stepped back",
                "un enemigo está al alcance cuerpo a cuerpo, de su lado; vuelve a sonar cuando todos se hayan alejado"),
            "radar_obstacle" => T(
                "a rock, tree or pillar standing alone on that side: step round it",
                "una roca, árbol o pilar aislado de ese lado: rodéalo"),
            "wall_tone" => T(
                "with wall tones on, a hiss on the side of a near wall; higher ahead, lower behind, louder when closer",
                "con los tonos de pared activados, un soplo del lado de una pared cercana; más agudo delante, más grave detrás, más fuerte al acercarte"),
            "enemy" or "elite" or "boss" => T("on its side, faster when closer", "a su lado, más rápido al acercarse"),
            "friend" => T("the two nearest characters, each on its side", "los dos personajes más cercanos, cada uno a su lado"),
            "player" => T("the nearest other player, on its side", "el otro jugador más cercano, a su lado"),
            "player_down" => T(
                "a player down, on its side, also off screen and behind walls: go to the body and hold A to revive",
                "un jugador caído, a su lado, también fuera de pantalla y tras paredes: ve hasta su cuerpo y mantén A para revivir"),
            "radar_wall" => T("the way closed on that side", "camino cerrado de ese lado"),
            "radar_open" => T("the way opened again", "el camino se abrió"),
            "radar_exit" => T("an exit on that line", "una salida en esa línea"),
            "radar_unexplored" => T(
                "an open way or exit followed by two pips: that side leads to ground you have not seen yet",
                "un camino libre o una salida seguidos de dos pitidos: ese lado lleva a terreno que aún no viste"),
            Services.LostHint.LoopName => T(
                "for a few seconds after you have walked a while without finding new ground: that side leads on",
                "unos segundos tras andar un rato sin encontrar terreno nuevo: por ese lado se sigue"),
            "bump" => T("you pushed into a wall", "empujaste contra una pared"),
            Services.DiscoveryNarrator.SoundKey => T(
                "a new exit, waypoint, checkpoint or unique monster came into view, with its name",
                "una salida, punto de ruta, punto de control o monstruo único nuevo a la vista, con su nombre"),
            "exit" => T(
                "the nearest one, on its side, repeated faster and louder as you get closer",
                "la más cercana, a su lado, se repite más rápido y más fuerte al acercarte"),
            "portal" or "waypoint" or "checkpoint" => T(
                "the nearest one, on its side, without a break; louder when closer, from the centre once you stand on it",
                "el más cercano, a su lado, sin parar; más fuerte al acercarte, al centro cuando estás encima"),
            "chest" or "corpse" or "breakable" => T("every one within about a screen, each on its side", "todos los que hay a una pantalla, cada uno a su lado"),
            _ => T("nearest one on screen, on its side", "el más cercano en pantalla, a su lado"),
        } + T(". Left or right plays it", ". Izquierda o derecha lo reproduce");

        /// <summary>Controls help topics, the settings menu's first group.</summary>
        public static string HelpTitle(string topic) => topic switch
        {
            "help_l3" => T("The L3 button in play", "El botón L3 en el juego"),
            "help_scanner" => T("Scanner", "Escáner"),
            "help_guide" => T("Guide", "Guía"),
            "help_menus" => T("Menus", "Menús"),
            _ => T("This menu", "Este menú"),
        };

        public static string HelpText(string topic) => topic switch
        {
            "help_l3" => T(
                "Tap: where the scanner's choice is, or repeat. Double tap: life, mana, states, the boss, the flasks, your level and the area's, your gold, the league encounter (tribute) and how much you have explored. Hold: the screen, the button hints and the skill bar. Hold a second and a half: this menu",
                "Toque: dónde está lo elegido en el escáner, o repetir. Doble toque: vida, maná, estados, el jefe, los frascos, tu nivel y el del área, tu oro, el encuentro de liga (tributo) y cuánto has explorado. Mantener: la pantalla, las pistas de botones y la barra de habilidades. Mantener un segundo y medio: este menú"),
            "help_scanner" => T(
                "Hold L3 and flick the right stick: up and down change the category, left and right go through it, nearest first. Quest is first: what the objective names",
                "Mantén L3 y mueve el stick derecho: arriba y abajo cambian de categoría, izquierda y derecha la recorren, de la más cercana a la más lejana. Misión va primero: lo que nombra el objetivo"),
            "help_guide" => T(
                "Hold L3 and press the right stick: a ping guides you to the scanner's choice along a free route, turns are named. Again while it guides turns it off",
                "Mantén L3 y presiona el stick derecho: un ping te guía hacia lo elegido en el escáner por un camino libre, y se nombran los giros. Otra vez mientras guía la apaga"),
            "help_menus" => T(
                "In menus L3 reads the next line of the focused thing, double tap the previous, hold the whole description",
                "En los menús L3 lee la siguiente línea de lo enfocado, doble toque la anterior, mantener la descripción completa"),
            _ => T(
                "Right stick up and down move, left and right open a submenu, change a value or play a sound. Tap L3 to go back up one level, tap it at the top to close and save; a double tap closes from anywhere",
                "Stick derecho arriba y abajo para moverte, izquierda y derecha para abrir un submenú, cambiar un valor o escuchar un sonido. Toca L3 para subir un nivel, y tócalo arriba del todo para cerrar y guardar; un doble toque cierra desde cualquier sitio"),
        };

        /// <summary>The settings menu's submenus (D2R submenus, nested), by key.</summary>
        public static string SettingsGroupName(string key) => key switch
        {
            "help" => T("Help", "Ayuda"),
            "character" => T("Your character", "Tu personaje"),
            "combat" => T("Combat", "Combate"),
            "enemies" => T("Enemies", "Enemigos"),
            "bosses" => T("Bosses and enemy states", "Jefes y estados de los enemigos"),
            "defence" => T("Defence", "Defensa"),
            "skills" => T("Charges and skills", "Cargas y habilidades"),
            "fight_quiet" => T("Quiet in fights", "Silencio en combate"),
            "navigation" => T("Navigation", "Navegación"),
            "surroundings" => T("Things and people nearby", "Cosas y personas cercanas"),
            "items" => T("Items", "Objetos"),
            "chat" => T("Chat", "Chat"),
            "speech" => T("Speech priority", "Prioridad de voz"),
            "sound" => T("Sound", "Sonido"),
            "sound_enemies" => T("Enemies, characters and combat", "Enemigos, personajes y combate"),
            "sound_dodge" => T("Dodge and parry", "Esquiva y Parada"),
            "sound_radar" => T("Radar and walls", "Radar y paredes"),
            "sound_beacon" => T("Guide", "Guía"),
            "sound_objects" => T("Objects and places", "Objetos y lugares"),
            "sound_charges" => T("Charges, skills and shield", "Cargas, habilidades y escudo"),
            "general" => T("General", "General"),
            _ => T("Sound glossary", "Glosario de sonidos"),
        };

        public static string Submenu => T("submenu", "submenú");

        public static string SettingsMenuTitle => T("Mod settings", "Ajustes del mod");

        public static string SettingsMenuClosed => T("Settings saved", "Ajustes guardados");

        /// <summary>"Sonidos de objetos, activado, 14 de 25".</summary>
        public static string SettingLine(string name, string value, int position, int count) => $"{name}, {value}, {ListPosition(position, count)}";

        public static string On => T("on", "activado");

        public static string Off => T("off", "desactivado");

        /// <summary>A language by its own name, as the game's language list shows it.</summary>
        public static string LanguageName(string code) => code switch
        {
            "es" => "Español",
            "en" => "English",
            "pt" => "Português",
            "fr" => "Français",
            "de" => "Deutsch",
            "ru" => "Русский",
            "th" => "ไทย",
            "ja" => "日本語",
            "ko" => "한국어",
            "zh-TW" => "繁體中文",
            _ => T("Same as the game", "Igual que el juego"),
        };

        /// <summary>A choice setting's value: a speech delivery level, else a language.</summary>
        public static string ChoiceName(string value) => value switch
        {
            Services.SpeechPriority.Waits => T("waits its turn", "espera su turno"),
            Services.SpeechPriority.Urgent => T("cuts in when urgent", "interrumpe si es urgente"),
            Services.SpeechPriority.CutsIn => T("cuts in", "interrumpe"),
            Services.SpeechPriority.Holds => T("cuts in and cannot be cut off", "interrumpe y no se corta"),
            Services.LootFilter.All => T("all", "todos"),
            Services.LootFilter.Magic => T("magic or better", "mágicos o mejores"),
            Services.LootFilter.Valuable => T("only currency, gems, quest items, rares and uniques", "solo monedas, gemas, objetos de misión, raros y únicos"),
            Services.ItemSoundSet.Game => "Path of Exile 2",
            Services.ItemSoundSet.DiabloIV => "Diablo IV",
            Services.CullThreshold.WithSkill => T("when one of your skills culls", "si una de tus habilidades remata"),
            Services.CullThreshold.Always => T("always", "siempre"),
            Services.CullThreshold.Never => T("never", "nunca"),
            Services.FightState.RaresAndUniques => T("against rares and uniques", "contra raros y únicos"),
            Services.FightState.AnyEnemy => T("against any enemy", "contra cualquier enemigo"),
            _ => LanguageName(value),
        };
    }
}
