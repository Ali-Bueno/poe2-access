namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;

    /// <summary>Runs every test; exit code 1 when any fails. Run: dotnet run --project tests/Poe2Accessibility.Tests</summary>
    public static class Program
    {
        private static int failures;

        public static int Main()
        {
            Run(nameof(BaselineIsSilentExceptTopLayer), BaselineIsSilentExceptTopLayer);
            Run(nameof(PopupNeedsTwoSnapshots), PopupNeedsTwoSnapshots);
            Run(nameof(ChangedTextIsReportedOnce), ChangedTextIsReportedOnce);
            Run(nameof(CounterTicksAreIgnored), CounterTicksAreIgnored);
            Run(nameof(ClosedPopupIsNewsAgain), ClosedPopupIsNewsAgain);
            Run(nameof(InGameBaseLayersNeverOwnInput), InGameBaseLayersNeverOwnInput);
            Run(nameof(TypingIsReportedAsTheEdit), TypingIsReportedAsTheEdit);
            Run(nameof(ChatReportsOnlyAppendedLines), ChatReportsOnlyAppendedLines);
            Run(nameof(L3GesturesAreToldApart), L3GesturesAreToldApart);
            Run(nameof(StickDirectionsBinInWorldSpace), StickDirectionsBinInWorldSpace);
            Run(nameof(ScannerBrowsesNearestFirstAndFollowsTheEntity), ScannerBrowsesNearestFirstAndFollowsTheEntity);
            Run(nameof(RouteLegsKeepRealTurnsOnly), RouteLegsKeepRealTurnsOnly);
            Run(nameof(LifeWarningsSpeakOncePerFall), LifeWarningsSpeakOncePerFall);
            Run(nameof(EnemyCuesPulseFasterWhenNearAndTakeTurns), EnemyCuesPulseFasterWhenNearAndTakeTurns);
            Run(nameof(EnemyCueSoundsStayInRangeAndDiffer), EnemyCueSoundsStayInRangeAndDiffer);
            Run(nameof(CuesRingOutAndTheMixerRoundsOff), CuesRingOutAndTheMixerRoundsOff);
            Run(nameof(PassageScanMeasuresTheWayAhead), PassageScanMeasuresTheWayAhead);
            Run(nameof(RadarSoundsConfirmedChangesOnly), RadarSoundsConfirmedChangesOnly);
            Run(nameof(RadarSoundsStayInRangeAndDiffer), RadarSoundsStayInRangeAndDiffer);
            Run(nameof(NearbyThingsAreNamedOncePerApproach), NearbyThingsAreNamedOncePerApproach);
            Run(nameof(WallBumpThudsOncePerContact), WallBumpThudsOncePerContact);
            Run(nameof(FriendsGetTheirOwnStream), FriendsGetTheirOwnStream);
            Run(nameof(PhrasesFollowTheGameLanguage), PhrasesFollowTheGameLanguage);
            Run(nameof(ConfirmGlyphMarksTheEntryBesideIt), ConfirmGlyphMarksTheEntryBesideIt);
            Run(nameof(BeaconPointsAlongTheRoute), BeaconPointsAlongTheRoute);
            Run(nameof(ClosedWayLeadsToItsNearestPoint), ClosedWayLeadsToItsNearestPoint);
            Run(nameof(QuestNamesItsTargets), QuestNamesItsTargets);
            Run(nameof(NearbyThingsWaitForTheCooldown), NearbyThingsWaitForTheCooldown);
            Run(nameof(StickClickWhileHoldingL3), StickClickWhileHoldingL3);
            Run(nameof(WallTonesSwellNearWalls), WallTonesSwellNearWalls);
            Run(nameof(NewsFollowItsDeliveryLevel), NewsFollowItsDeliveryLevel);
            Run(nameof(DodgeTimingIsLearntFromBlows), DodgeTimingIsLearntFromBlows);
            Run(nameof(RockStandsAloneWallDoesNot), RockStandsAloneWallDoesNot);
            Run(nameof(ReachPingsOncePerContact), ReachPingsOncePerContact);
            Run(nameof(UnexploredGroundLiesPastTheSeenEdge), UnexploredGroundLiesPastTheSeenEdge);
            Run(nameof(RouteStartsOutsideAPocket), RouteStartsOutsideAPocket);
            Run(nameof(BuffIdsNameAilments), BuffIdsNameAilments);
            Run(nameof(OnlyHarmfulAnimationsWarn), OnlyHarmfulAnimationsWarn);
            Run(nameof(MapMarkersFindTheirCheckpoints), MapMarkersFindTheirCheckpoints);
            Run(nameof(MovedFieldsAreFoundNearestFirst), MovedFieldsAreFoundNearestFirst);
            Run(nameof(MovedControlFieldsAreTrustedOnceTwoControlsAgree), MovedControlFieldsAreTrustedOnceTwoControlsAgree);
            Run(nameof(CheckpointsAreNamedByTheirNeighbour), CheckpointsAreNamedByTheirNeighbour);
            Run(nameof(FarBarrierStaysClosedUntilSeenNear), FarBarrierStaysClosedUntilSeenNear);
            Run(nameof(ScrolledRowsAreNotNews), ScrolledRowsAreNotNews);
            Run(nameof(WrappedLabelsJoin), WrappedLabelsJoin);
            Run(nameof(InlineItemArtIsNamed), InlineItemArtIsNamed);
            Run(nameof(KnownPlacesOutliveTheSession), KnownPlacesOutliveTheSession);
            Run(nameof(RadarLabelsSpeakSpanish), RadarLabelsSpeakSpanish);
            Console.WriteLine(failures == 0 ? "All tests passed." : $"{failures} test(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void BaselineIsSilentExceptTopLayer()
        {
            var differ = new ScreenDiffer();
            var screen = Snapshot(Layer(1, 0, true, Text(10, "Log In")), Layer(2, 1, true, Text(20, "Terms of use")));
            Equal(0, differ.Apply(screen).Count, "first snapshot");
            var changes = differ.Apply(screen);
            Equal(1, changes.Count, "second snapshot");
            Equal(ScreenChangeKind.TopLayerChanged, changes[0].Kind, "kind");
            Equal((IntPtr)2, changes[0].Layer.Address, "the popup above the screen owns input");
        }

        private static void PopupNeedsTwoSnapshots()
        {
            var differ = Settled(out var screen);
            var withPopup = Snapshot(screen.Layers.Append(Layer(3, 2, true, Text(30, "Account"))).ToArray());
            Equal(0, differ.Apply(withPopup).Count, "one sighting");
            var changes = differ.Apply(withPopup);
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.LayerOpened && change.Layer.Address == (IntPtr)3), "opened");
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.TopLayerChanged && change.Layer.Address == (IntPtr)3), "top");
        }

        private static void ChangedTextIsReportedOnce()
        {
            var differ = Settled(out _);
            var warrior = Snapshot(Layer(1, 0, true, Text(10, "Warrior"), Text(11, "I wanted very little")));
            differ.Apply(warrior);
            var changes = differ.Apply(warrior);
            Equal(1, changes.Count, "one change");
            Equal("Warrior|I wanted very little", string.Join("|", changes[0].Texts.Select(text => text.Text)), "texts");
            Equal(0, differ.Apply(warrior).Count, "not repeated");
        }

        private static void CounterTicksAreIgnored()
        {
            var differ = new ScreenDiffer();
            var before = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(12, "Time Left: 0H 0M 5S")));
            differ.Apply(before);
            differ.Apply(before);
            var after = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(12, "Time Left: 0H 0M 4S")));
            differ.Apply(after);
            Equal(0, differ.Apply(after).Count, "digit-only change");
        }

        private static void ClosedPopupIsNewsAgain()
        {
            var differ = Settled(out var screen);
            var withPopup = Snapshot(screen.Layers.Append(Layer(3, 2, false, Text(30, "Notice"))).ToArray());
            differ.Apply(withPopup);
            differ.Apply(withPopup);
            differ.Apply(screen);
            differ.Apply(screen);
            differ.Apply(withPopup);
            Equal(true, differ.Apply(withPopup).Any(change => change.Kind == ScreenChangeKind.LayerOpened), "reopened");
        }

        private static void InGameBaseLayersNeverOwnInput()
        {
            var differ = new ScreenDiffer { FreezeBaseLayers = true };
            var arrival = Snapshot(Layer(1, 0, true, Text(10, "HUD")), Layer(2, 5, true, Text(20, "Chat")));
            differ.Apply(arrival);
            Equal(0, differ.Apply(arrival).Count, "arrival is silent");
            var inventory = Snapshot(arrival.Layers.Append(Layer(3, 3, true, Text(30, "Inventory"))).ToArray());
            differ.Apply(inventory);
            var changes = differ.Apply(inventory);
            Equal(true, changes.Any(change => change.Kind == ScreenChangeKind.TopLayerChanged && change.Layer.Address == (IntPtr)3),
                "a panel opened later owns input even below a base layer");

            // The tutorial box already showing on arrival ("L to move, R to aim") is read; the rest stays silent.
            var tutorial = new ScreenDiffer { FreezeBaseLayers = true, ArrivalLayer = (IntPtr)4 };
            var start = Snapshot(Layer(1, 0, true, Text(10, "HUD")), Layer(4, 13, false, Text(40, "L to move")));
            tutorial.Apply(start);
            var read = tutorial.Apply(start);
            Equal("TextsAppeared 4 L to move", string.Join("|", read.Select(change => $"{change.Kind} {change.Layer.Address} {change.Texts[0].Text}")),
                "the arrival layer's texts are news in the baseline");
        }

        private static void TypingIsReportedAsTheEdit()
        {
            Equal(new TextEdit("i", string.Empty), TextEdit.Between("Yark", "Yarki"), "typed one");
            Equal(new TextEdit(string.Empty, "s"), TextEdit.Between("Yarkidius", "Yarkidiu"), "deleted one");
            Equal(new TextEdit("Yarkidius", string.Empty), TextEdit.Between(string.Empty, "Yarkidius"), "pasted into empty");
            Equal<TextEdit?>(null, TextEdit.Between("abc", "abd"), "replaced");
        }

        private static void ChatReportsOnlyAppendedLines()
        {
            var before = new[] { "a", "b", "c" };
            Equal("d", string.Join("|", ChatDiff.Appended(before, new[] { "a", "b", "c", "d" })), "appended");
            Equal("d|e", string.Join("|", ChatDiff.Appended(before, new[] { "c", "d", "e" })), "window slid");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "a", "b", "c" })), "rebuilt elements, same lines");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "x", "y", "a", "b", "c" })), "history expanded");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "b", "c" })), "list shrank");
            Equal(string.Empty, string.Join("|", ChatDiff.Appended(before, new[] { "p", "q" })), "unrelated list is a new baseline");
            Equal("AFK|AFK", string.Join("|", ChatDiff.Appended(new[] { "AFK", "x" }, new[] { "AFK", "x", "AFK", "AFK" })), "repeated text counts");
        }

        private static void L3GesturesAreToldApart()
        {
            Equal("Tap", Play(Down(0), Up(100), Up(450)), "single tap after the double-tap window");
            Equal("DoubleTap", Play(Down(0), Up(100), Down(200), Up(300), Up(700)), "two quick taps");
            Equal("Hold", Play(Down(0), Down(600), Up(900), Up(1300)), "held past the threshold");
            Equal("FlickUp|FlickRight", Play(Down(0), Stick(100, 0f, 0.9f), Stick(150, 0f, 0f), Stick(200, 0.9f, 0f), Stick(900, 0.9f, 0f), Up(1000), Up(1400)),
                "flicks while held, no hold or tap");
            Equal("Tap", Play(Down(0), Stick(100, 0f, 0.5f), Up(200), Up(600)), "half deflection is no flick");
        }

        private static void StickDirectionsBinInWorldSpace()
        {
            // Isometric diamond: world +X goes right-down on screen, +Y left-down (y down).
            var x = new System.Numerics.Vector2(1f, 0.5f);
            var y = new System.Numerics.Vector2(-1f, 0.5f);
            Equal(0, StickDirection.Octant(x, y, 1f, -1f), "screen right");
            Equal(2, StickDirection.Octant(x, y, -1f, -1f), "screen up");
            Equal(6, StickDirection.Octant(x, y, 1f, 1f), "screen down");
            Equal(7, StickDirection.Octant(x, y, 1f, 0f), "world +X is down-right");
            // 30 degrees above the stick's right in world space: up-right, although the squash draws it flatter.
            Equal(1, StickDirection.Octant(x, y, 0.299f, -1.115f), "binned in world space");
        }

        private static void ScannerBrowsesNearestFirstAndFollowsTheEntity()
        {
            var exit = new ScanEntry(1, ScanCategory.Exits, "Clearfell", 0, 0, 0);
            var near = new ScanEntry(2, ScanCategory.Npcs, "Renly", 0, 0, 0);
            var far = new ScanEntry(3, ScanCategory.Npcs, "Una", 0, 0, 0);
            var browser = new ScanBrowser();
            var list = new[] { exit, near, far };
            Equal("Clearfell True", Said(browser.ChangeEntry(list, 1, 0)), "first flick opens the first category");
            Equal("Renly True", Said(browser.ChangeCategory(list, 1, 100)), "next category, nearest entry");
            Equal("Una False", Said(browser.ChangeEntry(list, 1, 200)), "next entry");
            Equal("Renly False", Said(browser.ChangeEntry(list, 1, 300)), "wraps");
            Equal("Una False", Said(browser.ChangeEntry(list, -1, 400)), "wraps backwards");
            Equal("Renly False", Said(browser.ChangeEntry(new[] { exit, far, near }, 1, 500)), "follows Una after a re-sort");
            Equal("Una False", Said(browser.ChangeEntry(new[] { exit, far, near }, 1, 20000)), "stale: nearest again");
            Equal("Clearfell True", Said(browser.ChangeCategory(list, 1, 20100)), "categories wrap");
            Equal("None False", Said(browser.ChangeCategory(Array.Empty<ScanEntry>(), 1, 20200)), "nothing around");
            browser.ChangeCategory(list, 1, 30000);
            browser.ChangeEntry(list, 1, 30100);
            Equal("Una", browser.Current(list, 90000)?.Name, "the choice stays however long the walk");
            Equal(null, browser.Current(new[] { exit, near }, 90100)?.Name, "gone once its entity is");
        }

        private static void RouteLegsKeepRealTurnsOnly()
        {
            // A* staircase: a slanted run east (two east, one north-east, repeated), a 4-cell jog west, then a run north.
            var route = new List<System.Numerics.Vector2> { new(0, 0) };
            void Walk(int dx, int dy, int times)
            {
                for (var i = 0; i < times; i++)
                {
                    route.Add(route[^1] + new System.Numerics.Vector2(dx, dy));
                }
            }

            for (var i = 0; i < 10; i++)
            {
                Walk(1, 0, 2);
                Walk(1, 1, 1);
            }

            Walk(-1, 0, 4);
            Walk(0, 1, 40);
            var corners = RoutePlanner.Corners(route, 3f);
            Equal(4, corners.Count, "staircase gone, the jog's two corners kept");
            static int Octant(System.Numerics.Vector2 delta) =>
                (int)Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4) + 8) % 8;
            var legs = RoutePlanner.Legs(corners, Octant, 5f);
            Equal("0:32 2:40", string.Join(" ", legs.Select(leg => $"{leg.Direction}:{Math.Round(leg.Length)}")), "the jog is dropped");
            Equal(1, RoutePlanner.Legs(RoutePlanner.Corners(route.Take(3).ToList(), 3f), Octant, 5f).Count, "a short route keeps its way");
        }

        private static void EnemyCuesPulseFasterWhenNearAndTakeTurns()
        {
            var scheduler = new EnemyCueScheduler(4, 100, 150, 600);
            var near = new CueTarget(1, CueSynth.Layer.None, 0.1f, 0f, 1f);
            var far = new CueTarget(2, CueSynth.Layer.None, -0.9f, 0f, 0f);
            List<uint> Run(IReadOnlyList<CueTarget> targets, long fromMs, long toMs)
            {
                var played = new List<uint>();
                for (var now = fromMs; now < toMs; now += 10)
                {
                    if (scheduler.Next(now, targets) is { } target)
                    {
                        played.Add(target.Id);
                    }
                }

                return played;
            }

            var alone = Run(new[] { near }, 0, 1000);
            Equal(7, alone.Count, "contact pulses every 150 ms in a second");
            scheduler.Reset();
            var both = Run(new[] { near, far }, 0, 1200);
            Equal(true, both.Count(id => id == 1) > both.Count(id => id == 2) * 2, "the near one pulses much more");
            Equal(true, both.Contains(2u), "the far one still gets turns");

            scheduler.Reset();
            var boss = new CueTarget(3, CueSynth.Layer.Boss, 0.5f, -0.5f, 0.2f);
            var crowd = Enumerable.Range(10, 8).Select(id => new CueTarget((uint)id, CueSynth.Layer.None, 0f, 0.1f, 0.9f)).ToArray();
            var mixed = Run(crowd.Append(boss).ToArray(), 0, 1000);
            Equal(true, mixed.Contains(3u), "a far boss is heard among a near crowd");
            Equal(4, mixed.Distinct().Count(), "four streams at most");

            scheduler.Reset();
            Run(new[] { near }, 0, 50);
            var swapped = Run(new[] { near with { Id = 5 } }, 50, 140);
            Equal(0, swapped.Count, "a new nearest enemy keeps the stream's cadence");
        }

        private static void EnemyCueSoundsStayInRangeAndDiffer()
        {
            const int rate = 44100;
            var growl = CueSynth.Enemy(CueSynth.Layer.None, rate);
            var elite = CueSynth.Enemy(CueSynth.Layer.Elite, rate);
            var boss = CueSynth.Enemy(CueSynth.Layer.Boss, rate);
            foreach (var (name, sound) in new[] { ("growl", growl), ("elite", elite), ("boss", boss) })
            {
                Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"{name} lasts 50-250 ms");
                Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{name} stays within full scale");
                Equal(true, MathF.Abs(sound[^1]) < 0.05f, $"{name} ends near silence");
            }

            int Crossings(float[] sound) => sound.Zip(sound.Skip(1)).Count(pair => pair.First < 0 != pair.Second < 0);
            Equal(true, Crossings(elite) > Crossings(growl) * 2, "the elite layer is brighter than the growl");
            Equal(true, boss.Length > growl.Length, "the boss ring rings past the growl");
        }

        private static void CuesRingOutAndTheMixerRoundsOff()
        {
            const int rate = 44100;
            foreach (var (name, sound) in new[] { ("beacon", CueSynth.Ping(rate)), ("dodge", CueSynth.Whoosh(rate)) })
            {
                Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{name} stays within full scale");
                Equal(true, MathF.Abs(sound[^1]) < 0.01f, $"{name}'s room tail fades to silence");
                Equal(true, sound.Length < rate / 2, $"{name} is over within half a second");
            }

            Equal(0.5f, CueSynth.Limit(0.5f), "below the knee the mix is untouched");
            Equal(true, CueSynth.Limit(3f) <= 1f && CueSynth.Limit(-3f) >= -1f, "a pile-up never passes full scale");
            Equal(true, CueSynth.Limit(0.95f) > CueSynth.Limit(0.9f), "louder stays louder");
        }

        /// <summary>A walkable nibble grid (two cells per byte) with the given cells blocked.</summary>
        private static byte[] Grid(int columns, int rows, params (int X, int Y)[] blocked)
        {
            var grid = Enumerable.Repeat((byte)0x11, columns / 2 * rows).ToArray();
            foreach (var (x, y) in blocked)
            {
                grid[(y * columns / 2) + (x / 2)] &= (byte)((x & 1) == 0 ? 0xF0 : 0x0F);
            }

            return grid;
        }

        private static void PassageScanMeasuresTheWayAhead()
        {
            var start = new System.Numerics.Vector2(20.5f, 50.5f);
            var east = new System.Numerics.Vector2(1, 0);
            var open = PassageScan.Scan(Grid(100, 100), 50, start, east, 2, 40, 20);
            Equal(true, open.Depth >= 39 && !open.Unknown, "open floor runs to the reach");
            var wall = Enumerable.Range(45, 11).Select(y => (30, y)).ToArray();
            var blocked = PassageScan.Scan(Grid(100, 100, wall), 50, start, east, 2, 40, 20);
            Equal(true, blocked.Depth is > 8 and < 10, $"a wall 10 cells ahead stops it ({blocked.Depth})");
            var post = PassageScan.Scan(Grid(100, 100, (30, 50)), 50, start, east, 2, 40, 20);
            Equal(true, post.Depth >= 39, "a one-cell post is walked around inside the lane");
            var edge = PassageScan.Scan(Grid(100, 100), 50, new System.Numerics.Vector2(90.5f, 50.5f), east, 2, 40, 20);
            Equal(true, edge.Unknown && edge.Depth < 20, "the grid's end is unknown, not a wall");
        }

        private static void RadarSoundsConfirmedChangesOnly()
        {
            var sensor = new RadarSensor(new RadarThresholds(20, 15, 3, 200, 100, 500));
            var axis = new System.Numerics.Vector2(1, 0);
            var open = new PassageScan.Result(40, false);
            var wall = new PassageScan.Result(12, false);
            var x = 0f;
            RadarReading? Step(long now, PassageScan.Result scan, float move = 1f, bool exit = false)
            {
                x += move;
                return sensor.Update(now, new System.Numerics.Vector2(x, 0), axis, scan, exit);
            }

            Equal(null, Step(0, open), "the first reading is a silent baseline");
            Equal(null, Step(50, open), "unchanged");
            Equal(null, Step(100, wall), "a wall must be confirmed first");
            Equal(null, Step(150, wall), "one cell walked");
            Equal(null, Step(200, wall), "two cells walked");
            Equal(RadarReading.Wall, Step(250, wall), "confirmed after three cells");
            Equal(null, Step(275, wall), "said once");
            Equal(null, Step(300, new PassageScan.Result(17, false)), "between close and open the wall holds");
            // The wall heard stands at 18; backing away until it is farther than the opening depth reads open.
            foreach (var at in new long[] { 350, 400, 425, 450, 475 })
            {
                x -= 3f;
                Equal(null, sensor.Update(at, new System.Numerics.Vector2(x, 0), axis, new PassageScan.Result(18 - x, false), false), $"backing away to {x} is not an opening");
            }

            Equal(null, Step(500, open, 0f, true), "an exit waits for confirmation");
            Equal(null, Step(600, open, 0f, true), "still standing: not yet");
            Equal(RadarReading.Exit, Step(700, open, 0f, true), "standing still confirms after a moment");
            Equal(null, Step(850, open, 1f), "open again");
            Equal(null, Step(900, open, 1f), "open again");
            Equal(null, Step(950, open, 1f), "open is confirmed but held by the opening interval");
            Equal(RadarReading.Open, Step(1350, open, 1f), "then said");
            sensor.Reset();
            Equal(null, Step(1400, wall), "after a reset the first reading is silent again");
        }

        private static void RadarSoundsStayInRangeAndDiffer()
        {
            const int rate = 44100;
            foreach (var reading in new[] { RadarReading.Open, RadarReading.Exit, RadarReading.Wall })
            {
                foreach (var direction in Enum.GetValues<RadarSynth.Direction>())
                {
                    var sound = RadarSynth.Cue(reading, direction, rate);
                    Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"{reading} {direction} lasts 50-250 ms");
                    Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{reading} {direction} within full scale");
                }
            }

            int Crossings(float[] sound) => sound.Zip(sound.Skip(1)).Count(pair => pair.First < 0 != pair.Second < 0);
            var exitUp = RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Up, rate);
            var exitDown = RadarSynth.Cue(RadarReading.Exit, RadarSynth.Direction.Down, rate);
            var wallUp = RadarSynth.Cue(RadarReading.Wall, RadarSynth.Direction.Up, rate);
            Equal(true, Crossings(exitDown) < Crossings(exitUp), "below the player sounds lower");
            Equal(true, Crossings(exitUp) > Crossings(wallUp) * 3, "the exit tin is far above the wall tock");
        }

        private static void NearbyThingsAreNamedOncePerApproach()
        {
            var tracker = new ProximityTracker(200, 300);
            var checkpoint = new ScanEntry(1, ScanCategory.Waypoints, "Checkpoint", 1000, 0, 0);
            var stash = new ScanEntry(2, ScanCategory.Objects, "Stash", 1150, 0, 0);
            string Walk(float x) => string.Join(", ", tracker.Update(x, 0, new[] { checkpoint, stash }).Select(entry => entry.Name));
            Equal(string.Empty, Walk(0), "far away");
            Equal("Checkpoint", Walk(820), "walked up to the checkpoint");
            Equal("Stash", Walk(950), "then the stash comes within reach");
            Equal(string.Empty, Walk(1000), "standing on the checkpoint says nothing new");
            Equal(string.Empty, Walk(760), "stepping back inside the re-arm distance stays quiet");
            Equal(string.Empty, Walk(500), "walked away");
            Equal("Checkpoint, Stash", Walk(1050), "back again: both, nearest first");
        }

        private static void BeaconPointsAlongTheRoute()
        {
            // A 20x20 open grid (two cells per byte, 0x11 = both walkable) with a wall at x = 10 except a gap at y = 18.
            const int size = 20;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            for (var y = 0; y < size; y++)
            {
                if (y != 18)
                {
                    grid[(y * row) + 5] &= 0xF0; // x = 10 is the low nibble of byte 5
                }
            }

            var route = new List<System.Numerics.Vector2>();
            for (var y = 2; y <= 18; y++)
            {
                route.Add(new System.Numerics.Vector2(5, y));
            }

            for (var x = 6; x <= 15; x++)
            {
                route.Add(new System.Numerics.Vector2(x, 18));
            }

            var player = new System.Numerics.Vector2(5, 2);
            var from = BeaconMath.NearestIndex(route, player);
            Equal(0, from, "the player stands on the first cell");
            var aim = BeaconMath.AimPoint(grid, row, route, from, player, 25f);
            Equal(true, aim.X <= 9, "the ping never points through the wall");
            Equal(true, aim.Y > 10, "it looks ahead down the corridor");
            Equal(true, BeaconMath.IntervalMs(50f) == BeaconMath.NearIntervalMs, "close: fastest");
            Equal(true, BeaconMath.IntervalMs(10000f) == BeaconMath.FarIntervalMs, "far: slowest");
            Equal(true, BeaconMath.IntervalMs(1000f) < BeaconMath.IntervalMs(2000f), "slower when farther");
            var (panRight, _) = BeaconMath.Voice(new System.Numerics.Vector2(1, 0));
            var (_, pitchUp) = BeaconMath.Voice(new System.Numerics.Vector2(0, -1));
            var (_, pitchDown) = BeaconMath.Voice(new System.Numerics.Vector2(0, 1));
            Equal(true, panRight > 0.9f && pitchUp > 1f && pitchDown < 1f, "right is right, up is higher, down is lower");
        }

        private static void ClosedWayLeadsToItsNearestPoint()
        {
            // 20x20 open grid split by a full wall at x = 10: the right half cannot be reached.
            const int size = 20;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            for (var y = 0; y < size; y++)
            {
                grid[(y * row) + 5] &= 0xF0;
            }

            var start = new System.Numerics.Vector2(2, 10);
            var goal = new System.Numerics.Vector2(17, 10);
            Equal(true, RoutePlanner.FindRoute(grid, row, start, goal) == null, "no full route");
            var partial = RoutePlanner.FindRoute(grid, row, start, goal, true, out var reached);
            Equal(false, reached, "the goal is not reached");
            Equal(true, partial != null && partial[^1].X == 9, "the way ends at the wall, nearest to the goal");
        }

        private static void RockStandsAloneWallDoesNot()
        {
            // 30x30 open grid: a 2x2 rock ahead (x 10-11, y 14-15) and, elsewhere, a wall across the whole map at y = 25.
            const int size = 30;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            void Block(int x, int y) => grid[(y * row) + (x / 2)] &= (byte)((x & 1) == 0 ? 0xF0 : 0x0F);
            foreach (var (x, y) in new[] { (10, 14), (11, 14), (10, 15), (11, 15) })
            {
                Block(x, y);
            }

            for (var x = 0; x < size; x++)
            {
                Block(x, 25);
            }

            var right = new System.Numerics.Vector2(1, 0);
            var down = new System.Numerics.Vector2(0, 1);
            Equal(true, PassageScan.IsSmallObstacle(grid, row, new System.Numerics.Vector2(5.5f, 14.5f), right, 4f, 5), "a rock standing alone");
            Equal(false, PassageScan.IsSmallObstacle(grid, row, new System.Numerics.Vector2(20.5f, 20.5f), down, 4f, 5), "a wall across the map");
        }

        private static void RouteStartsOutsideAPocket()
        {
            // 60x60 open ground; the player stands on a blocked block (x 20-24, y 20-24) whose centre cell (22,22) is a
            // walkable pocket of one cell. The nearest walkable cell is that pocket; the route must start outside it.
            const int size = 60;
            var row = size / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * size).ToArray();
            void Set(int x, int y, bool open) => grid[(y * row) + (x / 2)] = (byte)(open
                ? grid[(y * row) + (x / 2)] | ((x & 1) == 0 ? 0x01 : 0x10)
                : grid[(y * row) + (x / 2)] & ((x & 1) == 0 ? 0xF0 : 0x0F));
            for (var y = 20; y <= 24; y++)
            {
                for (var x = 20; x <= 24; x++)
                {
                    Set(x, y, false);
                }
            }

            Set(22, 22, true);
            var route = RoutePlanner.FindRoute(grid, row, new System.Numerics.Vector2(22.4f, 22.4f), new System.Numerics.Vector2(50, 50), false, out var reached);
            Equal(true, route != null && reached, "a route from the ground around the pocket");
        }

        private static void RadarLabelsSpeakSpanish()
        {
            Localization.Detect("es");
            Equal("Escaleras hacia arriba", Announcements.LandmarkLabel("Stairs Up"), "a whole label");
            Equal("Crop Circle (gema de habilidad de nivel 4)", Announcements.LandmarkLabel("Crop Circle (Skill Gem Level 4)"), "a reward note, the name kept");
            Equal("Sisters of Garukhan (10% de resistencia al rayo)", Announcements.LandmarkLabel("Sisters of Garukhan (10% Lightning Res)"), "a resistance");
            Equal("Kabala (+2 puntos de pasiva)", Announcements.LandmarkLabel("Kabala (+2 Passive)"), "passive points");
            Localization.Detect("en");
            Equal("Stairs Up", Announcements.LandmarkLabel("Stairs Up"), "English untouched");
        }

        private static void KnownPlacesOutliveTheSession()
        {
            var file = System.IO.Path.GetTempFileName();
            try
            {
                var first = new KnownPlaces(file);
                Equal(true, first.Add("kehgan", "camp|waypoint|Transporter|0|0"), "a place is new the first time");
                Equal(false, first.Add("kehgan", "camp|waypoint|Transporter|0|0"), "then known");
                var next = new KnownPlaces(file);
                Equal(false, next.Add("kehgan", "camp|waypoint|Transporter|0|0"), "still known in the next session");
                Equal(true, next.Add("yarkbruja", "camp|waypoint|Transporter|0|0"), "another character has not seen it");
            }
            finally
            {
                System.IO.File.Delete(file);
            }
        }

        private static void InlineItemArtIsNamed()
        {
            ItemNames.Set(new() { ["en"] = new() { ["CurrencyIdentification"] = "Scroll of Wisdom", ["CurrencyUpgradeToMagic"] = "Orb of Transmutation" } });
            const string identify = "Identify <<itemvisual:CurrencyIdentification>> x 1";
            Equal(true, ControllerGlyphs.HasUnnamedItemArt(identify), "the identify legend shows its scroll only as art");
            Equal("Identify Scroll of Wisdom x 1", ControllerGlyphs.Plain(identify), "the art is named in place");
            const string named = "Use an <<itemvisual:CurrencyUpgradeToMagic>> <currencyitem>{Orb of Transmutation}";
            Equal(false, ControllerGlyphs.HasUnnamedItemArt(named), "art followed by its own name is not named twice");
            Equal("Use an Orb of Transmutation", ControllerGlyphs.Render(named), "no stray brackets where the art was");
            Equal(false, ControllerGlyphs.HasUnnamedItemArt("Identify <<itemvisual:Unknown>> x 1"), "unknown art stays as the game strips it");
            ItemNames.Set(new());
        }

        private static void WrappedLabelsJoin()
        {
            Equal("Ocultar las pestañas de Solo retirar", Announcements.Flatten("Ocultar las pestañas de \nSolo retirar"), "link word");
            Equal("Socket a Support Gem of any colour", Announcements.Flatten("Socket a Support Gem of any\ncolour"), "lower case");
            Equal("1 Strength = +2 Life. 5 Strength = +1 Damage", Announcements.Flatten("1 Strength = +2 Life\n5 Strength = +1 Damage"), "two lines");
        }

        private static void ScrolledRowsAreNotNews()
        {
            var differ = Settled(out _);
            var scrolled = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch")));
            differ.Apply(scrolled);
            var changes = differ.Apply(scrolled);
            Equal(true, changes.Single().Scrolled, "a row came in as another left");

            var grown = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch"), Text(13, "Details")));
            differ.Apply(grown);
            differ.Apply(grown);
            differ.Apply(grown);
            var details = Snapshot(Layer(1, 0, true, Text(11, "All I need"), Text(12, "Witch"), Text(13, "Details"), Text(14, "More")));
            differ.Apply(details);
            Equal(false, differ.Apply(details).Single().Scrolled, "a text added with nothing gone");
        }

        private static void FarBarrierStaysClosedUntilSeenNear()
        {
            // 400x10 open strip; a barrier at cells 380-381 reads closed (believed at any distance), then
            // walkable again while the player is still at x 10, past the network bubble: closed until they stand at x 300.
            const int row = 200;
            var open = Enumerable.Repeat((byte)0x11, row * 10).ToArray();
            var shut = (byte[])open.Clone();
            for (var y = 0; y < 10; y++)
            {
                shut[(y * row) + 190] = 0;
            }

            var memory = new BarrierMemory();
            memory.Observe(open, open, row, 10, 5);
            memory.Observe(shut, open, row, 10, 5);
            memory.Observe(open, open, row, 10, 5);
            var grid = (byte[])open.Clone();
            memory.Apply(grid);
            Equal(false, RoutePlanner.IsWalkable(grid, row, 380, 5), "seen open from afar: still closed");
            Equal(20, memory.ClosedCount, "remembered cells");
            memory.Observe(open, open, row, 300, 5);
            grid = (byte[])open.Clone();
            memory.Apply(grid);
            Equal(true, RoutePlanner.IsWalkable(grid, row, 380, 5), "seen open from near by");

            // Already closed at the first sample (the plugin started beside it): open in the grid as loaded is enough.
            var fresh = new BarrierMemory();
            fresh.Observe(shut, open, row, 10, 5);
            fresh.Observe(open, open, row, 10, 5);
            grid = (byte[])open.Clone();
            fresh.Apply(grid);
            Equal(false, RoutePlanner.IsWalkable(grid, row, 381, 5), "closed before the first sample");
        }

        private static void UnexploredGroundLiesPastTheSeenEdge()
        {
            // 400x200 open field split by a wall at x = 200 with no gap; the player stands at (40, 100).
            const int width = 400, height = 200;
            var row = width / 2;
            var grid = Enumerable.Repeat((byte)0x11, row * height).ToArray();
            for (var y = 0; y < height; y++)
            {
                grid[(y * row) + 100] = 0; // cells 200 and 201
            }

            var map = new ExplorationMap();
            map.Visit(new System.Numerics.Vector2(40, 100), width, height);
            var targets = map.Frontier(grid, row, new System.Numerics.Vector2(40, 100), 8);
            Equal(true, targets.Count > 0, "unseen ground east of the start is offered");
            Equal(true, targets.All(cell => cell.X < 200), "nothing past the unbroken wall");
            Equal(true, targets.All(cell => System.Numerics.Vector2.Distance(cell, new System.Numerics.Vector2(40, 100)) > ExplorationMap.SeenRadiusCells - ExplorationMap.BlockCells),
                "targets lie at the seen edge");
            for (var x = 25; x < 200; x += 50)
            {
                for (var y = 25; y < 200; y += 50)
                {
                    map.Visit(new System.Numerics.Vector2(x, y), width, height);
                }
            }

            Equal(0, map.Frontier(grid, row, new System.Numerics.Vector2(120, 100), 8).Count, "the west field walked through: nothing left");
        }

        private static void ReachPingsOncePerContact()
        {
            // Reach 40, release 25: a fight swaying around the edge pings once; it arms again only past 65 or with no enemy.
            var gate = new ReachGate();
            var pings = new[] { 120f, 50f, 38f, 45f, 30f, 60f, 70f, 39f, (float?)null, 10f }
                .Select(gap => gate.Update(gap, 40f, 25f) ? "P" : ".");
            Equal("..P....P.P", string.Join(string.Empty, pings), "one ping per contact, re-armed past reach + release or with no enemy");
        }

        private static void DodgeTimingIsLearntFromBlows()
        {
            var timings = new DodgeTimings();
            var key = DodgeTimings.Key("Metadata/Monsters/MudGolem/MudGolemWet1@3", 13);
            Equal("Metadata/Monsters/MudGolem/MudGolemWet1|13", key, "the kind without its level");
            Equal(true, timings.Delay(key) == null, "unknown until it hits");
            Equal(1000L, DodgeTimings.WarnAt(1000, null, 500), "unknown: at the wind-up");
            foreach (var delay in new[] { 1170, 1150, 3900, 1180, 1160 })
            {
                timings.Learn(key, delay);
            }

            Equal(1170, timings.Delay(key) ?? 0, "the median ignores a blow from another monster");
            Equal(1670L, DodgeTimings.WarnAt(1000, timings.Delay(key), 500), "half a second before the blow");
            Equal(1000L, DodgeTimings.WarnAt(1000, 300, 500), "a blow quicker than the lead: at once");
            timings.Learn(key, 9000);
            Equal(1170, timings.Delay(key) ?? 0, "a blow long after the start is not that attack's");
            var copy = DodgeTimings.FromLines(timings.ToLines());
            Equal(timings.Delay(key), copy.Delay(key), "saved and loaded");
            var seeded = DodgeTimings.FromLines(new[] { $"{key}=900,900,900" }.Concat(new[] { $"{key}=1200,1210,1190" }));
            Equal(1190, seeded.Delay(key) ?? 0, "the player's own blows outweigh the shipped ones");
            var filed = new DodgeTimings();
            filed.AddHitTimes(new[] { "# comment", $"{key}=1169/2333", "Metadata/Monsters/X|1=20/100", "broken line" });
            Equal(1169, filed.Delay(key) ?? 0, "before any blow, the game files' hit time");
            Equal(2333, filed.Swing(key) ?? 0, "and how long one swing lasts");
            Equal(true, filed.Delay("Metadata/Monsters/X|1") == null, "a hit sooner than any blow is not believed");
            filed.Learn(key, 1150);
            Equal(1150, filed.Delay(key) ?? 0, "a blow felt outweighs the files");
        }

        private static void NewsFollowItsDeliveryLevel()
        {
            var output = new RecordingOutput();
            SpeechPriority.Chosen = null;
            output.Say(SpeechKind.StatusBegins, "a");
            output.Say(SpeechKind.GroundItems, "b");
            output.Say(SpeechKind.LifeAndMana, "c", urgent: true);
            output.Say(SpeechKind.LifeAndMana, "d");
            SpeechPriority.Chosen = _ => SpeechPriority.CutsIn;
            output.Say(SpeechKind.GroundItems, "e");
            SpeechPriority.Chosen = _ => "nonsense";
            output.Say(SpeechKind.Quest, "f");
            SpeechPriority.Chosen = null;
            Equal("urge a|b False|urge c|d False|e True|f False", string.Join("|", output.Calls), "each kind at its level, urgent news holds, defaults for unknown values");
        }

        private sealed class RecordingOutput : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"{text} {interrupt}");

            public void Urge(string text, int holdMs = 1500) => this.Calls.Add($"urge {text}");

            public void Repeat()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }

        private static void WallTonesSwellNearWalls()
        {
            Equal(0f, RadarSynth.WallToneGain(20f, 20f), "open to the range: silent");
            Equal(1f, RadarSynth.WallToneGain(0f, 20f), "touching the wall: full");
            Equal(true, RadarSynth.WallToneGain(2f, 20f) > RadarSynth.WallToneGain(10f, 20f), "closer is louder");
            var loop = RadarSynth.WallTone(RadarSynth.Direction.Up, 44100);
            Equal(true, MathF.Abs(loop[0] - loop[^1]) < 0.08f, "the loop has no seam");
            static float Rms(float[] samples) => MathF.Sqrt(samples.Sum(s => s * s) / samples.Length);
            Equal(true, MathF.Abs(Rms(loop) - 0.04f) < 0.001f, "a touching wall sits at -28 dBFS RMS (D2R)");
            var behind = Rms(RadarSynth.WallTone(RadarSynth.Direction.Down, 44100)) / Rms(loop);
            Equal(true, MathF.Abs(behind - 1.413f) < 0.01f, "the band behind is lifted 3 dB");
        }

        private static void QuestNamesItsTargets()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Exits, "Campamento de Sierraclara", 0, 0, 0),
                new ScanEntry(2, ScanCategory.Enemies, "El molinero hinchado, único", 0, 0, 0),
                new ScanEntry(3, ScanCategory.Enemies, "Ahogado", 0, 0, 0),
                new ScanEntry(4, ScanCategory.Waypoints, "Punto de control", 0, 0, 0),
                new ScanEntry(5, ScanCategory.Npcs, "Renly", 0, 0, 0),
            };
            string Names(params string[] texts) => string.Join(" | ", QuestMatcher.Targets(entries, texts).Select(entry => entry.Name));
            Equal("Campamento de Sierraclara | El molinero hinchado, único", Names("Llegar a Sierraclara", "Busca al Molinero y encuentra refugio en Sierraclara"), "proper nouns of the objective");
            Equal(string.Empty, Names("Los ahogados observan entre las lodosas ramas"), "ordinary monsters never");
            Equal("Renly", Names("Talk to Renly"), "a name inside the sentence");
            Equal(string.Empty, Names("Renly"), "a label equal to the name is its own nameplate");
            Equal(true, QuestMatcher.Targets(entries, new[] { "Talk to Renly" }).All(entry => entry.Category == ScanCategory.Quest), "copies in the Quest category");
            Equal(string.Empty, string.Join(" | ", QuestMatcher.Targets(entries, new[] { "Suelo traicionero", "Registra Sierraclara para encontrar la entrada al Lodazal" }, "Sierraclara").Select(entry => entry.Name)),
                  "the area the player searches is not a destination");
            Equal("Renly, con misión", string.Join(" | ", QuestMatcher.Targets(new[] { new ScanEntry(6, ScanCategory.Npcs, "Renly, con misión", 0, 0, 0, "npc_quest") },
                                                                                new[] { "Habla con Renly" }).Select(entry => entry.Name)),
                  "an NPC named with its quest marker");
        }

        private static void NearbyThingsWaitForTheCooldown()
        {
            var tracker = new ProximityTracker(200, 300, 60000);
            var checkpoint = new ScanEntry(1, ScanCategory.Waypoints, "Checkpoint", 0, 0, 0);
            string At(float x, long now) => string.Join(", ", tracker.Update(x, 0, new[] { checkpoint }, now).Select(entry => entry.Name));
            Equal("Checkpoint", At(100, 0), "first approach");
            Equal(string.Empty, At(500, 5000), "stepped away while fighting");
            Equal(string.Empty, At(100, 9000), "back within the minute: quiet");
            Equal(string.Empty, At(500, 30000), "away again");
            Equal("Checkpoint", At(100, 70000), "after the cooldown it is news again");
        }

        private static void StickClickWhileHoldingL3()
        {
            var gestures = new L3Gestures();
            var seen = new List<PadGesture>();
            seen.AddRange(gestures.Update(0, true, 0, 0));
            seen.AddRange(gestures.Update(100, true, 0, 0, true));
            seen.AddRange(gestures.Update(200, true, 0, 0, true));
            seen.AddRange(gestures.Update(700, true, 0, 0));
            seen.AddRange(gestures.Update(800, false, 0, 0));
            seen.AddRange(gestures.Update(1300, false, 0, 0));
            Equal("StickClick", string.Join(",", seen), "one click, no hold or tap after it");
        }

        private static void MovedFieldsAreFoundNearestFirst()
        {
            Equal("152,160,144,168,136", string.Join(",", OffsetSearch.Outward(0x98, 0x10, 8)), "where measured first, then one step either side");
            Equal("8,16,0,24", string.Join(",", OffsetSearch.Outward(8, 0x10, 8, 0)), "never below the floor");
            Equal(0x98, OffsetSearch.First(0x98, 0x100, 8, offset => offset == 0x98 || offset == 0xA0) ?? -1, "kept where measured when it still fits");
            Equal(0xA0, OffsetSearch.First(0x98, 0x100, 8, offset => offset is 0xA0 or 0x40) ?? -1, "the nearest fit wins");
            Equal(-8, OffsetSearch.First(0, 0x100, 8, shift => shift == -8, -0x100) ?? 1, "a shift may be negative");
            Equal(true, OffsetSearch.First(0x98, 0x20, 8, offset => offset == 0x200) == null, "nothing within reach: lost");
        }

        private static void MovedControlFieldsAreTrustedOnceTwoControlsAgree()
        {
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var controls = new ControlFields(new CompatibilityReport(log), () => 0);
            static bool Moved(int shift) => shift == 0x10;
            Equal(0x10, controls.Shift(ControlGroup.Slider, 1, Moved), "a moved slider is read where it fits");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 1, Moved), "the same slider again");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 2, Moved), "a second slider agrees");
            Equal(0x10, controls.Shift(ControlGroup.Slider, 3, _ => false), "trusted once two agreed, even on an unreadable one");
            Equal(0, controls.Current(ControlGroup.ComboBox), "another group is not moved by it");
            var followed = new ControlFields(new CompatibilityReport(log), () => 8);
            Equal(8, followed.Shift(ControlGroup.ComboBox, 1, shift => shift is 0 or 8), "tried first as far as the label text moved");
            Equal(8, followed.Current(ControlGroup.Inventory), "an unjudged group follows the label text");
        }

        private static void MapMarkersFindTheirCheckpoints()
        {
            static System.Numerics.Vector2 V(float x, float y) => new(x, y);

            // The map draws the world 20 times smaller; the places come in another order than the markers.
            var places = new[] { V(-400, 600), V(1000, 0), V(0, -1600) };
            var markers = new[] { V(50, 0), V(0, -80), V(-20, 30) };
            Equal("1,2,0", string.Join(",", MapPlaces.Assign(markers, places)), "each marker to the place in its direction and distance");
            Equal("1", string.Join(",", MapPlaces.Assign(new[] { V(30, 4) }, places)), "a lone marker takes the place it points at");
            Equal("-1", string.Join(",", MapPlaces.Assign(new[] { V(-30, -30) }, new[] { V(100, 0) })), "nothing in its direction: unnamed");
            Equal("0,-1", string.Join(",", MapPlaces.Assign(new[] { V(50, 0), V(0, 50) }, new[] { V(1000, 0) })), "a checkpoint never seen stays unnamed");
            Equal(string.Empty, string.Join(",", MapPlaces.Assign(Array.Empty<System.Numerics.Vector2>(), places)), "no markers");
        }

        private static void CheckpointsAreNamedByTheirNeighbour()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Waypoints, "Punto de control", 0, 0, 0, "checkpoint"),
                new ScanEntry(2, ScanCategory.Enemies, "Ahogado", 50, 0, 0),
                new ScanEntry(3, ScanCategory.Exits, "Campamento de Sierraclara", 900, 0, 0, "exit"),
                new ScanEntry(4, ScanCategory.Npcs, "Una", 1500, 0, 0),
                new ScanEntry(5, ScanCategory.Objects, "Barrera cerrada", 100, 0, 0, "barrier"),
            };
            var origin = new System.Numerics.Vector2(0, 0);
            Equal("Campamento de Sierraclara", MapPlaces.NamedNear(origin, entries, 2000)?.Name ?? "none", "the nearest named place, not a monster, barrier or checkpoint");
            Equal("none", MapPlaces.NamedNear(origin, entries, 500)?.Name ?? "none", "nothing named close enough");
        }

        private static void OnlyHarmfulAnimationsWarn()
        {
            var names = new Dictionary<int, string>
            {
                [1] = "Submerge", [2] = "Hide", [3] = "SpellBuff", [4] = "GroundSlam", [5] = "SpellProjectile", [6] = "LeapAttack",
                [7] = "BossMovement", [8] = "HeavyStrike", [9] = "DoryaniHumanBossCrystalToss", [10] = "Idle",
            };
            string? NameOf(int id) => names.TryGetValue(id, out var name) ? name : null;
            Equal(DodgeThreat.None, DodgeThreats.Of(1, NameOf), "going under ground hurts no one");
            Equal(DodgeThreat.None, DodgeThreats.Of(2, NameOf), "hiding");
            Equal(DodgeThreat.None, DodgeThreats.Of(3, NameOf), "a self buff");
            Equal(DodgeThreat.Melee, DodgeThreats.Of(4, NameOf), "a slam lands beside the monster");
            Equal(DodgeThreat.Ranged, DodgeThreats.Of(5, NameOf), "a projectile crosses the distance");
            Equal(DodgeThreat.Ranged, DodgeThreats.Of(6, NameOf), "a leap reaches from afar though it says attack");
            Equal(DodgeThreat.None, DodgeThreats.Of(7, NameOf), "a boss walking");
            Equal(DodgeThreat.Melee, DodgeThreats.Of(8, NameOf), "a heavy strike");
            Equal(DodgeThreat.Ranged, DodgeThreats.Of(9, NameOf), "a toss, not a cry");
            Equal(DodgeThreat.None, DodgeThreats.Of(10, NameOf), "standing");
            Equal(DodgeThreat.Ranged, DodgeThreats.Of(99, NameOf), "an id the table lacks (a game update) still warns");
            Equal(DodgeThreat.None, DodgeThreats.Of(0, NameOf), "no animation");
        }

        private static void BuffIdsNameAilments()
        {
            Equal(StatusAilment.Chilled, StatusAilments.FromBuffId("chilled") ?? StatusAilment.Cursed, "chill");
            Equal(StatusAilment.Frozen, StatusAilments.FromBuffId("frozen") ?? StatusAilment.Cursed, "freeze");
            Equal(StatusAilment.Poisoned, StatusAilments.FromBuffId("poisoned") ?? StatusAilment.Cursed, "poison");
            Equal(StatusAilment.Electrocuted, StatusAilments.FromBuffId("electrocuted") ?? StatusAilment.Cursed, "electrocution before shock");
            Equal(true, StatusAilments.FromBuffId("chill_immunity") == null, "immunities are not ailments");
            Equal(true, StatusAilments.FromBuffId("flask_effect_life") == null, "flasks are not ailments");
            Equal(true, StatusAilments.FromBuffId("flammability_ignite_chance_buildup_79") == null, "build-up is not the ailment yet");
            // Ids of the game's BuffDefinitions that only mention a state (seen as false stuns live, 2026-10-24).
            foreach (var mention in new[] { "sprint_heavy_stunnable_79", "number_of_times_stunned_in_past_duration_from_ot_79", "have_been_stunned_recently",
                         "stunning_magic_buff", "chill_prevention", "flask_bonus_reduced_self_poison_duration", "have_ignited_an_enemy_recently", "frozen_damage_increase" })
            {
                Equal(true, StatusAilments.FromBuffId(mention) == null, $"{mention} is not the state");
            }

            Equal(StatusAilment.Stunned, StatusAilments.FromBuffId("stun_display_buff") ?? StatusAilment.Cursed, "the stun itself");
            Equal(StatusAilment.Slowed, StatusAilments.FromBuffId("mud_slow_79") ?? StatusAilment.Cursed, "a slow by its last word");
            Equal(StatusAilment.Ignited, StatusAilments.FromBuffId("ignited_79") ?? StatusAilment.Cursed, "ignite");
            Equal(StatusAilment.Cursed, StatusAilments.FromBuffId("curse_enfeeble") ?? StatusAilment.Frozen, "a curse by its first word");
        }

        private static void WallBumpThudsOncePerContact()
        {
            var bump = new WallBumpDetector(new WallBumpLimits(0.5f, 200, 15f, 30f, 45f, 300));
            var right = new System.Numerics.Vector2(1, 0);
            var down = new System.Numerics.Vector2(0, 1);
            var still = new System.Numerics.Vector2(100, 100);
            Equal(false, bump.Update(0, right, still, true), "pushing starts the clock");
            Equal(false, bump.Update(100, right, still, true), "not stopped long enough");
            Equal(true, bump.Update(250, right, still, true), "stopped against the wall: thud");
            Equal(false, bump.Update(400, right, still, true), "still pushing: once per contact");
            Equal(false, bump.Update(1000, right, still, true), "still once");
            Equal(false, bump.Update(1100, System.Numerics.Vector2.Zero, still, true), "let go");
            Equal(false, bump.Update(1500, right, still, true), "pushing again restarts the clock");
            Equal(true, bump.Update(1750, right, still, true), "after a pause the wall thuds again");
            Equal(false, bump.Update(1800, down, still, false), "turned along the wall, nothing ahead");
            Equal(false, bump.Update(2000, right, still, false), "a stall with nothing ahead is silent");
            var walking = new WallBumpDetector(new WallBumpLimits(0.5f, 200, 15f, 30f, 45f, 300));
            for (var t = 0; t <= 1000; t += 50)
            {
                Equal(false, walking.Update(t, right, new System.Numerics.Vector2(t / 5f, 0), true), $"sliding along at {t} ms is not a bump");
            }
        }

        private static void FriendsGetTheirOwnStream()
        {
            var scheduler = new EnemyCueScheduler(2, 100, 150, 600);
            var npc = new CueTarget(99, CueSynth.Layer.Friend, 0.3f, 0f, 0.5f);
            var enemies = Enumerable.Range(1, 5).Select(id => new CueTarget((uint)id, CueSynth.Layer.None, 0f, 0f, 0.9f)).ToArray();
            var played = new List<uint>();
            for (var now = 0L; now < 1500; now += 10)
            {
                if (scheduler.Next(now, enemies.Append(npc).ToArray()) is { } target)
                {
                    played.Add(target.Id);
                }
            }

            Equal(true, played.Contains(99u), "the NPC taps beside the enemies");
            Equal(2, played.Where(id => id != 99).Distinct().Count(), "the NPC does not take an enemy stream");
            var tap = CueSynth.Enemy(CueSynth.Layer.Friend, 44100);
            Equal(true, tap.Length < CueSynth.Growl(44100).Length && tap.All(sample => MathF.Abs(sample) <= 1f), "the tap is short and in range");
        }

        private static void LifeWarningsSpeakOncePerFall()
        {
            var alarm = new VitalAlarm(10, 50, 25);
            string Feed(params int[] values) => string.Join(" ", values.Select(value => alarm.Update(value)?.ToString() ?? "-"));
            Equal("- 50 - - 25", Feed(100, 49, 45, 51, 20), "each threshold once on the way down");
            Equal("- 50", Feed(61, 40), "re-armed 10 points above");
            Equal("- 25", Feed(100, 10), "a big hit says only the lowest");
        }

        private static void PhrasesFollowTheGameLanguage()
        {
            try
            {
                Localization.Detect("en");
                Equal("2 of 5", Announcements.ListPosition(2, 5), "english position");
                Equal("Life 25 percent", Announcements.LowLife(25), "english life");
                Equal("Wounded Man, 9 steps, up left, no route", Announcements.ScanEntry("Wounded Man", 9, Announcements.StickDirections[3], 0, null, false), "english scan entry");
                Equal("right", Announcements.StickDirections[0], "english direction");
                Equal("D-pad right", Announcements.ButtonName("DPadRight", false), "english button");

                Localization.Detect("es");
                Equal("2 de 5", Announcements.ListPosition(2, 5), "spanish position");
                Equal("Vida 25 por ciento", Announcements.LowLife(25), "spanish life");
                Equal("Wounded Man, 9 pasos, arriba a la izquierda, sin ruta", Announcements.ScanEntry("Wounded Man", 9, Announcements.StickDirections[3], 0, null, false), "spanish scan entry");
                Equal("Renly, 1 paso, abajo 3, luego izquierda", Announcements.ScanEntry("Renly", 1, "abajo", 3, "izquierda", true), "spanish scan entry with a turn");
                Equal("cruceta derecha", Announcements.ButtonName("DPadRight", false), "spanish button");
                Equal("A", Announcements.ButtonName("Button4", false), "letters stay");

                Equal("es", Localization.ParseGameLanguage("[GENERAL]\nlanguage=en\n[LANGUAGE]\nlanguage=ES\n"), "ini section");
                Localization.Detect("auto");
                Equal(true, Localization.Language is "en" or "es", "auto never throws");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void ConfirmGlyphMarksTheEntryBesideIt()
        {
            // Three dialogue options stacked 30 px apart, 200 px wide.
            var entries = new List<(System.Numerics.Vector2 Position, System.Numerics.Vector2 Size)>
            {
                (new(100, 100), new(200, 30)),
                (new(100, 130), new(200, 30)),
                (new(100, 160), new(200, 30)),
            };
            Equal(1, UiSemantics.NearestRect(entries, new(150, 145)), "glyph inside the second entry");
            Equal(2, UiSemantics.NearestRect(entries, new(80, 172)), "glyph drawn left of the third entry");
            Equal(0, UiSemantics.NearestRect(entries, new(80, 90)), "glyph above the list");
            Equal(-1, UiSemantics.NearestRect(new List<(System.Numerics.Vector2, System.Numerics.Vector2)>(), new(0, 0)), "no entries");
        }

        private static string Said(ScanAnswer answer) => $"{answer.Entry?.Name ?? "None"} {answer.CategoryChanged}";

        private static (long At, bool L3, float X, float Y) Down(long at) => (at, true, 0f, 0f);

        private static (long At, bool L3, float X, float Y) Up(long at) => (at, false, 0f, 0f);

        private static (long At, bool L3, float X, float Y) Stick(long at, float x, float y) => (at, true, x, y);

        private static string Play(params (long At, bool L3, float X, float Y)[] samples)
        {
            var gestures = new L3Gestures();
            var seen = new List<string>();
            foreach (var sample in samples)
            {
                seen.AddRange(gestures.Update(sample.At, sample.L3, sample.X, sample.Y).Select(gesture => gesture.ToString()));
            }

            return string.Join("|", seen);
        }

        private static ScreenDiffer Settled(out ScreenSnapshot screen)
        {
            var differ = new ScreenDiffer();
            screen = Snapshot(Layer(1, 0, true, Text(10, "Ranger"), Text(11, "All I need")));
            differ.Apply(screen);
            differ.Apply(screen);
            return differ;
        }

        private static ScreenSnapshot Snapshot(params ScreenLayer[] layers) => new((IntPtr)100, layers);

        private static ScreenLayer Layer(long address, int index, bool controls, params ScreenText[] texts) =>
            new((IntPtr)address, index, texts, controls, false);

        private static ScreenText Text(long address, string text) => new((IntPtr)address, text, string.Empty, false, true);

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new InvalidOperationException($"{what}: expected {expected}, got {actual}");
            }
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"FAIL {name}: {ex.Message}");
            }
        }
    }
}
