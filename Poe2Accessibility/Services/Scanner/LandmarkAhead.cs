namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The map pieces a sighted player sees coming, said before the player reaches them: stairs, a door, a bridge, a narrow
    ///     passage, once each as it comes within a look (the owner, 2026-11-16: "una escalera hacia dirección tal";
    ///     docs/research/map-topology.md). Pure: the caller passes positions in grid cells and says the result with its side
    ///     and distance. What the player stands on is <see cref="PlaceTracker"/>'s, not news here.
    /// </summary>
    public sealed class LandmarkAhead
    {
        /// <summary>
        ///     The kinds that change where a player can go or name a part of the map worth heading for. Furniture (a window, a
        ///     fireplace, columns) and the many huts and graves are said only when walked onto.
        /// </summary>
        private static readonly HashSet<string> Leading = new()
        {
            "stairs", "stairs_up", "stairs_down", "door", "gate", "bridge", "entrance", "ramp", "hole", "walkway", "platform",
            "dock", "alley", "corridor", "courtyard", "ballroom", "church", "temple", "tower", "ruins", "graveyard", "waterfall",
            "chasm", "library", "arena", Narrow,
        };

        /// <summary>The kind of a narrow passage between two parts of the map (<see cref="MapTopology"/>).</summary>
        public const string Narrow = "narrow";

        private readonly float reachCells;
        private readonly float hereCells;
        private readonly float rearmCells;
        private readonly Dictionary<(Vector2 Grid, string Kind), bool> known = new();

        /// <param name="reachCells">A piece this near is in sight (the reveal radius, about half the screen's height).</param>
        /// <param name="hereCells">A piece first met this near is underfoot: said by <see cref="PlaceTracker"/> instead.</param>
        /// <param name="rearmCells">A piece said is said again only after the player went this far from it.</param>
        public LandmarkAhead(float reachCells, float hereCells, float rearmCells)
        {
            this.reachCells = reachCells;
            this.hereCells = hereCells;
            this.rearmCells = rearmCells;
        }

        /// <summary>Whether a kind is said ahead at all.</summary>
        public static bool Leads(string kind) => Leading.Contains(kind);

        /// <summary>A new area: nothing is known.</summary>
        public void Reset() => this.known.Clear();

        /// <summary>The pieces come into sight since the last update, nearest first, at most <paramref name="most"/>.</summary>
        public IReadOnlyList<(Vector2 Grid, string Kind)> Update(Vector2 player, IEnumerable<(Vector2 Grid, string Kind)> places, int most = 1)
        {
            var all = places.Where(place => Leads(place.Kind)).ToList();
            var come = new List<(Vector2 Grid, string Kind)>();
            foreach (var place in all)
            {
                var away = Vector2.Distance(player, place.Grid);
                if (this.known.TryGetValue(place, out var said))
                {
                    if (away > this.rearmCells)
                    {
                        this.known.Remove(place);
                    }
                    else if (!said && away > this.reachCells)
                    {
                        this.known.Remove(place); // left behind unsaid (underfoot when first met): news when it is seen again
                    }

                    continue;
                }

                if (away > this.reachCells)
                {
                    continue;
                }

                // Met underfoot (arriving on it, or a piece that wraps the player): the step's own line names it.
                this.known[place] = away > this.hereCells;
                if (away > this.hereCells)
                {
                    come.Add(place);
                }
            }

            // Another kind past the limit waits for the next update.
            var say = new List<(Vector2 Grid, string Kind)>();
            var kinds = new HashSet<string>();
            foreach (var place in come.OrderBy(place => Vector2.Distance(player, place.Grid)))
            {
                if (kinds.Contains(place.Kind))
                {
                    continue;
                }

                if (say.Count >= most)
                {
                    this.known.Remove(place);
                    continue;
                }

                kinds.Add(place.Kind);
                say.Add(place);
            }

            // The rest of a piece's kind around it is the same sight: a staircase drawn as two pieces is said once.
            foreach (var place in say)
            {
                foreach (var other in all.Where(other => other.Kind == place.Kind && Vector2.Distance(other.Grid, place.Grid) <= this.reachCells))
                {
                    this.known[other] = true;
                }
            }

            return say;
        }
    }
}
