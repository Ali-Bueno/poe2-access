namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>What the world map's picture of an area says of it.</summary>
    public enum MapAreaState
    {
        /// <summary>Nothing to say: no such pin, or its bytes do not read as flags.</summary>
        None,
        Unknown,
        Visited,
        Waypoint,

        /// <summary>Entered, its waypoint not told (a travel map).</summary>
        Entered,
    }

    /// <summary>What the map's pictures say of an area: its own name where the marker has another's, entered or not, what is left within it (as it is said) and what is done.</summary>
    public sealed record MapAreaPictures(string RealName, MapAreaState State, IReadOnlyList<(string Name, string Line)> Pending, IReadOnlyList<string> Done)
    {
        public static readonly MapAreaPictures Empty = new(string.Empty, MapAreaState.None, Array.Empty<(string, string)>(), Array.Empty<string>());
    }

    /// <summary>The state of an area from its pin's bytes.</summary>
    public static class MapAreaStates
    {
        /// <summary>The bytes must be flags of a shown pin that agree: what is not known was never entered, and a hidden pin says nothing.</summary>
        public static MapAreaState Of(byte known, byte visited, byte waypoint) => (known, visited, waypoint) switch
        {
            (1, 0, 0 or 1) => MapAreaState.Unknown,
            (1, 1, 0) => MapAreaState.Visited,
            (1, 1, 1) => MapAreaState.Waypoint,
            _ => MapAreaState.None,
        };
    }
}
