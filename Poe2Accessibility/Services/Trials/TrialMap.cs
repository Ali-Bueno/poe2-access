namespace Poe2Accessibility.Services
{
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

        /// <summary>A room's standing; the player is one column before the first that has a room to enter.</summary>
        /// <param name="column">The room's column.</param>
        /// <param name="open">The room offers to enter.</param>
        /// <param name="firstOpen">The first column with a room to enter, negative for none.</param>
        public static TrialRoomStanding Standing(int column, bool open, int firstOpen)
        {
            if (open)
            {
                return TrialRoomStanding.Open;
            }

            if (firstOpen < 0 || column >= firstOpen)
            {
                return TrialRoomStanding.Ahead;
            }

            return column == firstOpen - 1 ? TrialRoomStanding.Here : TrialRoomStanding.Behind;
        }
    }
}
