namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>Where a room stands against the player on the Trial's map.</summary>
    public enum TrialRoomStanding
    {
        /// <summary>The player can enter it now.</summary>
        Open,

        /// <summary>In the column the player stands in.</summary>
        Here,

        /// <summary>Left behind: the Trial never goes back.</summary>
        Behind,

        /// <summary>Further on, not reachable yet.</summary>
        Ahead,

        /// <summary>The room the player stands in: the one of its column the game draws its glow under.</summary>
        Current,

        /// <summary>Further on, with a way to it from the player's room: the game draws that way in gold.</summary>
        Reachable,

        /// <summary>Beside the rooms the player can enter: no way leads to it from the player's room, and the Trial never goes back.</summary>
        Beside,
    }

    /// <summary>
    ///     The Trial of the Sekhemas' map, a floor's rooms in columns from the entrance on the left to the boss on the right
    ///     (docs/research/endgame-ui.md). Which rooms link to which is drawn on a canvas and not read. Pure.
    /// </summary>
    public static class TrialMap
    {
        private const int MinColumns = 4;

        /// <summary>
        ///     Whether columns holding these many buttons are the map: one room at each end, the entrance and the boss, and a
        ///     choice of rooms between them.
        /// </summary>
        public static bool IsMap(IReadOnlyList<int> rooms) =>
            rooms.Count >= MinColumns && rooms[0] == 1 && rooms[^1] == 1 && rooms.All(count => count > 0) && rooms.Any(count => count > 1);

        /// <summary>
        ///     Whether links read from the game fit the map on screen: for each column but the last, each of its rooms' ways
        ///     into the next column, every one a room that column has. Anything else is not this map's links (a field a patch
        ///     moved, another floor), and none is said.
        /// </summary>
        /// <param name="rooms">The rooms of each column, as the window shows them.</param>
        /// <param name="links">Per column and room, the rooms of the next column it leads to.</param>
        public static bool Fits(IReadOnlyList<int> rooms, IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links)
        {
            if (links.Count != rooms.Count - 1)
            {
                return false;
            }

            for (var column = 0; column < links.Count; column++)
            {
                if (links[column].Count != rooms[column] || links[column].Any(ways => ways.Any(room => room < 0 || room >= rooms[column + 1])))
                {
                    return false;
                }
            }

            // Every room past the entrance is led to: the game draws no room without a way in.
            return Enumerable.Range(1, rooms.Count - 1).All(column =>
                Enumerable.Range(0, rooms[column]).All(room => links[column - 1].Any(ways => ways.Contains(room))));
        }

        /// <summary>The rooms of <paramref name="from"/>'s column that lead, by any way, to a room further on; empty when none does.</summary>
        /// <param name="links">Links that <see cref="Fits"/> the map.</param>
        /// <param name="column">The column of the rooms to start from.</param>
        /// <param name="from">The rooms to start from (the ones the player can enter).</param>
        /// <param name="targetColumn">The column of the room to reach, past <paramref name="column"/>.</param>
        /// <param name="target">The room to reach.</param>
        public static IReadOnlyList<int> WaysTo(IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, int column, IEnumerable<int> from, int targetColumn, int target) =>
            from.Where(start => Reaches(links, column, start, targetColumn, target)).ToList();

        private static bool Reaches(IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, int column, int room, int targetColumn, int target)
        {
            var here = new HashSet<int> { room };
            for (var at = column; at < targetColumn && here.Count > 0; at++)
            {
                here = here.SelectMany(each => links[at][each]).ToHashSet();
            }

            return column <= targetColumn && here.Contains(target);
        }

        /// <summary>
        ///     The rooms the player took, a column each from the entrance, out of the game's byte a column; empty when they are
        ///     not a walk along the links (not this floor's, or a field a patch moved).
        /// </summary>
        /// <param name="taken">A byte a column: the row taken there, <paramref name="none"/> from the first column not reached.</param>
        public static IReadOnlyList<int> Walk(IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, IReadOnlyList<byte> taken, byte none)
        {
            var walk = taken.TakeWhile(row => row != none).Select(row => (int)row).ToList();
            var valid = walk.Count > 0 && walk.Count <= links.Count + 1 && taken.Skip(walk.Count).All(row => row == none) && walk[0] < links[0].Count;
            for (var column = 0; valid && column + 1 < walk.Count; column++)
            {
                valid = links[column][walk[column]].Contains(walk[column + 1]);
            }

            return valid ? walk : Array.Empty<int>();
        }

        /// <summary>
        ///     How a room stands with the floor's links known: where the player is by the rooms taken, a room further on by
        ///     whether a way leads to it, with the rooms to enter it is reached through (when there is a choice) and the rooms
        ///     it leads to (when the next column is a choice).
        /// </summary>
        /// <param name="links">Links that <see cref="Fits"/> the map.</param>
        /// <param name="walk">The rooms taken (<see cref="Walk"/>), not empty.</param>
        /// <param name="open">The rooms of the next column that offer to enter, empty before the player's room is done.</param>
        public static (TrialRoomStanding Standing, IReadOnlyList<int> Through, IReadOnlyList<int> LeadsTo) View(
            IReadOnlyList<IReadOnlyList<IReadOnlyList<int>>> links, IReadOnlyList<int> walk, int column, int room, IReadOnlyList<int> open)
        {
            var own = walk.Count - 1;
            var none = (IReadOnlyList<int>)Array.Empty<int>();
            // The last column but one leads to the boss alone: no choice to tell.
            var leadsTo = column < links.Count && column + 1 < links.Count ? links[column][room] : none;
            if (column < own)
            {
                return (TrialRoomStanding.Behind, none, none);
            }

            if (column == own)
            {
                return room == walk[own] ? (TrialRoomStanding.Current, none, leadsTo) : (TrialRoomStanding.Here, none, none);
            }

            if (WaysTo(links, own, new[] { walk[own] }, column, room).Count == 0)
            {
                return (TrialRoomStanding.Beside, none, none);
            }

            if (column == own + 1 && open.Contains(room))
            {
                return (TrialRoomStanding.Open, none, leadsTo);
            }

            var through = open.Count > 1 && column > own + 1 ? WaysTo(links, own + 1, open, column, room) : none;
            return (TrialRoomStanding.Reachable, through, leadsTo);
        }

        /// <summary>A room's standing; the player is one column before the first that has a room to enter.</summary>
        /// <param name="column">The room's column.</param>
        /// <param name="open">The room offers to enter.</param>
        /// <param name="firstOpen">The first column with a room to enter, negative for none.</param>
        /// <param name="marked">The room carries the game's mark of where the player stands; believed only in the player's column.</param>
        public static TrialRoomStanding Standing(int column, bool open, int firstOpen, bool marked = false)
        {
            if (open)
            {
                return TrialRoomStanding.Open;
            }

            if (firstOpen < 0 || column > firstOpen)
            {
                return TrialRoomStanding.Ahead;
            }

            if (column == firstOpen)
            {
                // "Not reachable yet" was said of these, as if they would open later (user's map, 2026-11-12).
                return TrialRoomStanding.Beside;
            }

            if (column < firstOpen - 1)
            {
                return TrialRoomStanding.Behind;
            }

            return marked ? TrialRoomStanding.Current : TrialRoomStanding.Here;
        }
    }
}
