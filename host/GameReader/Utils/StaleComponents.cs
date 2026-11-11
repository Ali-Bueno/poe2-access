namespace GameReader.Utils
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Components that point back to another entity than the one listing them. A patch moves the back-pointer in every
    ///     entity; where some entities' components still point back, the layout holds and the others list components the
    ///     game has since rebuilt (objects of the Vastiri Outskirts after Act 2's opening scene, 2026-11-11: 4 of 6, taken
    ///     for a game update). Pure.
    /// </summary>
    public static class StaleComponents
    {
        /// <summary>
        ///     The owners to read again, given each sampled component's owner and whether it points back to it (null: not
        ///     read); empty when none points back (a moved field, for the layout search) or all do.
        /// </summary>
        public static IReadOnlyCollection<long> Owners(IEnumerable<(long Owner, bool? PointsBack)> samples)
        {
            var judged = samples.Where(sample => sample.PointsBack != null).ToList();
            return judged.Any(sample => sample.PointsBack == true)
                ? judged.Where(sample => sample.PointsBack == false).Select(sample => sample.Owner).ToHashSet()
                : new HashSet<long>();
        }
    }
}
