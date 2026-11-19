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
            (nameof(AChaosRoundIsToldByItsPieces), AChaosRoundIsToldByItsPieces),
            (nameof(EachLeagueRingsWithItsOwnSound), EachLeagueRingsWithItsOwnSound),
            (nameof(GoingRoundInCirclesCountsSinceTheLastNewGround), GoingRoundInCirclesCountsSinceTheLastNewGround),
            (nameof(AFarCharacterIsDrawnOnlyInTown), AFarCharacterIsDrawnOnlyInTown),
        };

        private static void AFarCharacterIsDrawnOnlyInTown()
        {
            Equal(false, MinimapPointer.Drawn(false, true, 2000, 250), "the Water Goddess 2,000 cells off in a boss room: not drawn");
            Equal(true, MinimapPointer.Drawn(false, true, 200, 250), "within the NPC icon's reach the minimap shows her");
            Equal(true, MinimapPointer.Drawn(true, true, 2000, 0), "in a town every character the game knows of");
            Equal(true, MinimapPointer.Drawn(false, false, 2000, 0), "an exit or a waypoint keeps its icon wherever the game has it");
        }

        private static void GoingRoundInCirclesCountsSinceTheLastNewGround()
        {
            const int never = int.MaxValue;
            var watch = new LostWatch();
            watch.Update(0, 5, true, never);
            for (var now = 250; now <= 30000; now += 250)
            {
                watch.Update(now, 5, true, never);
            }

            Equal(true, watch.SinceNewGroundMs >= 30000, "thirty seconds over seen ground");
            Equal(true, watch.OverSeenGround(20000), "the hint toward unexplored ground comes...");
            Equal(true, watch.SinceNewGroundMs >= 30000, "...and the circling goes on counting");
            watch.Update(30250, 6, true, never);
            Equal(0L, watch.SinceNewGroundMs, "new ground ends it");
        }

        private static void EachLeagueRingsWithItsOwnSound()
        {
            Equal(LeaguePieces.ExpeditionSound, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionDetonator@12"), "the detonator, by its piece");
            Equal(string.Empty, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker"), "a marker is listed, not sounded: there are dozens");
            Equal(LeaguePieces.DeliriumSound, LeaguePieces.SoundOf("Metadata/Terrain/Gallows/Leagues/Delirium/Objects/DeliriumInitiator"), "the mirror");
            Equal(LeaguePieces.BreachSound, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Brequel/BrequelInitiator"), "the breach");
            Equal(LeaguePieces.AbyssSound, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Abyss/AbyssNodeBase"), "the pit");
            Equal(LeaguePieces.AbyssSound, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Abyss/SomethingNew"), "an object the table does not hold, by its league's folder");
            Equal(string.Empty, LeaguePieces.SoundOf("Metadata/Terrain/Leagues/Ritual/RitualRuneInteractable"), "the Ritual altar keeps its own");
            Equal(string.Empty, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Stash"), "anything else has none");
            Equal(LeaguePieces.Sounds.Length, new HashSet<string>(LeaguePieces.Sounds).Count, "each league sound its own key");
        }

        private static void AChaosRoundIsToldByItsPieces()
        {
            const string core = ChaosTrial.Folder + "SoulCoreEncounter/SoulCorePickup";
            Equal(ChaosPiece.SoulCore, ChaosTrial.PieceOf(core + "@38"), "a piece by its file, whatever its instance tail");
            Equal(null, ChaosTrial.PieceOf(ChaosTrial.Folder + "LiftButton_Platform"), "the lift's pressure plate is no lever");
            Equal((true, false), ChaosTrial.Asks(ChaosPiece.SoulCore, holdingCore: false), "a core rings while none is held");
            Equal((false, false), ChaosTrial.Asks(ChaosPiece.SoulCore, holdingCore: true), "and not while one is: a second cannot be taken");
            Equal((true, true), ChaosTrial.Asks(ChaosPiece.StoneAltar, holdingCore: true), "the altar rings against the core's damage, as a clock's goal");
            Equal((false, false), ChaosTrial.Asks(ChaosPiece.StoneAltar, holdingCore: false), "an altar with nothing to put in it is silent");
            Equal((true, false), ChaosTrial.Asks(ChaosPiece.Selector, false, new[] { (ChaosTrial.LeverEnabledState, 1L), ("rewardstaken", 0L) }), "the modifier switch rings before the round");
            Equal((false, false), ChaosTrial.Asks(ChaosPiece.Selector, false, new[] { (ChaosTrial.LeverEnabledState, 0L), ("rewardstaken", 0L) }), "and not once a modifier is chosen");
            Equal((false, false), ChaosTrial.Asks(ChaosPiece.LiftLever, false, new[] { (ChaosTrial.LeverEnabledState, 0L) }), "a lever that cannot be pulled is silent");
            Equal(true, ChaosTrial.StillAsks(ChaosPiece.Lift, new[] { (ChaosTrial.LiftActivatedState, 1L) }), "a lift that can be ridden is listed and rings");
            Equal(false, ChaosTrial.StillAsks(ChaosPiece.Lift, new[] { (ChaosTrial.LiftActivatedState, 0L), ("minimap_icon", 0L) }), "a shut one is not");
            var lifts = new LiftNews();
            Equal(false, lifts.Activated(5, true), "a lift ridable when first seen is no news");
            Equal(false, lifts.Activated(6, false), "nor one shut");
            Equal(true, lifts.Activated(6, true), "one turning ridable is");
            Equal(false, lifts.Activated(6, true), "once");
            Equal(true, lifts.Activated(5, false) || lifts.Activated(5, true), "and again after it was ridden and shut");
            Equal(null, lifts.Moved(7, 2), "a lift first seen up has not moved");
            Equal(1, lifts.Moved(7, 1), "setting off down");
            Equal(null, lifts.Moved(7, 1), "said once a trip");
            Equal(2, lifts.Moved(7, 2), "and up again");
            Equal(null, lifts.Moved(7, null), "an unread transition is nothing");
            var lever = new System.Numerics.Vector3(0, 0, 100);
            Equal(false, ChaosTrial.LiftAway(lever, new[] { new System.Numerics.Vector3(150, 0, 120) }), "the lift by the lever on its floor: pulling does nothing");
            Equal(true, ChaosTrial.LiftAway(lever, new[] { new System.Numerics.Vector3(150, 0, -480) }), "the lift on the other floor: the lever calls it");
            Equal(null, ChaosTrial.LiftAway(lever, new[] { new System.Numerics.Vector3(2000, 0, 100) }), "no lift near: as any lever");
            var deck = new LiftNews();
            Equal(false, deck.Boarded(9, false, 0), "beside the lift");
            Equal(true, deck.Boarded(9, true, 500), "stepping on");
            Equal(false, deck.Boarded(9, true, 900), "said once");
            Equal(false, deck.Boarded(9, false, 1000), "a step off its edge");
            Equal(false, deck.Boarded(9, true, 1500), "and back on at once is not news");
            Equal(false, deck.Boarded(9, false, 2000), "off");
            Equal(true, deck.Boarded(9, true, 4500), "on again after a while");
            var trip = new LiftNews();
            trip.Moved(8, 2, 0);
            Equal(1, trip.Moved(8, 1, 1000), "sets off down");
            Equal(null, trip.Arrived(8, 2500), "on its way");
            Equal(1, trip.Arrived(8, 3000), "down once the trip is over");
            Equal(null, trip.Arrived(8, 3500), "said once");
            Equal(false, ChaosTrial.StillAsks(ChaosPiece.BloodCircle, new[] { (ChaosTrial.CircleFilledState, 1L) }), "a full circle is done");
            Equal(true, ChaosTrial.StillAsks(ChaosPiece.BloodCircle, new[] { (ChaosTrial.CircleFilledState, 0L), ("spawning", 1L) }), "one filling is not");
            Equal(true, TrialTraps.KindOf("Metadata/Monsters/LeagueUltimatum/Turrets/FireTurret1@40") == TrialTrap.Turret, "a hazard by its MonsterVarieties Id");
            Equal(true, TrialTraps.IsFiring("Metadata/Monsters/LeagueUltimatum/Daemon/StormcallerRune", new[] { ("charging", 1L) }), "a rune charging is about to strike");

            var news = new ChaosRoundNews();
            Equal(ChaosRound.Collect, news.Began(new[] { (1u, ChaosPiece.SoulCore), (2u, ChaosPiece.StoneAltar) }), "the round is said as its pieces appear");
            Equal(null, news.Began(new[] { (1u, ChaosPiece.SoulCore), (3u, ChaosPiece.SoulCore) }), "and once: another core is the same round");
            Equal(null, news.Began(new[] { (9u, ChaosPiece.LiftLever) }), "a lever belongs to no round");
            Equal(null, news.Began(new[] { (1u, ChaosPiece.SoulCore) }), "pieces seen again after walking off are no news");
            news.Began(Array.Empty<(uint, ChaosPiece)>());
            Equal(ChaosRound.Collect, news.Began(new[] { (20u, ChaosPiece.SoulCore) }), "a later round of the same kind has new pieces");
            Equal(ChaosRound.Sacrifice, news.Began(new[] { (30u, ChaosPiece.BloodCircle) }), "another kind is said at once");

            Equal(false, news.Filled(30, false), "a circle filling");
            Equal(true, news.Filled(30, true), "full: said");
            Equal(false, news.Filled(30, true), "once");
            Equal(null, news.Core(false), "no core, no news");
            Equal(true, news.Core(true), "a core taken");
            Equal(null, news.Core(true), "still in hand");
            Equal(false, news.Core(false), "out of the hand");
            Equal(1, news.Ruin(1), "Ruin as it grows");
            Equal(null, news.Ruin(1), "not while it stands");
            Equal(3, news.Ruin(3), "each time it grows");
            Equal(null, news.Ruin(0), "gone with the run");
        }

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

            // At the nook's end, the room ten cells behind its wall is reachable only round by the hall (owner, 2026-11-16).
            var nookEnd = new Vector2(286, 226);
            var east = new Vector2(1, 0);
            map.Visit(nookEnd, width, height);
            map.Frontier(grid, width / 2, nookEnd, 8);
            Equal(null, map.UnseenAlong(grid, width / 2, nookEnd, east, ExplorationMap.MarkReachCells), "no mark through the nook's end wall");
            Equal(true, map.UnseenAlong(grid, width / 2, new Vector2(310, 226), east, ExplorationMap.MarkReachCells) != null, "the same ground is marked from inside the room");
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
            Equal(UnseenSize.Large, map.UnseenAlong(grid, width / 2, player, new Vector2(0, -1), ExplorationMap.MarkReachCells), "the radar's two pips toward the great hall");
            Equal(UnseenSize.Small, map.UnseenAlong(grid, width / 2, player, new Vector2(1, 0), ExplorationMap.MarkReachCells), "one pip toward the room that soon ends");
            Equal(null, map.UnseenAlong(grid, width / 2, player, new Vector2(-1, 0), ExplorationMap.MarkReachCells), "none toward the hall's own wall");
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
