namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>Where a named tile stands among an area's tiles (AreaInstance.TgtTilesLocations), in grid cells. Pure.</summary>
    public static class TilePlaces
    {
        /// <summary>
        ///     The pieces of the tile <paramref name="key"/>: one entry's when the key names its place ("file.tdt x:1-y:2"),
        ///     else every entry of that file, in the table's order.
        /// </summary>
        public static List<Vector2> Spots(IReadOnlyDictionary<string, List<Vector2>> tiles, string key)
        {
            if (key.Contains(':', StringComparison.Ordinal))
            {
                return tiles.TryGetValue(key, out var exact) ? exact : new List<Vector2>();
            }

            var prefix = key + ".";
            var spots = new List<Vector2>();
            foreach (var tile in tiles)
            {
                if (tile.Key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    spots.AddRange(tile.Value);
                }
            }

            return spots;
        }

        /// <summary>
        ///     One centre per group of the tile's pieces within <paramref name="reach"/> of each other (the same tile on two
        ///     floors is two places, not their middle); none when the area has no such tile.
        /// </summary>
        public static Vector2[] Centres(IReadOnlyDictionary<string, List<Vector2>> tiles, string key, float reach)
        {
            var spots = Spots(tiles, key);
            if (spots.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            var groups = MinimapMarks.Groups(spots, reach);
            var centres = new Vector2[groups.Count];
            for (var group = 0; group < centres.Length; group++)
            {
                var members = groups[group];
                centres[group] = new Vector2(members.Average(member => spots[member].X), members.Average(member => spots[member].Y));
            }

            return centres;
        }
    }
}
