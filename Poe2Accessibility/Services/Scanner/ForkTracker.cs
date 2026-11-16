namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>How a way out of a fork stands, as a sighted player reads it off the screen and the minimap.</summary>
    public enum ForkWay
    {
        /// <summary>Not walked yet and not seen to end: the way on, as far as a look tells.</summary>
        GoesOn,

        /// <summary>A dead end a look shows whole, with nothing in it the player is after.</summary>
        DeadEnd,

        /// <summary>Its ground already seen.</summary>
        Explored,
    }

    /// <summary>
    ///     Which fork of the map's shape (<see cref="MapTopology"/>) is news: each once an area, when the player walks into it,
    ///     and the way the player came by, which is not told (the owner, 2026-11-16: "me cuesta encontrar salidas").
    ///     Pure; docs/radar.md "Forks".
    /// </summary>
    public sealed class ForkTracker
    {
        /// <summary>A way within this angle (60°) of where the player came from is the way back.</summary>
        private const float BackCosine = 0.5f;

        private readonly HashSet<int> said = new();
        private int? inside;

        /// <summary>The fork just walked into, when it was never told this area; null otherwise.</summary>
        public int? Enter(int? node)
        {
            if (node == this.inside)
            {
                return null;
            }

            this.inside = node;
            return node is { } entered && this.said.Add(entered) ? entered : null;
        }

        /// <summary>A new area: every fork is news again.</summary>
        public void Reset()
        {
            this.said.Clear();
            this.inside = null;
        }

        /// <summary>The index of the way the player came by (the one nearest <paramref name="cameFrom"/>), or null when none is near it.</summary>
        public static int? WayBack(IReadOnlyList<Vector2> directions, Vector2 cameFrom)
        {
            if (cameFrom.LengthSquared() < 1e-6f)
            {
                return null;
            }

            var back = Vector2.Normalize(cameFrom);
            var best = directions.Select((direction, index) => (Index: index, Dot: Vector2.Dot(Vector2.Normalize(direction), back)))
                .OrderByDescending(way => way.Dot).FirstOrDefault();
            return directions.Count > 0 && best.Dot >= BackCosine ? best.Index : null;
        }

        /// <summary>
        ///     What to tell of a fork's ways (side words, state): grouped by state, the way on first; null when no way is new,
        ///     since a fork whose ways are all walked or dead ends tells nothing a player needs.
        /// </summary>
        public static IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)>? Tell(IEnumerable<(string Side, ForkWay State)> ways)
        {
            var groups = ways.GroupBy(way => way.State).OrderBy(group => group.Key)
                .Select(group => (group.Key, (IReadOnlyList<string>)group.Select(way => way.Side).Distinct().ToList())).ToList();
            return groups.Any(group => group.Key == ForkWay.GoesOn) ? groups : null;
        }
    }
}
