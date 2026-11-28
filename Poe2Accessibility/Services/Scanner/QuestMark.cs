namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     A place the shown world map marks with a quest's "Quest Location" (the exclamation mark over its pin), and the
    ///     nearest areas of the same map whose waypoint is active, as a sighted player sees the blue swirl beside the mark.
    /// </summary>
    /// <param name="Quests">The quests marking it, as their list names them.</param>
    /// <param name="Area">Its WorldAreas row, to know its pin under the cursor.</param>
    /// <param name="Steps">
    ///     Exits from the place to those waypoints: 0 when its own is active, <see cref="NoneNear"/> when the map shows none within
    ///     reach, <see cref="NotKnown"/> when its connections did not read or the map is a travel map, which shows no waypoints.
    /// </param>
    /// <param name="Side">Where the first of them lies on the map from the mark ("down"), empty when unknown.</param>
    public sealed record QuestMark(IReadOnlyList<string> Quests, string Place, MapAreaState State, IntPtr Area, IReadOnlyList<string> Waypoints, int Steps, string Side)
    {
        public const int NoneNear = -1;

        /// <summary>The compatibility report's name for the step a quest of the map's list is on (UiSemantics.QuestMarks).</summary>
        public const string StepsPart = "quest marks";

        public const int NotKnown = -2;
    }

    /// <summary>The nearest areas with an active waypoint, walking the map's connections. Pure.</summary>
    public static class QuestMarkRoutes
    {
        /// <summary>Farther than this a waypoint is no help in finding the mark (an act holds about ten areas end to end).</summary>
        public const int MaxSteps = 6;

        /// <summary>
        ///     The fewest exits from <paramref name="start"/> to an area <paramref name="isGoal"/> takes, and every such area at that
        ///     distance in the order found; (0, start) when the start is one, (<see cref="QuestMark.NoneNear"/>, none) when none is
        ///     within <see cref="MaxSteps"/>.
        /// </summary>
        public static (int Steps, IReadOnlyList<T> Goals) Nearest<T>(T start, Func<T, IEnumerable<T>> next, Func<T, bool> isGoal)
            where T : notnull
        {
            if (isGoal(start))
            {
                return (0, new[] { start });
            }

            var seen = new HashSet<T> { start };
            var level = new List<T> { start };
            for (var steps = 1; steps <= MaxSteps && level.Count > 0; steps++)
            {
                var following = new List<T>();
                foreach (var area in level)
                {
                    following.AddRange(next(area).Where(seen.Add));
                }

                var goals = following.Where(isGoal).ToList();
                if (goals.Count > 0)
                {
                    return (steps, goals);
                }

                level = following;
            }

            return (QuestMark.NoneNear, Array.Empty<T>());
        }
    }
}
