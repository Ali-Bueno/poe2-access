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
    ///     Which fork or room of the map's shape (<see cref="MapTopology"/>) is news as the player walks into it: the first
    ///     time, and again once the player went away and came back ("ya estuviste aquí": the owner, 2026-11-16, lost in Ogham
    ///     Manor, could not tell a loop walked twice); never the way the player came by. Each place keeps the name it was
    ///     given on the first visit. Pure; docs/radar.md "Forks".
    /// </summary>
    public sealed class ForkTracker
    {
        /// <summary>A way within this angle (60°) of where the player came from is the way back.</summary>
        private const float BackCosine = 0.5f;

        private readonly float awayCells;
        private readonly Dictionary<int, (string Name, Vector2 Centre, bool Away)> told = new();
        private readonly Dictionary<string, int> names = new();
        private int? inside;

        /// <param name="awayCells">Coming back to a place is news once the player went this far from it.</param>
        public ForkTracker(float awayCells = 2 * ExplorationMap.SeenRadiusCells) => this.awayCells = awayCells;

        /// <summary>A new area, or a new map shape: every place is news again and names start over.</summary>
        public void Reset()
        {
            this.told.Clear();
            this.names.Clear();
            this.inside = null;
        }

        /// <summary>
        ///     The place just walked into, when it is news: its name and whether it was visited before; null otherwise.
        ///     <paramref name="name"/> gives a new place its name's stem ("Cruce", "Sala con chimenea"); a stem given twice is
        ///     numbered from its second place, and a plain kind is always numbered ("Cruce 3"). A place inside a named room takes
        ///     <paramref name="room"/>'s name as it is: every fork of the chapel is the chapel.
        /// </summary>
        public (int Node, string Name, bool Again)? Enter(int? node, Vector2 centre, Vector2 player, System.Func<(string Stem, bool Numbered)> name, string? room = null)
        {
            foreach (var (id, place) in this.told.ToList())
            {
                if (!place.Away && Vector2.Distance(player, place.Centre) > this.awayCells)
                {
                    this.told[id] = place with { Away = true };
                }
            }

            if (node == this.inside)
            {
                return null;
            }

            this.inside = node;
            if (node is not { } entered)
            {
                return null;
            }

            if (this.told.TryGetValue(entered, out var known))
            {
                if (!known.Away)
                {
                    return null;
                }

                this.told[entered] = known with { Away = false };
                return (entered, known.Name, true);
            }

            var named = room;
            if (named == null)
            {
                var (stem, numbered) = name();
                var count = this.names[stem] = this.names.GetValueOrDefault(stem) + 1;
                named = numbered || count > 1 ? $"{stem} {count}" : stem;
            }

            this.told[entered] = (named, centre, false);
            return (entered, named, false);
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
        ///     What to tell of a place's ways (side words, state): grouped by state, the way on first; empty when no way is new,
        ///     since ways all walked or dead ends tell nothing a player needs.
        /// </summary>
        public static IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> Tell(IEnumerable<(string Side, ForkWay State)> ways)
        {
            // Two ways on one side: the one that goes on is what the side means ("sigue arriba… explorado arriba" told nothing).
            var all = ways.ToList();
            var onward = all.Where(way => way.State == ForkWay.GoesOn).Select(way => way.Side).ToHashSet();
            var groups = all.Where(way => way.State == ForkWay.GoesOn || !onward.Contains(way.Side))
                .GroupBy(way => way.State).OrderBy(group => group.Key)
                .Select(group => (group.Key, (IReadOnlyList<string>)group.Select(way => way.Side).Distinct().ToList())).ToList();
            return groups.Any(group => group.Key == ForkWay.GoesOn) ? groups : new List<(ForkWay, IReadOnlyList<string>)>();
        }
    }
}
