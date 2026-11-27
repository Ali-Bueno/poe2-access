namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Atziri's Temple on the ground: slot lines, the console's map laid on the slots, the next room.</summary>
    public static class TempleSlotsTests
    {
        private const int S = TempleSlots.SlotCells;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(SlotLinesRunThroughTheWalls), SlotLinesRunThroughTheWalls),
            (nameof(TheMapIsFoundTurnedAndShiftedOnTheGround), TheMapIsFoundTurnedAndShiftedOnTheGround),
            (nameof(AMirroredMapIsNotGuessed), AMirroredMapIsNotGuessed),
            (nameof(TheNextRoomIsTheNearestNotDoneAlongThePaths), TheNextRoomIsTheNearestNotDoneAlongThePaths),
            (nameof(TheMapAndTheRoomsDoneOutliveARestartInTheSameTemple), TheMapAndTheRoomsDoneOutliveARestartInTheSameTemple),
        };

        private static void TheMapAndTheRoomsDoneOutliveARestartInTheSameTemple()
        {
            var folder = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests");
            var file = System.IO.Path.Join(folder, "temple-" + Guid.NewGuid().ToString("N") + ".json");
            var log = new PluginLog(folder, false);
            try
            {
                TempleRun.Attach(new SettingsStore<TempleMemory>(file, log));
                TempleRun.Publish(Layout().ToDictionary(each => each.Key, each => (each.Value ? "Camino" : "Sala", each.Value)));
                TempleRun.Enter("temple A");
                TempleRun.Complete((1, 5));

                TempleRun.Attach(new SettingsStore<TempleMemory>(file, log)); // the mod restarted
                Equal(Layout().Count, TempleRun.Layout.Count, "the map read before the restart");
                Equal(("Sala", false), TempleRun.Layout[(1, 5)], "a room's title and kind");
                TempleRun.Enter("temple A");
                Equal(true, TempleRun.Done.Contains((1, 5)), "the room done before, back in the same temple");
                TempleRun.Enter("temple B");
                Equal(0, TempleRun.Done.Count, "another temple starts with none done");
                Equal(Layout().Count, TempleRun.Layout.Count, "the map stays for the next temple");
            }
            finally
            {
                System.IO.File.Delete(file);
                TempleRun.Enter(string.Empty);
            }
        }

        /// <summary>A temple of seven rooms off one spine of paths from the Vestibule to Atziri (no two ways fit it).</summary>
        private static Dictionary<(int Row, int Column), bool> Layout()
        {
            var cells = new Dictionary<(int Row, int Column), bool>();
            for (var row = 0; row <= 8; row++)
            {
                cells[(row, 4)] = true;
            }

            cells[(3, 5)] = true;
            cells[(3, 6)] = true;
            foreach (var room in new[] { (1, 5), (2, 3), (3, 7), (4, 6), (5, 3), (7, 5), (8, 3), TempleGrid.AtziriChamber })
            {
                cells[room] = false;
            }

            return cells;
        }

        /// <summary>The ground as the game builds it: the map turned (row toward −Y, column toward +X) from slot (2, 12).</summary>
        private static (TempleSlots.Ground Ground, (int X, int Y) Spawn) Ground(Dictionary<(int Row, int Column), bool> layout, int boundaryX = 138, int boundaryY = 115)
        {
            (int X, int Y) SlotOf(int row, int column) => (2 + column, 12 - row);
            var slots = layout.ToDictionary(each => SlotOf(each.Key.Row, each.Key.Column), each => each.Value);
            slots[SlotOf(-1, 4)] = true; // the entrance's hall, before the Vestibule
            slots[SlotOf(10, 4)] = true; // Atziri's way
            slots[SlotOf(11, 4)] = true;
            var (width, height) = (boundaryX + (11 * S) + 50, boundaryY + (13 * S) + 50);
            var row = (width + 1) / 2;
            var grid = new byte[row * height];
            const int wall = 6, door = 12, corridor = 60, mid = S / 2;
            foreach (var ((slotX, slotY), path) in slots)
            {
                bool Open(int dx, int dy) => slots.ContainsKey((slotX + dx, slotY + dy));
                var (x0, y0) = (boundaryX + ((slotX - 1) * S), boundaryY + ((slotY - 1) * S));
                for (var y = 0; y < S; y++)
                {
                    for (var x = 0; x < S; x++)
                    {
                        bool walk;
                        var (alongY, alongX) = (Math.Abs(x - mid) < corridor / 2, Math.Abs(y - mid) < corridor / 2);
                        if (path)
                        {
                            walk = (alongY && alongX) || (alongY && (y < mid ? Open(0, -1) : Open(0, 1))) || (alongX && (x < mid ? Open(-1, 0) : Open(1, 0)));
                        }
                        else
                        {
                            var (left, right, top, bottom) = (x < wall, x >= S - wall, y < wall, y >= S - wall);
                            var (doorY, doorX) = (Math.Abs(y - mid) < door / 2, Math.Abs(x - mid) < door / 2);
                            walk = !(left || right || top || bottom) ||
                                   (doorY && ((left && Open(-1, 0)) || (right && Open(1, 0)))) ||
                                   (doorX && ((top && Open(0, -1)) || (bottom && Open(0, 1))));
                        }

                        var (gx, gy) = (x0 + x, y0 + y);
                        if (walk && gx >= 0 && gy >= 0 && gx < width && gy < height)
                        {
                            grid[(gy * row) + (gx / 2)] |= (byte)(1 << ((gx & 1) * 4));
                        }
                    }
                }
            }

            return (TempleSlots.Measure(grid, row), SlotOf(-1, 4));
        }

        private static void SlotLinesRunThroughTheWalls()
        {
            var (ground, _) = Ground(Layout());
            Equal(138, ground.BoundaryX, "the slot lines across X");
            Equal(115, ground.BoundaryY, "the slot lines across Y");
            Equal(true, ground.Holds(2 + 5, 12 - 1), "a room's slot holds ground");
            Equal(false, ground.Holds(2 + 0, 12 - 0), "an empty cell's slot is solid");
        }

        private static void TheMapIsFoundTurnedAndShiftedOnTheGround()
        {
            var layout = Layout();
            var (ground, spawn) = Ground(layout);
            var frame = TempleSlots.Match(ground, layout.Keys.ToHashSet(), spawn, out var wrong);
            Expect(true, frame != null, "the map fits one way");
            Equal(0, wrong, "every cell fits");
            var centre = frame!.CentreOf((1, 5));
            Near(138 + ((7 - 1) * S) + (S / 2f), centre.X, "the room's middle across X");
            Near(115 + ((11 - 1) * S) + (S / 2f), centre.Y, "the room's middle across Y");
            Equal((1, 5), frame.CellOf(centre.X + 100, centre.Y - 100), "a point in the room's slot");
            Equal((7, 5), frame.CellOf(frame.CentreOf((7, 5)).X, frame.CentreOf((7, 5)).Y), "another room");
            Equal((-1, 4), frame.CellOf(frame.CentreOf((-1, 4)).X, frame.CentreOf((-1, 4)).Y), "the hall before the Vestibule lies off the map");
        }

        private static void AMirroredMapIsNotGuessed()
        {
            var layout = new Dictionary<(int Row, int Column), bool>();
            for (var row = 0; row <= 8; row++)
            {
                layout[(row, 4)] = true;
            }

            layout[(2, 3)] = false;
            layout[(2, 5)] = false;
            layout[TempleGrid.AtziriChamber] = false;
            var (ground, spawn) = Ground(layout);
            Expect(true, TempleSlots.Match(ground, layout.Keys.ToHashSet(), spawn, out _) == null, "two ways fit: none is taken");
        }

        private static void TheNextRoomIsTheNearestNotDoneAlongThePaths()
        {
            var layout = Layout();
            var done = new HashSet<(int Row, int Column)> { (1, 5) };
            Equal(((int, int)?)(2, 3), TempleGrid.NextRoom((1, 5), layout, done), "from the first room, the one off the next path");
            done.Add((4, 6));
            Equal(((int, int)?)(3, 7), TempleGrid.NextRoom((4, 6), layout, done), "two cells down the side way before five along the spine");
            foreach (var room in layout.Where(each => !each.Value).Select(each => each.Key))
            {
                done.Add(room);
            }

            Equal(((int, int)?)TempleGrid.AtziriChamber, TempleGrid.NextRoom((8, 3), layout, done), "every room done: Atziri's Chamber");
        }
    }
}
