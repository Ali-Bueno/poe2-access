namespace Poe2Accessibility
{
    using System.Linq;

    /// <summary>Controls and buttons: labels, check box and slider values, button names, typed text and unavailable states.</summary>
    public static partial class Announcements
    {
        /// <summary>Fallback when a combo's entry text cannot be decoded yet: position within the list.</summary>
        public static string ComboPosition(int oneBasedIndex, int count) => T($"option {oneBasedIndex} of {count}", $"opción {oneBasedIndex} de {count}");

        /// <summary>Sliders without an on-screen label: a percentage when the range is 0 to 100, else the value out of its maximum.</summary>
        public static string SliderValue(int value, int min, int max) =>
            min == 0 && max == 100 ? T($"{value} percent", $"{value} por ciento")
            : max > min ? T($"{value} of {max}", $"{value} de {max}") : $"{value}";

        public static string Checked => T("checked", "marcado");

        public static string Unchecked => T("unchecked", "desmarcado");

        public static string NoHints => T("No button hints on screen.", "No hay indicaciones de botones en pantalla.");

        /// <summary>Said after a read of the screen: the controls it could not name, so the player knows there is more than was read.</summary>
        public static string ControlsWithoutName(int count) =>
            count == 1 ? T("1 control without a name", "1 control sin nombre") : T($"{count} controls without a name", $"{count} controles sin nombre");

        public static string NothingToRead => T("Nothing to read here.", "Nada que leer aquí.");

        public static string NothingFound => T("Nothing found here.", "No se encontró nada aquí.");

        /// <summary>
        ///     The heading of a button menu: the texts it shows above its buttons. The menu A opens beside another player
        ///     shows only that player's class, level, league and name ("Cazadora Niv. 6, Liga …, LaGuanche. Invitar al
        ///     grupo"), with no word that it is a player (2026-11-10): said as one when a player of that name is there.
        /// </summary>
        public static string ButtonMenuContext(System.Collections.Generic.IReadOnlyList<string> header, System.Func<string, bool>? isPlayerNamed)
        {
            var shown = string.Join(", ", header);
            return shown.Length > 0 && isPlayerNamed != null && header.Any(text => isPlayerNamed(text.Trim())) ? T($"Player: {shown}", $"Jugador: {shown}") : shown;
        }

        public static string ControlLabel(string humanisedId) => humanisedId switch
        {
            "league" => T("League", "Liga"),
            "gateway" => T("Gateway", "Servidor"),
            "language" => T("Language", "Idioma"),
            "options" => T("Options", "Opciones"),
            "collapse" => T("Collapse", "Contraer"),

            // The build planner's list (passive tree, gem engraving window): the game's "Active Build" (ClientStrings).
            "BuildPlannerComboBox" or "build planner" => T("Active Build", "Build activa"),
            _ => humanisedId,
        };

        /// <summary>A control that is shown but cannot be used yet (locked button).</summary>
        public static string Unavailable(string label) => $"{label}, {UnavailableState}";

        public static string UnavailableState => T("unavailable", "no disponible");

        public static string NewsBanner => T("News", "Anuncio");

        /// <summary>Said with the frame around a text box.</summary>
        public static string TypeWithA => T("A to type", "A para escribir");

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

        /// <summary>The skill picker's red cross: choosing it leaves the button without a skill.</summary>
        public static string ClearBinding => T("empty the button", "vaciar el botón");

        /// <summary>A list entry drawn grey among coloured ones (a dialogue topic already heard).</summary>
        public static string Dimmed => T("dimmed", "atenuado");

        /// <summary>Echo of typing in a text field: the typed characters, or what was deleted.</summary>
        public static string Typed(string added, string removed) =>
            added.Length > 0 ? Character(added) : T($"deleted {Character(removed)}", $"borrado {Character(removed)}");

        /// <summary>A password field is never read back, only how long it is (docs/menu-speech-rules.md); empty when it is empty.</summary>
        public static string PasswordLength(int length) => length switch
        {
            0 => string.Empty,
            1 => T("1 character", "1 carácter"),
            _ => T($"{length} characters", $"{length} caracteres"),
        };

        public static string ValueOrEmpty(string value) => value.Length == 0 ? Empty : value;

        /// <summary>A lone space is silent in most screen readers, so it is named.</summary>
        private static string Character(string text) => text == " " ? T("space", "espacio") : text;

        /// <summary>Joins two buttons shown for the same action.</summary>
        public static string ButtonSeparator => T(" or ", " o ");

        /// <summary>Joins buttons pressed together ("RT plus L3").</summary>
        public static string ButtonCombination => T(" plus ", " más ");

        /// <summary>A label with the controller button bound to it ("Select, A").</summary>
        /// <summary>
        ///     "Show quests, X"; a label that goes on from its button ("&lt;&lt;view&gt;&gt; to open the menus.") keeps the
        ///     game's order: "View to open the menus."
        /// </summary>
        public static string WithButton(string label, string button) =>
            button.Length == 0 ? label : label.Length > 0 && char.IsLower(label[0]) ? $"{button} {label}" : $"{label}, {button}";
    }
}
