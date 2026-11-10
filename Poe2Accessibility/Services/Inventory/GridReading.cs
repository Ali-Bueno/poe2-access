namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One item of a grid as it is said in the grid's reading.</summary>
    /// <param name="Named">Name, rarity and stack ("Canto de condenación, raro"; the base's name until its tooltip was read).</param>
    /// <param name="Place">"column 3, row 1", or empty for a slot that has none.</param>
    /// <param name="OnBuildBase">The item is the base the build asks for in a slot it fits.</param>
    /// <param name="BuildFit">Per slot the build asks for: "Build, Gloves: another base".</param>
    public sealed record GridItem(string Named, int Column, int Row, string Place, bool OnBuildBase, IReadOnlyList<string> BuildFit);

    /// <summary>A whole grid said at once, without moving its cursor: the count, the build's pieces, then the rest in reading order. Pure.</summary>
    public static class GridReading
    {
        /// <summary>The items on a base the build asks for first, each group by row and then column.</summary>
        public static IReadOnlyList<GridItem> Ordered(IEnumerable<GridItem> items) =>
            items.OrderBy(item => item.OnBuildBase ? 0 : 1).ThenBy(item => item.Row).ThenBy(item => item.Column).ToList();

        /// <summary>"3 items. Rough Greaves, rare, column 3, row 1, Build, Boots: same base. …"; "Empty" for a grid without items.</summary>
        public static string Say(IEnumerable<GridItem> items)
        {
            var ordered = Ordered(items);
            return ordered.Count == 0
                ? Announcements.Capitalised(Announcements.Empty)
                : Announcements.Sentences(ordered.Select(Line).Prepend(Announcements.GridItems(ordered.Count)));
        }

        private static string Line(GridItem item) =>
            string.Join(", ", new[] { item.Named, item.Place }.Concat(item.BuildFit).Where(part => part.Length > 0));
    }
}
