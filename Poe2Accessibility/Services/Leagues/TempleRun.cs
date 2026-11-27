namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     What is known of the temple being run, shared by the console's map (which reads the layout) and <see cref="TempleWatch"/>
    ///     (which finds the player on the ground): the rooms and paths placed, the cell the player stands in, the rooms done. Kept on
    ///     disk, so a restart of the mod loses neither the map read in the hub nor the rooms done in the same temple.
    /// </summary>
    public static class TempleRun
    {
        private static readonly HashSet<(int Row, int Column)> DoneRooms = new();
        private static SettingsStore<TempleMemory>? store;
        private static string instance = string.Empty;

        /// <summary>Gets the last map read: each built cell's title and whether it is a path; empty before any.</summary>
        public static IReadOnlyDictionary<(int Row, int Column), (string Title, bool Path)> Layout { get; private set; } =
            new Dictionary<(int Row, int Column), (string Title, bool Path)>();

        /// <summary>Gets or sets the map cell the player stands in, inside the temple; null elsewhere or when unknown.</summary>
        public static (int Row, int Column)? Here { get; set; }

        /// <summary>Gets the rooms completed in this temple, as seen.</summary>
        public static IReadOnlySet<(int Row, int Column)> Done => DoneRooms;

        /// <summary>Reads what an earlier run of the mod kept, and keeps what follows in the same file.</summary>
        public static void Attach(SettingsStore<TempleMemory> file)
        {
            store = file;
            var kept = file.Load(new TempleMemory());
            Layout = kept.Cells.ToDictionary(cell => (cell.Row, cell.Column), cell => (cell.Title, cell.Path));
            instance = kept.Instance;
            DoneRooms.Clear();
            DoneRooms.UnionWith(kept.Done.Where(cell => cell.Length == 2).Select(cell => (cell[0], cell[1])));
        }

        /// <summary>A map read: kept when it has more than the Vestibule (the window may be read before its tooltips load) and differs.</summary>
        public static void Publish(IReadOnlyDictionary<(int Row, int Column), (string Title, bool Path)> layout)
        {
            var same = layout.Count == Layout.Count && layout.All(each => Layout.TryGetValue(each.Key, out var known) && known == each.Value);
            if (layout.Count > 1 && !same)
            {
                Layout = new Dictionary<(int Row, int Column), (string Title, bool Path)>(layout);
                Save();
            }
        }

        /// <summary>A room completed.</summary>
        public static void Complete((int Row, int Column) cell)
        {
            if (DoneRooms.Add(cell))
            {
                Save();
            }
        }

        /// <summary>A temple entered: the same instance as the one kept keeps its rooms done, another starts with none.</summary>
        /// <param name="areaHash">The area instance's hash.</param>
        public static void Enter(string areaHash)
        {
            Here = null;
            if (areaHash != instance)
            {
                instance = areaHash;
                DoneRooms.Clear();
                Save();
            }
        }

        private static void Save() => store?.Save(new TempleMemory
        {
            Cells = Layout.Select(each => new TempleMemory.Cell { Row = each.Key.Row, Column = each.Key.Column, Title = each.Value.Title, Path = each.Value.Path })
                .OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList(),
            Instance = instance,
            Done = DoneRooms.OrderBy(cell => cell).Select(cell => new[] { cell.Row, cell.Column }).ToList(),
        });
    }

    /// <summary>The temple kept on disk (`config/temple.json`): the console's last map, and the rooms done in the instance last run.</summary>
    public sealed class TempleMemory
    {
        public List<Cell> Cells { get; set; } = new();

        /// <summary>Gets or sets the area hash of the temple the rooms were done in.</summary>
        public string Instance { get; set; } = string.Empty;

        public List<int[]> Done { get; set; } = new();

        public sealed class Cell
        {
            public int Row { get; set; }

            public int Column { get; set; }

            public string Title { get; set; } = string.Empty;

            public bool Path { get; set; }
        }
    }
}
