namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>What tells a dead end from the way on without naming the way: the size of the unseen ground, and a hint that comes with time.</summary>
    public static class ExplorationHintTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ADeadEndBesideUnseenGroundIsNoGroundThatGoesOn), ADeadEndBesideUnseenGroundIsNoGroundThatGoesOn),
            (nameof(UnseenGroundIsSizedAndTheRadarMarksWhatGoesOnFirst), UnseenGroundIsSizedAndTheRadarMarksWhatGoesOnFirst),
            (nameof(ALongWalkThroughAnAreaBringsTheHintEvenOverNewGround), ALongWalkThroughAnAreaBringsTheHintEvenOverNewGround),
            (nameof(ADoorNotOpenedIsAWayStillToTry), ADoorNotOpenedIsAWayStillToTry),
            (nameof(TheQuestStepsOwnExitIsTheTargetNotOneTheTextMentions), TheQuestStepsOwnExitIsTheTargetNotOneTheTextMentions),
        };

        private static void ADoorNotOpenedIsAWayStillToTry()
        {
            const float worldPerCell = 10f;
            var doors = new ClosedDoors();
            doors.Shut(7, new Vector3(4000, 0, 0));
            doors.Shut(8, new Vector3(500, 0, 0));
            doors.Shut(9, new Vector3(90, 0, 0));
            Equal("9 8 7", string.Join(" ", doors.Nearest(Vector2.Zero).Select(door => door.Id)), "nearest first");
            doors.Forget(8);
            Equal(2, doors.Count, "an opened door is no longer one to try");
            // Door 9 stands beside the player and the game no longer has it; door 7 is past the bubble, where nothing is awake.
            doors.ForgetGone(id => false, Vector2.Zero, worldPerCell);
            Equal("7", string.Join(" ", doors.Nearest(Vector2.Zero).Select(door => door.Id)), "a door gone from beside the player is dropped, a far one kept");
            doors.Shut(9, new Vector3(90, 0, 0));
            doors.ForgetGone(id => true, Vector2.Zero, worldPerCell);
            Equal(2, doors.Count, "a near door still awake stays");
            Equal(true, ExplorationMap.Rank(string.Empty) < ExplorationMap.Rank(ClosedDoors.Kind) && ExplorationMap.Rank(ClosedDoors.Kind) < ExplorationMap.Rank(ExplorationMap.SmallKind),
                "after the ground that goes on, before what soon ends");
        }

        private static void TheQuestStepsOwnExitIsTheTargetNotOneTheTextMentions()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Exits, "Keth", 0, 0, 0, QuestMatcher.ExitKind),
                new ScanEntry(0x80000001, ScanCategory.Exits, "Santuarios enterrados", 900, 0, 0),
                new ScanEntry(2, ScanCategory.Npcs, "Zarka", 5, 0, 0),
            };
            var texts = new[] { "Encuentra el Santuario en el corazón de Keth" };
            string Names(IReadOnlyCollection<string>? leadsTo) =>
                string.Join(" | ", QuestMatcher.Targets(entries, texts, "La ciudad perdida", "Keth", leadsTo: leadsTo).Select(entry => entry.Name));
            Equal("Keth", Names(null), "by the words alone, the exit back");
            Equal("Santuarios enterrados", Names(new[] { "Santuarios enterrados" }), "the step ends on entering the Buried Shrines: its exit, seen or not");
            Equal("Keth", Names(Array.Empty<string>()), "tables that say nothing change nothing");
            var named = QuestMatcher.Targets(entries, new[] { "Habla con Zarka en Keth" }, "La ciudad perdida", "Keth", leadsTo: new[] { "Santuarios enterrados" });
            Equal("Zarka | Santuarios enterrados", string.Join(" | ", named.Select(entry => entry.Name)), "whom the text names stays a target");
        }

        private static void ADeadEndBesideUnseenGroundIsNoGroundThatGoesOn()
        {
            const int width = 600, height = 300;
            var player = new Vector2(60, 120);
            // A hall; a corridor east to a room; and a corridor south then east that ends ten cells short of that room's wall:
            // block by block the two touch, cell by cell a wall stands between them.
            var grid = MapRadarTests.Carved(width, height, (20, 100, 100, 140), (100, 116, 300, 124), (300, 20, 560, 280), (60, 140, 68, 230), (60, 222, 290, 230));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var places = map.Frontier(grid, width / 2, player, 8);
            Equal(2, places.Count, "the corridor to the room and the dead end");
            Equal(UnseenSize.Medium, places[0].Size, "the room is ground that goes on");
            Equal(true, places[0].Cell.X > 100 && places[0].Cell.Y < 140, "reached by the east corridor");
            Equal(UnseenSize.Pocket, places[1].Size, "the corridor that ends at the room's wall is a nook, whatever lies behind the wall");
            Equal(true, places[1].Cell.Y > 140, "south of the hall");
        }

        private static void UnseenGroundIsSizedAndTheRadarMarksWhatGoesOnFirst()
        {
            const int width = 900, height = 400;
            var player = new Vector2(60, 200);
            // A hall; east, a corridor to one room; north, a corridor to a hall as large as a good part of an area.
            var grid = MapRadarTests.Carved(width, height, (20, 180, 100, 220), (100, 196, 200, 204), (200, 170, 300, 290), (56, 52, 64, 180), (56, 52, 120, 60), (120, 0, 880, 150));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var places = map.Frontier(grid, width / 2, player, 8);
            Equal(2, places.Count, "north and east");
            Equal(UnseenSize.Large, places[0].Size, "the great hall first, though the room is nearer");
            Equal(true, places[0].Cell.Y < 180, "north of the hall");
            Equal(UnseenSize.Small, places[1].Size, "one room soon ends");
            Equal(UnseenSize.Large, map.UnseenAlong(player, new Vector2(0, -1), ExplorationMap.MarkReachCells), "the radar's two pips toward the great hall");
            Equal(UnseenSize.Small, map.UnseenAlong(player, new Vector2(1, 0), ExplorationMap.MarkReachCells), "one pip toward the room that soon ends");
            Equal(null, map.UnseenAlong(player, new Vector2(-1, 0), ExplorationMap.MarkReachCells), "none toward the hall's own wall");
            var onePip = RadarSynth.WithUnexploredTail(new float[100], RadarSynth.Direction.Left, 44100, pips: 1).Length;
            var twoPips = RadarSynth.WithUnexploredTail(new float[100], RadarSynth.Direction.Left, 44100).Length;
            Equal(true, onePip > 100 && onePip < twoPips, "the one-pip mark is the cue and one pip, shorter than the two");
            Equal(0, ExplorationMap.Rank(string.Empty), "ground that goes on stands first among the scanner's entries");
            Equal(true, ExplorationMap.Rank(ExplorationMap.SmallKind) < ExplorationMap.Rank(ExplorationMap.PocketKind), "then what soon ends, then nooks");
        }

        private static void ALongWalkThroughAnAreaBringsTheHintEvenOverNewGround()
        {
            const int never = int.MaxValue;
            const int soundAfter = 5000, spokenAfter = 10000;
            var watch = new LostWatch();
            var seen = 0;
            LostNews Polls(long from, long to, bool moving, int spoken = spokenAfter)
            {
                var most = LostNews.None;
                for (var now = from; now <= to; now += 250)
                {
                    // New ground at every poll, as down one dead end after another: never "lost" by the first clock.
                    Equal(false, watch.Update(now, seen++, moving, never, moving), "new ground is never walking without new ground");
                    var news = watch.Explored(soundAfter, spoken);
                    most = news > most ? news : most;
                }

                return most;
            }

            Equal(LostNews.None, Polls(1000, 4000, true), "not from the first minutes");
            Equal(LostNews.Sound, Polls(4250, 6000, true), "the sound once the area has been walked that long");
            Equal(LostNews.None, Polls(6250, 9000, true), "not again at once");
            Equal(LostNews.Spoken, Polls(9250, 11000, true), "the words after longer");
            Equal(LostNews.None, Polls(11250, 400000, false), "standing still is not exploring");
            Equal(LostNews.Spoken, Polls(400250, 400250 + LostWatch.AreaRepeatMs, true), "and again after as much moving");
            watch.Reset();
            Equal(LostNews.None, Polls(600000, 603000, true), "a new area starts again");
            Equal(LostNews.Sound, Polls(603250, 620000, true, spoken: 0), "0 for the words: the sound alone, whatever the time");

            // Walking where everything has been seen: soon, and again and again, until new ground is found.
            var walk = new LostWatch();
            bool Over(long from, long to, int blocks, bool walking)
            {
                var due = false;
                for (var now = from; now <= to; now += 250)
                {
                    walk.Update(now, blocks, walking, never);
                    due |= walk.OverSeenGround(3000);
                }

                return due;
            }

            Equal(false, Over(1000, 3000, 5, true), "not at once");
            Equal(true, Over(3250, 4500, 5, true), "after that long over seen ground");
            Equal(true, Over(4750, 8000, 5, true), "and again while nothing new is found");
            Equal(false, Over(8250, 12000, 5, false), "standing still is no walk");
            Equal(false, Over(12250, 14000, 6, true) | Over(14250, 15500, 7, true), "new ground starts the count again");
            Equal(false, walk.OverSeenGround(0), "0: never");
        }
    }
}
