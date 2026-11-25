namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     Which named room (<see cref="RoomMatcher"/>) is news as the player walks into it: the first time, and again once the
    ///     player went far from it and came back, as <see cref="ForkTracker"/> does for forks. Pure; docs/research/rooms.md.
    /// </summary>
    public sealed class RoomTracker
    {
        private readonly float awayCells;
        private readonly Dictionary<NamedRoom, bool> away = new();
        private NamedRoom? inside;

        /// <param name="awayCells">Coming back to a room is news once the player went this far from its box.</param>
        public RoomTracker(float awayCells = 2 * ExplorationMap.LookCells) => this.awayCells = awayCells;

        /// <summary>The room the player stands in, as of the last <see cref="Enter"/>.</summary>
        public NamedRoom? Inside => this.inside;

        /// <summary>A new area: every room is news again.</summary>
        public void Reset()
        {
            this.away.Clear();
            this.inside = null;
        }

        /// <summary>
        ///     One line for one entry: a fork walked into speaks (named after the room it stands in) and carries the room's
        ///     news; a fork in a room already named on the way in, with no way to tell, says nothing; else the room alone.
        ///     Null for nothing to say.
        /// </summary>
        public static (string Name, bool Again, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> Ways)? Line(
            string? room, bool? roomNews, (string Name, bool Again)? fork, IReadOnlyList<(ForkWay State, IReadOnlyList<string> Sides)> ways)
        {
            if (fork is { } place)
            {
                return place.Name == room && roomNews == null && ways.Count == 0 ? null : (place.Name, place.Again || roomNews == true, ways);
            }

            return room != null && roomNews is { } again ? (room, again, ways) : null;
        }

        /// <summary>The room just walked into, when it is news: false the first time, true when come back to; null otherwise.</summary>
        public bool? Enter(NamedRoom? room, Vector2 player)
        {
            foreach (var known in this.away.Where(pair => !pair.Value && pair.Key.DistanceTo(player) > this.awayCells).Select(pair => pair.Key).ToList())
            {
                this.away[known] = true;
            }

            if (room == this.inside)
            {
                return null;
            }

            this.inside = room;
            if (room is not { } entered)
            {
                return null;
            }

            if (this.away.TryGetValue(entered, out var wentAway))
            {
                if (!wentAway)
                {
                    return null;
                }

                this.away[entered] = false;
                return true;
            }

            this.away[entered] = false;
            return false;
        }
    }
}
