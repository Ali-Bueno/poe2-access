namespace Poe2Accessibility
{
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>The forks of the map's shape: "Cruce: sigue arriba y a la izquierda, sin salida a la derecha".</summary>
    public static partial class Announcements
    {
        /// <summary>A fork's line: its kind, then each state with its sides, the way on first.</summary>
        public static string Fork(bool room, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> ways)
        {
            var parts = ways.Select(way => ForkWayLine(way.State, Both(way.Sides)));
            return $"{(room ? T("Room", "Sala") : T("Fork", "Cruce"))}: {string.Join(", ", parts)}";
        }

        private static string ForkWayLine(ForkWay state, string sides) => state switch
        {
            ForkWay.DeadEnd => T($"dead end {sides}", $"sin salida {sides}"),
            ForkWay.Explored => T($"explored {sides}", $"explorado {sides}"),
            _ => T($"goes on {sides}", $"sigue {sides}"),
        };

        /// <summary>"arriba y a la izquierda"; three or more as "arriba, abajo y a la izquierda".</summary>
        private static string Both(IReadOnlyList<string> sides) => sides.Count switch
        {
            0 => string.Empty,
            1 => sides[0],
            _ => T($"{string.Join(", ", sides.Take(sides.Count - 1))} and {sides[^1]}", $"{string.Join(", ", sides.Take(sides.Count - 1))} y {sides[^1]}"),
        };
    }
}
