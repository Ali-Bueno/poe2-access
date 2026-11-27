namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What is known of the temple being run, shared by the console's map (which reads the layout) and <see cref="TempleWatch"/>
    ///     (which finds the player on the ground): the rooms and paths placed, the cell the player stands in, the rooms done.
    /// </summary>
    public static class TempleRun
    {
        private static readonly HashSet<(int Row, int Column)> DoneRooms = new();

        /// <summary>Gets the last map read: each built cell's title and whether it is a path; empty before any.</summary>
        public static IReadOnlyDictionary<(int Row, int Column), (string Title, bool Path)> Layout { get; private set; } =
            new Dictionary<(int Row, int Column), (string Title, bool Path)>();

        /// <summary>Gets or sets the map cell the player stands in, inside the temple; null elsewhere or when unknown.</summary>
        public static (int Row, int Column)? Here { get; set; }

        /// <summary>Gets the rooms completed in this temple, as seen.</summary>
        public static IReadOnlySet<(int Row, int Column)> Done => DoneRooms;

        /// <summary>A map read: kept when it has more than the Vestibule (the window may be read before its tooltips load) and differs.</summary>
        public static void Publish(IReadOnlyDictionary<(int Row, int Column), (string Title, bool Path)> layout)
        {
            var same = layout.Count == Layout.Count && layout.All(each => Layout.TryGetValue(each.Key, out var known) && known == each.Value);
            if (layout.Count > 1 && !same)
            {
                Layout = new Dictionary<(int Row, int Column), (string Title, bool Path)>(layout);
            }
        }

        /// <summary>A room completed.</summary>
        public static void Complete((int Row, int Column) cell) => DoneRooms.Add(cell);

        /// <summary>A new temple: nothing done, nowhere yet.</summary>
        public static void Restart()
        {
            DoneRooms.Clear();
            Here = null;
        }
    }
}
