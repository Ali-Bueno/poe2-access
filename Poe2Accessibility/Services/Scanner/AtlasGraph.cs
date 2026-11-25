namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Pure walks over the Atlas's places: link distances on the catalog graph, and the nearest revealed node in
    ///     each stick direction on the grid (for the row-less nodes the catalog holds no links for). Grid places are
    ///     read with y up (the element's grid grows opposite the graph's y), converted here to screen sides.
    /// </summary>
    public static class AtlasGraph
    {
        /// <summary>Link distances from a node over the graph the lookup walks; unreached nodes are absent.</summary>
        public static Dictionary<string, int> Distances(string from, Func<string, IEnumerable<string>?> links) =>
            Distances(from, links, static _ => true);

        /// <summary>Link distances through the nodes <paramref name="allowed"/> admits (the revealed ones, for a route).</summary>
        public static Dictionary<string, int> Distances(string from, Func<string, IEnumerable<string>?> links, Func<string, bool> allowed)
        {
            var distances = new Dictionary<string, int>(StringComparer.Ordinal);
            if (!allowed(from))
            {
                return distances;
            }

            distances[from] = 0;
            var pending = new Queue<string>();
            pending.Enqueue(from);
            while (pending.Count > 0)
            {
                var here = pending.Dequeue();
                foreach (var other in links(here) ?? Enumerable.Empty<string>())
                {
                    if (allowed(other) && distances.TryAdd(other, distances[here] + 1))
                    {
                        pending.Enqueue(other);
                    }
                }
            }

            return distances;
        }

        /// <summary>
        ///     The shortest way in links from one node to another through admitted nodes, both ends included;
        ///     empty when no admitted way joins them.
        /// </summary>
        public static IReadOnlyList<string> Route(string from, string to, Func<string, IEnumerable<string>?> links, Func<string, bool> allowed)
        {
            if (!allowed(from) || !allowed(to))
            {
                return Array.Empty<string>();
            }

            var parent = new Dictionary<string, string>(StringComparer.Ordinal) { [from] = from };
            var pending = new Queue<string>();
            pending.Enqueue(from);
            while (pending.Count > 0)
            {
                var here = pending.Dequeue();
                if (here == to)
                {
                    break;
                }

                foreach (var other in links(here) ?? Enumerable.Empty<string>())
                {
                    if (allowed(other) && parent.TryAdd(other, here))
                    {
                        pending.Enqueue(other);
                    }
                }
            }

            if (!parent.ContainsKey(to))
            {
                return Array.Empty<string>();
            }

            var route = new List<string>();
            for (var at = to; ; at = parent[at])
            {
                route.Add(at);
                if (at == from)
                {
                    break;
                }
            }

            route.Reverse();
            return route;
        }

        /// <summary>A grid place as the stick's sides read it: y flipped to screen-down.</summary>
        public static Vector2 ScreenPlace(int gridX, int gridY) => new(gridX, -gridY);

        /// <summary>Grid steps between two places (king moves), how a hop count reads off the lattice.</summary>
        public static int GridSteps(int fromX, int fromY, int toX, int toY) =>
            Math.Max(Math.Abs(toX - fromX), Math.Abs(toY - fromY));

        /// <summary>
        ///     The nearest candidate in each of the stick's eight directions from here (screen places), nearest first
        ///     in its sector; the place itself never counts.
        /// </summary>
        public static IReadOnlyList<T> NearestPerDirection<T>(Vector2 here, IEnumerable<(Vector2 At, T Item)> candidates)
        {
            var best = new Dictionary<int, (float Distance, T Item)>();
            foreach (var (at, item) in candidates)
            {
                var offset = at - here;
                if (offset.Length() < 0.5f)
                {
                    continue;
                }

                var sector = PassiveStick.Nearest(offset);
                if (!best.TryGetValue(sector, out var held) || offset.Length() < held.Distance)
                {
                    best[sector] = (offset.Length(), item);
                }
            }

            return best.OrderBy(pair => pair.Value.Distance).Select(pair => pair.Value.Item).ToList();
        }
    }
}
