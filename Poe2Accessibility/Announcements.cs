namespace Poe2Accessibility
{
    using System;
    using System.Linq;
    using GameHelper.RemoteEnums;

    /// <summary>
    ///     Every spoken string lives here (single translatable layer; no text inside services or hooks).
    /// </summary>
    public static partial class Announcements
    {
        private static string T(string en, string es) => Localization.Language switch
        {
            "en" => en,
            "es" => es,
            _ => Localization.Translate(en) ?? en,
        };

        /// <summary>Interpolated phrases: other languages look the English composite format up ("Life {0} percent").</summary>
        private static string T(Phrase en, Phrase es) => Localization.Language switch
        {
            "en" => en.Text(),
            "es" => es.Text(),
            _ => en.Text(Localization.Translate(en.Format)),
        };

        public static string PluginReady => T("Path of Exile 2 accessibility ready.", "Accesibilidad de Path of Exile 2 lista.");
        public static string GameDetected => T("Game detected.", "Juego detectado.");
        public static string UiDumpStarted => T("Dumping user interface tree.", "Volcando el árbol de la interfaz.");
        public static string UiDumpDone => T("User interface dump written.", "Volcado de la interfaz escrito.");
        public static string UiDumpNoRoot => T("No user interface root found for this screen; diagnostic file written.", "No se encontró la raíz de la interfaz de esta pantalla; archivo de diagnóstico escrito.");
        public static string UiDumpFailed => T("User interface dump failed.", "Falló el volcado de la interfaz.");
        public static string NothingToRepeat => T("Nothing to repeat.", "Nada que repetir.");
        public static string SettingsReloaded => T("Settings reloaded.", "Configuración recargada.");
        public static string SettingsInvalid => T("Settings file could not be read, keeping current settings.", "No se pudo leer el archivo de configuración; se mantiene la actual.");
        public static string UiDumpBlockedByDiagnostics => T("Disable memory diagnostics in Game Helper settings before dumping.", "Desactiva los diagnósticos de memoria en la configuración de Game Helper antes de volcar.");

        public static string HotkeyCollision(string key, string action) =>
            T(
                $"Warning: hotkey {key} for {action} is already used by Game Helper. Change it in settings.",
                $"Aviso: la tecla {key} para {action} ya la usa Game Helper. Cámbiala en la configuración.");

        public static string ForState(GameStateTypes state) => state switch
        {
            GameStateTypes.PreGameState => T("Splash screen.", "Pantalla de inicio."),
            GameStateTypes.LoginState => T("Login screen.", "Inicio de sesión."),
            GameStateTypes.WaitingState => T("Connecting.", "Conectando."),
            GameStateTypes.SelectCharacterState => T("Character selection.", "Selección de personaje."),
            GameStateTypes.CreateCharacterState => T("Character creation.", "Creación de personaje."),
            GameStateTypes.DeleteCharacterState => T("Delete character.", "Eliminar personaje."),
            GameStateTypes.ChangePasswordState => T("Change password.", "Cambiar contraseña."),
            GameStateTypes.CreditsState => T("Credits.", "Créditos."),
            GameStateTypes.LoadingState => T("Loading.", "Cargando."),
            GameStateTypes.AreaLoadingState => T("Loading area.", "Cargando área."),
            GameStateTypes.InGameState => T("In game.", "En juego."),
            GameStateTypes.EscapeState => T("Game menu.", "Menú del juego."),
            GameStateTypes.GameNotLoaded => T("Game closed.", "Juego cerrado."),
            _ => T("Unknown screen.", "Pantalla desconocida."),
        };

        /// <summary>Fallback when a combo's entry text cannot be decoded yet: position within the list.</summary>
        public static string ComboPosition(int oneBasedIndex, int count) => T($"option {oneBasedIndex} of {count}", $"opción {oneBasedIndex} de {count}");

        /// <summary>Sliders without an on-screen label: the raw number the game uses, as a percentage when the range is 0 to 100.</summary>
        public static string SliderValue(int value, int min, int max) =>
            min == 0 && max == 100 ? T($"{value} percent", $"{value} por ciento") : $"{value}";

        public static string Checked => T("checked", "marcado");

        /// <summary>What the quest map shows (its surface/underground toggle).</summary>
        public static string AboveGround => T("above ground", "encima del suelo");

        public static string BelowGround => T("below ground", "debajo del suelo");
        public static string Unchecked => T("unchecked", "desmarcado");

        public static string NoHints => T("No button hints on screen.", "No hay indicaciones de botones en pantalla.");

        public static string NothingToRead => T("Nothing to read here.", "Nada que leer aquí.");

        public static string NothingFound => T("Nothing found here.", "No se encontró nada aquí.");

        /// <summary>Life fell below a threshold: "Life 25 percent".</summary>
        public static string LowLife(int percent) => T($"Life {percent} percent", $"Vida {percent} por ciento");

        /// <summary>The left stick's eight directions, by <see cref="Services.StickDirection"/> octant.</summary>
        public static string[] StickDirections => new[]
        {
            T("right", "derecha"), T("up right", "arriba a la derecha"), T("up", "arriba"), T("up left", "arriba a la izquierda"),
            T("left", "izquierda"), T("down left", "abajo a la izquierda"), T("down", "abajo"), T("down right", "abajo a la derecha"),
        };

        public static string ScanCategoryName(Services.ScanCategory category) => category switch
        {
            Services.ScanCategory.Quest => T("Quest", "Misión"),
            Services.ScanCategory.Exits => T("Exits", "Salidas"),
            Services.ScanCategory.PointsOfInterest => T("Points of interest", "Puntos de interés"),
            Services.ScanCategory.Places => T("Places", "Lugares"),
            Services.ScanCategory.Unexplored => T("Unexplored", "Sin explorar"),
            Services.ScanCategory.Waypoints => T("Waypoints and checkpoints", "Puntos de ruta y de control"),
            Services.ScanCategory.Encounters => T("Encounters", "Encuentros"),
            Services.ScanCategory.Npcs => T("Characters", "Personajes"),
            Services.ScanCategory.Enemies => T("Enemies", "Enemigos"),
            Services.ScanCategory.Objects => T("Objects", "Elementos"),
            Services.ScanCategory.Chests => T("Chests and shrines", "Cofres y santuarios"),
            Services.ScanCategory.Items => T("Items", "Objetos"),
            Services.ScanCategory.Players => T("Players", "Jugadores"),
            _ => category.ToString(),
        };

        /// <summary>
        ///     D2R order: name, distance, direction ("Wounded Man, 9 steps, up left"); with a turn ahead, the steps
        ///     until it and the way it turns ("116 steps, down 3, then left"); "here" when on top of it; "no route"
        ///     when only the straight line is known.
        /// </summary>
        public static string ScanEntry(string name, int steps, string direction, int legSteps, string? then, bool routed, bool partial = false, bool barrier = false)
        {
            if (steps < 1)
            {
                return Here(name);
            }

            var leg = then == null ? direction : T($"{direction} {legSteps}, then {then}", $"{direction} {legSteps}, luego {then}");
            if (partial && barrier)
            {
                return T($"{name}, {Steps(steps)} away, a closed barrier cuts the way: {leg}",
                         $"{name}, a {Steps(steps)}, una barrera cerrada corta el camino: {leg}");
            }

            if (partial)
            {
                // Steps are the straight line; the legs lead as close as the ground goes (a gate, a bridge further on).
                return T($"{name}, {Steps(steps)} away, the way stops short: {leg}",
                         $"{name}, a {Steps(steps)}, el camino se corta antes: {leg}");
            }

            var route = routed ? string.Empty : T(", no route", ", sin ruta");
            return T($"{name}, {Steps(steps)}, {leg}{route}", $"{name}, {Steps(steps)}, {leg}{route}");
        }

        /// <summary>Something within reach: "Una, here".</summary>
        public static string Here(string name) => T($"{name}, here", $"{name}, aquí");

        /// <summary>A count of scanner steps: "1 step", "12 steps".</summary>
        public static string Steps(int steps) => steps == 1 ? T("1 step", "1 paso") : T($"{steps} steps", $"{steps} pasos");

        /// <summary>Another player: character name, then class ("Yarky, Witch").</summary>
        public static string OtherPlayer(string name, string characterClass, int level = 0)
        {
            var spoken = name.Replace('_', ' ').Trim(); // "O_GROSSO_J" was read with its underscores (2026-10-24)
            var who = characterClass.Length == 0 || characterClass == name ? spoken : $"{spoken}, {characterClass}";
            return level > 0 ? T($"{who}, level {level}", $"{who}, nivel {level}") : who;
        }

        /// <summary>
        ///     A label for a control known only by its id (nothing on screen names it): the humanised id ("league"),
        ///     translated where the game's own word is known; other ids stay as they are.
        /// </summary>
        public static string ControlLabel(string humanisedId) => humanisedId switch
        {
            "league" => T("League", "Liga"),
            "gateway" => T("Gateway", "Servidor"),
            "language" => T("Language", "Idioma"),
            _ => humanisedId,
        };

        public static string EnemyWithRarity(string name, GameHelper.RemoteEnums.Rarity rarity) => $"{name}, {RarityName(rarity)}";

        internal static string RarityName(GameHelper.RemoteEnums.Rarity rarity) => rarity switch
        {
            GameHelper.RemoteEnums.Rarity.Normal => T("normal", "normal"),
            GameHelper.RemoteEnums.Rarity.Magic => T("magic", "mágico"),
            GameHelper.RemoteEnums.Rarity.Rare => T("rare", "raro"),
            GameHelper.RemoteEnums.Rarity.Unique => T("unique", "único"),
            _ => rarity.ToString().ToLowerInvariant(),
        };

        /// <summary>A control that is shown but cannot be used yet (locked button).</summary>
        public static string Unavailable(string label) => $"{label}, {UnavailableState}";

        /// <summary>
        ///     A caption the game keeps in lower case and draws in small capitals ("reanudar juego" in the game menu,
        ///     "confirmar A" with its button, 0.5.5e) gets its first letter raised, so it reads as a name on a braille
        ///     display; a text with capitals in its words ("kehgan, Nivel 2 Monje") stays as written.
        /// </summary>
        public static string Capitalised(string text) =>
            text.Length > 0 && char.IsLower(text[0]) && !text.Split(' ').Any(word => word.Length > 1 && word.Any(char.IsUpper))
                ? char.ToUpperInvariant(text[0]) + text[1..]
                : text;

        public static string UnavailableState => T("unavailable", "no disponible");

        /// <summary>Spoken controller button for a glyph file (same name in every controller family); unknown glyphs keep their file name.</summary>
        public static string ButtonName(string glyphFile, bool playStation) =>
            ButtonNames(glyphFile) is { } names ? (playStation ? names.PlayStation : names.Xbox) : glyphFile;

        private static (string Xbox, string PlayStation)? ButtonNames(string glyphFile) => glyphFile switch
        {
            "Button4" => ("A", T("Cross", "Cruz")),
            "Button3" => ("B", T("Circle", "Círculo")),
            "Button1" => ("X", T("Square", "Cuadrado")),
            "Button2" => ("Y", T("Triangle", "Triángulo")),
            "ShoulderButtonLeft" => ("LB", "L1"),
            "ShoulderButtonRight" => ("RB", "R1"),
            "TriggerLeft" => ("LT", "L2"),
            "TriggerRight" => ("RT", "R2"),
            "LeftStickButton2" => (T("left stick click", "clic del stick izquierdo"), "L3"),
            "RightStickButton2" => (T("right stick click", "clic del stick derecho"), "R3"),
            "LeftStick2" => Both(T("left stick", "stick izquierdo")),
            "RightStick2" => Both(T("right stick", "stick derecho")),
            "DPad" => Both(T("D-pad", "cruceta")),
            "DPadUp" => Both(T("D-pad up", "cruceta arriba")),
            "DPadDown" => Both(T("D-pad down", "cruceta abajo")),
            "DPadLeft" => Both(T("D-pad left", "cruceta izquierda")),
            "DPadRight" => Both(T("D-pad right", "cruceta derecha")),
            "DPadUpDown" => Both(T("D-pad up and down", "cruceta arriba y abajo")),
            "DPadLeftRight" => Both(T("D-pad left and right", "cruceta izquierda y derecha")),
            "MenuButton" => ("Menu", T("Options", "Opciones")),
            "ViewButton" => ("View", T("Create", "Crear")),
            _ => null,
        };

        private static (string Xbox, string PlayStation) Both(string name) => (name, name);

        public static string Empty => T("empty", "vacío");

        /// <summary>A weapon-set toggle, named as the game's tooltips do ("Set de armas I").</summary>
        public static string WeaponSet(int number)
        {
            var numeral = number switch { 1 => "I", 2 => "II", _ => number.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            return T($"weapon set {numeral}", $"set de armas {numeral}");
        }

        /// <summary>The skill picker's red cross: choosing it leaves the button without a skill.</summary>
        public static string ClearBinding => T("empty the button", "vaciar el botón");

        /// <summary>A list entry drawn grey among coloured ones (a dialogue topic already heard).</summary>
        public static string Dimmed => T("dimmed", "atenuado");

        public static string EmptyCell => T("empty", "vacío");
        public static string UnknownItem => T("unknown item", "objeto desconocido");

        /// <summary>
        ///     Item text and position for a grid cell: equipment slots name the slot ("Gloves, empty") and have no
        ///     position, flasks say their slot number, the main inventory and other grids (stash, vendor) column and row.
        /// </summary>
        public static (string Text, string Value) InventoryCell(GameHelper.RemoteEnums.InventoryName? inventory, string item, int column, int row)
        {
            if (inventory == GameHelper.RemoteEnums.InventoryName.Flask1)
            {
                return (item, T($"flask slot {column + 1}", $"ranura de frasco {column + 1}"));
            }

            // Stash tabs are not player inventories, yet GameHelper's map can name one after an unrelated slot.
            return inventory is { } slot && SlotName(slot) is { } name ? ($"{name}, {item}", string.Empty) : (item, GridPosition(column, row));
        }

        public static string? SlotName(GameHelper.RemoteEnums.InventoryName inventory) => inventory switch
        {
            GameHelper.RemoteEnums.InventoryName.BodyArmour1 => T("Body armour", "Armadura corporal"),
            GameHelper.RemoteEnums.InventoryName.Weapon1 => T("Main hand", "Mano principal"),
            GameHelper.RemoteEnums.InventoryName.Offhand1 => T("Off hand", "Mano secundaria"),
            GameHelper.RemoteEnums.InventoryName.Weapon2 => T("Second weapon set, main hand", "Segundo set de armas, mano principal"),
            GameHelper.RemoteEnums.InventoryName.Offhand2 => T("Second weapon set, off hand", "Segundo set de armas, mano secundaria"),
            GameHelper.RemoteEnums.InventoryName.Helm1 => T("Helmet", "Casco"),
            GameHelper.RemoteEnums.InventoryName.Amulet1 => T("Amulet", "Amuleto"),
            GameHelper.RemoteEnums.InventoryName.Ring1 => T("Left ring", "Anillo izquierdo"),
            GameHelper.RemoteEnums.InventoryName.Ring2 => T("Right ring", "Anillo derecho"),
            GameHelper.RemoteEnums.InventoryName.Gloves1 => T("Gloves", "Guantes"),
            GameHelper.RemoteEnums.InventoryName.Boots1 => T("Boots", "Botas"),
            GameHelper.RemoteEnums.InventoryName.Belt1 => T("Belt", "Cinturón"),
            _ => null,
        };

        /// <summary>One-based cell of the inventory grid, read after the item.</summary>
        public static string GridPosition(int column, int row) => T($"column {column + 1}, row {row + 1}", $"columna {column + 1}, fila {row + 1}");

        /// <summary>Base name, stack size when more than one, rarity unless normal.</summary>
        public static string Item(string name, GameHelper.RemoteEnums.Rarity rarity, int count)
        {
            var parts = new System.Collections.Generic.List<string> { name.Length > 0 ? name : UnknownItem };
            if (count > 1)
            {
                parts.Add(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            if (rarity != GameHelper.RemoteEnums.Rarity.Normal)
            {
                parts.Add(RarityName(rarity));
            }

            return string.Join(", ", parts);
        }

        /// <summary>Echo of typing in a text field: the typed characters, or what was deleted.</summary>
        public static string Typed(string added, string removed) =>
            added.Length > 0 ? Character(added) : T($"deleted {Character(removed)}", $"borrado {Character(removed)}");

        public static string ValueOrEmpty(string value) => value.Length == 0 ? Empty : value;

        /// <summary>A lone space is silent in most screen readers, so it is named.</summary>
        private static string Character(string text) => text == " " ? T("space", "espacio") : text;

        /// <summary>Joins two buttons shown for the same action.</summary>
        public static string ButtonSeparator => T(" or ", " o ");

        /// <summary>Joins buttons pressed together ("RT plus L3").</summary>
        public static string ButtonCombination => T(" plus ", " más ");

        public static string Gold => T("gold", "oro");

        /// <summary>
        ///     A quest reward by its icon's kind ("SupportGem"); Spanish as the game's item classes say it (ItemClasses:
        ///     "Gemas de asistencia", "Viales" for charms, "Tablilla"). A kind not known yet is said as its art name.
        /// </summary>
        public static string QuestReward(string kind) => kind.ToLowerInvariant() switch
        {
            "artificersorb" => Services.ItemNames.Of("CurrencyAddEquipmentSocket") ?? T("Artificer's Orb", "Orbe del artesano"),
            "atlaspassive" => T("Atlas passive point", "punto de pasiva del Atlas"),
            "book" => T("book", "libro"),
            "charms" => T("charms", "viales"),
            "consumablebuff" => T("lasting bonus", "bonificación duradera"),
            "craftingbench" => T("crafting bench", "mesa de fabricación"),
            "flasks" => T("flasks", "frascos"),
            "gold" => Gold,
            "precursortablet" => T("tablet", "tablilla"),
            "skillgem" => T("skill gem", "gema de habilidad"),
            "spiritdraught" => T("spirit", "espíritu"),
            "supportgem" => T("support gem", "gema de asistencia"),
            _ => kind,
        };

        public static string LevelUp(int level) => T($"Level {level}", $"Nivel {level}");

        /// <summary>Local co-op: the other local player died while the followed one lives (<see cref="Services.CoopDowns"/>).</summary>
        public static string PartnerDown(string name) =>
            T($"{name} is down. Go to their body to revive them", $"{name} ha caído. Ve hasta su cuerpo para revivir");

        public static string PartnerUp(string name) => T($"{name} is back on their feet", $"{name} vuelve a estar en pie");

        /// <summary>Local co-op: the followed player died while the other lives, so there is no death screen yet.</summary>
        public static string YouAreDown => T("You are down. The other player can revive you", "Has caído. El otro jugador puede revivirte");

        public static string YouAreUp => T("You are back on your feet", "Vuelves a estar en pie");

        /// <summary>What the player just walked up to: "Checkpoint", "Una, Stash".</summary>
        public static string Nearby(System.Collections.Generic.IEnumerable<string> names) => string.Join(", ", names);

        /// <summary>An amount shown beside a currency icon ("128 gold"); a price drawn red is one the player cannot pay.</summary>
        public static string Amount(string amount, string currency, bool cannotAfford) =>
            cannotAfford ? T($"{amount} {currency}, not enough", $"{amount} {currency}, no alcanza") : $"{amount} {currency}";

        /// <summary>A label with the controller button bound to it ("Select, A").</summary>
        /// <summary>
        ///     "Show quests, X"; a label that goes on from its button ("&lt;&lt;view&gt;&gt; to open the menus.") keeps the
        ///     game's order: "View to open the menus."
        /// </summary>
        public static string WithButton(string label, string button) =>
            button.Length == 0 ? label : label.Length > 0 && char.IsLower(label[0]) ? $"{button} {label}" : $"{label}, {button}";

        /// <summary>"100%" or "(0/1)" still need a full stop; these already end a sentence or lead into the next.</summary>
        private const string SentenceEnds = ".!?:;";

        /// <summary>Screen texts read in a row: each one is its own sentence unless it already ends in punctuation.</summary>
        public static string Sentences(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ". ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        /// <summary>Parts of one announcement: comma-separated, but a part that ends a sentence is not followed by a comma.</summary>
        private static string Clauses(System.Collections.Generic.IEnumerable<string> parts)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (builder.Length > 0)
                {
                    builder.Append(SentenceEnds.Contains(builder[^1]) ? " " : ", ");
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        public static string GameAttached => T("Game Helper attached to the game.", "Game Helper conectado al juego.");
        public static string GameNotAttached => T("Game Helper is not attached to the game.", "Game Helper no está conectado al juego.");
        public static string NoGameProcess => T("No Path of Exile process is running.", "No hay ningún proceso de Path of Exile en ejecución.");

        /// <summary>"Área: Sierraclara, nivel 2." (the area level is its monsters' level; 0 when unknown).</summary>
        public static string Area(string areaName, int level = 0) =>
            level > 0 ? T($"Area: {areaName}, level {level}.", $"Área: {areaName}, nivel {level}.") : T($"Area: {areaName}.", $"Área: {areaName}.");

        /// <summary>
        ///     The character's level, how far it is to the next (D2R experience percent) and the area's level (its
        ///     monsters'), for the vitals readout: "Nivel 4, 35 por ciento hacia el nivel 5, nivel del área 2".
        /// </summary>
        public static string Levels(int character, int area, int? progress = null)
        {
            var own = character <= 0 ? string.Empty
                : progress is { } percent ? T($"Level {character}, {percent} percent to level {character + 1}", $"Nivel {character}, {percent} por ciento hacia el nivel {character + 1}")
                : LevelUp(character);
            return own.Length > 0 && area > 0 ? T($"{own}, area level {area}", $"{own}, nivel del área {area}")
                : own.Length > 0 ? own
                : area > 0 ? T($"area level {area}", $"nivel del área {area}")
                : string.Empty;
        }

        /// <summary>Menu rule: name, then state/value, then role. Value-bearing controls read "label, value, role".</summary>
        /// <param name="fullTooltip">Also read a long tooltip (item mods, gem stats) after the position.</param>
        public static string Focused(Models.AccessibleUiNode node, bool fullTooltip = false)
        {
            if (node.Role == Models.UiRole.Link)
            {
                return Link(node);
            }

            var role = node.Role switch
            {
                Models.UiRole.Button => T("button", "botón"),
                Models.UiRole.ComboBox => T("combo box", "lista desplegable"),
                Models.UiRole.TextField => T("edit field", "campo de texto"),
                Models.UiRole.CheckBox => T("check box", "casilla de verificación"),
                Models.UiRole.Slider => T("slider", "control deslizante"),
                Models.UiRole.Tab => T("tab", "pestaña"),
                Models.UiRole.Link => T("link", "enlace"),
                _ => string.Empty,
            };
            var parts = new System.Collections.Generic.List<string>(3);
            if (node.Text.Length > 0)
            {
                parts.Add(Capitalised(node.Text));
            }

            if (node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            if (role.Length > 0 && !node.IsHovered)
            {
                parts.Add(role);
            }

            if (node.IsCollapsible)
            {
                parts.Add(node.IsExpanded ? Expanded : Collapsed);
            }

            if (!node.IsEnabled)
            {
                parts.Add(UnavailableState);
            }

            // Name, state, place in the list, then what it means (D2R order): the position never trails a description.
            if (node.Position.Length > 0)
            {
                parts.Add(node.Position);
            }

            var tooltip = Unsaid(node.Tooltip, node.Text);
            if ((node.IsHovered || node.Role == Models.UiRole.Link) && tooltip.Count > 0)
            {
                parts.Add(Sentences(tooltip));
            }
            else if (IsShortTooltip(tooltip))
            {
                parts.Add(Sentences(tooltip));
            }

            if (fullTooltip && !node.IsHovered && tooltip.Count > 0 && !IsShortTooltip(tooltip))
            {
                parts.Add(Sentences(tooltip));
            }

            return parts.Count == 0 ? UnnamedElement : Clauses(parts);
        }

        public static string UnnamedElement => T("Unnamed element", "Elemento sin nombre");

        /// <summary>The icon button that empties a search box (the game's own hint for it: "Borrar búsqueda").</summary>
        public static string ClearSearch => T("Clear search", "Borrar búsqueda");

        /// <summary>A collapsible row shown open (a Challenges category after A).</summary>
        public static string Expanded => T("expanded", "expandido");

        public static string Collapsed => T("collapsed", "contraído");

        /// <summary>A hint whose button is held, not pressed: "Comprobar precio, mantener Y".</summary>
        public static string HoldButton(string button) => T($"hold {button}", $"mantener {button}");

        /// <summary>
        ///     A tooltip without its title when the label already said it: a Skills row "NIVEL: 1, Rayo atronador, DPS: 12,5"
        ///     read the skill's name again as the tooltip's first line (2026-10-25).
        /// </summary>
        private static System.Collections.Generic.IReadOnlyList<string> Unsaid(System.Collections.Generic.IReadOnlyList<string> tooltip, string said)
        {
            if (tooltip.Count == 0 || said.Length == 0)
            {
                return tooltip;
            }

            var title = tooltip[0].Trim();
            var clauses = said.Split(", ");
            return clauses.Any(clause => clause.Trim().Equals(title, StringComparison.OrdinalIgnoreCase)) ? tooltip.Skip(1).ToList() : tooltip;
        }

        /// <summary>"Evasion Rating, 7. Evasion, link. Evasion Rating grants a chance …": the line, the keyword, its definition.</summary>
        /// <summary>A keyword link: the text holding it (unless just said for the previous link), the keyword, its definition.</summary>
        public static string Link(Models.AccessibleUiNode node, bool withRow = true)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (withRow && node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            parts.Add(T($"{node.Text}, link", $"{node.Text}, enlace"));
            parts.AddRange(node.Tooltip);
            return Sentences(parts);
        }

        public static string ListPosition(int oneBasedIndex, int count) => T($"{oneBasedIndex} of {count}", $"{oneBasedIndex} de {count}");

        /// <summary>Cells of one row or entry: a caption ending in ':' runs into its value ("LVL: 1, Punch").</summary>
        public static string Cells(System.Collections.Generic.IEnumerable<string> cells)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var cell in cells)
            {
                if (builder.Length > 0)
                {
                    builder.Append(builder[^1] == ':' || SentenceEnds.Contains(builder[^1]) ? " " : ", ");
                }

                builder.Append(cell);
            }

            return builder.ToString();
        }

        /// <summary>A label wrapped over several lines by the layout (the world map legend) read as one line.</summary>
        public static string Flatten(string text) =>
            SpaceRuns.Replace(text.Contains('\n') ? JoinLines(text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : text, " ");

        /// <summary>Where inline art was cut out of a text ("Identify <<itemvisual:…>> x 1" reads "Identify   x 1").</summary>
        private static readonly System.Text.RegularExpressions.Regex SpaceRuns = new(" {2,}", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>
        ///     Lines of one text: a line starting in lower case continues the previous one (layout wrapping, "Socket
        ///     a Support Gem of any / colour to …"); any other starts a new sentence ("1 Strength = +2 Life / 5 Strength …").
        /// </summary>
        public static string JoinLines(System.Collections.Generic.IEnumerable<string> lines)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var line in lines)
            {
                if (builder.Length > 0)
                {
                    var continues = char.IsLower(line[0]) || SentenceEnds.Contains(builder[^1]) || EndsInLinkWord(builder);
                    builder.Append(continues ? " " : ". ");
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

        /// <summary>
        ///     A line ending in a short lower-case word wraps mid-phrase even when the next starts in capitals: "Ocultar
        ///     las pestañas de" / "Solo retirar" (a tab type's name) is one option label.
        /// </summary>
        private static bool EndsInLinkWord(System.Text.StringBuilder builder)
        {
            var end = builder.Length;
            var start = end;
            while (start > 0 && char.IsLetter(builder[start - 1]))
            {
                start--;
            }

            return end - start is > 0 and <= MaxLinkWordLength && char.IsLower(builder[start]) &&
                   (start == 0 || builder[start - 1] == ' ');
        }

        /// <summary>"de", "del", "la", "of", "the", "to": the length of the words a phrase cannot end on.</summary>
        private const int MaxLinkWordLength = 3;

        /// <summary>A combo opened in place: the D-pad now steps its value.</summary>
        public static string ComboOpened(string valueAndPosition) => T($"Choosing: {valueAndPosition}", $"Eligiendo: {valueAndPosition}");

        /// <summary>A combo closed on its value.</summary>
        public static string ComboClosed(string value) => T($"Chosen: {value}", $"Elegido: {value}");

        /// <summary>A changed value with its place among the choices: "Adaptativo (experimental), 3 de 4".</summary>
        public static string ValueAt(string value, string position) => position.Length == 0 ? value : $"{value}, {position}";

        /// <summary>One table row: "Maximum Life, 42"; a caption ending in ':' runs into its value ("LVL: 1").</summary>
        public static string LabelValue(string label, string value) => label.EndsWith(':') ? $"{label} {value}" : $"{label}, {value}";

        /// <summary>Tooltips up to this size are read with the focus (D2R: name, state, tooltip, position); longer ones on request.</summary>
        private const int MaxAutoTooltipLines = 2;

        private const int MaxAutoTooltipChars = 200;

        public static string NoDetails => T("No details.", "Sin detalles.");

        public static bool IsShortTooltip(System.Collections.Generic.IReadOnlyList<string> tooltip) =>
            tooltip.Count is > 0 and <= MaxAutoTooltipLines && tooltip.Sum(line => line.Length) <= MaxAutoTooltipChars;

        /// <summary>The focused element's full tooltip (skill gems, rich descriptions).</summary>
        public static string Details(Models.AccessibleUiNode node) =>
            node.Tooltip.Count == 0 ? NoDetails : Sentences(node.Tooltip);

        /// <summary>Entering a panel: its title before the focused item.</summary>
        public static string InContext(string context, string focused) =>
            context.Length == 0 ? focused : focused.Length == 0 ? context : Sentences(new[] { context, focused });

        /// <summary>
        ///     GameHelper Radar's labels are notes players wrote in English ("Stairs Up", "Crop Circle (Skill Gem Level 4)"):
        ///     in other languages their common words and reward notes are translated; names stay as written.
        /// </summary>
        public static string LandmarkLabel(string label)
        {
            if (Localization.Language == "en")
            {
                return label;
            }

            var trimmed = label.Trim();
            if (LandmarkWord(trimmed) is { } whole)
            {
                return whole;
            }

            var replacements = LandmarkNoteTexts;
            for (var i = 0; i < LandmarkNotes.Length; i++)
            {
                trimmed = LandmarkNotes[i].Replace(trimmed, replacements[i]);
            }

            return trimmed;
        }

        private static string? LandmarkWord(string label) => label.ToLowerInvariant() switch
        {
            "boss" => T("Boss", "Jefe"),
            "bosses" => T("Bosses", "Jefes"),
            "boss stairs" => T("Boss stairs", "Escaleras del jefe"),
            "stairs" => T("Stairs", "Escaleras"),
            "stairs up" => T("Stairs Up", "Escaleras hacia arriba"),
            "stairs down" => T("Stairs Down", "Escaleras hacia abajo"),
            "entrance" => T("Entrance", "Entrada"),
            "transition" => T("Transition", "Paso a otra zona"),
            "beacon" => T("Beacon", "Baliza"),
            "ancient seal" => T("Ancient Seal", "Sello antiguo"),
            "looted vault" => T("Looted Vault", "Cámara saqueada"),
            "rare fight" => T("Rare Fight", "Combate con un monstruo raro"),
            "medallion" => T("Medallion", "Medallón"),
            "bell (rewards)" => T("Bell (Rewards)", "Campana (recompensas)"),
            "flask buff - need key" => T("Flask buff - Need key", "Mejora de frascos, necesita una llave"),
            "pillars - buff choice" => T("Pillars - Buff choice", "Pilares, elige una mejora"),
            "rhoa - no reward?" => T("Rhoa - No reward?", "Rhoa, quizá sin recompensa"),
            "to devourer" => T("To Devourer", "Hacia el Devorador"),
            "arena / hunting grounds" => T("Arena / Hunting Grounds", "Arena o coto de caza"),
            "forgotten corpses" => T("Forgotten Corpses", "Cadáveres olvidados"),
            "forgotten riches" => T("Forgotten Riches", "Riquezas olvidadas"),
            _ => null,
        };

        private static readonly System.Text.RegularExpressions.Regex[] LandmarkNotes =
        {
            new(@"Support [Gg]em Level (\d+) and Flasks"),
            new(@"Support [Gg]em Level (\d+)"),
            new(@"Support [Gg]em"),
            new(@"Skill Gem Level (\d+)"),
            new(@"Spirit Gem Level (\d+)"),
            new(@"(\d+) Spirit"),
            new(@"\+(\d+) Max Life"),
            new(@"\+(\d+) Passives?"),
            new(@"(\d+)% Cold Res"),
            new(@"(\d+)% Fire Res"),
            new(@"(\d+)% Lightning Res"),
            new(@"(\d+)% Chaos Res"),
            new(@"Res Ring"),
            new(@"\(waypoint\)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new(@"Salvaging Bench"),
            new(@" - "),
        };

        /// <summary>The replacement of each <see cref="LandmarkNotes"/> pattern, in the same order.</summary>
        private static string[] LandmarkNoteTexts => new[]
        {
            T("support gem level $1 and flasks", "gema de apoyo de nivel $1 y frascos"),
            T("support gem level $1", "gema de apoyo de nivel $1"),
            T("support gem", "gema de apoyo"),
            T("skill gem level $1", "gema de habilidad de nivel $1"),
            T("spirit gem level $1", "gema de espíritu de nivel $1"),
            T("$1 spirit", "$1 de espíritu"),
            T("+$1 maximum life", "+$1 de vida máxima"),
            T("+$1 passive points", "+$1 puntos de pasiva"),
            T("$1% cold resistance", "$1% de resistencia al frío"),
            T("$1% fire resistance", "$1% de resistencia al fuego"),
            T("$1% lightning resistance", "$1% de resistencia al rayo"),
            T("$1% chaos resistance", "$1% de resistencia al caos"),
            T("resistance ring", "anillo de resistencia"),
            T("(waypoint)", "(punto de ruta)"),
            T("salvaging bench", "mesa de reciclaje"),
            ", ",
        };

        /// <summary>A text too long to say at once: how many paragraphs it has and how to read them.</summary>
        public static string LongText(int paragraphs) => T(
            $"Long text, {paragraphs} paragraphs: scroll it with the stick to read it",
            $"Texto largo, {paragraphs} párrafos: desplázalo con el stick para leerlo");

        /// <summary>One paragraph of a long text, after its place in it.</summary>
        public static string LongTextParagraph(string paragraph, int number, int total) => T($"{number} of {total}. {paragraph}", $"{number} de {total}. {paragraph}");

        /// <summary>Numbered tabs ("-1-" vendor pages, "1" default stash tabs) read "tab 1"; named tabs keep their name.</summary>
        public static string TabTitle(string caption)
        {
            var number = caption.Trim('-', ' ');
            return number.Length > 0 && number.All(char.IsDigit) ? T($"tab {number}", $"pestaña {number}") : caption;
        }

        public static string Status(bool attached, GameStateTypes state, string areaName, string backend, string processReport)
        {
            if (!attached)
            {
                var detail = string.IsNullOrEmpty(processReport) ? NoGameProcess : processReport;
                return T($"{GameNotAttached} {detail} Speech backend {backend}.", $"{GameNotAttached} {detail} Motor de voz {backend}.");
            }

            return string.IsNullOrEmpty(areaName)
                ? T($"{ForState(state)} Speech backend {backend}.", $"{ForState(state)} Motor de voz {backend}.")
                : T($"{ForState(state)} {Area(areaName)} Speech backend {backend}.", $"{ForState(state)} {Area(areaName)} Motor de voz {backend}.");
        }

        public static string GameProcessMatches(string processName) =>
            T(
                $"Process {processName} is running with the expected window title; Game Helper should attach within seconds.",
                $"El proceso {processName} está en ejecución con el título de ventana esperado; Game Helper debería conectarse en unos segundos.");

        public static string GameProcessTitleMismatch(string processName, string title) =>
            string.IsNullOrEmpty(title)
                ? T(
                    $"Process {processName} is running but has no main window yet.",
                    $"El proceso {processName} está en ejecución pero aún no tiene ventana principal.")
                : T(
                    $"Process {processName} is running but its window title is \"{title}\", not \"Path of Exile 2\", so Game Helper will not attach.",
                    $"El proceso {processName} está en ejecución pero el título de su ventana es \"{title}\", no \"Path of Exile 2\", así que Game Helper no se conectará.");

        public static string GameProcessInaccessible(string processName) =>
            T(
                $"Process {processName} is running but cannot be inspected; run Game Helper as administrator.",
                $"El proceso {processName} está en ejecución pero no se puede inspeccionar; ejecuta Game Helper como administrador.");
    }
}
