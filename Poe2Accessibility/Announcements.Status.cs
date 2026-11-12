namespace Poe2Accessibility
{
    using GameReader.RemoteEnums;

    /// <summary>Game, area and character status: lifecycle messages, screen names, levels, co-op downs and the connection report.</summary>
    public static partial class Announcements
    {
        public static string PluginReady => T("Path of Exile 2 accessibility ready.", "Accesibilidad de Path of Exile 2 lista.");
        public static string GameDetected => T("Game detected.", "Juego detectado.");
        public static string UiDumpStarted => T("Dumping user interface tree.", "Volcando el árbol de la interfaz.");
        public static string UiDumpDone => T("User interface dump written.", "Volcado de la interfaz escrito.");
        public static string UiDumpNoRoot => T("No user interface root found for this screen; diagnostic file written.", "No se encontró la raíz de la interfaz de esta pantalla; archivo de diagnóstico escrito.");
        public static string UiDumpFailed => T("User interface dump failed.", "Falló el volcado de la interfaz.");
        public static string NothingToRepeat => T("Nothing to repeat.", "Nada que repetir.");
        public static string SettingsReloaded => T("Settings reloaded.", "Configuración recargada.");
        public static string SettingsInvalid => T("Settings file could not be read, keeping current settings.", "No se pudo leer el archivo de configuración; se mantiene la actual.");

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

        /// <summary>Life fell below a threshold: "Life 25 percent".</summary>
        public static string LowLife(int percent) => T($"Life {percent} percent", $"Vida {percent} por ciento");

        public static string LevelUp(int level) => T($"Level {level}", $"Nivel {level}");

        /// <summary>Local co-op: the other local player died while the followed one lives (<see cref="Services.CoopDowns"/>).</summary>
        public static string PartnerDown(string name) =>
            T($"{name} is down. Go to their body to revive them", $"{name} ha caído. Ve hasta su cuerpo para revivir");

        public static string PartnerUp(string name) => T($"{name} is back on their feet", $"{name} vuelve a estar en pie");

        /// <summary>Local co-op: the followed player died while the other lives, so there is no death screen yet.</summary>
        public static string YouAreDown => T("You are down. The other player can revive you", "Has caído. El otro jugador puede revivirte");

        public static string YouAreUp => T("You are back on your feet", "Vuelves a estar en pie");

        public static string GameAttached => T("The mod is connected to the game.", "El mod está conectado al juego.");
        public static string GameNotAttached => T("The mod is not connected to the game.", "El mod no está conectado al juego.");
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

        /// <summary>The game came back as another build while the mod kept running: its catalogs are made only when the mod starts.</summary>
        public static string GameUpdatedUnderMod =>
            T("The game was updated. Restart the mod to update its game data", "El juego se actualizó. Reinicia el mod para actualizar sus datos");

        public static string Status(bool attached, GameStateTypes state, string areaName, string backend, string processReport)
        {
            if (!attached)
            {
                var detail = string.IsNullOrEmpty(processReport) ? NoGameProcess : processReport;
                return T($"{GameNotAttached} {detail} Speech backend {backend}.", $"{GameNotAttached} {detail} Motor de voz {backend}.");
            }

            // The area's name outlives the game it was read in: at the login screen after leaving a character it is the
            // last one's ("Inicio de sesión. Área: La caravana de los ardura", 2026-11-10), so it is said only in a game.
            var inAnArea = state is GameStateTypes.InGameState or GameStateTypes.EscapeState or GameStateTypes.AreaLoadingState;
            return string.IsNullOrEmpty(areaName) || !inAnArea
                ? T($"{ForState(state)} Speech backend {backend}.", $"{ForState(state)} Motor de voz {backend}.")
                : T($"{ForState(state)} {Area(areaName)} Speech backend {backend}.", $"{ForState(state)} {Area(areaName)} Motor de voz {backend}.");
        }

        public static string GameProcessMatches(string processName) =>
            T(
                $"Process {processName} is running with the expected window title; the mod should connect within seconds.",
                $"El proceso {processName} está en ejecución con el título de ventana esperado; el mod debería conectarse en unos segundos.");

        public static string GameProcessTitleMismatch(string processName, string title) =>
            string.IsNullOrEmpty(title)
                ? T(
                    $"Process {processName} is running but has no main window yet.",
                    $"El proceso {processName} está en ejecución pero aún no tiene ventana principal.")
                : T(
                    $"Process {processName} is running but its window title is \"{title}\", not \"Path of Exile 2\", so the mod will not connect.",
                    $"El proceso {processName} está en ejecución pero el título de su ventana es \"{title}\", no \"Path of Exile 2\", así que el mod no se conectará.");

        public static string GameProcessInaccessible(string processName) =>
            T(
                $"Process {processName} is running but cannot be inspected; run the mod as administrator.",
                $"El proceso {processName} está en ejecución pero no se puede inspeccionar; ejecuta el mod como administrador.");
    }
}
