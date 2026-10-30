namespace Poe2Accessibility
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Spoken strings of the guide beacon, vitals, target and text fields (same translatable layer).</summary>
    public static partial class Announcements
    {
        /// <summary>"Guiding to Clearfell, 116 steps, down 3, then left" (the scanner's route description).</summary>
        public static string BeaconLocked(string described) => T($"Guiding to {described}", $"Guiando hacia {described}");

        /// <summary>Passive node kinds and states.</summary>
        public static string PassiveKeystone => T("keystone", "piedra angular");

        public static string PassiveSmall => T("small node", "nodo pequeño");

        public static string PassiveNotable => T("notable", "notable");

        public static string PassiveJewelSocket => T("jewel socket", "engarce de joya");

        public static string PassiveAllocated => T("allocated", "asignado");

        public static string PassiveReachable => T("reachable", "alcanzable");

        public static string PassiveLocked => T("not linked", "sin conexión");

        /// <summary>"Attack Speed, notable, can be allocated".</summary>
        public static string PassiveNode(string name, string kind, string state) =>
            string.Join(", ", new[] { name, kind, state }.Where(part => part.Length > 0));

        public static string PassiveLink(string name, string state) => $"{name}, {state}";

        /// <summary>A link to the class's start (the portrait at the tree's centre).</summary>
        /// <summary>A link to a class start: the player's own ("start of your class, Monk") or another class's.</summary>
        public static string PassiveStartLink(string name, bool own) =>
            own ? T($"start of your class, {name}", $"inicio de tu clase, {name}") : T($"start, {name}", $"inicio, {name}");

        /// <summary>"Shadow or Monk".</summary>
        public static string Either(IEnumerable<string> names) => string.Join(ButtonSeparator, names);

        /// <summary>"Links: up, Armour and Evasion, allocated; down left, Attribute, can be allocated".</summary>
        public static string PassiveLinks(IEnumerable<string> links) => T("Links: ", "Conexiones: ") + string.Join("; ", links);

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

        /// <summary>The route stops short of the target (closed gate): the guide leads as close as the way goes.</summary>
        public static string BeaconBarrierRoute(string name) =>
            T($"A closed barrier cuts the way to {name}; guiding as close as it goes", $"Una barrera cerrada corta el camino a {name}; te guío lo más cerca posible");

        public static string BeaconPartialRoute(string name) =>
            T($"The way to {name} is closed further on; guiding as close as it goes", $"El camino a {name} está cerrado más adelante; te guío lo más cerca posible");

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

        /// <summary>A flask with the uses it has left, for the vitals readout and after a drink: "Frasco de vida menor, 2 usos".</summary>
        public static string Flask(string name, int uses) => uses switch
        {
            <= 0 => FlaskEmpty(name),
            1 => T($"{name}, 1 use", $"{name}, 1 uso"),
            _ => T($"{name}, {uses} uses", $"{name}, {uses} usos"),
        };

        /// <summary>How much of the area's reachable ground has been seen, for the vitals readout (D2R): "Explorado 40 por ciento".</summary>
        public static string Explored(int percent) => T($"Explored {percent} percent", $"Explorado {percent} por ciento");

        /// <summary>A flask with no use left: "Frasco de vida menor vacío".</summary>
        public static string FlaskEmpty(string name) => T($"{name} empty", $"{name} vacío");

        /// <summary>A flask that gained a use from kills or time: "Frasco de vida menor recargado, 2 usos".</summary>
        public static string FlaskRefilled(string name, int uses) => uses == 1
            ? T($"{name} refilled, 1 use", $"{name} recargado, 1 uso")
            : T($"{name} refilled, {uses} uses", $"{name} recargado, {uses} usos");

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

        /// <summary>
        ///     Character creation's name step: the field takes typing at once and the stick only moves once B has left it;
        ///     Enter, or A on Continue, creates the character (verified live 2026-10-28).
        /// </summary>
        public static string CreationNameHelp => T(
            "Type the name. B leaves the field, then the left stick moves between name, league and Continue. To create the character: Enter, or A on Continue.",
            "Escribe el nombre. Con B sales del campo y el stick izquierdo pasa por nombre, liga y Continuar. Para crear el personaje: Intro, o A en Continuar.");

        /// <summary>A boss's life fell past a step: "The Bloated Miller 40 percent".</summary>
        public static string BossLife(string name, int percent) => T($"{name} {percent} percent", $"{name} {percent} por ciento");

        public static string BossDefeated(string name) => T($"{name} defeated", $"{name} derrotado");

        /// <summary>A rare or unique enemy now within culling range: "Beira, de la manada putrefacta: golpe de gracia".</summary>
        public static string Cullable(string name) => T($"{name}: culling strike", $"{name}: golpe de gracia");

        /// <summary>
        ///     What the player holds now, in the game's words: "3 cargas de poder", "sin cargas de aguante"; the others by
        ///     name and count ("Infusión de fuego: 2"), Arcane Surge by name alone.
        /// </summary>
        public static string Charges(Services.ChargeKind kind, int count) => (kind, count) switch
        {
            (Services.ChargeKind.Power, 0) => T("no power charges", "sin cargas de poder"),
            (Services.ChargeKind.Power, 1) => T("1 power charge", "1 carga de poder"),
            (Services.ChargeKind.Power, _) => T($"{count} power charges", $"{count} cargas de poder"),
            (Services.ChargeKind.Frenzy, 0) => T("no frenzy charges", "sin cargas de frenesí"),
            (Services.ChargeKind.Frenzy, 1) => T("1 frenzy charge", "1 carga de frenesí"),
            (Services.ChargeKind.Frenzy, _) => T($"{count} frenzy charges", $"{count} cargas de frenesí"),
            (Services.ChargeKind.Endurance, 0) => T("no endurance charges", "sin cargas de aguante"),
            (Services.ChargeKind.Endurance, 1) => T("1 endurance charge", "1 carga de aguante"),
            (Services.ChargeKind.Endurance, _) => T($"{count} endurance charges", $"{count} cargas de aguante"),
            (Services.ChargeKind.Spirit, 0) => T("no spirit charges", "sin cargas de espíritu"),
            (Services.ChargeKind.Spirit, 1) => T("1 spirit charge", "1 carga de espíritu"),
            (Services.ChargeKind.Spirit, _) => T($"{count} spirit charges", $"{count} cargas de espíritu"),
            (Services.ChargeKind.ArcaneSurge, 0) => T("Arcane Surge ended", "Sobrecarga arcana terminada"),
            (Services.ChargeKind.ArcaneSurge, _) => ChargeName(kind),
            (Services.ChargeKind.Combo, 0) => T("no Combo", "sin combo"),
            _ => T($"{ChargeName(kind)}: {count}", $"{ChargeName(kind)}: {count}"),
        };

        /// <summary>The game's name for a resource counted by name (BuffDefinitions, read 2026-10-29).</summary>
        public static string ChargeName(Services.ChargeKind kind) => kind switch
        {
            Services.ChargeKind.FireInfusion => T("Fire Infusion", "Infusión de fuego"),
            Services.ChargeKind.ColdInfusion => T("Cold Infusion", "Infusión de hielo"),
            Services.ChargeKind.LightningInfusion => T("Lightning Infusion", "Infusión de rayo"),
            Services.ChargeKind.FireSurge => T("Fire Surge", "Sobrecarga de fuego"),
            Services.ChargeKind.ColdSurge => T("Cold Surge", "Sobrecarga de hielo"),
            Services.ChargeKind.LightningSurge => T("Lightning Surge", "Sobrecarga de rayo"),
            Services.ChargeKind.ChaosSurge => T("Chaos Surge", "Sobrecarga de caos"),
            Services.ChargeKind.DruidicProwess => T("Druidic Prowess", "Proeza druídica"),
            Services.ChargeKind.ArcaneSurge => T("Arcane Surge", "Sobrecarga arcana"),
            Services.ChargeKind.Combo => T("Combo", "Combo"),
            _ => T("Rage", "Furia"),
        };

        /// <summary>Rage reaching its maximum: "Furia al máximo".</summary>
        public static string RageFull => T("Rage full", "Furia al máximo");

        public static string RageEmpty => T("no Rage", "sin furia");

        /// <summary>Rage in the vitals report: "Furia 18 de 30".</summary>
        public static string Rage(int count, int max) => T($"Rage {count} of {max}", $"Furia {count} de {max}");

        /// <summary>An enemy state's name, as the game's KeywordPopups call it (read 2026-10-26).</summary>
        public static string EnemyStateName(Services.EnemyState state) => state switch
        {
            Services.EnemyState.PrimedForStun => T("Primed for Stun", "Listo para el aturdimiento"),
            Services.EnemyState.HeavyStunned => T("Heavy Stun", "Aturdimiento total"),
            Services.EnemyState.Frozen => T("Frozen", "Congelado"),
            Services.EnemyState.Electrocuted => T("Electrocution", "Parálisis"),
            Services.EnemyState.Pinned => T("Pinned", "Retenido"),
            Services.EnemyState.ArmourBroken => T("Armour Break", "Rotura de armadura"),
            Services.EnemyState.Dazed => T("Daze", "Desorientación"),
            _ => T("Parried", "Parado"),
        };

        /// <summary>A rare or unique enemy has just entered states: "Beira, de la manada putrefacta: aturdimiento total".</summary>
        public static string EnemyEntered(string name, IEnumerable<Services.EnemyState> states)
        {
            var named = string.Join(", ", states.Select(EnemyStateName));
            return T($"{name}: {named}", $"{name}: {named}");
        }

        /// <summary>The enemy state a sound glossary key ("state_Frozen") stands for.</summary>
        public static Services.EnemyState? StateOfSound(string key) =>
            key.StartsWith("state_", StringComparison.Ordinal) && Enum.TryParse<Services.EnemyState>(key["state_".Length..], out var state) ? state : null;

        /// <summary>Once per area, as the weak-point heartbeat first leads to a boss's head.</summary>
        public static string WeakPointGuide(string name) =>
            T($"{name}: strike the head, follow the heartbeat", $"{name}: golpea la cabeza, sigue el latido");

        /// <summary>"Poisoned" as it begins (D2R: high priority, cannot be cut off).</summary>
        public static string AilmentBegins(Services.StatusAilment ailment) => ailment switch
        {
            Services.StatusAilment.StopMoving => T("Stop moving!", "¡Quédate quieto!"),
            Services.StatusAilment.KeepMoving => T("Keep moving!", "¡Sigue moviéndote!"),
            _ => AilmentName(ailment),
        };

        /// <summary>"No longer poisoned" once it has been gone for half a second.</summary>
        public static string AilmentEnds(Services.StatusAilment ailment) => ailment is Services.StatusAilment.StopMoving or Services.StatusAilment.KeepMoving
            ? T("You may move freely", "Ya puedes moverte libremente")
            : T($"No longer {AilmentName(ailment).ToLowerInvariant()}", $"Ya no estás {AilmentName(ailment).ToLowerInvariant()}");

        public static string AilmentName(Services.StatusAilment ailment) => ailment switch
        {
            Services.StatusAilment.Frozen => T("Frozen", "Congelado"),
            Services.StatusAilment.Chilled => T("Chilled", "Enfriado"),
            Services.StatusAilment.Shocked => T("Shocked", "Electrizado"),
            Services.StatusAilment.Electrocuted => T("Electrocuted", "Electrocutado"),
            Services.StatusAilment.Ignited => T("Ignited", "En llamas"),
            Services.StatusAilment.Poisoned => T("Poisoned", "Envenenado"),
            Services.StatusAilment.Bleeding => T("Bleeding", "Sangrando"),
            Services.StatusAilment.Stunned => T("Stunned", "Aturdido"),
            Services.StatusAilment.Pinned => T("Pinned", "Inmovilizado"),
            Services.StatusAilment.Slowed => T("Slowed", "Ralentizado"),
            Services.StatusAilment.Maimed => T("Maimed", "Mutilado"),
            Services.StatusAilment.Blinded => T("Blinded", "Cegado"),
            _ => T("Cursed", "Maldito"),
        };

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
            nameof(AccessibilitySettings.QuietItemsInFights) => T("Silence items on the ground in fights", "Silenciar objetos del suelo en combate"),
            nameof(AccessibilitySettings.QuietChatInFights) => T("Silence characters' lines in fights", "Silenciar frases de personajes en combate"),
            nameof(AccessibilitySettings.AnnounceHand) => T("Item in hand", "Objeto en la mano"),
            nameof(AccessibilitySettings.AnnounceMoves) => T("Equipped and moved items", "Objetos equipados y movidos"),
            nameof(AccessibilitySettings.RoutePrompts) => T("Turn prompts", "Indicaciones de giro"),
            nameof(AccessibilitySettings.AnnounceLevelUp) => T("Level up", "Subida de nivel"),
            nameof(AccessibilitySettings.Radar) => T("Radar", "Radar"),
            nameof(AccessibilitySettings.ReadPlayer1) => T("Local co-op: read for player 1", "Cooperativo local: lectura para el jugador 1"),
            nameof(AccessibilitySettings.ReadPlayer2) => T("Local co-op: read for player 2", "Cooperativo local: lectura para el jugador 2"),
            nameof(AccessibilitySettings.CheckForUpdates) => T("Check for updates at start", "Buscar actualizaciones al iniciar"),
            nameof(AccessibilitySettings.SmallObstacles) => T("Small obstacles sound hollow", "Obstáculos pequeños con toc hueco"),
            nameof(AccessibilitySettings.WallTones) => T("Wall tones", "Tonos de pared"),
            nameof(AccessibilitySettings.UnexploredLoop) => T("Unexplored area loop", "Bucle de zona sin explorar"),
            nameof(AccessibilitySettings.UnexploredMark) => T("Radar: unexplored mark", "Radar: marca de sin explorar"),
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
            nameof(AccessibilitySettings.WallToneRange) => T("Wall tone range", "Alcance de los tonos de pared"),
            nameof(AccessibilitySettings.MeleeReach) => T("Reach ping", "Aviso de alcance cuerpo a cuerpo"),
            nameof(AccessibilitySettings.MeleeReachRange) => T("Reach distance", "Distancia de alcance"),
            nameof(AccessibilitySettings.EnemySounds) => T("Enemy sounds", "Sonidos de enemigos"),
            nameof(AccessibilitySettings.NpcSounds) => T("NPC sounds", "Sonidos de personajes"),
            nameof(AccessibilitySettings.PlayerSounds) => T("Other players' sounds", "Sonidos de otros jugadores"),
            nameof(AccessibilitySettings.WallBump) => T("Wall bump", "Choque con paredes"),
            nameof(AccessibilitySettings.ObjectSounds) => T("Object sounds", "Sonidos de objetos"),
            nameof(AccessibilitySettings.LootSounds) => T("Items heard and named", "Objetos que suenan y se nombran"),
            nameof(AccessibilitySettings.DodgeWarnings) => T("Dodge warnings", "Avisos para esquivar"),
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

        public static string MovedTo(string name, string panel) => T($"Moved to {panel}: {name}", $"Movido a {panel}: {name}");

        public static string TakenFrom(string name, string panel) => T($"Taken from {panel}: {name}", $"Sacado de {panel}: {name}");

        public static string Obtained(string name) => T($"Obtained: {name}", $"Obtenido: {name}");

        public static string Dropped(string name) => T($"Dropped: {name}", $"Tirado: {name}");

        public static string UsedUp(string name) => T($"Used: {name}", $"Usado: {name}");

        public static string HandEmpty => T("You no longer hold an item", "Ya no llevas ningún objeto");

        /// <summary>A scanner entry of the unexplored category: where unseen ground begins.</summary>
        public static string UnexploredArea => T("Unexplored ground", "Zona sin explorar");

        /// <summary>The scanner's entry for the place of the last death in this area (D2R).</summary>
        public static string DeathPlace => T("Where you died", "Donde moriste");

        /// <summary>A barrier that opened on its own (scanner-style place: "The way opened, 4 steps, left").</summary>
        public static string BarrierOpened => T("The way opened", "Se abrió el paso");

        /// <summary>A closed barrier in the scanner's objects (a tutorial beam, a gate); it has no name of its own.</summary>
        public static string ClosedBarrier => T("Closed barrier", "Barrera cerrada");

        /// <summary>
        ///     A league device by its minimap icon (MinimapIcons.dat id), as the game's own strings call it (ClientStrings
        ///     "Altar de ritual {0}", "Interactúa para ver los favores"); the devices have no render name.
        /// </summary>
        /// <param name="boss">The altar that summons an area's boss, not one of those its quest sends the player to.</param>
        public static string LeagueDevice(string icon, bool boss) => icon switch
        {
            Services.MinimapMarks.RitualDoneIcon => T("Ritual altar, done", "Altar de ritual, completado"),
            Services.MinimapMarks.RitualIcon when boss => T("Boss's ritual altar", "Altar de ritual del jefe"),
            Services.MinimapMarks.RitualIcon => T("Ritual altar", "Altar de ritual"),
            _ => T("League encounter", "Encuentro de liga"),
        };

        /// <summary>A boss's arena as its minimap icon labels it ("Sala del jefe: Corazón de la tumba"); map bosses have no label.</summary>
        public static string BossRoom(string label) => label.Length > 0
            ? T($"Boss room: {label}", $"Sala del jefe: {label}")
            : T("Boss room", "Sala del jefe");

        /// <summary>The arena of a boss already slain (its icon turned grey).</summary>
        public static string BossRoomDefeated(string label) => label.Length > 0
            ? T($"Boss room, defeated: {label}", $"Sala del jefe, derrotado: {label}")
            : T("Boss room, defeated", "Sala del jefe, derrotado");

        /// <summary>A banner of the shop's featured page (pictures only); A opens what it shows.</summary>
        public static string ShopFeatured => T("featured", "destacado");

        /// <summary>A shop item's price: the shop's own currency, points ("250 puntos").</summary>
        public static string ShopPrice(string price) => T($"{price} points", $"{price} puntos");

        /// <summary>A readable sign or plaque (the game's story glyphs: "Pueblo de Ogham" by the road, "Proclamación de Asinia").</summary>
        public static string Sign(string name) => T($"Sign: {name}", $"Letrero: {name}");

        /// <summary>An NPC with the quest indicator over its head.</summary>
        public static string NpcWithQuest(string name) => T($"{name}, has a quest", $"{name}, con misión");

        public static string GoldPickedUp(long amount) => T($"Picked up {amount} gold", $"Recogido {amount} de oro");

        public static string PickedUp(string name, int added) => added > 0
            ? T($"Picked up {added} more: {name}", $"Recogido {added} más: {name}")
            : T($"Picked up: {name}", $"Recogido: {name}");

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
            Services.UnexploredLoop.LoopName => T("Unexplored area", "Zona sin explorar"),
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
            Services.UnexploredLoop.LoopName => T(
                "without a break, where the nearest unexplored ground begins, on its side; louder when closer",
                "sin parar, donde empieza el terreno sin explorar más cercano, a su lado; más fuerte al acercarte"),
            "bump" => T("you pushed into a wall", "empujaste contra una pared"),
            Services.DiscoveryNarrator.SoundKey => T(
                "a new exit, waypoint, checkpoint or unique monster came into view, with its name",
                "una salida, punto de ruta, punto de control o monstruo único nuevo a la vista, con su nombre"),
            "exit" or "portal" or "waypoint" or "checkpoint" => T(
                "the nearest one, on its side, without a break; louder when closer, from the centre once you stand on it",
                "el más cercano, a su lado, sin parar; más fuerte al acercarte, al centro cuando estás encima"),
            "chest" or "corpse" or "breakable" => T("every one within about a screen, each on its side", "todos los que hay a una pantalla, cada uno a su lado"),
            _ => T("nearest one on screen, on its side", "el más cercano en pantalla, a su lado"),
        } + T(". Left or right plays it", ". Izquierda o derecha lo reproduce");

        /// <summary>The rarity word that ends a unique monster's scanner name ("El molinero hinchado, único").</summary>
        public static string UniqueRarityWord => RarityName(GameHelper.RemoteEnums.Rarity.Unique);

        public static string RareRarityWord => RarityName(GameHelper.RemoteEnums.Rarity.Rare);

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

        /// <summary>The layout parts not found after a game update, in one line.</summary>
        public static string CompatibilityMissing(IReadOnlyList<string> parts)
        {
            var names = parts.Select(CompatibilityPart).ToList();
            var list = names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} {T("and", "y")} {names[^1]}";
            return T(
                $"Compatibility warning: cannot find {list}. The game may have been updated; those readings may fail",
                $"Aviso de compatibilidad: no encuentro {list}. Puede que el juego se haya actualizado; esas lecturas pueden fallar");
        }

        private static string CompatibilityPart(string part) => part switch
        {
            "element names" => T("the interface's control names", "los nombres de la interfaz"),
            "element texts" => T("the interface's texts", "los textos de la interfaz"),
            "entity names" => T("the names of characters and monsters", "los nombres de personajes y monstruos"),
            "targetable flag" => T("which monsters and characters are in play", "qué monstruos y personajes están en juego"),
            "HUD" => T("the HUD", "el HUD"),
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
            "quest marker" => T("which characters have a quest", "qué personajes tienen misión"),
            "interactive flag" or "text input flag" or "navigable flag" or "enabled flag" => T("the kinds of interface controls", "los tipos de controles de la interfaz"),
            "game states" => T("the game's state (GameHelper needs an update)", "el estado del juego (GameHelper necesita una actualización)"),
            _ => part,
        };

        public static string DeathScreenHidden => T("The death screen is hidden: Y shows it", "La pantalla de muerte está oculta: Y la muestra");

        /// <summary>A place on a map by what it is beside: "cerca de Campamento de Sierraclara".</summary>
        public static string NearPlace(string name) => T($"near {name}", $"cerca de {name}");

        /// <summary>A straight-line distance in scanner steps: "a 35 pasos".</summary>
        public static string StepsAway(int steps) => T($"{Steps(steps)} away", $"a {Steps(steps)}");

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
                "Right stick up and down move, left and right open a submenu, change a value or play a sound. Tap L3 to go back to the submenus, tap it there to close and save; a double tap closes from anywhere",
                "Stick derecho arriba y abajo para moverte, izquierda y derecha para abrir un submenú, cambiar un valor o escuchar un sonido. Toca L3 para volver a los submenús, y tócalo allí para cerrar y guardar; un doble toque cierra desde cualquier sitio"),
        };

        /// <summary>The settings menu's groups (D2R submenus), by key.</summary>
        public static string SettingsGroupName(string key) => key switch
        {
            "help" => T("Help", "Ayuda"),
            "character" => T("Your character", "Tu personaje"),
            "enemies" => T("Enemies", "Enemigos"),
            "defence" => T("Defence", "Defensa"),
            "navigation" => T("Navigation", "Navegación"),
            "surroundings" => T("Things and people nearby", "Cosas y personas cercanas"),
            "items" => T("Items", "Objetos"),
            "chat" => T("Chat", "Chat"),
            "speech" => T("Speech priority", "Prioridad de voz"),
            "volumes" => T("Volumes", "Volúmenes"),
            "sound_levels" => T("Volume of each sound", "Volumen de cada sonido"),
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

        public static string Percent(int value) => T($"{value} percent", $"{value} por ciento");

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
            Services.CullThreshold.WithSkill => T("when one of your skills culls", "si una de tus habilidades remata"),
            Services.CullThreshold.Always => T("always", "siempre"),
            Services.CullThreshold.Never => T("never", "nunca"),
            Services.FightState.RaresAndUniques => T("against rares and uniques", "contra raros y únicos"),
            Services.FightState.AnyEnemy => T("against any enemy", "contra cualquier enemigo"),
            _ => LanguageName(value),
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
