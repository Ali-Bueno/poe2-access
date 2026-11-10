namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Where the passive tree's state keeps its graph file and its allocated list, and the file its edge list: near where
    ///     measured first, then through the whole object, where a place counts only with two facts that agree and when no
    ///     other place holds another answer (<see cref="OffsetSearch.Only"/>). Pure: the readings are given.
    /// </summary>
    public static class PassiveGraphSearch
    {
        /// <summary>
        ///     The state's slot of the graph file: nearest <paramref name="measured"/> a pointer to a file with the graph's name
        ///     (<paramref name="named"/>); else, anywhere in the state, one that also leads to an edge list
        ///     (<paramref name="holdsEdges"/>). Two slots of the same file are one answer.
        /// </summary>
        public static int? File(Func<int, long> pointerAt, Func<long, bool> named, Func<long, bool> holdsEdges, int measured, int radius, int step, int limit) =>
            OffsetSearch.NearThenWide(
                measured, radius, step, at => pointerAt(at) is var file && file != 0 && named(file),
                step, limit, at => pointerAt(at) is var file && file != 0 && named(file) && holdsEdges(file), pointerAt);

        /// <summary>
        ///     The state's allocated list when the measured one is another list: first as far from where it was measured as
        ///     the graph file beside it moved (<paramref name="together"/>), then nearest, then anywhere; always a list of
        ///     several ids, each once, all nodes of the graph. <paramref name="vectorAt"/> tells two places of one list apart
        ///     from two lists.
        /// </summary>
        public static int? Allocated(Func<int, IReadOnlyCollection<int>?> listAt, Func<int, long> vectorAt, IReadOnlySet<int> known, int measured, int together, int radius, int step, int limit)
        {
            bool Strong(int at) => AllocatedListCheck.Strong(listAt(at), known);
            return together != measured && together >= 0 && Strong(together)
                ? together
                : OffsetSearch.NearThenWide(measured, radius, step, Strong, step, limit, Strong, vectorAt);
        }

        /// <summary>
        ///     The file's content pointer and the content's edge list, each nearest where measured; else anywhere in the file
        ///     and the content, when every pair that fits leads to the same list. <paramref name="edgesAt"/> gives the list's
        ///     first edge address under a pair when it reads as the graph's edge list (edges that join node records), 0 otherwise.
        /// </summary>
        public static (int Content, int List)? Edges(Func<int, int, long> edgesAt, (int Content, int List) measured, int radius, int step, (int Content, int List) limit)
        {
            foreach (var content in OffsetSearch.Outward(measured.Content, radius, step))
            {
                if (OffsetSearch.First(measured.List, radius, step, at => edgesAt(content, at) != 0) is { } list)
                {
                    return (content, list);
                }
            }

            var fits = new List<(int Content, int List, long Edges)>();
            for (var content = measured.Content % step; content < limit.Content; content += step)
            {
                for (var list = measured.List % step; list < limit.List; list += step)
                {
                    if (edgesAt(content, list) is var edges && edges != 0)
                    {
                        fits.Add((content, list, edges));
                    }
                }
            }

            if (fits.Count == 0 || fits.Any(fit => fit.Edges != fits[0].Edges))
            {
                return null;
            }

            var nearest = fits.OrderBy(fit => Math.Abs(fit.Content - measured.Content) + Math.Abs(fit.List - measured.List)).First();
            return (nearest.Content, nearest.List);
        }
    }
}
