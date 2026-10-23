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
        private static string T(string en, string es) => Localization.IsSpanish ? es : en;

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
        public static string Unchecked => T("unchecked", "desmarcado");

        public static string NoHints => T("No button hints on screen.", "No hay indicaciones de botones en pantalla.");

        public static string NothingToRead => T("Nothing to read here.", "Nada que leer aquí.");

        public static string NothingFound => T("Nothing found here.", "No se encontró nada aquí.");

        /// <summary>Life fell below a threshold: "Life 25 percent".</summary>
        public static string LowLife(int percent) => T($"Life {percent} percent", $"Vida {percent} por ciento");

        /// <summary>The left stick's eight directions, by <see cref="Services.StickDirection"/> octant.</summary>
        public static string[] StickDirections => Localization.IsSpanish ? StickDirectionsEs : StickDirectionsEn;

        private static readonly string[] StickDirectionsEn = { "right", "up right", "up", "up left", "left", "down left", "down", "down right" };

        private static readonly string[] StickDirectionsEs =
            { "derecha", "arriba a la derecha", "arriba", "arriba a la izquierda", "izquierda", "abajo a la izquierda", "abajo", "abajo a la derecha" };

        public static string ScanCategoryName(Services.ScanCategory category) => category switch
        {
            Services.ScanCategory.Quest => T("Quest", "Misión"),
            Services.ScanCategory.Exits => T("Exits", "Salidas"),
            Services.ScanCategory.Waypoints => T("Waypoints and checkpoints", "Puntos de ruta y de control"),
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
        public static string ScanEntry(string name, int steps, string direction, int legSteps, string? then, bool routed)
        {
            if (steps < 1)
            {
                return T($"{name}, here", $"{name}, aquí");
            }

            var leg = then == null ? direction : T($"{direction} {legSteps}, then {then}", $"{direction} {legSteps}, luego {then}");
            return T(
                $"{name}, {steps} {(steps == 1 ? "step" : "steps")}, {leg}{(routed ? string.Empty : ", no route")}",
                $"{name}, {steps} {(steps == 1 ? "paso" : "pasos")}, {leg}{(routed ? string.Empty : ", sin ruta")}");
        }

        /// <summary>Another player: character name, then class ("Yarky, Witch").</summary>
        public static string OtherPlayer(string name, string characterClass) =>
            characterClass.Length == 0 || characterClass == name ? name : $"{name}, {characterClass}";

        public static string EnemyWithRarity(string name, GameHelper.RemoteEnums.Rarity rarity) => $"{name}, {RarityName(rarity)}";

        internal static string RarityName(GameHelper.RemoteEnums.Rarity rarity) => rarity switch
        {
            GameHelper.RemoteEnums.Rarity.Normal => "normal",
            GameHelper.RemoteEnums.Rarity.Magic => T("magic", "mágico"),
            GameHelper.RemoteEnums.Rarity.Rare => T("rare", "raro"),
            GameHelper.RemoteEnums.Rarity.Unique => T("unique", "único"),
            _ => rarity.ToString().ToLowerInvariant(),
        };

        /// <summary>A control that is shown but cannot be used yet (locked button).</summary>
        public static string Unavailable(string label) => $"{label}, {UnavailableState}";

        public static string UnavailableState => T("unavailable", "no disponible");

        /// <summary>Glyph file (same name in every controller family) → (Xbox name, PlayStation name).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, (string Xbox, string PlayStation)> Buttons = new()
        {
            ["Button4"] = ("A", "Cross"),
            ["Button3"] = ("B", "Circle"),
            ["Button1"] = ("X", "Square"),
            ["Button2"] = ("Y", "Triangle"),
            ["ShoulderButtonLeft"] = ("LB", "L1"),
            ["ShoulderButtonRight"] = ("RB", "R1"),
            ["TriggerLeft"] = ("LT", "L2"),
            ["TriggerRight"] = ("RT", "R2"),
            ["LeftStickButton2"] = ("left stick click", "L3"),
            ["RightStickButton2"] = ("right stick click", "R3"),
            ["LeftStick2"] = ("left stick", "left stick"),
            ["RightStick2"] = ("right stick", "right stick"),
            ["DPad"] = ("D-pad", "D-pad"),
            ["DPadUp"] = ("D-pad up", "D-pad up"),
            ["DPadDown"] = ("D-pad down", "D-pad down"),
            ["DPadLeft"] = ("D-pad left", "D-pad left"),
            ["DPadRight"] = ("D-pad right", "D-pad right"),
            ["DPadUpDown"] = ("D-pad up and down", "D-pad up and down"),
            ["DPadLeftRight"] = ("D-pad left and right", "D-pad left and right"),
            ["MenuButton"] = ("Menu", "Options"),
            ["ViewButton"] = ("View", "Create"),
        };

        /// <summary>Spoken controller button for a glyph file; unknown glyphs keep their file name.</summary>
        public static string ButtonName(string glyphFile, bool playStation) =>
            Buttons.TryGetValue(glyphFile, out var names) ? LocalButton(glyphFile, names, playStation) : glyphFile;

        private static string LocalButton(string glyphFile, (string Xbox, string PlayStation) names, bool playStation)
        {
            if (Localization.IsSpanish && ButtonsEs.TryGetValue(glyphFile, out var es))
            {
                return playStation ? es.PlayStation : es.Xbox;
            }

            return playStation ? names.PlayStation : names.Xbox;
        }

        /// <summary>Spanish names where they differ from <see cref="Buttons"/> (letters and View stay).</summary>
        private static readonly System.Collections.Generic.Dictionary<string, (string Xbox, string PlayStation)> ButtonsEs = new()
        {
            ["Button4"] = ("A", "Cruz"),
            ["Button3"] = ("B", "Círculo"),
            ["Button1"] = ("X", "Cuadrado"),
            ["Button2"] = ("Y", "Triángulo"),
            ["LeftStickButton2"] = ("clic del stick izquierdo", "L3"),
            ["RightStickButton2"] = ("clic del stick derecho", "R3"),
            ["LeftStick2"] = ("stick izquierdo", "stick izquierdo"),
            ["RightStick2"] = ("stick derecho", "stick derecho"),
            ["DPad"] = ("cruceta", "cruceta"),
            ["DPadUp"] = ("cruceta arriba", "cruceta arriba"),
            ["DPadDown"] = ("cruceta abajo", "cruceta abajo"),
            ["DPadLeft"] = ("cruceta izquierda", "cruceta izquierda"),
            ["DPadRight"] = ("cruceta derecha", "cruceta derecha"),
            ["DPadUpDown"] = ("cruceta arriba y abajo", "cruceta arriba y abajo"),
            ["DPadLeftRight"] = ("cruceta izquierda y derecha", "cruceta izquierda y derecha"),
            ["MenuButton"] = ("Menu", "Opciones"),
            ["ViewButton"] = ("View", "Crear"),
        };

        public static string Empty => T("empty", "vacío");

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

        public static string LevelUp(int level) => T($"Level {level}", $"Nivel {level}");

        /// <summary>What the player just walked up to: "Checkpoint", "Una, Stash".</summary>
        public static string Nearby(System.Collections.Generic.IEnumerable<string> names) => string.Join(", ", names);

        /// <summary>An amount shown beside a currency icon ("128 gold"); a price drawn red is one the player cannot pay.</summary>
        public static string Amount(string amount, string currency, bool cannotAfford) =>
            cannotAfford ? T($"{amount} {currency}, not enough", $"{amount} {currency}, no alcanza") : $"{amount} {currency}";

        /// <summary>A label with the controller button bound to it ("Select, A").</summary>
        public static string WithButton(string label, string button) => button.Length == 0 ? label : $"{label}, {button}";

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

        public static string Area(string areaName) => T($"Area: {areaName}.", $"Área: {areaName}.");

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
                parts.Add(node.Text);
            }

            if (node.Value.Length > 0)
            {
                parts.Add(node.Value);
            }

            if (role.Length > 0 && !node.IsHovered)
            {
                parts.Add(role);
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

            if ((node.IsHovered || node.Role == Models.UiRole.Link) && node.Tooltip.Count > 0)
            {
                parts.Add(Sentences(node.Tooltip));
            }
            else if (IsShortTooltip(node.Tooltip))
            {
                parts.Add(Sentences(node.Tooltip));
            }

            if (fullTooltip && !node.IsHovered && node.Tooltip.Count > 0 && !IsShortTooltip(node.Tooltip))
            {
                parts.Add(Sentences(node.Tooltip));
            }

            return parts.Count == 0 ? T("Unnamed element", "Elemento sin nombre") : Clauses(parts);
        }

        /// <summary>"Evasion Rating, 7. Evasion, link. Evasion Rating grants a chance …": the line, the keyword, its definition.</summary>
        private static string Link(Models.AccessibleUiNode node)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (node.Value.Length > 0)
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
                    builder.Append(builder[^1] == ':' ? " " : ", ");
                }

                builder.Append(cell);
            }

            return builder.ToString();
        }

        /// <summary>A label wrapped over several lines by the layout (the world map legend) read as one line.</summary>
        public static string Flatten(string text) =>
            text.Contains('\n') ? JoinLines(text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : text;

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
                    var continues = char.IsLower(line[0]) || SentenceEnds.Contains(builder[^1]);
                    builder.Append(continues ? " " : ". ");
                }

                builder.Append(line);
            }

            return builder.ToString();
        }

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
