namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>What one reading of the passive tree's allocated list says about where the list is.</summary>
    public enum AllocatedVerdict
    {
        /// <summary>Nothing allocated, too few ids to tell, or a list between right and wrong: judged again later.</summary>
        Unjudged,

        /// <summary>The list where measured names the graph's nodes.</summary>
        Kept,

        /// <summary>The list where measured names under one in ten of them: it is another list.</summary>
        Broken,
    }

    /// <summary>
    ///     Judges the passive tree's allocated list by the graph ids it holds. A node a class sees in another's place
    ///     (<see cref="PassiveOverrides"/>) may stand in the list under an id the graph's records do not give, so a right list can
    ///     hold a few unknown ids; a list read at a wrong place holds almost none that are known (few u16 values are graph ids).
    ///     Between the two nothing is decided, and only a list every id of which is known can take the place. Pure.
    /// </summary>
    public static class AllocatedListCheck
    {
        /// <summary>A list this small proves nothing either way unless every id is known.</summary>
        public const int MinJudged = 3;

        /// <summary>One id in ten may be unknown in a list that is kept (and always one).</summary>
        private const int UnknownPerKnown = 10;

        /// <summary>The graph's ids are usable once most records gave one: a tree whose rows were not readable yet judges nothing.</summary>
        public static bool IdsUsable(int ids, int records) => records > 0 && ids * 2 >= records;

        /// <summary>
        ///     The ids a list may hold once the game's files give the graph's (<see cref="PassiveTreeCatalog.GraphIds"/>): every
        ///     node of the files, and what the rows in memory add, the ids of the nodes a class sees in others' place.
        /// </summary>
        public static IReadOnlySet<int> AllIds(IReadOnlySet<int> fromFiles, IReadOnlySet<int> fromRows)
        {
            var all = new HashSet<int>(fromFiles);
            all.UnionWith(fromRows);
            return all;
        }

        /// <summary>What the list where measured says; <paramref name="list"/> is null when no list reads there.</summary>
        public static AllocatedVerdict Judge(IReadOnlyCollection<int>? list, IReadOnlySet<int> known)
        {
            if (list is not { Count: > 0 })
            {
                return AllocatedVerdict.Unjudged;
            }

            var named = list.Count(known.Contains);
            var unknown = list.Count - named;
            if (unknown == 0 || (named >= MinJudged - 1 && unknown <= System.Math.Max(1, list.Count / UnknownPerKnown)))
            {
                return AllocatedVerdict.Kept;
            }

            return list.Count >= MinJudged && named * UnknownPerKnown < list.Count ? AllocatedVerdict.Broken : AllocatedVerdict.Unjudged;
        }

        /// <summary>A list that may take the measured one's place: several ids, each once, every one a node of the graph.</summary>
        public static bool Strong(IReadOnlyCollection<int>? list, IReadOnlySet<int> known) =>
            list is { Count: >= MinJudged } && list.All(known.Contains) && list.Distinct().Count() == list.Count;
    }
}
