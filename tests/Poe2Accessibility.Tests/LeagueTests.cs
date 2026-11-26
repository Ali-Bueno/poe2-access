namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The league mechanics' objects: which are listed by their states and icons, and which changes are said, each once.</summary>
    public static class LeagueTests
    {
        private const string Remnant = "Metadata/MiscellaneousObjects/Expedition2/Expedition2Encounter";
        private const string Marker = "Metadata/MiscellaneousObjects/Expedition/ExpeditionMarker";
        private const string Shard = "Metadata/MiscellaneousObjects/Delirium/DeliriumShardSeethingChyme";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APieceIsListedWhileItsStatesSayItIsThereToFind), APieceIsListedWhileItsStatesSayItIsThereToFind),
            (nameof(ARemnantsProgressIsSaidOnceAtEachStep), ARemnantsProgressIsSaidOnceAtEachStep),
            (nameof(EachMechanicsChangeIsSaidOnce), EachMechanicsChangeIsSaidOnce),
            (nameof(AStrongboxAmbushThenItCanBeOpened), AStrongboxAmbushThenItCanBeOpened),
            (nameof(ARogueExileIsSaidOnceAsTheFightStarts), ARogueExileIsSaidOnceAsTheFightStarts),
            (nameof(TheShrineUsedAndThePossessedAreSaidOnce), TheShrineUsedAndThePossessedAreSaidOnce),
            (nameof(TheFogIsSaidOnlyWhileTheMirrorRuns), TheFogIsSaidOnlyWhileTheMirrorRuns),
            (nameof(WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften), WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften),
            (nameof(LeagueLinesReadAsTheGameWritesThem), LeagueLinesReadAsTheGameWritesThem),
            (nameof(AMonsterInAnEssenceCrystalIsOutOfTheFightUntilItShatters), AMonsterInAnEssenceCrystalIsOutOfTheFightUntilItShatters),
            (nameof(AnAbyssPitsChestRingsAsItself), AnAbyssPitsChestRingsAsItself),
            (nameof(ATransitionIsReadOnlyWhenItCanBeOne), ATransitionIsReadOnlyWhenItCanBeOne),
            (nameof(AnEssenceCracksAndAnAbyssOpensByTheirTransitions), AnEssenceCracksAndAnAbyssOpensByTheirTransitions),
            (nameof(TheFindsNoChargeReachesAreCountedOnceTheStateReads), TheFindsNoChargeReachesAreCountedOnceTheStateReads),
            (nameof(ALesserShrineIsNotTakenForTheOrdinaryOneOfItsName), ALesserShrineIsNotTakenForTheOrdinaryOneOfItsName),
            (nameof(TheEncounterTrackerSaysItsTitleOnceAndItsCountsAsTheyStep), TheEncounterTrackerSaysItsTitleOnceAndItsCountsAsTheyStep),
            (nameof(ABreachRingIsSaidByQuartersAndAtItsEdge), ABreachRingIsSaidByQuartersAndAtItsEdge),
            (nameof(ATrialRoundIsSaidAsItChanges), ATrialRoundIsSaidAsItChanges),
            (nameof(TheTrialsOwnLinesOnScreenAreSaidAsTheyAppear), TheTrialsOwnLinesOnScreenAreSaidAsTheyAppear),
            (nameof(TheFogsFrontAndRearMoveAsTheGameMovesThem), TheFogsFrontAndRearMoveAsTheGameMovesThem),
            (nameof(TheFogIsSpokenOfOnlyOnceItAgreesWithTheGame), TheFogIsSpokenOfOnlyOnceItAgreesWithTheGame),
            (nameof(AMarkerWithoutAnIconIsToldByItsModel), AMarkerWithoutAnIconIsToldByItsModel),
            (nameof(TheChargeBeingPlacedPointsToTheNearestOpenFindOnceStill), TheChargeBeingPlacedPointsToTheNearestOpenFindOnceStill),
        };

        private static void AMarkerWithoutAnIconIsToldByItsModel()
        {
            // Live 2026-11-26: an ExpeditionMarker read with no minimap icon; ExpeditionMarkersCommon names only a model per kind.
            const string Folder = "Metadata/Terrain/Doodads/Leagues/Expedition/";
            Equal("ExpeditionRegularMonster", LeaguePieces.MarkerIcon(string.Empty, Folder + "monstermarker_02.ao"), "a monster marker");
            Equal("ExpeditionEliteMonster", LeaguePieces.MarkerIcon(string.Empty, Folder + "elitemarker.ao"), "an elite marker, not taken for a monster one");
            Equal("ExpeditionChest1", LeaguePieces.MarkerIcon(string.Empty, Folder + "chestmarker3_02.ao"), "a set's variant");
            Equal("ExpeditionChest1", LeaguePieces.MarkerIcon(string.Empty, Folder + "ChestMarkers/ChestUniques.ao"), "a typed chest");
            Equal("ExpeditionBoss", LeaguePieces.MarkerIcon("ExpeditionBoss", Folder + "monstermarker.ao"), "the game's own icon wins");
            Equal(string.Empty, LeaguePieces.MarkerIcon(string.Empty, string.Empty), "no model, nothing");
            Equal(ExpeditionChest.Uniques, LeaguePieces.ChestOf(Folder + "ChestMarkers/ChestUniques.ao"), "its kind");
            Equal(ExpeditionChest.Treasure, LeaguePieces.ChestOf(Folder + "ChestMarkers/ChestMetamorph.ao"), "the treasure destination");
            Equal(ExpeditionChest.Generic, LeaguePieces.ChestOf(Folder + "chestmarker_signpost_01.ao"), "a generic one");
            Equal(null, LeaguePieces.ChestOf(Folder + "monstermarker.ao"), "a monster's is no chest");
            var rule = LeaguePieces.RuleOf(Marker)!;
            Equal(ExpeditionFind.Elite, LeaguePieces.FindOf(rule.Piece, LeaguePieces.MarkerIcon(string.Empty, Folder + "elitemarker_03.ao")), "found as what it buries");
            Equal(true, LeaguePieces.IsListed(rule, Array.Empty<(string, long)>(), LeaguePieces.MarkerIcon(string.Empty, Folder + "chestmarker2.ao")), "and listed");
        }

        private static void TheChargeBeingPlacedPointsToTheNearestOpenFindOnceStill()
        {
            var guide = new PlacementGuide();
            var open = new List<(uint Id, ExpeditionFind Find, Vector2 Place)> { (1, ExpeditionFind.Chest, new Vector2(40, 0)), (2, ExpeditionFind.Elite, new Vector2(0, 20)) };
            Equal(null, guide.Next(new Vector2(0, 0), open, 0), "the charge just appeared");
            Equal(null, guide.Next(new Vector2(0, 0), open, PlacementGuide.SettleMs - 1), "not still long enough");
            var next = guide.Next(new Vector2(0, 0), open, PlacementGuide.SettleMs);
            Equal(ExpeditionFind.Elite, next?.Find, "the nearest open find");
            Equal(new Vector2(0, 20), next?.Offset, "from the charge");
            Equal(null, guide.Next(new Vector2(0, 0), open, PlacementGuide.SettleMs * 3), "said once while it stands");
            Equal(null, guide.Next(new Vector2(10, 0), open, 5000), "moved: waits again");
            Equal(null, guide.Next(new Vector2(10, 0), open, 5000 + PlacementGuide.SettleMs - 1), "still waiting");
            Equal(new Vector2(-10, 20), guide.Next(new Vector2(10, 0), open, 5000 + PlacementGuide.SettleMs)?.Offset, "a new spot: said again");
            Equal(null, guide.Next(null, open, 7000), "placing ended");
            Equal(null, guide.Next(new Vector2(10, 0), Array.Empty<(uint, ExpeditionFind, Vector2)>(), 9000), "nothing left open");
            Equal(null, guide.Next(new Vector2(10, 0), Array.Empty<(uint, ExpeditionFind, Vector2)>(), 9000 + PlacementGuide.SettleMs), "nothing to say");
        }

        // Row 0 of AfflictionBalancePerLevel (area level 1): the front starts at 150 cells a second and slows to 30 over 100 s;
        // the rear starts 10 s in at 1 cell a second and gains 0.38 a second each second.
        private static readonly FogBalance LevelOne = new(30f, 1f, 10f, 0.38f, 100f, 150f);

        private static void TheFogsFrontAndRearMoveAsTheGameMovesThem()
        {
            Equal(true, FogMath.IsPlausible(LevelOne), "the shipped row");
            Equal(false, FogMath.IsPlausible(new FogBalance(0f, 0f, 0f, 0f, 0f, 0f)), "the row of levels nobody plays, all zero: not believed");
            Equal(false, FogMath.IsPlausible(new FogBalance(30f, 1f, 10f, float.NaN, 100f, 150f)), "nor bytes that are no numbers");
            Equal(0f, FogMath.Front(LevelOne, -1.8f), "before the mirror's clock starts");
            Equal(1440f, FogMath.Front(LevelOne, 10f), "ten seconds in: 150 a second, slowing");
            Equal(9000f, FogMath.Front(LevelOne, 100f), "the ramp over");
            Equal(9300f, FogMath.Front(LevelOne, 110f), "then 30 cells a second");

            var running = new FogClocks(20f, 20f, float.PositiveInfinity, 1f, 0f, false);
            Equal(0f, FogMath.Rear(LevelOne, running with { RearTime = 9f }), "the rear waits its ten seconds");
            Equal(29f, FogMath.Rear(LevelOne, running), "ten seconds moving: 10 at its speed and 19 gained");
            Equal(0f, FogMath.Rear(LevelOne, running with { Never = true }), "a map whose fog never fades");
            Expect(true, Math.Abs(FogMath.Rear(LevelOne, running with { RearTime = 16f, ExtraDelay = 2f }) - 7.04f) < 0.01f,
                "the area's own delay is added to the row's: four seconds moving, 4 at its speed and 3.04 gained");
            Expect(true, FogMath.Rear(LevelOne, new FogClocks(30f, 30f, 25f, 1f, 0f, false)) > FogMath.Rear(LevelOne, running with { Time = 30f, RearTime = 30f }),
                "after the end the rear rushes on");
            Equal(FogMath.Front(LevelOne, 30f), FogMath.Rear(LevelOne, new FogClocks(30f, 60f, 25f, 1f, 0f, false)), "and never passes the front: the band closes");
            Equal(8000, FogMath.Shown(12345.6f, 8000), "the bar shows whole cells, never past the area's most");

            Equal((3, 4), FogMath.CellOf(260, 510, 10, 20), "the map's cells are half tiles from its origin in tiles, the nearest one");
            Equal(22, FogMath.NodeOf(260), "and the grid's nodes half tiles from the area's corner");
            Equal(100f, FogMath.Between(100f, 100f, 100f, 100f, 260, 260), "flat ground between four nodes");
            Expect(true, FogMath.Between(100f, 111.5f, 100f, 111.5f, 258, 253) is > 104f and < 108f, "between two nodes along x");
            Equal(151, FogMath.DepthOf(100f, 50.2f, 8000), "the region's depth and the way inside it, rounded up");
            Equal(8000, FogMath.DepthOf(100f, 9000f, 8000), "never past the most");
            Equal(null, FogMath.DepthOf(float.PositiveInfinity, 3f, 8000), "a region the fog does not reach");
        }

        private static void TheFogIsSpokenOfOnlyOnceItAgreesWithTheGame()
        {
            var news = new FogNews();
            Equal(null, news.Heard(100, 500, 0, 8000, true, 0), "in the band, as the game says: judged, not yet trusted");
            for (var at = 400; at <= 1600; at += 400)
            {
                Equal(null, news.Heard(100, 500, 0, 8000, true, at), "still earning its voice");
            }

            Equal(true, news.Trusted, "five agreements well away from the edges");
            Equal(null, news.Heard(300, 600, 100, 8000, true, 2000), "deep in the band: nothing to say");
            Equal(FogLine.RearNear, news.Heard(300, 900, 280, 8000, true, 3000), "the rear twenty cells behind");
            Equal(null, news.Heard(300, 900, 285, 8000, true, 4000), "said once in seven seconds");
            Equal(FogLine.LeftBehind, news.Heard(300, 900, 320, 8000, false, 5000), "the rear has passed: out, and the fog went on");
            Equal(null, news.Heard(300, 900, 330, 8000, false, 6000), "not again at once");
            Equal(FogLine.LeftBehind, news.Heard(300, 900, 340, 8000, false, 8200), "but again within the four seconds there are");
            Equal(FogLine.Outran, news.Heard(950, 900, 340, 8000, false, 9000), "past the front: out the other way");
            Equal(FogLine.FrontNear, news.Heard(895, 900, 340, 8000, true, 10000), "at the front's edge");

            var wrong = new FogNews();
            for (var at = 0; at <= 4000; at += 400)
            {
                Equal(null, wrong.Heard(100, 500, 0, 8000, false, at), "the game says out where the port says in: never trusted, never spoken");
            }

            Equal(false, wrong.Trusted, "a port that does not agree stays silent");
            Equal(null, wrong.Heard(700, 500, 0, 8000, null, 5000), "and with the stat unread nothing is judged");

            // A stat that does not read at all says "out" for ever: agreeing with it outside the band proves nothing.
            var deaf = new FogNews();
            for (var at = 0; at <= 8000; at += 400)
            {
                Equal(null, deaf.Heard(700, 500, 0, 8000, false, at), "out by both: never the fog's voice on that alone");
            }

            Equal(false, deaf.Trusted, "trust needs the game to have said in");

            var early = new FogNews();
            for (var at = 0; at <= 8000; at += 400)
            {
                Equal(null, early.Heard(100, 0, 0, 8000, false, at), "the mirror not yet walked through: no band, nothing judged, nothing said");
            }

            Equal((0, 0), early.Record, "the wait before the start counts for nothing");

            var checkedNow = new FogNews();
            for (var at = 0; at <= 8000; at += 400)
            {
                checkedNow.Heard(100, 500, 0, 8000, true, at);
            }

            Equal(null, checkedNow.Heard(700, 500, 0, 8000, true, 9000), "past the front by the port, in by the game: the game's word stands, nothing said");
            Equal(null, checkedNow.Heard(300, 900, 280, 8000, false, 10000), "the rear near by the port, out by the game: it is not coming, it has come");
            Equal(true, checkedNow.Trusted, "two disagreements in twenty-three do not take its voice");
            Equal(FogLine.RearNear, checkedNow.Heard(300, 900, 280, 8000, null, 11000), "the stat unread at that moment does not gag a port that earned its voice");
            for (var at = 12000; at <= 14000; at += 400)
            {
                checkedNow.Heard(700, 500, 0, 8000, true, at);
            }

            Equal(false, checkedNow.Trusted, "but one that keeps disagreeing loses it");
            Equal("The fog has left you behind: it went up right", Announcements.Fog(FogLine.LeftBehind, "up right"), "which way it went, as a side of the screen");
            Equal("The fog has left you behind", Announcements.Fog(FogLine.LeftBehind, string.Empty), "or without one where the ground does not tell");
        }

        private static void TheTrialsOwnLinesOnScreenAreSaidAsTheyAppear()
        {
            // Each element by its own address: the objective 1, the clock 2, the banner 3, a label 4.
            var news = new ChaosTrialHudNews();
            Equal(0, news.Heard(new[] { (1L, "Slay all Monsters") }, 0).Count, "what stands when first looked at is no news");
            Equal(0, news.Heard(new[] { (1L, "Slay all Monsters"), (2L, "1:30") }, 1000).Count, "a clock has no words: never said");
            Equal(0, news.Heard(new[] { (1L, "Slay all Monsters"), (2L, "1:29") }, 2000).Count, "nor as it runs");
            Equal("Survive", string.Join(" | ", news.Heard(new[] { (1L, "Survive"), (2L, "1:00") }, 60000)), "the next round's objective");
            Equal("Return to the Ring", string.Join(" | ", news.Heard(new[] { (1L, "Survive"), (3L, "Return to the Ring") }, 61000)), "the banner as it comes up");
            Equal(0, news.Heard(new[] { (1L, "Survive") }, 62000).Count, "it blinks off");
            Equal(0, news.Heard(new[] { (1L, "Survive"), (3L, "Return to the Ring") }, 63000).Count, "and on again: the same banner still");
            Equal(0, news.Heard(new[] { (1L, "Survive"), (3L, "Return to the Ring") }, 64000).Count, "while it stands");
            for (var at = 65000; at <= 71000; at += 1000)
            {
                Equal(0, news.Heard(new[] { (1L, "Survive") }, at).Count, "the player is back in the ring: the objective stands, looked at every second");
            }

            Equal("Return to the Ring", string.Join(" | ", news.Heard(new[] { (1L, "Survive"), (3L, "Return to the Ring") }, 72000)), "out again after a while: said again");
            Equal("Round 3/10", string.Join(" | ", news.Heard(new[] { (4L, "Round 3/10") }, 300000)), "a new line with words is said even when it carries numbers");
            Equal(0, news.Heard(new[] { (4L, "Round 4/10") }, 301000).Count, "its numbers alone changing is not: the round is said from the ruleset");

            // A popup has one label of the same name per choice: each is its own line, standing side by side.
            Equal("Lightning Runes | Blood Globules", string.Join(" | ", news.Heard(new[] { (10L, "Lightning Runes"), (11L, "Blood Globules") }, 400000)), "three choices are three lines, not one fighting itself");
            for (var at = 401000; at <= 408000; at += 1000)
            {
                Equal(0, news.Heard(new[] { (10L, "Lightning Runes"), (11L, "Blood Globules") }, at).Count, "and standing, they are not said over and over");
            }

            Equal("Vaal Omnitect", string.Join(" | ", news.Heard(new[] { (10L, "Vaal Omnitect"), (12L, "Vaal Omnitect") }, 409000)), "two elements with one text are one line to hear");
        }

        private static void ABreachRingIsSaidByQuartersAndAtItsEdge()
        {
            Equal(50f, BreachRings.ValueOf(0f, 100f, 10f, 5f), "half its time gone: half grown");
            Equal(40f, BreachRings.ValueOf(40f, 80f, 0f, 0f) is { } stood ? stood - 40f : -1f, "no time to grow in: it stands at where it grows to");
            Equal(null, BreachRings.ValueOf(float.NaN, 100f, 10f, 5f), "numbers that cannot be a ring's are not believed");
            Equal(null, BreachRings.ValueOf(0f, 250f, 10f, 5f), "nor a size past 100");
            Equal(300, (int)Math.Round(BreachRings.RadiusOf(100f)), "fully grown: 300 cells, the game's constant");
            Equal(198, (int)Math.Round(BreachRings.RadiusOf(50f)), "half grown is two thirds of the radius: the 0.6 power");
            Equal(0, (int)BreachRings.RadiusOf(0f), "nothing yet");

            var centre = new Vector2(1000f, 1000f);
            var news = new RulesetNews();
            Equal(0, news.Breach(new BreachRing(centre, 5f, 1), centre, 0).Count, "the hand just touched: no news");
            Equal(0, news.Breach(new BreachRing(centre, 20f, 1), centre, 1000).Count, "growing, the player inside");
            Equal("BreachQuarter", string.Join(" ", news.Breach(new BreachRing(centre, 26f, 1), centre, 2000)), "a quarter");
            Equal(0, news.Breach(new BreachRing(centre, 30f, 1), centre, 3000).Count, "said once");
            Equal("BreachHalf", string.Join(" ", news.Breach(new BreachRing(centre, 55f, 2), centre, 4000)), "half, enraged or not");
            var far = centre + new Vector2(BreachRings.RadiusOf(55f) * 1.2f, 0f);
            Equal("OutsideBreach", string.Join(" ", news.Breach(new BreachRing(centre, 55f, 2), far, 9000)), "the player walks out of the ring");
            Equal(0, news.Breach(new BreachRing(centre, 55f, 2), centre, 10000).Count, "back at once: not said within three seconds");
            Equal("InsideBreach", string.Join(" ", news.Breach(new BreachRing(centre, 55f, 2), centre, 12500)), "then said, so what was last heard is where the player stands");
            Equal(0, news.Breach(new BreachRing(centre, 60f, 3), far, 20000).Count, "closing: nothing more of it");

            var two = new RulesetNews();
            var other = centre + new Vector2(900f, 0f);
            Equal(0, two.Breach(new BreachRing(centre, 60f, 1), centre, 0).Count, "inside one ring, past its half");
            Equal(0, two.Breach(new BreachRing(other, 10f, 1), other, 30000).Count, "another ring just touched: no quarter of the first, nothing left");
            Equal("BreachQuarter", string.Join(" ", two.Breach(new BreachRing(other, 30f, 1), other, 31000)), "its own quarters from its own start");

            var seenFromAfar = new RulesetNews();
            Equal(0, seenFromAfar.Breach(new BreachRing(centre, 60f, 1), far, 0).Count, "a ring first read from outside");
            Equal(0, seenFromAfar.Breach(new BreachRing(centre, 60f, 1), far, 9000).Count, "is nothing the player left");
            Equal(0, seenFromAfar.Breach(new BreachRing(centre, 60f, 1), centre, 12000).Count, "walking in is no news either");
            Equal("OutsideBreach", string.Join(" ", seenFromAfar.Breach(new BreachRing(centre, 60f, 1), far, 16000)), "walking out after that is");
        }

        private static void ATrialRoundIsSaidAsItChanges()
        {
            var news = new RulesetNews();
            Equal(null, news.RoundWon(null), "no trial here");
            Equal(null, news.RoundWon(1), "the round standing when first read is no news");
            Equal(1, news.RoundWon(2), "a room completed: the client counts the next round, the one won is said (2026-11-19: \"ronda 2\" in the first)");
            Equal(null, news.RoundWon(2), "said once");
            Equal(null, news.RoundWon(null), "the trial left");
            Equal("Round 1 of 4 won", Announcements.TrialRoundWon(1, 4), "with the trial's own count");
            Equal("Round 1 won", Announcements.TrialRoundWon(1, null), "no count when the tables are not known");
            Equal("Round 2 of 4: Survive", Announcements.TrialRoundAsks(2, 4, "Survive"), "a room's round and what it asks");

            var first = new IntPtr(0x1000);
            var second = new IntPtr(0x2000);
            Equal("Slay all Monsters", news.Objective(first, "Slay all Monsters"), "the first room's objective is said: walking in is when it is needed");
            Equal(null, news.Objective(first, "Slay all Monsters"), "once");
            Equal(null, news.Objective(IntPtr.Zero, null), "between rooms");
            Equal(null, news.Objective(first, "Slay all Monsters"), "the same room back is no news");
            Equal("Slay all Monsters", news.Objective(second, "Slay all Monsters"), "another room asking the same is a new round");
            Equal("Survive", news.Objective(first, "Survive"), "the next room's");
            Equal(null, news.Progress(0.1f), "the bar when first read");
            Equal(25, news.Progress(0.3f), "a quarter of the challenge");
            Equal(null, news.Progress(0.4f), "said once");
            Equal(75, news.Progress(0.8f), "two quarters at once: the one reached");
            Equal(null, news.Progress(1f), "full: the round's end is said by the game");
            Equal(null, news.Progress(0.02f), "a new round starts it again");
            Equal(50, news.Progress(0.5f), "and its quarters are said anew");
            Equal(null, news.Progress(0.49f), "a bar wavering about a quarter");
            Equal(null, news.Progress(0.51f), "says it once");
            Equal(null, news.Progress(0.2f), "falling back is no news");
            Equal(null, news.Progress(0.3f), "nor climbing again to what was said");
            Equal(75, news.Progress(0.76f), "only a quarter not yet said in the round");
            Equal(null, news.RoundWon(3), "the round read again");
            Equal(3, news.RoundWon(4), "the next room completed");
            Equal(25, news.Progress(0.3f), "its quarters are its own, even when the bar never read empty between");
            Equal("Challenge Progress: 50%", Announcements.TrialProgress(50), "under the bar's own name");

            var altar = new LeagueNews();
            Equal(0, altar.Heard(3, LeaguePiece.RitualAltar, States(("current_state", 0)), "RitualRune").Count, "an altar with its monsters around");
            Equal("RitualReady", string.Join(" ", altar.Heard(3, LeaguePiece.RitualAltar, States(("current_state", 1)), "RitualRune")), "they are dead: the altar can be used");
            Equal(0, altar.Heard(3, LeaguePiece.RitualAltar, States(("current_state", 2)), "RitualRune").Count, "the player began it: no news");
            Equal("RitualDone", string.Join(" ", altar.Heard(3, LeaguePiece.RitualAltar, States(("current_state", 3)), "RitualRuneFinished")), "won");
            Equal("Explosives Placed: 2/5; 1 not covered", Announcements.ExplosivesPlaced(2, 5, 1), "the game's own count, then what it lacks");
            Equal("Explosives Placed: 2", Announcements.ExplosivesPlaced(2, null, null), "the total unread: as before");

            var ids = GameIds.Parse(new[] { "# the game's numbers", "stat current_rage 5", "ruleset StandardRules 0", "ruleset UltimatumRules 49" });
            Equal(49, ids.Ruleset("UltimatumRules"), "a ruleset's place in the session is its row in the game's table, read from the files");
            Equal(null, ids.Ruleset("ExpeditionRules"), "one the catalog does not list: the reader falls back on the build's own");
            Equal("UltimatumRules", ids.RulesetName(49), "and the name of a row, for the log");
            Equal(null, ids.RulesetName(50), "a row not listed has none");
        }

        private static void TheEncounterTrackerSaysItsTitleOnceAndItsCountsAsTheyStep()
        {
            var news = new EncounterNews();
            Equal(0, news.Heard(Array.Empty<string>(), 0).Count, "nothing shown");
            Equal("Expedition Encounter", string.Join(" | ", news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 0/5" }, 1000)), "the encounter begins: its title at once");
            Equal(true, news.ShowsCount, "the game counts the charges itself");
            Equal("Explosives Placed: 0/5", string.Join(" | ", news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 0/5" }, 1800)), "then how many charges there are, once it stands");
            Equal(0, news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 1/5" }, 3000).Count, "a charge placed: not before the count stands still");
            Equal(0, news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 2/5" }, 3300).Count, "another at once");
            Equal("Explosives Placed: 2/5", string.Join(" | ", news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 2/5" }, 4100)), "two steps at once are one line");
            Equal(0, news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 2/5" }, 6000).Count, "said once");
            Equal(0, news.Heard(Array.Empty<string>(), 7000).Count, "a menu hides the HUD");
            Equal(0, news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 2/5" }, 8000).Count, "and showing it again makes no title new");
            Equal(0, news.Heard(new[] { "Expedition Encounter", "Explosives Placed: 2/5" }, 9000).Count, "nor the count it already said... once it stood");

            var ship = new List<string>();
            EncounterLines.Add(ship, "Return to Ship", "Return to Ship");
            EncounterLines.Add(ship, " + ", "<<xbox_button_lt>>+<<xbox_button_dpad_up>>");
            EncounterLines.Add(ship, "Return to Ship", "Return to Ship");
            Equal(Announcements.WithButton("Return to Ship", ControllerGlyphs.FromMarkup("<<xbox_button_lt>>+<<xbox_button_dpad_up>>")), string.Join(" | ", ship), "a HUD action's buttons go with it, and no bare plus");
            var legend = new List<string> { "Return to Ship" };
            EncounterLines.Add(legend, " +  Return to Ship", "<<xbox_button_lt>>+<<xbox_button_dpad_up>> Return to Ship");
            Equal(ship[0], string.Join(" | ", legend), "the same from a legend repeating the title");
            var chord = Announcements.ButtonName("TriggerLeft", false) + Announcements.ButtonCombination + Announcements.ButtonName("DPadUp", false);
            var pictured = EncounterLines.Build(new EncounterLines.Node[]
            {
                new(-1, 0, "Expedition Encounter", "Expedition Encounter", string.Empty),
                new(-1, 0, string.Empty, string.Empty, string.Empty),
                new(1, 1, string.Empty, string.Empty, Announcements.ButtonName("TriggerLeft", false)),
                new(1, 1, "+", "+", string.Empty),
                new(1, 1, string.Empty, string.Empty, Announcements.ButtonName("DPadUp", false)),
                new(1, 1, "Place Explosives", "Place Explosives", string.Empty),
            });
            Equal($"Expedition Encounter | Place Explosives, {chord}", string.Join(" | ", pictured), "button pictures beside a bare plus are one chord with their label (live 2026-11-26: only the plus was said)");
            var apart = EncounterLines.Build(new EncounterLines.Node[]
            {
                new(-1, 0, "Detonate", "Detonate", string.Empty),
                new(-1, 0, string.Empty, string.Empty, Announcements.ButtonName("Button4", false)),
                new(-1, 0, string.Empty, string.Empty, Announcements.ButtonName("Button3", false)),
            });
            Equal($"Detonate, {Announcements.ButtonName("Button4", false)}{Announcements.ButtonSeparator}{Announcements.ButtonName("Button3", false)}", string.Join(" | ", apart), "two pictures with no plus are either button");
            var action = new EncounterNews();
            Equal(0, action.Heard(Array.Empty<string>(), 0).Count, "not offered yet");
            Equal("Return to Ship, L2 plus D-pad up", string.Join(" | ", action.Heard(new[] { "Return to Ship, L2 plus D-pad up" }, 1000)), "a button's own number is no count");

            var ritual = new EncounterNews();
            Equal(0, ritual.Heard(new[] { "Ritual Encounter", "100 Tribute", "2 Rituals Remaining" }, 0).Count, "what the tracker shows when first looked at is no news");
            Equal(0, ritual.Heard(new[] { "Ritual Encounter", "140 Tribute", "2 Rituals Remaining" }, 1000).Count, "a bare number running on is not news: it stays in the vitals");
            Equal(0, ritual.Heard(new[] { "Ritual Encounter", "140 Tribute", "2 Rituals Remaining" }, 3000).Count, "however long it stands");
            Equal(false, ritual.ShowsCount, "no count out of a total here");
            ritual.Reset();
            Equal(0, ritual.Heard(new[] { "Delirium Encounter" }, 0).Count, "a new area: looked at afresh");

            Equal("2 not covered", Announcements.FindsUncovered(2), "after the game's own count, only what it lacks");
            Equal(string.Empty, Announcements.FindsUncovered(null), "and nothing when that is not read");
        }

        private static void ALesserShrineIsNotTakenForTheOrdinaryOneOfItsName()
        {
            static bool Lesser(string id) => id is "shrine_acceleration" or "shrine_brutal";
            Equal("shrine_acceleration", LeagueNews.LesserShrineOf("shrine_acceleration", Lesser), "the lesser shrine's own buff");
            Equal(null, LeagueNews.LesserShrineOf("shrine_acceleration_2", Lesser), "the ordinary one ends in what the host's suffix looks like: it is no lesser one");
            Equal("shrine_brutal", LeagueNews.LesserShrineOf("shrine_brutal_1A", Lesser), "the host's suffix on a second copy, in its capitals");
            Equal(null, LeagueNews.LesserShrineOf("flask_effect_life", Lesser), "any other buff");

            var news = new LeagueNews();
            Equal(0, news.LesserShrines(new[] { "shrine_brutal" }, Lesser).Count, "a buff there as the area opens is no news");
            Equal("shrine_acceleration", string.Join(" ", news.LesserShrines(new[] { "shrine_brutal", "shrine_acceleration" }, Lesser)), "the one just taken");
            Equal(0, news.LesserShrines(new[] { "shrine_brutal", "shrine_acceleration" }, Lesser).Count, "said once");
            Equal(0, news.LesserShrines(new[] { "shrine_acceleration_2" }, Lesser).Count, "the ordinary shrine has its own line");
            Expect(true, Announcements.LesserShrine("Lesser Brutal Shrine", "shrine_brutal").StartsWith("Lesser Brutal Shrine. ", StringComparison.Ordinal), "the game's name, then what it does");
            Equal("Shrine of something", Announcements.LesserShrine("Shrine of something", "shrine_new"), "a buff of none of the six: the name alone");
        }

        private static void AnAbyssPitsChestRingsAsItself()
        {
            Equal(true, LeaguePieces.IsAbyssChest("Metadata/Chests/Abyss/AbyssChestWeapons"), "the Abyssal Arsenal (Chests.dat 87)");
            Equal(true, LeaguePieces.IsAbyssChest("Metadata/Chests/Abyss/AbyssChestRareFinalArmour"), "the Abyssal Armoury, rare, the last pit's");
            Equal(true, LeaguePieces.IsAbyssChest("Metadata/Chests/Abyss/AbyssLargeChestGeneric"), "a large Abyssal Trove");
            Equal(true, LeaguePieces.IsAbyssChest("Metadata/Chests/AbyssChest01"), "the Hoard");
            Equal(false, LeaguePieces.IsAbyssChest("Metadata/Chests/Abyss/AbyssStrongboxWeapons"), "a strongbox rings as a strongbox");
            Equal(false, LeaguePieces.IsAbyssChest("Metadata/Chests/Abyss/AbyssDepthsChest"), "the Depths' plain chest is a chest");
            Equal(false, LeaguePieces.IsAbyssChest("Metadata/MiscellaneousObjects/Abyss/AbyssChestSpawner"), "not a chest's file");
        }

        private static void AMonsterInAnEssenceCrystalIsOutOfTheFightUntilItShatters()
        {
            Equal(true, EssencePrison.IsCrystal("Metadata/MiscellaneousObjects/Monolith@24"), "an essence's crystal, whatever its instance tail");
            Equal(false, EssencePrison.IsCrystal("Metadata/MiscellaneousObjects/MiniMonolith"), "the small crystals around it were never measured: their monsters ring");
            Equal(true, EssencePrison.IsCrystal("Metadata/MiscellaneousObjects/SuperMonolith"), "a map's great crystal");
            Equal(false, EssencePrison.IsCrystal("Metadata/MiscellaneousObjects/Monolith_Landmark"), "a landmark of the same word is none");
            Equal(false, EssencePrison.IsCrystal("Metadata/Terrain/Hideouts/Monolith/Objects/ArenaLift"), "nor an object of another folder");

            Equal(true, EssencePrison.IsWhole((1, 4), true), "built: it holds");
            Equal(true, EssencePrison.IsWhole((3, 4), true), "cracked twice: it still holds");
            Equal(false, EssencePrison.IsWhole((4, 4), true), "shattered: it holds nothing, though the game deletes it five seconds later");
            Equal(false, EssencePrison.IsWhole((3, 4), false), "the game no longer lets it be used: free, whatever its transition reads");
            Equal(true, EssencePrison.IsWhole((2, 4), null), "a target that does not read leaves the transition's word");
            Equal(true, EssencePrison.IsWhole(null, true), "transition unread: by the game still letting it be used");
            Equal(false, EssencePrison.IsWhole(null, false), "and not once it cannot");
            Equal(false, EssencePrison.IsWhole(null, null), "neither reads: it holds nothing, better a foe heard");
            Equal(true, EssencePrison.IsShattered((4, 4)), "the last state is the shattered one");
            Equal(false, EssencePrison.IsShattered(null), "unread is not shattered: the crystal stays listed");

            var crystals = new[] { new Vector2(1000f, 1000f) };
            var held = EssencePrison.Held(crystals, new[] { (7u, new Vector2(1004f, 998f), false), (8u, new Vector2(1200f, 1000f), false) });
            Equal("7", string.Join(" ", held), "the monster standing in the crystal is held, one a few steps off is not");
            Equal(0, EssencePrison.Held(Array.Empty<Vector2>(), new[] { (7u, new Vector2(1004f, 998f), false) }).Count, "no whole crystal: nobody is held");
            var pack = EssencePrison.Held(crystals, new[]
            {
                (9u, new Vector2(1380f, 1000f), true),
                (10u, new Vector2(1000f + EssencePrison.PackReachWorld + 1, 1000f), true),
            });
            Equal("9", string.Join(" ", pack), "the held monster's pack around the crystal is held too (user 2026-11-17), not beyond its reach");
            Equal(0, EssencePrison.Held(Array.Empty<Vector2>(), new[] { (9u, new Vector2(1380f, 1000f), true) }).Count, "the crystal shattered: its pack is freed with it");

            EssencePrison.Publish(held);
            Equal(true, EssencePrison.Holds(7), "told to every reader that asks whether a monster is in the fight");
            Equal(false, EssencePrison.Holds(8), "and only of the held");
            EssencePrison.Publish(new HashSet<uint>());
            Equal(false, EssencePrison.Any, "freed: it rings again at the next look");
        }

        private static void ATransitionIsReadOnlyWhenItCanBeOne()
        {
            Equal((1, 4), Transitions.Judge(1, 4), "an object as it is built");
            Equal((8, 16), Transitions.Judge(8, 16), "an Abyss fissure fully open");
            Equal(null, Transitions.Judge(0, 4), "the game counts from 1: a 0 says the byte is not the state");
            Equal(null, Transitions.Judge(5, 4), "nor is one past the file's count");
            Equal(null, Transitions.Judge(1, 1), "the game's parser takes 2 to 127 states");
            Equal(null, Transitions.Judge(1, 200), "so a count beyond is another field");
        }

        private static void AnEssenceCracksAndAnAbyssOpensByTheirTransitions()
        {
            var crystal = LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Monolith@3")!;
            Equal(LeaguePiece.EssenceCrystal, crystal.Piece, "the crystal is followed");
            Equal(false, crystal.Lists, "and listed by its icon, as before");
            Equal(null, LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/MiniMonolith"), "the small ones crack with it: no news of their own");

            var news = new LeagueNews();
            Equal(0, news.Heard(1, LeaguePiece.EssenceCrystal, States((LeaguePieces.Transition, 1)), "Essence").Count, "whole when first seen: no news");
            Equal("EssenceCracked", string.Join(" ", news.Heard(1, LeaguePiece.EssenceCrystal, States((LeaguePieces.Transition, 2)), "Essence")), "the first crack");
            Equal("EssenceCrackedMore", string.Join(" ", news.Heard(1, LeaguePiece.EssenceCrystal, States((LeaguePieces.Transition, 3)), "Essence")), "the second");
            Equal("EssenceFreed", string.Join(" ", news.Heard(1, LeaguePiece.EssenceCrystal, States((LeaguePieces.Transition, 4)), "Essence")), "shattered: the monster is free");
            Equal(0, news.Heard(1, LeaguePiece.EssenceCrystal, States((LeaguePieces.Transition, 4)), "Essence").Count, "said once");
            Equal(0, news.Heard(2, LeaguePiece.EssenceCrystal, States(), "Essence").Count, "a crystal whose transition does not read says nothing");
            Equal("Essence: Blood Cretin", Announcements.Essence("Blood Cretin", 1, false), "one essence, the usual, is not counted aloud");
            Equal("Essence: Blood Cretin, 3 essences, corrupted", Announcements.Essence("Blood Cretin", 3, true), "what a sighted player reads on the crystal");
            Equal("Essence", Announcements.Essence(string.Empty, 0, false), "states unread: as before");
            Equal("Essence: Blood Cretin. Essence of Flames, Essence of the Body, corrupted",
                Announcements.Essence("Blood Cretin", 2, true, new[] { "Essence of Flames", "Essence of the Body" }), "the essences by name when they read: the count says nothing more");

            var crack = LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Abyss/AbyssCrack@9")!;
            Equal("8", string.Join(" ", Enumerable.Range(1, 16).Where(state => LeaguePieces.IsListed(crack, States((LeaguePieces.Transition, state)), "AbyssCrackActive"))),
                "a fissure is listed while fully open, the one state its icon shows in");
            Equal(false, LeaguePieces.IsListed(crack, States(), "AbyssCrackActive"), "its icon alone lists nothing: the row is there closed or open");
            Equal(LeaguePieces.AbyssSound, LeaguePieces.SoundOf("Metadata/MiscellaneousObjects/Abyss/AbyssCrack"), "it rings as the Abyss: the nearest leads along the fissure");

            Equal(0, news.Heard(5, LeaguePiece.AbyssPit, States((LeaguePieces.Transition, 1)), "AbyssPit").Count, "a pit closed when first seen");
            Equal("AbyssOpens", string.Join(" ", news.Heard(5, LeaguePiece.AbyssPit, States((LeaguePieces.Transition, 2)), "AbyssPit")), "the fissures closed up to it: it opens");
            Equal("AbyssClosed", string.Join(" ", news.Heard(5, LeaguePiece.AbyssPit, States((LeaguePieces.Transition, 3)), LeaguePieces.PitClosedIcon)),
                "its end, told by the transition and by the icon at once, is said once");
            Equal(0, news.Heard(6, LeaguePiece.AbyssPit, States(), "AbyssPit").Count, "a pit whose transition does not read");
            Equal("AbyssClosed", string.Join(" ", news.Heard(6, LeaguePiece.AbyssPit, States(), LeaguePieces.PitClosedIcon)), "is still told closed by its icon");

            Expect(true, LeaguePieces.NamesMatched().All(name => name.State != LeaguePieces.Transition), "the transition is ours: no file is asked for a state of that name");
        }

        private static void TheFindsNoChargeReachesAreCountedOnceTheStateReads()
        {
            Equal(null, PlacementNews.Uncovered(new[] { false, false, false }), "none reads as reached: the state may not be read at all, nothing is claimed");
            Equal(2, PlacementNews.Uncovered(new[] { true, false, false }), "one reached: the other two are left");
            Equal(0, PlacementNews.Uncovered(new[] { true, true }), "every find reached");
            Equal(null, PlacementNews.Uncovered(Array.Empty<bool>()), "no finds");

            Equal(28, ExpeditionReach.Radius("MapSteppe"), "a charge's blast in a map, in grid units, as measured in the game");
            Equal(34, ExpeditionReach.Radius("ExpeditionLogBook_Reef"), "and in a logbook, where it is a fifth wider");
            Equal(90, ExpeditionReach.Range("G2_4_1"), "how far from the last charge the next can go");
            Equal(108, ExpeditionReach.Range("ExpeditionLogBook_Tundra"), "and in a logbook");
            var charges = new[] { new Vector2(100f, 100f), new Vector2(180f, 100f) };
            Equal(true, ExpeditionReach.AnyReaches(charges, new Vector2(120f, 110f), 28), "a find inside one blast");
            Equal(false, ExpeditionReach.AnyReaches(charges, new Vector2(140f, 100f), 28), "one between two blasts that do not meet");
            Equal(false, ExpeditionReach.AnyReaches(Array.Empty<Vector2>(), new Vector2(100f, 100f), 28), "no charge placed");
            ExpeditionReach.Publish(new HashSet<uint> { 7 });
            Equal(true, ExpeditionReach.IsCovered(7), "told to the scanner, which names the find covered");
            ExpeditionReach.Publish(new HashSet<uint>());
            Equal(false, ExpeditionReach.IsCovered(7), "until the area changes");
        }

        private static (string, long)[] States(params (string, long)[] states) => states;

        private static void APieceIsListedWhileItsStatesSayItIsThereToFind()
        {
            var remnant = LeaguePieces.RuleOf(Remnant + "@12")!;
            Equal(LeaguePiece.VerisiumRemnant, remnant.Piece, "a piece by its file, whatever its instance tail");
            Equal(true, remnant.WithoutTarget, "listed through its waves, when the game makes it untargetable");
            Equal("0 1 2 5 6", string.Join(" ", Enumerable.Range(0, 9).Where(value => LeaguePieces.IsListed(remnant, States(("activated", value)), string.Empty))),
                "listed until done (7) and not once spoiled (3, 4, 8)");
            Equal(false, LeaguePieces.IsListed(remnant, States(), string.Empty), "silent when its states are not read");
            Equal(null, LeaguePieces.RuleOf(Remnant + "Daemon"), "the remnant's daemon is no remnant");

            var marker = LeaguePieces.RuleOf(Marker + "@3")!;
            Equal(true, LeaguePieces.IsListed(marker, States(), "ExpeditionChest2"), "a marker by its icon");
            Equal(false, LeaguePieces.IsListed(marker, States(), string.Empty), "not before the game gives it one");
            Equal(ExpeditionFind.Elite, LeaguePieces.FindOf(LeaguePiece.ExpeditionMarker, "ExpeditionEliteMonster"), "what its icon says it buries");
            Equal(null, LeaguePieces.FindOf(LeaguePiece.ExpeditionMarker, "ExpeditionDynamite"), "a charge's icon is no find");
            var relic = LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionRelic")!;
            Equal(true, LeaguePieces.IsListed(relic, States(("exploded", 0)), "ExpeditionPOI"), "a relic with its icon");
            Equal(false, LeaguePieces.IsListed(relic, States(("exploded", 1)), "ExpeditionPOI"), "and not once blown");

            var shard = LeaguePieces.RuleOf(Shard)!;
            Equal(LeaguePiece.ShardPortal, shard.Piece, "the red shard opens a portal");
            Equal(true, LeaguePieces.IsListed(shard, States(("visible", 1), ("detonate", 0)), string.Empty), "a shard risen and whole");
            Equal(false, LeaguePieces.IsListed(shard, States(("visible", 1), ("detonate", 1)), string.Empty), "not once shattered");
            Equal(false, LeaguePieces.IsListed(shard, States(("visible", 0), ("detonate", 0)), string.Empty), "nor before it rises");
            Equal(null, LeaguePieces.RuleOf(Shard + "Return"), "a shard's family is its exact files");
            Equal(true, LeaguePieces.IsListed(LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Delirium/DeliriumGigaMirror")!, States(("enabled", 1)), "GrandMirrorActive"), "the Grand Mirror once enabled");

            var spirit = LeaguePieces.RuleOf("Metadata/Monsters/TormentedSpirits/TormentedSpiritoftheOwlPrimal@5")!;
            Equal(LeaguePiece.AzmeriSpirit, spirit.Piece, "a spirit by the start of its name");
            Equal(true, LeaguePieces.IsListed(spirit, States(("active", 1), ("wisp_time", 30)), "SpiritActivated"), "listed while its icon is on");
            Equal(false, LeaguePieces.IsListed(spirit, States(("active", 0)), "SpiritActivated"), "not before");
            Equal(null, LeaguePieces.RuleOf("Metadata/Monsters/TormentedSpirits/TormentedSpiritoftheOwlPrimalPossesedDaemon"), "the daemon in a possessed monster is no wisp to follow");
            Equal(true, LeaguePieces.IsListed(LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Brequel/BrequelSkillObject")!, States(("ready", 1)), string.Empty), "a hive's skill once ready");
            Equal(false, LeaguePieces.RuleOf("Metadata/Chests/StrongBoxes/CasterStrongboxHigh")!.Lists, "a strongbox is followed, the chest rule lists it");
            Equal(LeaguePiece.ExpeditionDetonator, LeaguePieces.RuleOf("Metadata/MiscellaneousObjects/Expedition/ExpeditionDetonatorTreasureIsland@2")!.Piece, "every detonator");
        }

        private static void ARemnantsProgressIsSaidOnceAtEachStep()
        {
            var news = new LeagueNews();
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 1)), "Expedition2RemnantActive").Count, "what it shows when first seen is no news");
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 1)), "Expedition2RemnantActive").Count, "nor while it stands");
            Equal("RemnantWaves", string.Join(" ", news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 5)), "Expedition2RemnantActive")), "its waves");
            Equal("RemnantReward", string.Join(" ", news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 6)), "Expedition2RemnantActive")), "its reward");
            Equal(0, news.Heard(1, LeaguePiece.VerisiumRemnant, States(("activated", 7)), "Expedition2RemnantDeactivated").Count, "done: dropped from the list, nothing said");
            foreach (var spoiled in new[] { 3L, 4L, 8L })
            {
                var id = (uint)(10 + spoiled);
                news.Heard(id, LeaguePiece.VerisiumRemnant, States(("activated", 2)), string.Empty);
                Equal("RemnantSpoiled", string.Join(" ", news.Heard(id, LeaguePiece.VerisiumRemnant, States(("activated", spoiled)), string.Empty)), $"{spoiled} spoils it");
            }

            news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 0)), string.Empty);
            Equal("RemnantTome", string.Join(" ", news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 1)), string.Empty)), "the campaign's: its tome once its first fight is over");
            news.Reset();
            Equal(0, news.Heard(30, LeaguePiece.VerisiumRemnant, States(("activated", 5)), string.Empty).Count, "a new area knows nothing of the last");
        }

        private static void EachMechanicsChangeIsSaidOnce()
        {
            var news = new LeagueNews();
            string Heard(uint id, LeaguePiece piece, string icon, params (string, long)[] states) => string.Join(" ", news.Heard(id, piece, states, icon));

            Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 0), ("activated", 0));
            Equal("DetonatorReady", Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 1), ("activated", 0)), "every charge placed");
            Equal("Detonated", Heard(1, LeaguePiece.ExpeditionDetonator, "ExpeditionDetonator", ("explosives_placed", 1), ("activated", 1)), "and blown");

            Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 0), ("wave", 0));
            Equal("SimulacrumWave", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 1), ("wave", 2)), "a wave begins");
            Equal(3, LeaguePieces.WaveOf(States(("active", 1), ("wave", 2))), "counted from 1");
            Equal("SimulacrumWaveCleared", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 0), ("wave", 3)), "and is over");
            Equal("SimulacrumOver", Heard(2, LeaguePiece.Simulacrum, string.Empty, ("active", 2), ("wave", 7)), "the Simulacrum's end");

            Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 0));
            Equal("BreachOpen", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 1)), "a breach opens");
            Equal("BreachStabilised", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 2)), "is stabilised");
            Equal("StableBreachCollapsed", Heard(3, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 3)), "and collapses");
            Heard(4, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 1));
            Equal("UnstableBreachCollapsed", Heard(4, LeaguePiece.BreachInitiator, "Breach", ("encounter_state", 4)), "one never stabilised");

            Heard(5, LeaguePiece.VaalBeacon, "IncursionArchitectUpgrade", ("activated", 1), ("open", 0));
            Equal("BeaconReady", Heard(5, LeaguePiece.VaalBeacon, "IncursionArchitectUpgrade", ("activated", 2), ("open", 0)), "a beacon to use");
            Equal("BeaconDone BeaconPortal", Heard(5, LeaguePiece.VaalBeacon, "IncursionEncounterComplete", ("activated", 3), ("open", 1)), "used, with its portal");

            Heard(6, LeaguePiece.RitualAltar, "RitualRune", ("current_state", 2));
            Equal("RitualDone", Heard(6, LeaguePiece.RitualAltar, "RitualRuneFinished", ("current_state", 3)), "a ritual won");
            Equal(string.Empty, Heard(6, LeaguePiece.RitualAltar, "RitualRuneFinished", ("current_state", 4)), "its favours bought are no news");

            Heard(7, LeaguePiece.AbyssPit, "AbyssPitActive");
            Equal("AbyssClosed", Heard(7, LeaguePiece.AbyssPit, LeaguePieces.PitClosedIcon), "a pit closes as its icon dims");
            Equal(string.Empty, Heard(8, LeaguePiece.AbyssPit, LeaguePieces.PitClosedIcon), "one closed before it was seen is not");
        }

        private static void AStrongboxAmbushThenItCanBeOpened()
        {
            var news = new LeagueNews();
            string Heard(uint id, long started, long opened) => string.Join(" ", news.Heard(id, LeaguePiece.Strongbox, States(("started", started), ("opened", opened), ("robot", 0)), "Strongbox"));
            Heard(1, 0, 0);
            Equal("StrongboxAmbush", Heard(1, 1, 0), "opened by the player: the ambush");
            Equal(string.Empty, Heard(1, 1, 0), "said once");
            Equal("StrongboxReady", Heard(1, 0, 0), "the fight over, it can be opened");
            Heard(2, 1, 0);
            Equal(string.Empty, Heard(2, 0, 1), "not when it has opened itself");
        }

        private static void ARogueExileIsSaidOnceAsTheFightStarts()
        {
            var news = new LeagueNews();
            string Heard(uint id, long bar) => string.Join(" ", news.Heard(id, LeaguePiece.RogueExile, States(("boss_life_bar", bar), ("fleeing", 0)), "RogueExile"));
            Equal("RogueExileFight", Heard(1, 1), "already fighting when first seen");
            Heard(1, 0);
            Equal(string.Empty, Heard(1, 1), "once, though the bar comes back");
            Equal(string.Empty, Heard(2, 0), "another not yet engaged");
            Equal("RogueExileFight", Heard(2, 1), "engages");
        }

        private static void TheShrineUsedAndThePossessedAreSaidOnce()
        {
            var news = new LeagueNews();
            Equal(0, news.Shrines(new[] { "shrine_fire_2", "frozen" }).Count, "a shrine's buff there as the area opens is not news");
            Equal("Divine", string.Join(" ", news.Shrines(new[] { "shrine_fire_2", "shrine_godmode_2_1A" })), "a new shrine, by its buff whatever the host's suffix");
            Equal(0, news.Shrines(new[] { "shrine_fire_2", "shrine_godmode_2" }).Count, "not while it lasts");
            Equal(0, news.Shrines(new[] { "shrine_godmode_2_boss" }).Count, "a boss's own copy is no shrine of the player's");
            Equal("Meteoric", string.Join(" ", news.Shrines(new[] { "shrine_fire_2" })), "the same shrine used again");
            Equal(true, news.Possessed(40), "a possessed monster");
            Equal(false, news.Possessed(40), "once");
        }

        private static void TheFogIsSaidOnlyWhileTheMirrorRuns()
        {
            var news = new LeagueNews();
            Equal(null, news.Fog(1), "no mirror walked through: the stat is not news");
            Equal(null, news.Fog(0), "nor its change");
            news.Heard(9, LeaguePiece.DeliriumMirror, States(("interacted", 0)), "AfflictionInitiator");
            Equal("DeliriumBegan", string.Join(" ", news.Heard(9, LeaguePiece.DeliriumMirror, States(("interacted", 1)), "AfflictionInitiator")), "walked through the mirror");
            Equal(true, news.DeliriumRunning, "the encounter runs");
            Equal(LeagueLine.InFog, news.Fog(1), "in the fog");
            Equal(null, news.Fog(1), "said once");
            Equal(null, news.Fog(null), "unread is no change");
            Equal(LeagueLine.OutOfFog, news.Fog(0), "out of it");
            news.Reset();
            Equal(false, news.DeliriumRunning, "a new area");
        }

        private static void WhatAChargeWouldBlowIsSaidOnChangeAndNotTooOften()
        {
            var placing = new PlacementNews();
            var none = Array.Empty<(uint, ExpeditionFind)>();
            var chests = new[] { (1u, ExpeditionFind.Chest), (2u, ExpeditionFind.Chest), (3u, ExpeditionFind.Elite) };
            string Said(IReadOnlyList<(ExpeditionFind Find, int Count)>? finds) =>
                finds == null ? "-" : string.Join(", ", finds.Select(find => $"{find.Count} {find.Find}"));

            Equal("-", Said(placing.Covers(chests, false, 0)), "not placing: silent");
            Equal("-", Said(placing.Covers(none, true, 100)), "placing starts over nothing: nothing to say yet");
            Equal("2 Chest, 1 Elite", Said(placing.Covers(chests, true, 1000)), "by kind and count");
            Equal("-", Said(placing.Covers(chests, true, 1100)), "only on change");
            Equal("-", Said(placing.Covers(chests.Take(1).ToArray(), true, 1000 + PlacementNews.CoverEveryMs - 1)), "not sooner than the limit");
            Equal("1 Chest", Said(placing.Covers(chests.Take(1).ToArray(), true, 1000 + PlacementNews.CoverEveryMs)), "the change once it may be said");
            Equal(string.Empty, Said(placing.Covers(none, true, 3000)), "covering nothing is said too");
            Equal("-", Said(placing.Covers(none, false, 3100)), "leaving placement is silent");

            Equal(true, placing.Refused(true), "a place refused");
            Equal(false, placing.Refused(true), "once");
            Equal(false, placing.Refused(false), "allowed again");

            Equal(null, placing.Placed(new[] { 7u, 8u }), "the charges there as the area opens");
            Equal(3, placing.Placed(new[] { 7u, 8u, 9u }), "a new charge: the count");
            Equal(null, placing.Placed(new[] { 9u }), "one walked away from is not a charge undone");
        }

        private static void LeagueLinesReadAsTheGameWritesThem()
        {
            Localization.Set("es", new());
            try
            {
                Equal("Cubre 2 cofres, 1 élite", Announcements.ExpeditionCovers(new[] { (ExpeditionFind.Chest, 2), (ExpeditionFind.Elite, 1) }), "what a charge would blow");
                Equal("No cubre nada", Announcements.ExpeditionCovers(Array.Empty<(ExpeditionFind, int)>()), "and nothing");
                Equal("Oleada 3 de 7", Announcements.League(LeagueLine.SimulacrumWave, string.Empty, 3), "the Simulacrum's wave");
                Equal("Caja fuerte arcana: emboscada", Announcements.League(LeagueLine.StrongboxAmbush, "Caja fuerte arcana", 0), "a strongbox by the game's name");
                Equal("Caja fuerte: ya se puede abrir", Announcements.League(LeagueLine.StrongboxReady, string.Empty, 0), "or the game's word for one");
                Equal("Cofre de expedición", Announcements.LeaguePieceName(LeaguePiece.ExpeditionMarker, ExpeditionFind.Chest), "a marker by what it buries");
                Equal(true, Announcements.Shrine(ShrineKind.Meteoric).StartsWith("Santuario de meteoritos.", StringComparison.Ordinal), "a shrine by its name first");
            }
            finally
            {
                Localization.Set("en", new());
            }

            Equal("Covers 1 boss, 2 relics", Announcements.ExpeditionCovers(new[] { (ExpeditionFind.Boss, 1), (ExpeditionFind.Relic, 2) }), "in English");
        }
    }
}
