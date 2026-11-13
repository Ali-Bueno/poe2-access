namespace Poe2Accessibility
{
    using System.Linq;

    /// <summary>The guide beacon phrases of <see cref="Announcements"/>: route turns, arrival, loss and why there is no route.</summary>
    public static partial class Announcements
    {
        /// <summary>"Guiding to Clearfell, 116 steps, down 3, then left" (the scanner's route description).</summary>
        public static string BeaconLocked(string described) => T($"Guiding to {described}", $"Guiando hacia {described}");

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
    }
}
