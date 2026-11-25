namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The forks and rooms of the map's shape: "Cruce con escaleras: sigue arriba y a la izquierda, sin salida a la derecha".</summary>
    public static partial class Announcements
    {
        /// <summary>Kinds of map piece that are a room by themselves: the room is named after them alone.</summary>
        private static readonly HashSet<string> RoomKinds = new() { "ballroom", "church", "temple", "library", "courtyard", "arena", "forge", "market", "graveyard" };

        /// <summary>
        ///     A place's name stem from what stands in it (the most telling map piece, null for none) and whether it is numbered:
        ///     "Salón de baile", "Zona con chimenea", "Cruce con escaleras de bajada", or "Cruce" numbered by the tracker. An
        ///     open place is a "Zona", not a "Sala": the map's rooms are as often a stretch of desert as a hall (the owner, 2026-11-16).
        /// </summary>
        public static (string Stem, bool Numbered) ForkName(bool room, string? kind)
        {
            if (kind == null)
            {
                return (room ? T("Area", "Zona") : T("Fork", "Cruce"), true);
            }

            var piece = PlaceName(kind);
            if (RoomKinds.Contains(kind))
            {
                return (char.ToUpperInvariant(piece[0]) + piece[1..], false);
            }

            return (room ? T($"Area with {piece}", $"Zona con {piece}") :T($"Fork with {piece}", $"Cruce con {piece}"), false);
        }

        /// <summary>A place's line: its name, "ya estuviste aquí" when come back to, then each state with its sides, the way on first.</summary>
        public static string Fork(string name, bool again, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> ways)
        {
            var head = again ? T($"{name}, you've been here", $"{name}, ya estuviste aquí") : name;
            return ways.Count == 0 ? head : $"{head}: {string.Join(", ", ways.Select(way => ForkWayLine(way.State, Both(way.Sides))))}";
        }

        /// <summary>A way's side with the known places it leads to: "arriba hacia El valle rojo".</summary>
        public static string SideToward(string side, IEnumerable<string> places)
        {
            var toward = Both(places.ToList());
            return toward.Length == 0 ? side : $"{side} {toward}";
        }

        private static string ForkWayLine(ForkWay state, string sides) => state switch
        {
            ForkWay.DeadEnd => T($"dead end {sides}", $"sin salida {sides}"),
            ForkWay.Explored => T($"explored {sides}", $"explorado {sides}"),
            _ => T($"goes on {sides}", $"sigue {sides}"),
        };

        /// <summary>A fork left behind with ways still going on: "Dejas un camino pendiente a la izquierda".</summary>
        public static string PendingLeft(int count, IReadOnlyList<string> sides) => count == 1
            ? T($"One way left pending {Both(sides)}", $"Dejas un camino pendiente {Both(sides)}")
            : T($"{count} ways left pending: {Both(sides)}", $"Dejas {count} caminos pendientes: {Both(sides)}");

        /// <summary>A dead end or a place with nothing new, before the nearest way left pending.</summary>
        public static string PendingNothingHere => T("Nothing more this way", "Por aquí no hay nada más");

        /// <summary>At an exit of the area, before the nearest way left pending.</summary>
        public static string PendingBeforeExit(int count) => count == 1
            ? T("Before leaving: one way pending", "Antes de salir: un camino pendiente")
            : T($"Before leaving: {count} ways pending", $"Antes de salir: {count} caminos pendientes");

        /// <summary>The scanner's name of a way left pending at a place: "Camino pendiente en Cruce 3".</summary>
        public static string PendingWayAt(string place) => T($"Way pending at {place}", $"Camino pendiente en {place}");

        /// <summary>"arriba y a la izquierda"; three or more as "arriba, abajo y a la izquierda".</summary>
        private static string Both(IReadOnlyList<string> sides) => sides.Count switch
        {
            0 => string.Empty,
            1 => sides[0],
            _ => T($"{string.Join(", ", sides.Take(sides.Count - 1))} and {sides[^1]}", $"{string.Join(", ", sides.Take(sides.Count - 1))} y {sides[^1]}"),
        };
    }
}
