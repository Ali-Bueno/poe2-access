namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     The walkable ground as forks, rooms and ways (<see cref="MapTopology"/>, docs/research/map-topology.md), on drawn
    ///     grids: corridors 16 cells wide (the study's Manor corridors run 24), rooms 200 across, a cave of overlapping discs.
    /// </summary>
    public static class MapTopologyTests
    {
        /// <summary>The study's dumps (docs/research/map-topology.md): Manor Ramparts first, then three caves; measured when present.</summary>
        private static readonly string[] StudyDumps = { "terrain-20261108-124237", "terrain-20261108-115707", "terrain-20261108-133607", "terrain-20261108-134540" };

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ATJunctionIsOneForkWithThreeWays), ATJunctionIsOneForkWithThreeWays),
            (nameof(AnAlcoveIsANookOffTheCorridor), AnAlcoveIsANookOffTheCorridor),
            (nameof(ASmallPillarMakesNoForks), ASmallPillarMakesNoForks),
            (nameof(ALoopAroundAWideObstacleIsNoDeadEnd), ALoopAroundAWideObstacleIsNoDeadEnd),
            (nameof(ARoomWithThreeCorridorsIsOnePlace), ARoomWithThreeCorridorsIsOnePlace),
            (nameof(ACaveTellsItsRoomsByItsOwnWidth), ACaveTellsItsRoomsByItsOwnWidth),
            (nameof(SeparatePiecesAreNotJudgedTogether), SeparatePiecesAreNotJudgedTogether),
            (nameof(AOneCellGapIsAWayOn), AOneCellGapIsAWayOn),
            (nameof(ATwoCellGapIsAWayOn), ATwoCellGapIsAWayOn),
            (nameof(ACrackIntoANookIsNoNarrowPassage), ACrackIntoANookIsNoNarrowPassage),
            (nameof(ACancelledBuildStops), ACancelledBuildStops),
            (nameof(TheManorDumpReadsAsTheStudyDrewIt), TheManorDumpReadsAsTheStudyDrewIt),
        };

        private static void ATJunctionIsOneForkWithThreeWays()
        {
            var ground = new Ground(1800, 480);
            ground.Open(20, 200, 1780, 216);
            ground.Open(892, 216, 908, 400);
            var map = ground.Build();
            var fork = OnlyFork(map, "a T");
            Near(0, Vector2.Distance(map.Nodes[fork].Centre, new Vector2(900, 208)), "the fork stands where the corridors meet", 16);
            Equal(0, map.Nodes.Count(node => node.Kind == TopologyNodeKind.Room), "corridors hold no room");
            Equal(fork, map.NodeNear(new Vector2(900, 208), 0), "the fork is found where it stands");
            var west = WayToward(map, fork, -Vector2.UnitX, "west");
            var east = WayToward(map, fork, Vector2.UnitX, "east");
            var stem = WayToward(map, fork, Vector2.UnitY, "south");
            Equal(true, stem.Nook, "the short stem is under one look");
            Equal(false, west.Nook, "the long arm is more than a look");
            var dead = map.DeadEndToward(new Vector2(900, 208), new Vector2(40, 208));
            Equal(true, dead is { AreaCells: > 12_000 and < 16_000, ExtentCells: > 800 }, $"no goal anywhere: the west arm ends ({dead})");
            map.SetGoals(new[] { new Vector2(40, 208) });
            Equal(null, map.DeadEndToward(new Vector2(900, 208), new Vector2(40, 208)), "the west arm holds the exit");
            Equal(false, map.IsDeadEndWay(fork, west, out _), "the way to the exit");
            Equal(true, map.IsDeadEndWay(fork, east, out var info) && info.AreaCells > 12_000, "the east arm still ends");
        }

        private static void AnAlcoveIsANookOffTheCorridor()
        {
            var ground = new Ground(1800, 480);
            ground.Open(20, 200, 1780, 216);
            ground.Open(880, 216, 920, 336);
            var map = ground.Build();
            var fork = OnlyFork(map, "a corridor with an alcove");
            var alcove = WayToward(map, fork, Vector2.UnitY, "into the alcove");
            Equal(true, alcove.Nook, "the alcove is a nook");
            map.SetGoals(new[] { new Vector2(1770, 208) });
            Equal(true, map.IsDeadEndWay(fork, alcove, out var info) && info.AreaCells is > 3_000 and < 8_000 && info.ExtentCells is > 100 and < 160, $"the alcove has no way on ({info})");
            Equal(true, map.DeadEndToward(new Vector2(40, 208), new Vector2(900, 320)) != null, "walking into the alcove");
            Equal(null, map.DeadEndToward(new Vector2(40, 208), new Vector2(1770, 208)), "walking to the exit");
            var west = map.DeadEndToward(new Vector2(900, 320), new Vector2(40, 208));
            Equal(true, west is { AreaCells: > 12_000 } && Vector2.Distance(west.Value.Entrance, new Vector2(900, 208)) < 24, $"out of the alcove and west: that arm has no way on, entered at the fork ({west})");
            Equal(null, map.DeadEndToward(new Vector2(900, 320), new Vector2(1770, 208)), "out of the alcove and east, to the exit");
            map.SetGoals(Array.Empty<Vector2>());
            var back = map.DeadEndToward(new Vector2(900, 320), new Vector2(1770, 208));
            Equal(true, back is { AreaCells: > 20_000, ExtentCells: > 1_700 }, $"from deep in the branch, all the rest with no goal ({back})");
        }

        private static void ASmallPillarMakesNoForks()
        {
            var ground = new Ground(1800, 480);
            ground.Open(20, 200, 1780, 216);
            ground.Open(800, 168, 1000, 232);
            ground.Close(880, 188, 920, 212);
            var map = ground.Build();
            Equal(0, Forks(map).Count, "a pillar of 960 cells is filled: no pair of false forks");
        }

        private static void ALoopAroundAWideObstacleIsNoDeadEnd()
        {
            var ground = new Ground(1800, 480);
            ground.Open(20, 200, 1780, 216);
            ground.Open(780, 140, 1020, 260);
            ground.Close(840, 170, 960, 230);
            var map = ground.Build();
            var forks = Forks(map);
            Equal(2, forks.Count, "the corridor forks around the obstacle and joins again");
            foreach (var fork in forks)
            {
                var loopWays = map.Ways(fork).Where(way => !map.Edges[way.Edge].Bridge).ToList();
                Equal(2, loopWays.Count, "two ways of each fork go round the obstacle");
                Equal(false, loopWays.Any(way => way.Nook || map.IsDeadEndWay(fork, way, out _)), "neither is a nook nor a dead end");
            }

            Equal(null, map.DeadEndToward(new Vector2(810, 200), new Vector2(900, 155)), "round the loop: no branch parts them");
            Equal(null, map.DeadEndToward(new Vector2(900, 245), new Vector2(900, 155)), "from below to above the obstacle");
        }

        private static void ARoomWithThreeCorridorsIsOnePlace()
        {
            var ground = new Ground(1600, 1000);
            ground.Open(700, 300, 900, 500);
            ground.Open(100, 392, 700, 408);
            ground.Open(900, 392, 1500, 408);
            ground.Open(792, 500, 808, 900);
            var map = ground.Build();
            var rooms = map.Nodes.Where(node => node.Kind == TopologyNodeKind.Room).ToList();
            Equal(1, rooms.Count, "one room");
            var room = rooms[0].Id;
            Equal(3, map.Ways(room).Count, "three corridors leave it");
            WayToward(map, room, -Vector2.UnitX, "west");
            WayToward(map, room, Vector2.UnitX, "east");
            WayToward(map, room, Vector2.UnitY, "south");
            Equal(0, Forks(map).Count(node => map.Nodes[node].Kind == TopologyNodeKind.Junction), "the corridors meet in the room, not at forks");
            Equal(room, map.NodeNear(new Vector2(800, 400), 0), "in the middle of the room");
            Equal(room, map.NodeNear(new Vector2(720, 320), 0), "in its corner");
        }

        private static void ACaveTellsItsRoomsByItsOwnWidth()
        {
            // A winding passage of overlapping discs (half-width about 25, a cave's), two chambers, a boulder in each.
            var ground = new Ground(1400, 1000);
            var random = new Random(7);
            for (var i = 0; i <= 40; i++)
            {
                ground.Disc(100 + (i * 30), 500 + (int)(150 * Math.Sin(i * 0.25)), 22 + random.Next(10));
            }

            ground.Disc(400, 590, 110);
            ground.Disc(1000, 641, 100);
            ground.Disc(400, 590, 20, open: false);
            ground.Disc(1000, 641, 35, open: false);
            var map = ground.Build();
            Equal(true, map.RoomDistanceCells > 24, $"a cave's rooms start farther from walls ({map.RoomDistanceCells:0.0}, half-width {map.HalfWidthCells:0.0})");
            Equal(true, map.Nodes.Any(node => node.Kind == TopologyNodeKind.Room), $"open ground holds rooms (half-width {map.HalfWidthCells:0.0}, {string.Join("; ", map.Nodes)})");
            Equal(1, map.Nodes.Select(node => node.Piece).Distinct().Count(), "one piece of ground");
            Equal(true, Forks(map).Count <= map.Nodes.Count(node => node.Kind == TopologyNodeKind.Room) + 3, $"few forks in a cave ({Forks(map).Count})");
        }

        private static void SeparatePiecesAreNotJudgedTogether()
        {
            var ground = new Ground(1200, 600);
            ground.Open(20, 100, 1180, 116);
            ground.Open(20, 400, 1180, 416);
            ground.Open(600, 250, 630, 270);
            var map = ground.Build();
            Equal(true, map.PieceAt(new Vector2(40, 108)) != map.PieceAt(new Vector2(40, 408)), "two corridors, two pieces");
            Equal(null, map.PieceAt(new Vector2(615, 260)), "a speck under half a look is left out");
            Equal(null, map.DeadEndToward(new Vector2(40, 108), new Vector2(40, 408)), "a teleport joins pieces the grid does not");
        }

        private static void AOneCellGapIsAWayOn() => AGapIsAWayOn(1);

        private static void ATwoCellGapIsAWayOn() => AGapIsAWayOn(2);

        /// <summary>A room entered from the west whose only other exit is a gap <paramref name="gap"/> cells wide, the exit past it.</summary>
        private static void AGapIsAWayOn(int gap)
        {
            Ground Room(bool open)
            {
                var ground = new Ground(1800, 600);
                ground.Open(20, 292, 600, 308);
                ground.Open(600, 200, 800, 400);
                ground.Open(808, 292, 1780, 308);
                if (open)
                {
                    ground.Open(800, 300, 808, 300 + gap);
                }

                return ground;
            }

            var exit = new Vector2(1770, 300);
            var closed = Room(open: false).Build();
            closed.SetGoals(new[] { exit });
            Equal(true, closed.DeadEndToward(new Vector2(40, 300), new Vector2(700, 300)) != null, "walled in, the room ends");
            var map = Room(open: true).Build();
            map.SetGoals(new[] { exit });
            Equal(null, map.DeadEndToward(new Vector2(40, 300), new Vector2(700, 300)), $"a gap {gap} wide is a way on to the exit");
            Equal(null, map.DeadEndToward(new Vector2(700, 300), new Vector2(1500, 300)), "through the gap");
            var narrow = map.Narrows.Where(passage => Vector2.Distance(passage.Cell, new Vector2(804, 300)) < 12).ToList();
            Equal(1, narrow.Count, $"the gap is a narrow passage ({string.Join("; ", map.Narrows)})");
            Equal(true, narrow[0].WidthCells <= gap + 1 && narrow[0].Bridge && MathF.Abs(narrow[0].Direction.X) > 0.8f, $"its width, the only way, east-west ({narrow[0]})");
        }

        private static void ACrackIntoANookIsNoNarrowPassage()
        {
            var ground = new Ground(1800, 480);
            ground.Open(20, 200, 1780, 216);
            ground.Open(899, 216, 901, 226);
            ground.Open(880, 226, 920, 266);
            var map = ground.Build();
            Equal(0, map.Narrows.Count, $"a crack into 1,600 cells is no passage ({string.Join("; ", map.Narrows)})");
        }

        private static void ACancelledBuildStops()
        {
            var ground = new Ground(400, 100);
            ground.Open(20, 40, 380, 56);
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            var stopped = false;
            try
            {
                MapTopology.Build(ground.Bytes, ground.BytesPerRow, ground.Height, cancel.Token);
            }
            catch (OperationCanceledException)
            {
                stopped = true;
            }

            Equal(true, stopped, "a new area cancels the last one's build");
        }

        /// <summary>Measures the study's dumps when they are on this machine (the development host's debug folder); silent otherwise.</summary>
        private static void TheManorDumpReadsAsTheStudyDrewIt()
        {
            foreach (var stamp in StudyDumps)
            {
                var dump = FindDump(stamp);
                if (dump == null)
                {
                    continue;
                }

                var header = File.ReadLines(Path.ChangeExtension(dump, ".txt")).First();
                var match = Regex.Match(header, @"cell \((\d+), (\d+)\), (\d+) bytes per row");
                var bytes = File.ReadAllBytes(dump);
                var bytesPerRow = int.Parse(match.Groups[3].Value);
                var player = new Vector2(int.Parse(match.Groups[1].Value) + 0.5f, int.Parse(match.Groups[2].Value) + 0.5f);
                MapTopology.Build(bytes, bytesPerRow, bytes.Length / bytesPerRow, CancellationToken.None);
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var clock = Stopwatch.StartNew();
                var map = MapTopology.Build(bytes, bytesPerRow, bytes.Length / bytesPerRow, CancellationToken.None);
                var took = clock.Elapsed.TotalSeconds;
                allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                var piece = map.PieceAt(player);
                var rooms = map.Nodes.Count(node => node.Piece == piece && node.Kind == TopologyNodeKind.Room);
                var forks = Forks(map).Where(node => map.Nodes[node].Piece == piece).ToList();
                var classes = new int[3];
                foreach (var (edge, id) in map.Edges.Select((edge, id) => (edge, id)).Where(pair => pair.edge.Bridge && map.Nodes[pair.edge.A].Piece == piece))
                {
                    map.IsDeadEndWay(edge.A, map.Ways(edge.A).First(way => way.Edge == id), out var beyondA);
                    map.IsDeadEndWay(edge.B, map.Ways(edge.B).First(way => way.Edge == id), out var beyondB);
                    var smaller = Math.Min(beyondA.AreaCells, beyondB.AreaCells);
                    classes[smaller < MapTopology.RevealDiscCells ? 0 : smaller < 3 * MapTopology.RevealDiscCells ? 1 : 2]++;
                }

                Console.WriteLine($"     {stamp}: {took:0.00} s, {bytes.Length / 1e6:0.0} MB grid, {allocated / 1e6:0} MB allocated; half-width {map.HalfWidthCells:0.0}, rooms from {map.RoomDistanceCells:0.0}; "
                    + $"player's piece: {rooms} rooms, {forks.Count} forks ({forks.Count(node => map.Nodes[node].Kind == TopologyNodeKind.Junction)} plain), "
                    + $"branches nook/small/large {classes[0]}/{classes[1]}/{classes[2]}, narrows {map.Narrows.Count(narrow => map.PieceAt(narrow.Cell) == piece)} of {map.Narrows.Count}");
                if (stamp == StudyDumps[0])
                {
                    Equal(true, took < 5, $"built off the frame thread in {took:0.00} s");
                    Equal(true, rooms > 0 && forks.Count > 0, "the Manor has rooms and forks");
                }
            }
        }

        private static string? FindDump(string stamp)
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
            {
                var file = Path.Join(folder.FullName, "host", "Poe2Access", "bin", "Release", "net10.0-windows", "win-x64", "Plugins", "Poe2Accessibility", "debug", stamp + ".walkable");
                if (File.Exists(file) && File.Exists(Path.ChangeExtension(file, ".txt")))
                {
                    return file;
                }
            }

            return null;
        }

        /// <summary>The places (forks and rooms) with three ways or more.</summary>
        private static List<int> Forks(MapTopology map) =>
            map.Nodes.Where(node => node.Kind != TopologyNodeKind.End && map.Ways(node.Id).Count >= 3).Select(node => node.Id).ToList();

        private static int OnlyFork(MapTopology map, string what)
        {
            var forks = Forks(map);
            Equal(1, forks.Count, $"{what} has one fork ({string.Join("; ", map.Nodes)})");
            return forks[0];
        }

        private static Way WayToward(MapTopology map, int node, Vector2 direction, string what)
        {
            var ways = map.Ways(node).Where(way => Vector2.Dot(way.Direction, direction) > 0.8f).ToList();
            Equal(1, ways.Count, $"one way {what} ({string.Join("; ", map.Ways(node))})");
            return ways[0];
        }

        /// <summary>A walkable grid as the game lays it out: two cells per byte, the even cell in the low nibble.</summary>
        private sealed class Ground
        {
            /// <summary>The value of a walkable cell (the dumps' commonest, 5); any non-zero nibble walks.</summary>
            private const byte Walkable = 5;

            public Ground(int width, int height)
            {
                (this.BytesPerRow, this.Height) = (width / 2, height);
                this.Bytes = new byte[this.BytesPerRow * height];
            }

            public byte[] Bytes { get; }

            public int BytesPerRow { get; }

            public int Height { get; }

            /// <summary>Opens the cells x0 &lt;= x &lt; x1, y0 &lt;= y &lt; y1.</summary>
            public void Open(int x0, int y0, int x1, int y1) => this.Fill(x0, y0, x1, y1, true);

            public void Close(int x0, int y0, int x1, int y1) => this.Fill(x0, y0, x1, y1, false);

            public void Disc(int cx, int cy, int radius, bool open = true)
            {
                for (var y = cy - radius; y <= cy + radius; y++)
                {
                    for (var x = cx - radius; x <= cx + radius; x++)
                    {
                        if (((x - cx) * (x - cx)) + ((y - cy) * (y - cy)) <= radius * radius)
                        {
                            this.Set(x, y, open);
                        }
                    }
                }
            }

            public MapTopology Build() => MapTopology.Build(this.Bytes, this.BytesPerRow, this.Height, CancellationToken.None);

            private void Fill(int x0, int y0, int x1, int y1, bool open)
            {
                for (var y = y0; y < y1; y++)
                {
                    for (var x = x0; x < x1; x++)
                    {
                        this.Set(x, y, open);
                    }
                }
            }

            private void Set(int x, int y, bool open)
            {
                if (x < 0 || y < 0 || x >= 2 * this.BytesPerRow || y >= this.Height)
                {
                    return;
                }

                var i = (y * this.BytesPerRow) + (x / 2);
                var shift = (x & 1) * 4;
                this.Bytes[i] = (byte)((this.Bytes[i] & ~(15 << shift)) | ((open ? Walkable : 0) << shift));
            }
        }
    }
}
