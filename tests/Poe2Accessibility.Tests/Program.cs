namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper.RemoteEnums;
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
            Run(nameof(PartnerRadarHasItsOwnSounds), PartnerRadarHasItsOwnSounds);
            Run(nameof(LocalPlayerListTrustsOnlyItsOwnPlayerFirst), LocalPlayerListTrustsOnlyItsOwnPlayerFirst);
            Run(nameof(InputSeatFollowsWhoPressedLast), InputSeatFollowsWhoPressedLast);
            Run(nameof(CoopDownsSayWhoFellAndRose), CoopDownsSayWhoFellAndRose);
            Run(nameof(NearbyThingsAreNamedOncePerApproach), NearbyThingsAreNamedOncePerApproach);
            Run(nameof(WallBumpThudsOncePerContact), WallBumpThudsOncePerContact);
            Run(nameof(FriendsGetTheirOwnStream), FriendsGetTheirOwnStream);
            Run(nameof(TwoNpcsAPlayerAndABodyAreAllHeard), TwoNpcsAPlayerAndABodyAreAllHeard);
            Run(nameof(SwitchesAreToldFromBreakables), SwitchesAreToldFromBreakables);
            Run(nameof(PhrasesFollowTheGameLanguage), PhrasesFollowTheGameLanguage);
            Run(nameof(OtherLanguagesUsePhraseTables), OtherLanguagesUsePhraseTables);
            Run(nameof(ConfirmGlyphMarksTheEntryBesideIt), ConfirmGlyphMarksTheEntryBesideIt);
            Run(nameof(BeaconPointsAlongTheRoute), BeaconPointsAlongTheRoute);
            Run(nameof(ClosedWayLeadsToItsNearestPoint), ClosedWayLeadsToItsNearestPoint);
            Run(nameof(QuestNamesItsTargets), QuestNamesItsTargets);
            Run(nameof(ItemMovesAreSaidAsDeeds), ItemMovesAreSaidAsDeeds);
            Run(nameof(ReleaseTagsOrderByDayThenNumber), ReleaseTagsOrderByDayThenNumber);
            Run(nameof(GameDataStepsRerunAlone), GameDataStepsRerunAlone);
            Run(nameof(OldGameDataStampCarriesOver), OldGameDataStampCarriesOver);
            Run(nameof(TradeListingSaysItemPriceThenSeller), TradeListingSaysItemPriceThenSeller);
            Run(nameof(NearbyThingsWaitForTheCooldown), NearbyThingsWaitForTheCooldown);
            Run(nameof(StickClickWhileHoldingL3), StickClickWhileHoldingL3);
            Run(nameof(WallTonesSwellNearWalls), WallTonesSwellNearWalls);
            Run(nameof(NewsFollowItsDeliveryLevel), NewsFollowItsDeliveryLevel);
            Run(nameof(DodgeTimingIsLearntFromBlows), DodgeTimingIsLearntFromBlows);
            Run(nameof(ProjectileWarningsAddTheFlight), ProjectileWarningsAddTheFlight);
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
            Run(nameof(FocusDetailsAreNotScrolledRows), FocusDetailsAreNotScrolledRows);
            Run(nameof(KeptLessonIsHeardWhole), KeptLessonIsHeardWhole);
            Run(nameof(KeptLessonHoldsFromItsStart), KeptLessonHoldsFromItsStart);
            Run(nameof(PendingLessonIsRemindedNowAndThen), PendingLessonIsRemindedNowAndThen);
            Run(nameof(WeakPointLeadsToTheHead), WeakPointLeadsToTheHead);
            Run(nameof(WeakPointBeatsFasterThenSteady), WeakPointBeatsFasterThenSteady);
            Run(nameof(FlaskDrinksAreSaidBySlot), FlaskDrinksAreSaidBySlot);
            Run(nameof(ShieldRaisedAndLowered), ShieldRaisedAndLowered);
            Run(nameof(CullingThresholdsByRarity), CullingThresholdsByRarity);
            Run(nameof(EnemyStatesFromBuffsAndAnimations), EnemyStatesFromBuffsAndAnimations);
            Run(nameof(PrimedForStunByRarity), PrimedForStunByRarity);
            Run(nameof(EnemyStatesAreNewsOnlyAsTheyBegin), EnemyStatesAreNewsOnlyAsTheyBegin);
            Run(nameof(WrappedLabelsJoin), WrappedLabelsJoin);
            Run(nameof(InlineItemArtIsNamed), InlineItemArtIsNamed);
            Run(nameof(KnownPlacesOutliveTheSession), KnownPlacesOutliveTheSession);
            Run(nameof(MapPiecesAndPropsArePlaced), MapPiecesAndPropsArePlaced);
            Run(nameof(ChatSpeakersGetASpace), ChatSpeakersGetASpace);
            Run(nameof(SmallCapsCaptionsGetACapital), SmallCapsCaptionsGetACapital);
            Run(nameof(QuestRewardIconsAreNamed), QuestRewardIconsAreNamed);
            Run(nameof(PlacesAreSaidOnceAsTheyChange), PlacesAreSaidOnceAsTheyChange);
            Run(nameof(RadarLabelsSpeakSpanish), RadarLabelsSpeakSpanish);
            Run(nameof(ExperienceProgressIsWithinTheLevel), ExperienceProgressIsWithinTheLevel);
            Run(nameof(LootFilterKeepsWhatThePlayerChose), LootFilterKeepsWhatThePlayerChose);
            Run(nameof(InPlayFlagIsFoundFromStoryVariants), InPlayFlagIsFoundFromStoryVariants);
            Run(nameof(MovedFieldsAreFoundByWatchingThemChange), MovedFieldsAreFoundByWatchingThemChange);
            Run(nameof(FlagBitsAreFoundOnTheirCarriers), FlagBitsAreFoundOnTheirCarriers);
            Run(nameof(PadButtonsFollowTheLeftTrigger), PadButtonsFollowTheLeftTrigger);
            Run(nameof(TileGridIsFoundByWhatItsRecordsHold), TileGridIsFoundByWhatItsRecordsHold);
            Run(nameof(BaseNamesAreCheckedAgainstTooltipTitles), BaseNamesAreCheckedAgainstTooltipTitles);
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
            // The dodge warning's bass drop rings on after the hit, which comes 0.2 s in.
            foreach (var (name, sound, seconds) in new[] { ("beacon", CueSynth.Ping(rate), 0.5f), ("dodge", CueSynth.Dodge(rate), 1f) })
            {
                Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"{name} stays within full scale");
                Equal(true, MathF.Abs(sound[^1]) < 0.01f, $"{name}'s room tail fades to silence");
                Equal(true, sound.Length < rate * seconds, $"{name} is over within {seconds} s");
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

        private static void PartnerRadarHasItsOwnSounds()
        {
            const int rate = 44100;
            foreach (var reading in new[] { RadarReading.Open, RadarReading.Exit, RadarReading.Wall })
            {
                foreach (var direction in Enum.GetValues<RadarSynth.Direction>())
                {
                    var sound = RadarSynth.Cue(reading, direction, rate, RadarSynth.Voice.Second);
                    Equal(true, sound.Length > rate / 20 && sound.Length < rate / 4, $"second voice {reading} {direction} lasts 50-250 ms");
                    Equal(true, sound.All(sample => MathF.Abs(sample) <= 1f), $"second voice {reading} {direction} within full scale");
                }
            }

            float[] Window(float[] sound, int fromMs, int toMs) =>
                sound.Skip(rate * fromMs / 1000).Take(rate * (toMs - fromMs) / 1000).ToArray();
            int[] CrossingsAt(float[] sound) =>
                Enumerable.Range(1, sound.Length - 1).Where(i => sound[i - 1] < 0 != sound[i] < 0).ToArray();
            int Crossings(float[] sound, int fromMs, int toMs) => CrossingsAt(Window(sound, fromMs, toMs)).Length;
            float Loudest(float[] sound, int fromMs, int toMs) => Window(sound, fromMs, toMs).Max(MathF.Abs);
            double Irregularity(float[] sound)
            {
                var at = CrossingsAt(Window(sound, 60, 120));
                var gaps = at.Zip(at.Skip(1), (first, next) => (double)(next - first)).ToArray();
                var mean = gaps.Average();
                return Math.Sqrt(gaps.Average(gap => (gap - mean) * (gap - mean))) / mean;
            }

            double Brightness(float[] sound, int fromMs, int toMs)
            {
                var part = Window(sound, fromMs, toMs);
                return Math.Sqrt(part.Skip(1).Select((sample, i) => (double)(sample - part[i]) * (sample - part[i])).Sum() / part.Sum(sample => (double)sample * sample));
            }

            int Period(float[] sound)
            {
                // the first autocorrelation peak past the first trough: the fundamental, not a multiple of it
                var part = Window(sound, 20, 60);
                var match = Enumerable.Range(0, rate / 80).Select(lag => part.Skip(lag).Select((sample, i) => (double)sample * part[i]).Sum() / (part.Length - lag)).ToArray();
                var trough = Array.FindIndex(match, value => value < 0);
                var lag = Array.FindIndex(match, trough, value => value >= 0.9 * match.Skip(trough).Max());
                while (match[lag + 1] > match[lag])
                {
                    lag++;
                }

                return lag;
            }

            RadarSynth.Direction up = RadarSynth.Direction.Up, down = RadarSynth.Direction.Down, left = RadarSynth.Direction.Left;
            const RadarSynth.Voice second = RadarSynth.Voice.Second;
            var pluck = RadarSynth.Cue(RadarReading.Wall, up, rate, second);
            Equal(true, Loudest(pluck, 70, 90) < Loudest(pluck, 0, 20) / 4, "the wall is plucked once");
            var obstacle = RadarSynth.Obstacle(up, rate, second);
            var exit = RadarSynth.Cue(RadarReading.Exit, up, rate, second);
            Equal(true, Brightness(pluck, 0, 15) > Brightness(pluck, 45, 60) * 1.1, "the pluck starts bright and mellows");
            var tock = RadarSynth.Cue(RadarReading.Wall, up, rate);
            Equal(true, Math.Abs(Brightness(tock, 0, 15) - Brightness(tock, 45, 60)) < Brightness(tock, 0, 15) / 10, "the first voice's tock keeps one tone");
            Equal(true, Brightness(pluck, 0, 15) > Brightness(tock, 0, 15) * 3, "the plucked wall is richer than the first voice's tock, though lower");
            Equal(true, Period(exit) < Period(obstacle) && Period(obstacle) < Period(pluck), "plucked in the first voice's order: exit high, obstacle middle, wall low");
            Equal(true, Math.Abs(Period(pluck) - (rate / 196)) <= 2, "the wall is plucked at 196 Hz");
            Equal(true, Period(RadarSynth.Cue(RadarReading.Exit, down, rate, second)) > Period(exit), "below the player plucks lower");
            var whistle = RadarSynth.Cue(RadarReading.Open, left, rate, second);
            var breath = RadarSynth.Cue(RadarReading.Open, left, rate);
            Equal(breath.Length, whistle.Length, "the whistle lasts as long as the breath");
            Equal(true, Irregularity(whistle) < 0.1, "the second voice whistles a steady tone");
            Equal(true, Irregularity(breath) > 0.2, "the first voice breathes noise");
            var ahead = RadarSynth.Cue(RadarReading.Open, up, rate, second);
            var behind = RadarSynth.Cue(RadarReading.Open, down, rate, second);
            Equal(true, Crossings(ahead, 120, 160) > Crossings(ahead, 20, 60), "the whistle rises ahead");
            Equal(true, Crossings(behind, 120, 160) < Crossings(behind, 20, 60), "the whistle falls behind");
        }

        private static void CoopDownsSayWhoFellAndRose()
        {
            var downs = new CoopDowns();
            const long town = 1, field = 2, respawned = 3;
            Equal(0, downs.Update(field, true, ("yarkbruja", false)).Count, "the first reading (area loaded with the partner dead) is not news");
            Equal(Announcements.PartnerUp("yarkbruja"), downs.Update(field, true, ("yarkbruja", true)).Single(), "revived");
            Equal(Announcements.PartnerDown("yarkbruja"), downs.Update(field, true, ("yarkbruja", false)).Single(), "the partner falls");
            Equal(0, downs.Update(field, true, ("yarkbruja", false)).Count, "still down: said once");
            Equal(0, downs.Update(field, false, ("yarkbruja", false)).Count, "both dead: the death screen speaks");
            Equal(0, downs.Update(respawned, true, ("yarkbruja", true)).Count, "respawned at the checkpoint: a new instance, no news");
            Equal(Announcements.YouAreDown, downs.Update(respawned, false, ("yarkbruja", true)).Single(), "the followed player falls, the partner can revive");
            Equal(Announcements.YouAreUp, downs.Update(respawned, true, ("yarkbruja", true)).Single(), "revived by the partner");
            downs.Update(respawned, false, ("yarkbruja", true));
            downs.Reset();
            Equal(0, downs.Update(town, true, ("yarkbruja", true)).Count, "a reset (area change seen) starts over too");
            Equal(0, downs.Update(town, false, null).Count, "alone: the death screen's own reader");
        }

        private static void InputSeatFollowsWhoPressedLast()
        {
            (uint, float)? idle = (0u, 0f), a = (0x400u, 0f), walking = (0u, 1f);
            var alone = new InputSeat();
            Equal(1, alone.Update(true, false, a, a), "player 2 not read: player 1's controller");
            var second = new InputSeat();
            Equal(1, second.Update(false, true, a, idle), "player 2 read alone, before it presses: player 1 (it has not joined)");
            Equal(2, second.Update(false, true, idle, a), "player 2's first press");
            Equal(2, second.Update(false, true, a, idle), "then player 1's presses do not take it back");
            var both = new InputSeat();
            Equal(1, both.Update(true, true, idle, idle), "both read: player 1 first");
            Equal(1, both.Update(true, true, walking, a), "player 1 starts walking as player 2 presses A: both pressed, no change");
            Equal(1, both.Update(true, true, walking, idle), "player 1 keeps walking: holding a stick is not a press");
            Equal(2, both.Update(true, true, walking, (0x1u, 0f)), "player 2 moves in its menu");
            Equal(1, both.Update(true, true, (0x400u, 1f), idle), "player 1 presses A");
            Equal(1, both.Update(true, true, (0x400u, 1f), null), "player 2 unreadable: no press");
            Equal(2, both.Update(true, true, (0x400u, 1f), walking), "player 2 pushes a stick");
            both.Baseline();
            Equal(2, both.Update(true, true, (0x7u, 1f), idle), "another UI manager: its first reading is not a press");
            Equal(1, both.Update(true, true, (0x8u, 1f), idle), "then a press counts again");
        }

        private static void LocalPlayerListTrustsOnlyItsOwnPlayerFirst()
        {
            IntPtr self = 0x1000, partner = 0x2000, stranger = 0x3000;
            Equal((IntPtr?)partner, LocalPlayerList.Second(new[] { self, partner }, self), "couch co-op: the second entry");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self }, self), "alone: nobody");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { stranger, partner }, self), "a list not led by the player is not trusted");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, partner, stranger }, self), "more seats than the game has: not trusted");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, IntPtr.Zero }, self), "an empty seat");
            Equal((IntPtr?)null, LocalPlayerList.Second(new[] { self, partner }, IntPtr.Zero), "no player yet");
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

        private static void ExperienceProgressIsWithinTheLevel()
        {
            var table = new ExperienceTable(new uint[] { 0, 525, 1760, 4250334444, 4250334445 });
            Equal(0, table.PercentToNext(1, 0), "level 1 at its start");
            Equal(50, table.PercentToNext(2, 525 + 618), "half way through level 2");
            Equal(99, table.PercentToNext(3, 4250334443), "experience past int.MaxValue");
            Equal(null, table.PercentToNext(5, 4250334445), "the last level");
            Equal(null, table.PercentToNext(2, 100), "experience below the level (a stale read)");
            Equal(null, new ExperienceTable(Array.Empty<uint>()).PercentToNext(1, 0), "no table");
            Localization.Detect("es");
            Equal("Nivel 4, 35 por ciento hacia el nivel 5, nivel del área 2", Announcements.Levels(4, 2, 35), "vitals line");
            Equal("Nivel 4, nivel del área 2", Announcements.Levels(4, 2), "without the table");
            Localization.Detect("en");
        }

        private static void LootFilterKeepsWhatThePlayerChose()
        {
            Equal(true, LootFilter.Allows(LootFilter.All, LootTier.Normal), "all keeps white items");
            Equal(false, LootFilter.Allows(LootFilter.Magic, LootTier.Normal), "magic or better drops white items");
            Equal(true, LootFilter.Allows(LootFilter.Magic, LootTier.Magic), "magic or better keeps magic items");
            Equal(false, LootFilter.Allows(LootFilter.Valuable, LootTier.Magic), "valuable drops magic items");
            Equal(true, LootFilter.Allows(LootFilter.Valuable, LootTier.Valuable), "valuable keeps currency and rares");
            Equal(true, LootFilter.Allows("garbage", LootTier.Normal), "an unknown setting keeps everything");
            Equal(LootTier.Valuable, new ScanEntry(1, ScanCategory.Items, "x", 0, 0, 0).Loot, "an item of unknown worth stays heard");
        }

        private static void InPlayFlagIsFoundFromStoryVariants()
        {
            // Byte 0x10 is the flag; 0x11 is 1 on everything (no zero to tell it apart); 0x12 differs between Renly's variants
            // but is set on both.
            static byte[] Block(byte flag, byte variant) { var block = new byte[0x20]; block[0x10] = flag; block[0x11] = 1; block[0x12] = variant; return block; }
            var inPlay = new[] { Block(1, 1), Block(1, 1), Block(1, 1), Block(1, 1) };
            var renly = new IReadOnlyList<byte[]>[] { new[] { Block(1, 1), Block(0, 1) } };
            Equal(true, InPlayFlag.Fits(inPlay, renly, 0x10), "the flag fits");
            Equal(false, InPlayFlag.Fits(inPlay, renly, 0x11), "a byte that is 1 on everything does not");
            Equal(false, InPlayFlag.Fits(inPlay, renly, 0x12), "both variants in play: not the flag");
            Equal(false, InPlayFlag.Fits(inPlay, Array.Empty<IReadOnlyList<byte[]>>(), 0x10), "no story variants: nothing proves a 0");
            Equal(false, InPlayFlag.Fits(inPlay.Take(2).ToList(), renly, 0x10), "too little in play");
            Equal(0x10, OffsetSearch.First(0x14, 8, 1, at => InPlayFlag.Fits(inPlay, renly, at)), "found again from a moved start");
            Equal(1.0, InPlayFlag.Share(inPlay, 0x10), "share in play");
        }

        private static void MovedFieldsAreFoundByWatchingThemChange()
        {
            static Dictionary<int, long> Read(long atZero, long atEight, long counter) => new() { [0] = atZero, [8] = atEight, [16] = counter };
            static bool Index(long value) => value is >= 0 and < 4;
            var control = (IntPtr)0x1000;

            var measured = new ChangingField();
            measured.Observe(control, 0, Read(0, 3, 100), Index);
            Equal(true, measured.Observe(control, 0, Read(1, 3, 101), Index), "the expected field changing settles it");
            Equal(0, measured.Proved, "where measured");

            var moved = new ChangingField();
            moved.Observe(control, 0, Read(0, 0, 100), Index);
            Equal(false, moved.Observe(control, 0, Read(0, 1, 101), Index), "one change is not enough");
            Equal(true, moved.Observe(control, 0, Read(0, 2, 102), Index), "a second change elsewhere moves it");
            Equal(8, moved.Proved, "the field that moved with the selection (a counter out of range never counts)");

            var other = new ChangingField();
            other.Observe(control, 0, Read(0, 0, 100), Index);
            other.Observe(control, 0, Read(0, 1, 100), Index);
            Equal(false, other.Observe((IntPtr)0x2000, 0, Read(0, 2, 100), Index), "a new control starts over");
            Equal(null, other.Proved, "nothing proved yet");
        }

        private static void FlagBitsAreFoundOnTheirCarriers()
        {
            // Bit 11 (visible) is on everything; bit 27 marks the controls; one label wrongly carries it.
            const uint visible = 1u << 11, control = 1u << 27;
            var controls = Enumerable.Repeat(visible | control, 20).ToList();
            var labels = Enumerable.Repeat(visible, 19).Append(visible | control).ToList();
            Equal(27, BitSearch.FindBit(controls, labels, 27, 0.9, 0.1, 3), "where measured, a stray label tolerated");
            Equal(27, BitSearch.FindBit(controls, labels, 22, 0.9, 0.1, 3), "found from a moved start; the visible bit never fits");
            Equal(null, BitSearch.FindBit(controls.Take(2).ToList(), labels, 27, 0.9, 0.1, 3), "too few to judge");
            Equal(0.05, BitSearch.Share(labels, 27), "share");
        }

        private static void PadButtonsFollowTheLeftTrigger()
        {
            static PadButtonCheck Feed(int triggerBit, uint alsoHeld)
            {
                var check = new PadButtonCheck();
                for (var frame = 0; frame < 25; frame++)
                {
                    check.Observe((1u << triggerBit) | alsoHeld, 1f, 0f, 14);
                    check.Observe(alsoHeld, 0f, 0f, 14);
                    check.Observe(1u << 3, 0.4f, 0f, 14); // half pressed: not counted
                }

                return check;
            }

            Equal(0, Feed(14, 0).Shift, "where measured");
            Equal(2, Feed(16, 0).Shift, "every button moved by two");
            Equal(0, Feed(14, 1u << 10).Shift, "A held throughout is set both ways: not the trigger");
            var none = new PadButtonCheck();
            for (var frame = 0; frame < 25; frame++)
            {
                none.Observe(0, 1f, 0f, 14);
                none.Observe(0, 0f, 0f, 14);
            }

            Equal(true, none.Judged, "judged");
            Equal(null, none.Shift, "no bit follows the trigger");
        }

        private static void TileGridIsFoundByWhatItsRecordsHold()
        {
            // Six rows of ten sub-tiles; each tile spans five sub-tiles of two rows, its file at 0x1000 + 0x100 per tile.
            const int perRow = 10;
            const int subTiles = 60;
            static byte[] Grid(TileGridSearch.Layout layout, bool ids)
            {
                var noise = new Random(7);
                var grid = new byte[subTiles * layout.Stride];
                noise.NextBytes(grid);
                for (var i = 0; i < subTiles; i++)
                {
                    var (row, column) = (i / perRow, i % perRow);
                    var tile = ((row / 2) * 2) + (column / 5);
                    var at = i * layout.Stride;
                    BitConverter.GetBytes(0x1000L + (tile * 0x100)).CopyTo(grid, at + layout.FileOffset);
                    (grid[at + layout.IdsOffset], grid[at + layout.IdsOffset + 1], grid[at + layout.IdsOffset + 2]) =
                        ids ? ((byte)(column % 5), (byte)(row % 2), (byte)(tile % 8)) : ((byte)0, (byte)0, (byte)0);
                }

                return grid;
            }

            static bool NamesTile(long file, int path) => path == 0x10 && file is >= 0x1000 and < 0x2000;
            var measured = new TileGridSearch.Layout(0x38, 0x8, 0x10, 0x34);
            var moved = new TileGridSearch.Layout(0x40, 0x10, 0x10, 0x3A);
            Equal(measured, TileGridSearch.Find(Grid(measured, true), subTiles, perRow, measured, NamesTile), "where measured");
            Equal(moved, TileGridSearch.Find(Grid(moved, true), subTiles, perRow, measured, NamesTile), "a longer record with its fields moved");
            Equal(null, TileGridSearch.Find(Grid(measured, false), subTiles, perRow, measured, NamesTile), "no bytes step between neighbours");
            Equal(null, TileGridSearch.Find(Grid(measured, true), subTiles + 1, perRow, measured, NamesTile), "bytes that are not whole records");
            Equal((0x38, 0x40), TileGridSearch.FirstMoved(measured, moved), "the record size is reported first");
        }

        private static void BaseNamesAreCheckedAgainstTooltipTitles()
        {
            static Func<int, int, string> NameAt(int row, int name) => (r, n) => (r, n) == (row, name) ? "Broquel de cuero" : (r, n) == (0x10, 0x40) ? "de" : string.Empty;
            string[] titles = { "Broquel de cuero", "Sólido broquel de cuero del zorro", "Broquel de cuero", "Broquel de cuero" };

            var kept = new BaseNameField(0x10, 0x30);
            Equal(false, kept.Observe(1, titles[0], NameAt(0x10, 0x30)), "one title is not enough");
            Equal(false, kept.Observe(1, titles[0], NameAt(0x10, 0x30)), "the same item polled again counts once");
            Equal(true, kept.Observe(2, titles[1], NameAt(0x10, 0x30)), "a magic title holds the base name, whatever its case");
            Equal(false, kept.Moved, "where measured");

            var moved = new BaseNameField(0x10, 0x30);
            var settled = titles.Select((title, item) => moved.Observe(item, title, NameAt(0x18, 0x38))).Count(done => done);
            Equal(1, settled, "settled once");
            Equal((0x18, 0x38), moved.Offsets, "found after two misses and a second title");
            Equal(true, moved.Moved, "moved");

            var polled = new BaseNameField(0x10, 0x30);
            for (var i = 0; i < 20; i++)
            {
                polled.Observe(1, "Broquel de cuero", NameAt(0x100, 0x100));
            }

            Equal(false, polled.Judged, "one item polled for seconds settles nothing");

            var lost = new BaseNameField(0x10, 0x30);
            for (var item = 0; item < BaseNameField.MissesToGiveUp; item++)
            {
                lost.Observe(item, "Broquel de cuero", NameAt(0x100, 0x100)); // "de" at +0x40 is too short to count
            }

            Equal(true, lost.Lost, "no place holds the name");
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

        private const string TierLabel = "tier_label";

        private static void FocusDetailsAreNotScrolledRows()
        {
            Equal(true, FocusDetails.HeadedBy(new[] { Text(1, "Destrozahuesos"), Text(2, "AdE, Cuerpo a cuerpo, Golpe") }, "Destrozahuesos, Nivel 1", TierLabel),
                "a skill's details under its name, as the old ones left");
            Equal(false, FocusDetails.HeadedBy(new[] { Text(1, "Golpe perfecto"), Text(2, "Nivel 5") }, "Golpe perfecto, Nivel 5", TierLabel), "the focused row scrolling in");
            Equal(false, FocusDetails.HeadedBy(new[] { Text(1, "Acercar la cámara"), Text(2, "Activado") }, "Restricciones de equipamiento", TierLabel), "other rows scrolling in");
        }

        private static void KeptLessonIsHeardWhole()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 26, 10, 33, 21);
            Equal(true, floor.Admit("Orbe de aumento, 2, columna 1, fila 1", true, start), "the focus goes out");
            Equal(true, floor.Keep("Navega a tu arbol de pasivas", true, 2000, start.AddMilliseconds(200)), "the lesson in a menu cuts in");
            Equal(false, floor.Admit("Lanza de madera dura", true, start.AddMilliseconds(500)), "a move waits");
            Equal(false, floor.Admit("Comparar, clic del stick derecho", false, start.AddMilliseconds(600)), "its legend waits after it");
            Equal(false, floor.Admit("Vacío, columna 4, fila 2", true, start.AddMilliseconds(900)), "a later move replaces what waited");
            Equal(false, floor.Keep("Objeto sin identificar", true, 2000, start.AddMilliseconds(1000)), "a lesson never cuts the one being said");
            Equal(0, floor.Release(start.AddMilliseconds(1500)).Count, "nothing while the lesson is said");
            Equal("Vacío, columna 4, fila 2", string.Join("|", floor.Release(start.AddMilliseconds(4300))), "the latest move after the lessons");
            Equal(true, floor.Admit("Vacío, columna 3, fila 2", true, start.AddMilliseconds(4400)), "the floor is free again");
            floor.Keep("Navega a tu arbol de pasivas", true, 2000, start.AddMilliseconds(5000));
            floor.Admit("Inventario", true, start.AddMilliseconds(5100));
            floor.Clear(start.AddMilliseconds(5200));
            Equal(0, floor.Release(start.AddMilliseconds(9000)).Count, "stopped speech drops what waited");
        }

        private static void KeptLessonHoldsFromItsStart()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 26, 10, 32, 42);
            floor.Sent(new string('x', 100), true, start);
            Equal(false, floor.Keep("¡Has recibido un punto de pasivas!", false, 2000, start), "in play the lesson waits its turn");
            Equal(true, floor.Held(start.AddMilliseconds(100 * SpeechFloor.SpokenMsPerChar + 1900)), "held until said after what was ahead");
            Equal(false, floor.Held(start.AddMilliseconds(100 * SpeechFloor.SpokenMsPerChar + 2100)), "then free");

            var later = start.AddMinutes(1);
            floor.Sent(new string('x', 1000), true, later);
            floor.Keep("lesson", false, 2000, later);
            Equal(false, floor.Held(later.AddMilliseconds(SpeechFloor.MaxAheadMs + 2100)), "a long line ahead delays the hold only so far");
        }

        private static void PendingLessonIsRemindedNowAndThen()
        {
            Equal(false, LessonReminder.IsDue(TimeSpan.FromSeconds(3), false), "the box flickering during play");
            Equal(true, LessonReminder.IsDue(TimeSpan.FromSeconds(6), false), "shown again during play: the game waits for it");
            Equal(false, LessonReminder.IsDue(TimeSpan.FromSeconds(30), true), "back with a panel soon after");
            Equal(true, LessonReminder.IsDue(TimeSpan.FromSeconds(61), true), "still pending a minute later");
        }

        private static void WeakPointLeadsToTheHead()
        {
            const string Folder = "Metadata/Monsters/MudBurrower/";
            var player = new Vector2(1000, 1000);
            var tail = new BossPart(1, "El devorador", Folder + "MudBurrowerTailBoss", new Vector2(1100, 1000), 80, true, true);
            var head = new BossPart(2, "El devorador", Folder + "MudBurrowerHeadBoss", new Vector2(1600, 1000), 90, true, true);
            var trap = new BossPart(3, "El devorador", Folder + "MudBurrowerHeadTrap", new Vector2(1050, 1000), 60, false, true);
            var larva = new BossPart(4, "Larva de carne", "Metadata/Monsters/MudBurrower/Maggot", new Vector2(1010, 1000), 20, false, true);
            Equal(2u, WeakPoints.Pick(new[] { tail, trap, larva, head }, player)?.Id, "the unique head, not the nearer tail, trap or larva");
            Equal(null, WeakPoints.Pick(new[] { tail, trap, head with { InPlay = false } }, player)?.Id, "the head buried: silence, never a trap of its name");
            Equal(null, WeakPoints.Pick(new[] { head, larva }, player)?.Id, "no tail near: no guide");
            var duo = new BossPart(5, "Anundr", "Metadata/Monsters/DevourerDuo/DevourerBossDuoTail", player, 80, true, true);
            Equal(true, WeakPoints.IsTail(duo.Path) && !WeakPoints.IsTail("Metadata/Monsters/Tailor/Thief"), "the word in the last segment only");
        }

        private static void WeakPointBeatsFasterThenSteady()
        {
            Equal(true, WeakPoints.IntervalMs(2000, 40) > WeakPoints.IntervalMs(300, 40), "faster as the gap closes");
            Equal(WeakPoints.AtPartIntervalMs, WeakPoints.IntervalMs(30, 40), "steady within reach");
        }

        private static void FlaskDrinksAreSaidBySlot()
        {
            var drinks = new FlaskDrinks();
            const string Mana = "Frasco de maná menor";
            const string Life = "Frasco de vida menor";
            Equal(FlaskNews.New, drinks.Update(1, Mana, 6), "first read (the caller keeps an area's first read silent)");
            Equal(FlaskNews.Drunk, drinks.Update(1, Mana, 5), "a drink, whatever item entity the game made for it");
            Equal(FlaskNews.None, drinks.Update(1, Mana, 5), "no change");
            drinks.Update(0, Life, 1);
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 0), "the last use drunk: said once (empty)");
            Equal(FlaskNews.None, drinks.Update(0, Life, 0), "still dry");
            Equal(FlaskNews.Refilled, drinks.Update(0, Life, 1), "a use gained from kills");
            Equal(FlaskNews.New, drinks.Update(1, "Frasco de vida mayor", 2), "another flask put in the slot");
            Equal(FlaskNews.New, drinks.Update(2, "Frasco de maná mayor", 3), "a flask in a slot empty until now");
        }

        private static void CullingThresholdsByRarity()
        {
            Equal(true, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 35, 100), "normal at 35%");
            Equal(false, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 36, 100), "normal above 35%");
            Equal(false, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Magic, 21, 100), "magic above 20%");
            Equal(true, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Rare, 100, 1000), "rare at 10%");
            Equal(true, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Unique, 2500, 50000), "unique at 5%");
            Equal(false, CullThreshold.Reached(GameHelper.RemoteEnums.Rarity.Unique, 0, 50000), "dead is not cullable");
        }

        private static void EnemyStatesFromBuffsAndAnimations()
        {
            Equal(EnemyState.Frozen, EnemyStates.FromBuffId("frozen"), "frozen");
            Equal(EnemyState.ArmourBroken, EnemyStates.FromBuffId("broken_armour_debuff_79"), "skill id suffix dropped");
            Equal(EnemyState.Parried, EnemyStates.FromBuffId("parried"), "parried");
            Equal(null, EnemyStates.FromBuffId("parried_immunity"), "an immunity is not the state");
            Equal(null, EnemyStates.FromBuffId("trap_pinned"), "only the pinned buff itself");
            Equal(null, EnemyStates.FromBuffId("frozen_"), "empty suffix");
            Equal(EnemyState.HeavyStunned, EnemyStates.FromAnimation("HeavyStunBack"), "heavy stun animation");
            Equal(null, EnemyStates.FromAnimation("HeavyStunDeath"), "a stun death is a death");
            Equal(EnemyState.Electrocuted, EnemyStates.FromAnimation("ElectrocuteLeft"), "electrocuted animation");
        }

        private static void PrimedForStunByRarity()
        {
            Equal(false, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 18, 60), "hag at 30%: not yet");
            Equal(true, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 24, 60), "normal at 40%");
            Equal(true, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 54, 60), "hag at 54 of 60 (stunned by the next hit)");
            Equal(false, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Magic, 19, 40), "magic below 50%");
            Equal(true, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Rare, 600, 1000), "rare at 60%");
            Equal(false, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Unique, 690, 1000), "unique below 70%");
            Equal(false, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 10, 0), "no threshold read");
            Equal(false, PrimedThreshold.Reached(GameHelper.RemoteEnums.Rarity.Normal, 1_000_000, 60), "not a buildup");
        }

        private static void EnemyStatesAreNewsOnlyAsTheyBegin()
        {
            var news = new EnemyStateNews();
            news.BeginPoll();
            Equal("HeavyStunned", string.Join(",", news.Update(1, new[] { EnemyState.HeavyStunned }, 0)), "a heavy stun begins");
            news.EndPoll();
            news.BeginPoll();
            Equal("", string.Join(",", news.Update(1, new[] { EnemyState.HeavyStunned, EnemyState.HeavyStunned }, 100)), "still stunned");
            news.EndPoll();
            news.BeginPoll();
            Equal("", string.Join(",", news.Update(1, Array.Empty<EnemyState>(), 200)), "gone for a moment");
            Equal("", string.Join(",", news.Update(1, new[] { EnemyState.HeavyStunned }, 300)), "back within the hold: the same stun");
            Equal("Frozen", string.Join(",", news.Update(1, new[] { EnemyState.HeavyStunned, EnemyState.Frozen }, 400)), "frozen too");
            Equal("HeavyStunned", string.Join(",", news.Update(1, new[] { EnemyState.HeavyStunned }, 400 + EnemyStateNews.EndMs)), "a new heavy stun after the hold");
            news.EndPoll();
            news.BeginPoll();
            news.EndPoll();
            news.BeginPoll();
            Equal("Frozen", string.Join(",", news.Update(1, new[] { EnemyState.Frozen }, 1000)), "out of sight and back: news again");
        }

        private static void ShieldRaisedAndLowered()
        {
            var shield = new ShieldStance();
            Equal(ShieldChange.None, shield.Update("Idle", 0), "shield down");
            Equal(ShieldChange.Raised, shield.Update("ActiveBlockChannel", 100), "LB held: raised at once");
            Equal(ShieldChange.None, shield.Update("ActiveBlockChannelMoving", 200), "still up while walking");
            Equal(ShieldChange.None, shield.Update("Block", 250), "a blocked hit flickers in");
            Equal(ShieldChange.None, shield.Update("ActiveBlockChannel", 300), "and the shield is still up");
            Equal(ShieldChange.Lowered, shield.Update("ActiveBlockEnd", 400), "released: lowered at its end");
            Equal(ShieldChange.None, shield.Update("Idle", 500), "down stays quiet");
            shield.Update("ActiveBlockChannel", 600);
            Equal(ShieldChange.None, shield.Update("StunnedHeavy", 700), "knocked out of it: not yet");
            Equal(ShieldChange.Lowered, shield.Update("StunnedHeavy", 700 + ShieldStance.LostMs), "a lasting change lowers it");
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
            Equal(null, map.ExploredPercent(grid, row, new System.Numerics.Vector2(40, 100)), "nothing walked yet");
            map.Visit(new System.Numerics.Vector2(40, 100), width, height);
            var firstLook = map.ExploredPercent(grid, row, new System.Numerics.Vector2(40, 100));
            Equal(true, firstLook is > 0 and < 50, "a first look sees part of the reachable west field");
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
            Equal(100, map.ExploredPercent(grid, row, new System.Numerics.Vector2(120, 100)), "the unreachable east field does not count");
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
            Equal(true, filed.ProjectileSpeed(key) == null, "a line without a speed fires no projectile");
        }

        private static void ProjectileWarningsAddTheFlight()
        {
            var timings = new DodgeTimings();
            const string bolt = "Metadata/Monsters/Caster|31";
            const string boss = "Metadata/Monsters/Boss|9";
            timings.AddHitTimes(new[] { $"{bolt}=800/1500/1000", $"{boss}=8646/9466", "Metadata/Monsters/Boss|10=5000/0", "Metadata/Monsters/Caster|32=800/1500/fast" });
            Equal(800, timings.Delay(bolt) ?? 0, "the files time the release");
            Equal(1000, timings.ProjectileSpeed(bolt) ?? 0, "and the projectile's speed");
            Equal(8646, timings.Delay(boss) ?? 0, "a long boss attack is believed within its own swing");
            Equal(true, timings.Delay("Metadata/Monsters/Boss|10") == null, "but not past the longest blow learnt without a swing");
            Equal(true, timings.Delay("Metadata/Monsters/Caster|32") == null, "a broken speed drops the line");

            var travel = DodgeTimings.TravelMs(600f, timings.ProjectileSpeed(bolt));
            Equal(600, travel, "600 units at 1000 a second");
            Equal(0, DodgeTimings.TravelMs(600f, null), "no projectile, no flight");
            Equal(0, DodgeTimings.TravelMs(-20f, 1000), "bodies touching, no flight");
            Equal(1900L, DodgeTimings.WarnAt(1000, timings.Delay(bolt) + travel, 500), "half a second before the projectile arrives");
            Equal(1000L, DodgeTimings.WarnAt(1000, timings.Delay("Metadata/Monsters/Unknown|31") + travel, 500), "an untimed release still warns at the wind-up");

            Equal(true, DodgeTimings.CanStillHit(0, 1800, 600, 2500), "the animation still runs");
            Equal(true, DodgeTimings.CanStillHit(2000, 1800, 600, 2400), "released, the monster turned away, the projectile flies on");
            Equal(false, DodgeTimings.CanStillHit(1500, 1800, 600, 1600), "interrupted before the release: nothing comes");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800, 600, 2700), "landed long ago");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800, 0, 2100), "a blow beside the monster ends with its animation");
            Equal(false, DodgeTimings.CanStillHit(2000, 1800 + 1500, 600, 2100), "a later swing that ended before its release fired nothing");
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
            Equal("urge a|b False|urge c|mention d|e True|f False", string.Join("|", output.Calls), "each kind at its level, urgent news holds, a waiting life warning keeps its turn, defaults for unknown values");
        }

        private sealed class RecordingOutput : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"{text} {interrupt}");

            public void Urge(string text, int holdMs = 1500) => this.Calls.Add($"urge {text}");

            public void Keep(string text, int holdMs, bool interrupt = false) => this.Calls.Add($"keep {text} {interrupt}");

            public void Mention(string text) => this.Calls.Add($"mention {text}");

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

        private static void TradeListingSaysItemPriceThenSeller()
        {
            var seller = new List<CardLine> { new("Mike_58#7693", false), new("Jugador desconectado", false), new("listado hace 1 meses", false) };
            var item = new List<CardLine>
            {
                new("Vision doble. Ballesta de diada", false), new("Ballesta", false), new("Carga una saeta adicional", false),
                new("Precio de salida:", true), new("1x", true), new("Orbe de aumento", true), new("DPS total: 88,80", false),
            };
            var card = CardText.Read(new List<IReadOnlyList<CardLine>> { seller, item }, 6);
            Equal("Vision doble. Ballesta de diada, Precio de salida: 1x, Orbe de aumento, Mike_58#7693, Jugador desconectado, listado hace 1 meses",
                  card?.Label, "the item, its price, then the seller's column with the status");
            Equal("Ballesta | Carga una saeta adicional | DPS total: 88,80", string.Join(" | ", card?.Details ?? new List<string>()), "the item's other lines follow, the price not again");
            Equal(null, CardText.Read(new List<IReadOnlyList<CardLine>> { seller, new List<CardLine> { new("Ballesta", false) } }, 6), "lines that fit a table row stay a row");
            var unpriced = CardText.Read(new List<IReadOnlyList<CardLine>> { seller, item.Where(line => !line.Price).ToList() }, 3);
            Equal("Vision doble. Ballesta de diada, Mike_58#7693, Jugador desconectado, listado hace 1 meses", unpriced?.Label, "without a price the longest column leads");
        }

        private static void GameDataStepsRerunAlone()
        {
            const string install = "index=10:20\nexe=30:40\nfmod=2.03.06\n";
            var stamp = Poe2AccessLauncher.GameDataStamp.Parse(null).With(install, "sounds", 2).With(install, "places", 1);
            var read = Poe2AccessLauncher.GameDataStamp.Parse(stamp.ToString().Replace("\n", "\r\n"));
            Equal(install, read.Install, "the install lines survive the file");
            Equal(true, read.IsCurrent(install, "sounds", 2), "a step done at its version is current");
            Equal(true, read.IsCurrent(install, "places", 1), "the other steps are kept");
            Equal(false, read.IsCurrent(install, "sounds", 3), "a raised version reruns that step");
            Equal(false, read.IsCurrent(install, "area names", 1), "a step never done runs");
            Equal(false, read.IsCurrent("index=11:20\nexe=30:40\nfmod=2.03.06\n", "places", 1), "a game patch reruns every step");
            Equal(1, read.With("index=11:20\n", "sounds", 2).Steps.Count, "a step done on a patched install forgets the others");
        }

        private static void OldGameDataStampCarriesOver()
        {
            const string install = "index=10:20\nexe=30:40\nfmod=2.03.06\n";
            string[] steps = { "item sounds", "places", "sounds" };
            var current = Poe2AccessLauncher.GameDataStamp.FromLegacy("format=7\r\n" + install.Replace("\n", "\r\n"), install, steps, "sounds");
            Equal(3, current.Steps.Count, "the last format vouches for every step");
            Equal(true, current.IsCurrent(install, "sounds", Poe2AccessLauncher.GameDataStamp.LegacyVersion), "at the first version");
            var older = Poe2AccessLauncher.GameDataStamp.FromLegacy("format=5\n" + install, install, steps, "sounds");
            Equal(false, older.Steps.ContainsKey("sounds"), "an earlier format reruns the sounds");
            Equal(true, older.IsCurrent(install, "places", 1), "and keeps the catalogs it covered");
            Equal(0, Poe2AccessLauncher.GameDataStamp.FromLegacy("format=2\n" + install, install, steps, "sounds").Steps.Count, "a format before hit times and places covers nothing");
            Equal(0, Poe2AccessLauncher.GameDataStamp.FromLegacy("format=7\nindex=9:20\nexe=30:40\nfmod=2.03.06\n", install, steps, "sounds").Steps.Count, "another install covers nothing");
            Equal(0, Poe2AccessLauncher.GameDataStamp.FromLegacy(null, install, steps, "sounds").Steps.Count, "no old stamp covers nothing");
        }

        private static void ReleaseTagsOrderByDayThenNumber()
        {
            Equal(new Version(2026, 10, 28, 1), Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28"), "an unnumbered tag is the day's first release");
            Equal(true, Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28-2") > Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28"), "-2 follows the day's first");
            Equal(true, Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.29") > Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28-3"), "a later day follows any number");
            Equal(true, Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28-10") > Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28-9"), "numbers compare as numbers");
            Equal<Version?>(null, Poe2AccessLauncher.ReleaseTag.Parse("latest"), "not a dated tag");
            Equal<Version?>(null, Poe2AccessLauncher.ReleaseTag.Parse("v2026.10"), "a tag without its day");
            Equal<Version?>(null, Poe2AccessLauncher.ReleaseTag.Parse("v2026.10.28-0"), "releases are numbered from 2");
        }

        private static void ItemMovesAreSaidAsDeeds()
        {
            string Said(List<ItemMove> moves) => string.Join(" | ", moves.Select(move => $"{move.Kind} {move.Name} {move.Where}".Trim()));
            var main = InventoryName.MainInventory1;
            var none = new List<(InventoryName, string)>();
            Equal("Sold Brazales", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Brazales") }, Announcements.SlotName, true, 19, "Mercader")),
                  "gold rose with the departure: sold, whatever panel is open");
            Equal("Bought Lanza", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Lanza") }, none, Announcements.SlotName, true, -555, "")),
                  "gold fell with the arrival: bought");
            Equal("MovedTo Orbe Alijo", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Orbe") }, Announcements.SlotName, true, 0, "Alijo")),
                  "the hand let go into the stash");
            Equal("TakenFrom Orbe Alijo", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe") }, none, Announcements.SlotName, true, 0, "Alijo")),
                  "came from the stash");
            Equal("DroppedOrUsed Arco", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Arco") }, Announcements.SlotName, true, 0, "")),
                  "gone with only the inventory open: dropped or used, decided on the ground");
            Equal("Equipped Casco Helmet", Said(ItemMoves.Read(new List<(InventoryName, string)> { (InventoryName.Helm1, "Casco") }, new List<(InventoryName, string)> { (main, "Casco") },
                                                              Announcements.SlotName, true, 0, "")),
                  "equipped from the inventory: only that");
            Equal(string.Empty, Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe") }, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Orbe") },
                                                    Announcements.SlotName, true, 0, "Alijo")),
                  "put back from the hand: a placement, not news");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Orbe") }, Announcements.SlotName, false, 0, "")),
                  "during play the pick-up watch speaks");
            Equal("TakenFrom Runa Alijo", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Runa") }, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") },
                                                              Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "the hand picked it up in the stash and set it down in the inventory");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") }, Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "picked up and set down in the stash: rearranging, not news");
            Equal("Orbe de aumento", ItemMoves.Title("Orbe de aumento, 3"), "the title without the stack");
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
            var places = new[] { new ScanEntry(7, ScanCategory.Places, "entrada", 0, 0, 0), new ScanEntry(8, ScanCategory.Places, "tumba", 0, 0, 0) };
            Equal(string.Empty, string.Join(" | ", QuestMatcher.Targets(places, new[] { "Encuentra la entrada al Valle rojo en el Bosque singular", "Busca a Asinia en la Tumba de la consorte" })
                                                                   .Select(entry => entry.Name)),
                  "a place's common noun in the objective is not the target");
            var notes = new[]
            {
                new ScanEntry(9, ScanCategory.Exits, "Ogham Farmlands Or Deadend", 0, 0, 0, QuestMatcher.TileNote),
                new ScanEntry(10, ScanCategory.Exits, "Pueblo de Ogham", 0, 0, 0),
            };
            Equal("Pueblo de Ogham", string.Join(" | ", QuestMatcher.Targets(notes, new[] { "El tesoro salvaje de Ogham", "Encuentra la entrada del pueblo" })
                                                               .Select(entry => entry.Name)),
                  "a tile note shares a word with the quest, an area name may");
            Equal("Ogham Farmlands Or Deadend", string.Join(" | ", QuestMatcher.Targets(notes.Take(1).ToList(), new[] { "Find Ogham Farmlands Or Deadend" }).Select(entry => entry.Name)),
                  "a tile note named whole");
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

            var items = new ProximityTracker(200, 300, 60000, entry => entry.Category != ScanCategory.Items);
            var bow = new ScanEntry(2, ScanCategory.Items, "Arco corto", 0, 0, 0);
            string ItemAt(float x, long now) => string.Join(", ", items.Update(x, 0, new[] { bow }, now).Select(entry => entry.Name));
            Equal("Arco corto", ItemAt(100, 0), "an item walked up to");
            Equal(string.Empty, ItemAt(500, 3000), "walked away");
            Equal("Arco corto", ItemAt(100, 6000), "walked back: named again, no cooldown for items");
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

        private static void SwitchesAreToldFromBreakables()
        {
            Equal(true, Switches.IsSwitch("Metadata/Terrain/Gallows/Act1/1_8/Objects/MausoleumSecretDoorSwitch"), "the mausoleum's switch");
            Equal(true, Switches.IsSwitch("Metadata/Terrain/Gallows/Act4/4_5_1/Objects/PrisonLever"), "a lever");
            Equal(false, Switches.IsSwitch("Metadata/Chests/Urns/Urn1"), "an urn");
            Equal("boulder", Breakables.Material("Metadata/Chests/Boulders/BoulderDirty1"), "a rock");
            Equal("rubble", Breakables.Material("Metadata/Chests/GoblinChests/GobRockPile01"), "a rock pile before a rock");
            Equal("bigpot", Breakables.Material("Metadata/Chests/TitanValleyPots/Pot01"), "a big pot before a pot");
            Equal("urn", Breakables.Material("Metadata/Chests/GraveyardDungeonChests/CryptUrn01"), "a crypt urn");
            Equal("egg", Breakables.Material("Metadata/Chests/BirdEggChests/BirdEggChest01"), "an egg");
            Equal("", Breakables.Material("Metadata/Chests/Village/Scarecrows/Scarecrow01"), "no material: the common sound");
            Equal(true, Breakables.IsCorpse("Metadata/Chests/CorpseChests_95702/PirateCorpse/PirateCorpseChest01"), "a corpse chest is a body");
            Equal(true, Breakables.IsCorpse("Metadata/Chests/Village/Impale01"), "the village's impaled victim is a body");
            Equal(false, Breakables.IsCorpse("Metadata/Chests/Village/Scarecrows/Scarecrow01"), "a scarecrow is not");
            Equal(false, Switches.IsSwitch("Metadata/Chests/LeverRoom/Chest1"), "only the object's own name counts");
        }

        private static void TwoNpcsAPlayerAndABodyAreAllHeard()
        {
            var scheduler = new EnemyCueScheduler(2, 100, 150, 600);
            var targets = new[]
            {
                new CueTarget(1, CueSynth.Layer.Friend, 0.2f, 0f, 0.8f),
                new CueTarget(2, CueSynth.Layer.Friend, -0.2f, 0f, 0.7f),
                new CueTarget(3, CueSynth.Layer.Friend, 0.5f, 0f, 0.2f),
                new CueTarget(4, CueSynth.Layer.Player, 0.4f, 0f, 0.5f),
                new CueTarget(5, CueSynth.Layer.PlayerDown, 1f, 0f, 0f),
                new CueTarget(6, CueSynth.Layer.None, 0f, 0f, 0.9f),
            };
            var played = new HashSet<uint>();
            for (var now = 0L; now < 2000; now += 10)
            {
                if (scheduler.Next(now, targets) is { } target)
                {
                    played.Add(target.Id);
                }
            }

            Equal("1 2 4 5 6", string.Join(" ", played.Order()), "the two nearest NPCs, the player, the body and the enemy; not the third NPC");
            var taps = CueSynth.Enemy(CueSynth.Layer.Player, 44100);
            var call = CueSynth.Enemy(CueSynth.Layer.PlayerDown, 44100);
            Equal(true, taps.Length > CueSynth.Tap(44100).Length && taps.All(sample => MathF.Abs(sample) <= 1f), "the player's double tap is longer than the NPC's, in range");
            Equal(true, call.Max(MathF.Abs) < taps.Max(MathF.Abs) && call.All(sample => MathF.Abs(sample) <= 1f), "the body's call is softer than the taps, in range");
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
                Equal("Área: Sierraclara, nivel 2.", Announcements.Area("Sierraclara", 2), "area with its level");
                Equal("Área: Sierraclara.", Announcements.Area("Sierraclara"), "area level unknown");
                Equal("Nivel 4, nivel del área 2", Announcements.Levels(4, 2), "levels in the vitals");
                Equal("O GROSSO J, Guerrero, nivel 20", Announcements.OtherPlayer("O_GROSSO_J", "Guerrero", 20), "another player with level");

                Equal("es", Localization.ParseGameLanguage("[GENERAL]\nlanguage=en\n[LANGUAGE]\nlanguage=ES\n"), "ini section");
                Localization.Detect("auto");
                Equal(true, Localization.Languages.Contains(Localization.Language), "auto never throws");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void OtherLanguagesUsePhraseTables()
        {
            try
            {
                Equal("es", Localization.Normalize("ES"), "upper case code");
                Equal("pt", Localization.Normalize("pt-BR"), "regional code");
                Equal("zh-TW", Localization.Normalize("zh_tw"), "underscore code");
                Equal(null, Localization.Normalize("auto"), "auto is no language");

                Localization.Set("fr", new()
                {
                    ["Life {0} percent"] = "Vie à {0} pour cent",
                    ["left"] = "gauche",
                    ["{0} steps"] = "{0} pas",
                    ["{0}, {1}, {2}{3}"] = "{0} : {1}, {2}{3}",
                    [", no route"] = ", pas de chemin",
                    ["{0} of {1}"] = "{1} / {0} {2}",
                });
                Equal("Vie à 25 pour cent", Announcements.LowLife(25), "interpolated phrase by its format");
                Equal("gauche", Announcements.StickDirections[4], "plain phrase");
                Equal("up", Announcements.StickDirections[2], "missing phrase stays English");
                Equal("Una : 9 pas, gauche, pas de chemin", Announcements.ScanEntry("Una", 9, Announcements.StickDirections[4], 0, null, false), "nested phrases");
                Equal("2 of 5", Announcements.ListPosition(2, 5), "a broken translation falls back to English");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private static void ChatSpeakersGetASpace()
        {
            Equal("Renly: Esto es lo que tengo.", ChatDiff.Spoken("Renly:Esto es lo que tengo."), "speaker glued to the line");
            Equal("Una: Hola", ChatDiff.Spoken("Una: Hola"), "already spaced");
            Equal("Son las 12:30", ChatDiff.Spoken("Son las 12:30"), "a time keeps its colon");
            Equal("zzChopanz: Gran yelmo imperial", ChatDiff.Spoken("#zzChopanz:Gran yelmo imperial"), "global sign dropped");
            Equal("Ana: WTS", ChatDiff.Spoken("$Ana: WTS"), "trade sign dropped");
            var mine = new[] { "yarkey", string.Empty };
            Equal(true, ChatDiff.IsFrom("$yarkey: WTS botas", mine), "own trade line, said whatever the channel setting");
            Equal(true, ChatDiff.IsFrom("#<KAOS> yarkey:hola", mine), "own global line after a guild tag");
            Equal(true, ChatDiff.IsFrom("yarkey: ¡Necesito más tiempo!", mine), "own local remark");
            Equal(false, ChatDiff.IsFrom("#yarkeyz: hola", mine), "another player whose name starts the same");
            Equal(false, ChatDiff.IsFrom("@Para yarkey: hola", mine), "a whisper names its recipient, not the writer");
            Equal(false, ChatDiff.IsFrom("Son las 12:30", mine), "a line without a speaker");
            Equal(true, ChatDiff.IsPlayerChannel("#Ana: hola") && !ChatDiff.IsPlayerChannel("@De Ana: hola"), "whispers are not player channels");
            Equal("De Dahnte: hola", ChatDiff.Spoken("@De Dahnte:hola"), "whisper sign dropped");
            Equal("Ana: vamos", ChatDiff.Spoken("%Ana:vamos"), "party sign dropped");
        }

        private static void SmallCapsCaptionsGetACapital()
        {
            Equal("Reanudar juego", Announcements.Capitalised("reanudar juego"), "game menu entry");
            Equal("Confirmar A", Announcements.Capitalised("confirmar A"), "a button letter is not a capital word");
            Equal("kehgan, Nivel 2 Monje", Announcements.Capitalised("kehgan, Nivel 2 Monje"), "capitals in its words: as written");
            Equal("+ 0.0", Announcements.Capitalised("+ 0.0"), "no letter to raise");
            Equal(string.Empty, Announcements.Capitalised(string.Empty), "empty");
        }

        private static void QuestRewardIconsAreNamed()
        {
            Localization.Detect("es");
            Equal("gema de asistencia", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardSupportGem.dds|0,0,0,0|0"), "support gem");
            Equal("oro", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardGold.dds|0,0,0,0|0"), "gold");
            Equal("NewKind", QuestRewards.FromTextureKey("Art/2DItems/QuestItems/RewardNewKind.dds|0,0,0,0|0"), "a kind not known yet keeps its art name");
            Equal(null, QuestRewards.FromTextureKey("Art/2DItems/Currency/Ruthless/CoinPileTier1.dds|0,0,0,0|0"), "another image");
            Localization.Detect("en");
        }

        private static void MapPiecesAndPropsArePlaced()
        {
            const int sub = 23;
            var catalog = PlaceCatalog.Parse(@"{
                ""tiles"": { ""metadata/terrain/a/bridge_01.tdt"": ""bridge"", ""metadata/terrain/a/town.tdt"": ""mill"" },
                ""tileProps"": { ""metadata/terrain/a/bridge_01.tdt"": [[30, 5, ""cart""]] },
                ""rooms"": [ { ""arm"": ""town.arm"", ""anchors"": [[282, 252, ""waypoint""], [349, 237, ""well""]], ""props"": [[376, 211, ""cart""]] } ]
            }");
            var placements = new List<TilePlacement>
            {
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 0, new(0, 0)),
                new("metadata/terrain/a/bridge_01.tdt", 1, 0, 0, new(sub, 0)),
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 3, new(5 * sub, 2 * sub)),
                new("metadata/terrain/a/bridge_01.tdt", 1, 0, 3, new(6 * sub, 2 * sub)),
                new("metadata/terrain/a/bridge_01.tdt", 0, 0, 2, new(9 * sub, 2 * sub)),
            };
            placements.AddRange(Enumerable.Range(0, 10).Select(i => new TilePlacement("metadata/terrain/a/town.tdt", i, 0, 0, new(i * sub, 10 * sub))));
            placements.AddRange(Enumerable.Range(0, 20).Select(i => new TilePlacement("metadata/terrain/a/grass.tdt", 0, 0, 0, new(i * sub, 20 * sub))));
            placements.AddRange(Enumerable.Range(0, 40).Select(i => new TilePlacement("metadata/terrain/blank.tdt", 0, 0, 0, new(i * sub, 30 * sub))));
            var map = PlaceMap.Build(catalog, placements, sub);
            Equal("bridge", map.KindAt(new Vector2(30, 10)), "a named tile's sub-tiles");
            Equal(null, map.KindAt(new Vector2(30, 10 * sub + 5)), "a kind covering most of the area is the area itself");
            Equal(3, map.Pieces.Count(piece => piece.Kind == "bridge"), "separate bridges");
            Equal($"30,5 {5 * sub + 5},{2 * sub + sub - 30}", string.Join(" ", map.Props.Select(prop => $"{prop.Grid.X},{prop.Grid.Y}")), "props turned with their tile; an unknown turn leaves them out");
            Equal("2", string.Join(",", map.UnknownRotations), "and says which turn it was");
            Equal(false, map.MatchRooms(new[] { ("waypoint", new Vector2(282, 252)) }), "one anchor of two does not place a room");
            Equal(true, map.MatchRooms(new[] { ("waypoint", new Vector2(283, 252)), ("well", new Vector2(349, 236)) }), "both anchors place it");
            Equal(3, map.Props.Count, "the room's cart joins the tiles'");
        }

        private static void PlacesAreSaidOnceAsTheyChange()
        {
            var tracker = new PlaceTracker(23, 25, 60);
            var props = new List<(Vector2 Grid, string Kind)> { (new Vector2(100, 0), "cart") };
            string Walk(string? kind, float x) => string.Join("+", tracker.Update(kind, new Vector2(x, 0), props));
            Equal("", Walk("bridge", 0), "a piece waits one update");
            Equal("bridge", Walk("bridge", 2), "then it is said");
            Equal("", Walk("bridge", 4), "once");
            Equal("", Walk(null, 20), "plain ground near it keeps the name");
            Equal("", Walk("bridge", 24), "so the bridge's next piece is not news");
            Equal("cart", Walk(null, 80), "a prop in reach");
            Equal("", Walk(null, 90), "said once");
            Equal("", Walk(null, 200), "far away: forgotten");
            Equal("cart", Walk(null, 110), "and said again on the way back");
            Equal("", Walk("stairs", 300), "a new kind waits");
            Equal("stairs", Walk("stairs", 302), "and is said");
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
