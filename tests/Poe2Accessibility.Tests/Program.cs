namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Runs every test; exit code 1 when any fails. Run: dotnet run --project tests/Poe2Accessibility.Tests</summary>
    public static class Program
    {
        /// <summary>Dev command: compare the x64 decoder with a full capstone dump (re/decoder_truth.py).</summary>
        private const string DecoderTruthCommand = "x64-truth";

        /// <summary>Dev command: run the semantic rules of a catalog on an exe.</summary>
        private const string SemanticCommand = "semantic";

        private static int failures;

        public static int Main(string[] args)
        {
            if (args is [DecoderTruthCommand, var truth])
            {
                return X64DecoderTests.CompareFile(truth);
            }

            if (args is [SemanticCommand, var exe, var catalog])
            {
                return SemanticRuleTests.CheckExe(exe, catalog);
            }

            if (args is [StaticAnchorTests.CheckCommand, var image])
            {
                return StaticAnchorTests.CheckExe(image);
            }

            if (args is [StaticAnchorTests.GateCommand, var build, var catalogs])
            {
                return StaticAnchorTests.CheckBuild(build, catalogs);
            }

            if (args is [PrismTests.CheckCommand, var prismFolder])
            {
                return PrismTests.Check(prismFolder);
            }

            Run(nameof(BaselineIsSilentExceptTopLayer), BaselineIsSilentExceptTopLayer);
            Run(nameof(PopupNeedsTwoSnapshots), PopupNeedsTwoSnapshots);
            Run(nameof(ChangedTextIsReportedOnce), ChangedTextIsReportedOnce);
            Run(nameof(CounterTicksAreIgnored), CounterTicksAreIgnored);
            Run(nameof(ClosedPopupIsNewsAgain), ClosedPopupIsNewsAgain);
            Run(nameof(InGameBaseLayersNeverOwnInput), InGameBaseLayersNeverOwnInput);
            Run(nameof(TypingIsReportedAsTheEdit), TypingIsReportedAsTheEdit);
            Run(nameof(CreationNameDeadEndsAreSaid), CreationNameDeadEndsAreSaid);
            Run(nameof(DiabloLoopsStopAtTheirTopPitch), DiabloLoopsStopAtTheirTopPitch);
            Run(nameof(OnlyTheLoadingScreenSpeaksWhileAnAreaLoads), OnlyTheLoadingScreenSpeaksWhileAnAreaLoads);
            Run(nameof(ChatReportsOnlyAppendedLines), ChatReportsOnlyAppendedLines);
            Run(nameof(L3GesturesAreToldApart), L3GesturesAreToldApart);
            Run(nameof(StickDirectionsBinInWorldSpace), StickDirectionsBinInWorldSpace);
            Run(nameof(ScannerBrowsesNearestFirstAndFollowsTheEntity), ScannerBrowsesNearestFirstAndFollowsTheEntity);
            Run(nameof(RouteLegsKeepRealTurnsOnly), RouteLegsKeepRealTurnsOnly);
            Run(nameof(LifeWarningsSpeakOncePerFall), LifeWarningsSpeakOncePerFall);
            Run(nameof(EnemyCuesPulseFasterWhenNearAndTakeTurns), EnemyCuesPulseFasterWhenNearAndTakeTurns);
            Run(nameof(EnemyCueSoundsStayInRangeAndDiffer), EnemyCueSoundsStayInRangeAndDiffer);
            Run(nameof(CuesRingOutAndTheMixerRoundsOff), CuesRingOutAndTheMixerRoundsOff);
            Run(nameof(LoudCuesAreLimitedNotSaturated), LoudCuesAreLimitedNotSaturated);
            Run(nameof(AreaMemoryKeepsTheSameMapOnly), AreaMemoryKeepsTheSameMapOnly);
            Run(nameof(AreaMemoryCarriesAFixedMapsExits), AreaMemoryCarriesAFixedMapsExits);
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
            Run(nameof(ObjectsPlayTheSoundTheGameGivesThem), ObjectsPlayTheSoundTheGameGivesThem);
            Run(nameof(CodeFieldsNeedTwoSignaturesThatAgree), CodeFieldsNeedTwoSignaturesThatAgree);
            Run(nameof(CodeFieldCatalogParses), CodeFieldCatalogParses);
            Run(nameof(LayoutOverridesCountOnlyForTheirBuild), LayoutOverridesCountOnlyForTheirBuild);
            Run(nameof(CodeFieldsSettleEachFieldApart), CodeFieldsSettleEachFieldApart);
            Run(nameof(TheStunRecordIsFoundByItsHead), TheStunRecordIsFoundByItsHead);
            Run(nameof(TheGridMustHoldWhereThePlayerWalks), TheGridMustHoldWhereThePlayerWalks);
            Run(nameof(OpeningSoundsComeFromTheOpeningAnimation), OpeningSoundsComeFromTheOpeningAnimation);
            Run(nameof(PhrasesFollowTheGameLanguage), PhrasesFollowTheGameLanguage);
            Run(nameof(OtherLanguagesUsePhraseTables), OtherLanguagesUsePhraseTables);
            Run(nameof(ConfirmGlyphMarksTheEntryBesideIt), ConfirmGlyphMarksTheEntryBesideIt);
            Run(nameof(BeaconPointsAlongTheRoute), BeaconPointsAlongTheRoute);
            Run(nameof(ClosedWayLeadsToItsNearestPoint), ClosedWayLeadsToItsNearestPoint);
            Run(nameof(QuestNamesItsTargets), QuestNamesItsTargets);
            Run(nameof(QuestTrackerPageIsSaidWhenItChanges), QuestTrackerPageIsSaidWhenItChanges);
            Run(nameof(QuestPageWaitsForTheMenuToClose), QuestPageWaitsForTheMenuToClose);
            Run(nameof(QuestPageSaysOnlyTheQuestThatChanged), QuestPageSaysOnlyTheQuestThatChanged);
            Run(nameof(TurnedQuestPageIsToldFromTheObjectivesOwnChange), TurnedQuestPageIsToldFromTheObjectivesOwnChange);
            Run(nameof(TakingTheFloorCutsAKeptLine), TakingTheFloorCutsAKeptLine);
            Run(nameof(AFallingClockIsSaidAtItsMarks), AFallingClockIsSaidAtItsMarks);
            Run(nameof(TrialMapIsColumnsFromEntranceToBoss), TrialMapIsColumnsFromEntranceToBoss);
            Run(nameof(SheetWidgetsAreToldByTheirTooltipsKeys), SheetWidgetsAreToldByTheirTooltipsKeys);
            Run(nameof(TrialHudCountsWaterAndKeys), TrialHudCountsWaterAndKeys);
            Run(nameof(CameraViewIsASixteenByNineScreenAroundThePlayer), CameraViewIsASixteenByNineScreenAroundThePlayer);
            Run(nameof(CameraViewDrawsFromTheMatrixWhateverTheWindow), CameraViewDrawsFromTheMatrixWhateverTheWindow);
            Run(nameof(TrialTrapsAreToldByTheirFiles), TrialTrapsAreToldByTheirFiles);
            Run(nameof(TheGuideGoesFromCrystalToCrystal), TheGuideGoesFromCrystalToCrystal);
            Run(nameof(HonourIsSaidAsItFalls), HonourIsSaidAsItFalls);
            Run(nameof(WorldIsBackOnlyOnceNothingCoversIt), WorldIsBackOnlyOnceNothingCoversIt);
            Run(nameof(PanelOverTheGameIsTheTopLayerThatIsNotTheHud), PanelOverTheGameIsTheTopLayerThatIsNotTheHud);
            Run(nameof(ItemMovesAreSaidAsDeeds), ItemMovesAreSaidAsDeeds);
            Run(nameof(SalvageSaysWhatWentAndWhatCame), SalvageSaysWhatWentAndWhatCame);
            Run(nameof(ReleaseTagsOrderByDayThenNumber), ReleaseTagsOrderByDayThenNumber);
            Run(nameof(ReleaseManifestPinsThePackageToItsTag), ReleaseManifestPinsThePackageToItsTag);
            Run(nameof(GameDataStepsRerunAlone), GameDataStepsRerunAlone);
            Run(nameof(OldGameDataStampCarriesOver), OldGameDataStampCarriesOver);
            Run(nameof(GameDataStepsAreEachListedOnce), GameDataStepsAreEachListedOnce);
            Run(nameof(GameContentIsFoundInEitherLayout), GameContentIsFoundInEitherLayout);
            Run(nameof(KakaoClientIsAGameProcess), KakaoClientIsAGameProcess);
            Run(nameof(AClassSeesItsOwnPassives), AClassSeesItsOwnPassives);
            Run(nameof(SteamGameIsStartedBeforeTheKakaoClient), SteamGameIsStartedBeforeTheKakaoClient);
            Run(nameof(TradeListingSaysItemPriceThenSeller), TradeListingSaysItemPriceThenSeller);
            Run(nameof(NearbyThingsWaitForTheCooldown), NearbyThingsWaitForTheCooldown);
            Run(nameof(StickClickWhileHoldingL3), StickClickWhileHoldingL3);
            Run(nameof(WallTonesSwellNearWalls), WallTonesSwellNearWalls);
            Run(nameof(StashOpenedOnInventoryNamesTheInventory), StashOpenedOnInventoryNamesTheInventory);
            Run(nameof(NewsFollowItsDeliveryLevel), NewsFollowItsDeliveryLevel);
            Run(nameof(SettingsMenuOpensGroupsAndGoesBack), SettingsMenuOpensGroupsAndGoesBack);
            Run(nameof(PassiveStepsCountThePointsAlongTheLinks), PassiveStepsCountThePointsAlongTheLinks);
            Run(nameof(PassiveZoneNamesTheSideAndRing), PassiveZoneNamesTheSideAndRing);
            Run(nameof(PassiveFinderWalksItsLists), PassiveFinderWalksItsLists);
            Run(nameof(APassiveTrailTakesTheFewestPointsAlongTheBuild), APassiveTrailTakesTheFewestPointsAlongTheBuild);
            Run(nameof(TheTreeRemembersWhereEachCharacterLeftOff), TheTreeRemembersWhereEachCharacterLeftOff);
            Run(nameof(ObjectSoundsDieAwayPastTheirRange), ObjectSoundsDieAwayPastTheirRange);
            Run(nameof(BuildFilesAreReadWithTheirNotes), BuildFilesAreReadWithTheirNotes);
            Run(nameof(MinionsAreCountedByKind), MinionsAreCountedByKind);
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
            Run(nameof(ALayoutLostTooEarlyIsJudgedAgain), ALayoutLostTooEarlyIsJudgedAgain);
            Run(nameof(TownsAndHideoutsAreToldByTheirRow), TownsAndHideoutsAreToldByTheirRow);
            Run(nameof(TheHoveredPinIsTheKeyThatChangesWithTheCursor), TheHoveredPinIsTheKeyThatChangesWithTheCursor);
            Run(nameof(CheckpointsAreNamedByTheirNeighbour), CheckpointsAreNamedByTheirNeighbour);
            Run(nameof(FarBarrierStaysClosedUntilSeenNear), FarBarrierStaysClosedUntilSeenNear);
            Run(nameof(ScrolledRowsAreNotNews), ScrolledRowsAreNotNews);
            Run(nameof(FocusDetailsAreNotScrolledRows), FocusDetailsAreNotScrolledRows);
            Run(nameof(KeptLessonIsHeardWhole), KeptLessonIsHeardWhole);
            Run(nameof(KeptLessonHoldsFromItsStart), KeptLessonHoldsFromItsStart);
            Run(nameof(PlayerMoveFollowsAMessage), PlayerMoveFollowsAMessage);
            Run(nameof(PendingLessonIsRemindedNowAndThen), PendingLessonIsRemindedNowAndThen);
            Run(nameof(WeakPointLeadsToTheHead), WeakPointLeadsToTheHead);
            Run(nameof(WeakPointBeatsFasterThenSteady), WeakPointBeatsFasterThenSteady);
            Run(nameof(ShieldRaisedAndLowered), ShieldRaisedAndLowered);
            Run(nameof(StunBarSpeaksEachStepOnce), StunBarSpeaksEachStepOnce);
            Run(nameof(ParryCueOnlyForBlowsToParry), ParryCueOnlyForBlowsToParry);
            Run(nameof(ParryCueIsItsOwnSound), ParryCueIsItsOwnSound);
            Run(nameof(UnblockableAttacksAreNeverToParry), UnblockableAttacksAreNeverToParry);
            Run(nameof(HarmfulGroundIsKnownByItsBuff), HarmfulGroundIsKnownByItsBuff);
            Run(nameof(HazardAlarmLoopsWithoutASeam), HazardAlarmLoopsWithoutASeam);
            Run(nameof(CullingThresholdsByRarity), CullingThresholdsByRarity);
            Run(nameof(CullingSkillsComeFromTheCatalog), CullingSkillsComeFromTheCatalog);
            Run(nameof(ChargesAreKnownByTheirBuff), ChargesAreKnownByTheirBuff);
            Run(nameof(SkillsAreReadyOnceTheirCooldownEnds), SkillsAreReadyOnceTheirCooldownEnds);
            Run(nameof(PlacedObjectsSoundByDistance), PlacedObjectsSoundByDistance);
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
            Run(nameof(ATooltipLineSwappedInPlaceIsSaid), ATooltipLineSwappedInPlaceIsSaid);
            Run(nameof(OnlyASwappedLineCutsIn), OnlyASwappedLineCutsIn);
            Run(nameof(AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice), AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice);
            Run(nameof(RadarLabelsSpeakSpanish), RadarLabelsSpeakSpanish);
            Run(nameof(RadarBossNamesSpeakTheGamesMonsterName), RadarBossNamesSpeakTheGamesMonsterName);
            Run(nameof(ExperienceProgressIsWithinTheLevel), ExperienceProgressIsWithinTheLevel);
            Run(nameof(LootFilterKeepsWhatThePlayerChose), LootFilterKeepsWhatThePlayerChose);
            Run(nameof(ItemsSoundUnderTheirGroupsVolume), ItemsSoundUnderTheirGroupsVolume);
            Run(nameof(DiabloIVSetCoversLootByKindAndRarity), DiabloIVSetCoversLootByKindAndRarity);
            Run(nameof(LoopReaderKeepsAPitchedLoopClean), LoopReaderKeepsAPitchedLoopClean);
            Run(nameof(APileOfLoopsLeavesHeadroom), APileOfLoopsLeavesHeadroom);
            Run(nameof(SteamsCommandIsFoundInTheLaunchersArguments), SteamsCommandIsFoundInTheLaunchersArguments);
            Run(nameof(FightsLastWhileAnEngagingEnemyIsNear), FightsLastWhileAnEngagingEnemyIsNear);
            Run(nameof(AWaitingBossMakesNoFight), AWaitingBossMakesNoFight);
            Run(nameof(AHiddenMonsterIsToldByItsBuff), AHiddenMonsterIsToldByItsBuff);
            Run(nameof(ABossOfSeveralBodiesIsHeardOnce), ABossOfSeveralBodiesIsHeardOnce);
            Run(nameof(OnlyLinesSaidAloudNearbyAreLocalSpeech), OnlyLinesSaidAloudNearbyAreLocalSpeech);
            Run(nameof(InPlayFlagIsFoundFromStoryVariants), InPlayFlagIsFoundFromStoryVariants);
            Run(nameof(MovedFieldsAreFoundByWatchingThemChange), MovedFieldsAreFoundByWatchingThemChange);
            Run(nameof(FlagBitsAreFoundOnTheirCarriers), FlagBitsAreFoundOnTheirCarriers);
            Run(nameof(PadButtonsFollowTheLeftTrigger), PadButtonsFollowTheLeftTrigger);
            Run(nameof(TileGridIsFoundByWhatItsRecordsHold), TileGridIsFoundByWhatItsRecordsHold);
            Run(nameof(BaseNamesAreCheckedAgainstTooltipTitles), BaseNamesAreCheckedAgainstTooltipTitles);
            Run(nameof(MinimapMarksNameBossRoomsAndLandmarks), MinimapMarksNameBossRoomsAndLandmarks);
            Run(nameof(CrossingAnAreaLeadsToTheWayOn), CrossingAnAreaLeadsToTheWayOn);
            Run(nameof(ExploredGroundIsKeptAsBytes), ExploredGroundIsKeptAsBytes);
            Run(nameof(SignsAreNeverQuestTargetsAndQuestIconsAlways), SignsAreNeverQuestTargetsAndQuestIconsAlways);
            Run(nameof(FrontierTellsNooksFromGroundThatGoesOn), FrontierTellsNooksFromGroundThatGoesOn);
            Run(nameof(RadarHearsAnExitInItsWallAndOffItsLane), RadarHearsAnExitInItsWallAndOffItsLane);
            Run(nameof(MapCornerFollowsTheScreenNotTheGrid), MapCornerFollowsTheScreenNotTheGrid);
            Run(nameof(RadarFindsTheDoorwayToAnotherRoom), RadarFindsTheDoorwayToAnotherRoom);
            Run(nameof(QuestWayLeavesTheSeenGroundTowardTheGoal), QuestWayLeavesTheSeenGroundTowardTheGoal);
            Run(nameof(RadarMarksTheWayToUnseenGround), RadarMarksTheWayToUnseenGround);
            Run(nameof(LostMeansWalkingWithoutNewGround), LostMeansWalkingWithoutNewGround);
            Run(nameof(MapHintsAreSaidOncePerPlace), MapHintsAreSaidOncePerPlace);
            Run(nameof(ShippedMapHintsAreWellFormed), ShippedMapHintsAreWellFormed);
            Run(nameof(SpiritValuesNameTheirWeaponSet), SpiritValuesNameTheirWeaponSet);
            foreach (var (name, test) in X64DecoderTests.All().Concat(SemanticRuleTests.All()))
            {
                Run(name, test);
            }

            // One suite a line: two branches adding one each then merge by themselves.
            var suites = new[]
            {
                LayoutSearchTests.All(),
                HostSearchTests.All(),
                StdMapWalkTests.All(),
                TickCostsTests.All(),
                ReaderBufferTests.All(),
                ListingTests.All(),
                GridSamplesTests.All(),
                SettlingMemoTests.All(),
                NameFallbackTests.All(),
                ReachableRuleTests.All(),
                TrialDataTests.All(),
                PanelWidgetTests.All(),
                DodgeGateTests.All(),
                ClassMechanicTests.All(),
                LiveValueTests.All(),
                DodgeDataTests.All(),
                TelegraphTests.All(),
                RouteSpeedTests.All(),
                RadarLabelTests.All(),
                PassiveStickTests.All(),
                DatColumnSearchTests.All(),
                DatColumnKindTests.All(),
                GameFormatTests.All(),
                GemColumnTests.All(),
                ExtractorColumnTests.All(),
                ItemReadingTests.All(),
                BuildGemTests.All(),
                BuildGearTests.All(),
                FileCatalogTests.All(),
                AgreedChoiceTests.All(),
                ShopPurchaseTests.All(),
                BlockedMoveTests.All(),
                TooltipSettleTests.All(),
                StatKeyTests.All(),
                SkillNameTests.All(),
                GemIndexTests.All(),
                GearCatalogTests.All(),
                StaleValueTests.All(),
                StaticAnchorTests.All(),
                ExtractionGuardTests.All(),
                RobustnessTests.All(),
                HostAndToolsAuditTests.All(),
                SpeechEventTests.All(),
                SpokenOnceTests.All(),
                WorldCombatAuditTests.All(),
                PlayerItemNewsTests.All(),
            };
            foreach (var (name, test) in suites.SelectMany(suite => suite))
            {
                Run(name, test);
            }

            foreach (var (name, test) in EndgameTests.All().Concat(BossMechanicsTests.All()).Concat(StartUpTests.All()).Concat(StartupSpeechTests.All()).Concat(PrismTests.All()))
            {
                Run(name, test);
            }

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
            Equal(true, ScreenDiffer.SameBesideNumbers("11 minutos 2 segundos", "11 minutos 1 segundo"), "a clock passing 1 (Spanish)");
            Equal(true, ScreenDiffer.SameBesideNumbers("11 minutos 1 segundo", "11 minutos 0 segundos"), "and on to 0");
            Equal(true, ScreenDiffer.SameBesideNumbers("1 Sekunde", "2 Sekunden"), "German plural");
            Equal(true, ScreenDiffer.SameBesideNumbers("1 minute 5 seconds", "2 minutes 5 seconds"), "English plural");
            Equal(false, ScreenDiffer.SameBesideNumbers("3 rounds left", "3 lives left"), "another word is news");
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

        private static void OnlyTheLoadingScreenSpeaksWhileAnAreaLoads()
        {
            ScreenText Text(string name) => new(IntPtr.Zero, "text", name, false, true);
            Equal(true, Text("loading_screen_tip_label").IsLoadingScreen, "the tip");
            Equal(true, Text("loading_screen_area_name").IsLoadingScreen, "the area's name");
            Equal(false, Text("quest_title").IsLoadingScreen, "the HUD left in the tree");
            Equal(false, Text(string.Empty).IsLoadingScreen, "a chat line");
        }

        private static void DiabloLoopsStopAtTheirTopPitch()
        {
            Equal(1.12f, ItemSoundSet.LoopPitch(1.26f), "straight up stops at the top");
            Equal(1.05f, ItemSoundSet.LoopPitch(1.05f), "under it nothing changes");
            Equal(0.8f, ItemSoundSet.LoopPitch(0.8f), "down still sounds down");
        }

        private static void CreationNameDeadEndsAreSaid()
        {
            const int Max = 23;
            var typed = new CreationPresses(true, false, false);
            var enter = new CreationPresses(false, true, false);
            var a = new CreationPresses(false, false, true);
            var grey = new Func<bool>(() => true);
            var lit = new Func<bool>(() => false);
            var unread = new Func<bool>(() => throw new InvalidOperationException("Continue read for nothing"));
            CreationAdvice Typing(CreationNameGuide guide, long now, string name, CreationPresses presses, Func<bool> state) =>
                guide.Tick(now, CreationSpot.Typing, name, presses, state);

            var guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, Typing(guide, 0, "ab", default, unread), "entering the field");
            Equal(CreationAdvice.None, Typing(guide, 10, "ab", typed, unread), "a key is judged later");
            Equal(CreationAdvice.None, Typing(guide, 100, "ab", default, unread), "too soon to judge");
            Equal(CreationAdvice.CharacterRefused, Typing(guide, 300, "ab", default, unread), "a digit changes nothing");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Typing(guide, 10, "ab", typed, unread);
            Equal(CreationAdvice.None, Typing(guide, 40, "abc", default, unread), "a letter shows up");
            Equal(CreationAdvice.None, Typing(guide, 400, "abc", default, unread), "and nothing is said later");

            guide = new CreationNameGuide(Max);
            var full = new string('a', Max);
            Typing(guide, 0, full, default, unread);
            Typing(guide, 10, full, typed, unread);
            Equal(CreationAdvice.NameFull, Typing(guide, 300, full, default, unread), "a letter past the limit");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Equal(CreationAdvice.NameTooShort, Typing(guide, 10, "ab", enter, grey), "Enter with Continue grey");
            Equal(CreationAdvice.None, Typing(guide, 20, "ab", enter, grey), "a held Enter says it once");
            Equal(CreationAdvice.NameTooShort, Typing(guide, 2000, "ab", enter, grey), "pressed again later");
            Equal(CreationAdvice.None, Typing(guide, 3000, "abc", enter, lit), "Enter with a good name creates");
            Equal(CreationAdvice.HowToFinish, Typing(guide, 4000, "abc", a, lit), "A in the field does not create");
            Equal(CreationAdvice.NameTooShort, Typing(guide, 6000, "ab", a, grey), "A in the field, name too short");

            guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, Typing(guide, 0, "abc", a, unread), "the A that opened the field");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "ab", default, unread);
            Equal(CreationAdvice.NameTooShort, guide.Tick(10, CreationSpot.Outside, "ab", default, grey), "B with Continue grey");
            Equal(CreationAdvice.TypeOnTheName, guide.Tick(20, CreationSpot.Outside, "ab", typed, unread), "typing on the frame");
            Equal(CreationAdvice.None, guide.Tick(30, CreationSpot.Outside, "ab", typed, unread), "a held key says it once");

            guide = new CreationNameGuide(Max);
            Typing(guide, 0, "abc", default, unread);
            Equal(CreationAdvice.None, guide.Tick(10, CreationSpot.Outside, "abc", default, lit), "B with a good name lands on Continue");

            guide = new CreationNameGuide(Max);
            Equal(CreationAdvice.None, guide.Tick(0, CreationSpot.Elsewhere, string.Empty, typed, unread), "typing on the class choice");
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
            Equal(true, L3Gestures.MovedFocus(1000, 1040) && !L3Gestures.MovedFocus(1000, 700), "a focus born of the press is the game's doing, an older one is read");
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
        }

        private static void LoudCuesAreLimitedNotSaturated()
        {
            const int rate = 44100;
            var tone = Enumerable.Range(0, rate / 2).Select(i => 0.85f * MathF.Sin(2 * MathF.PI * 220 * i / rate)).ToArray();
            var (quiet, _) = PeakLimiter.Drive(tone, tone, 1f, rate);
            Equal(true, quiet.Zip(tone).All(pair => MathF.Abs(pair.First - pair.Second) < 1e-6f), "under the ceiling a cue is untouched");

            // A steady tone at full scale has no quiet body to raise; a cue's decaying body grows.
            foreach (var (name, sound, louder) in new[] { ("tone", tone, 1.1f), ("dodge", CueSynth.Dodge(rate), 1.5f), ("parry", CueSynth.Parry(rate), 1.5f) })
            {
                var (loud, _) = PeakLimiter.Drive(sound, sound, 3f, rate);
                Equal(sound.Length, loud.Length, $"{name} keeps its length at 300%");
                Equal(true, loud.Max(MathF.Abs) <= PeakLimiter.Ceiling + 1e-4f, $"{name} at 300% stays under the ceiling");
                Equal(true, Rms(loud) > Rms(sound) * louder, $"{name} at 300% is louder ({Rms(loud) / Rms(sound):0.00} times)");

                // Limited, not saturated: the output is the input times a gain that glides, never a reshaped wave.
                var gains = sound.Zip(loud).Where(pair => MathF.Abs(pair.First) > 0.05f).Select(pair => pair.Second / (pair.First * 3f)).ToArray();
                Equal(true, gains.All(gain => gain is > 0f and <= 1.0001f), $"{name}: only a gain, same sign");
                var jumps = sound.Select((sample, i) => i > 0 && MathF.Abs(sample) > 0.05f && MathF.Abs(sound[i - 1]) > 0.05f
                    ? MathF.Abs((loud[i] / (sample * 3f)) - (loud[i - 1] / (sound[i - 1] * 3f))) : 0f).Max();
                Equal(true, jumps < 0.02f, $"{name}: the gain glides ({jumps:0.0000} a sample at most)");
            }

            var output = new PeakLimiter(rate);
            var peak = 0f;
            for (var i = 0; i < rate; i++)
            {
                var left = 3f * MathF.Sin(2 * MathF.PI * 110 * i / rate);
                var right = -left;
                output.Process(ref left, ref right);
                peak = MathF.Max(peak, MathF.Max(MathF.Abs(left), MathF.Abs(right)));
            }

            Equal(true, peak <= PeakLimiter.Ceiling + 1e-4f, "a pile-up at the output never passes the ceiling");

            static float Rms(float[] samples) => MathF.Sqrt(samples.Average(sample => sample * sample));
        }

        private static void AreaMemoryKeepsTheSameMapOnly()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "explored-" + Guid.NewGuid().ToString("N"));
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var tiles = new Dictionary<string, List<Vector2>> { ["forest_a"] = new() { new(23, 0), new(0, 0) }, ["exit"] = new() { new(46, 23) } };
            var shuffled = new Dictionary<string, List<Vector2>> { ["exit"] = new() { new(46, 23) }, ["forest_a"] = new() { new(0, 0), new(23, 0) } };
            var regenerated = new Dictionary<string, List<Vector2>> { ["forest_a"] = new() { new(0, 0), new(23, 23) }, ["exit"] = new() { new(46, 0) } };
            var map = AreaMemory.Layout(tiles, 100, 20000);
            Equal(map, AreaMemory.Layout(shuffled, 100, 20000), "the same tiles in another order are the same map");
            Equal(true, map != AreaMemory.Layout(regenerated, 100, 20000), "other tiles are another map");
            Equal(true, map != AreaMemory.Layout(tiles, 120, 20000), "another grid is another map");

            var exit = new ScanEntry(7, ScanCategory.Exits, "Sierraclara", 1f, 2f, 3f, "exit", Sound: "exit", QuestMarked: true, Subject: "way");
            var snapshot = new AreaMemory.Snapshot(map, new byte[] { 1, 2, 3 }, "Campamento", new() { [7] = exit }, new() { 7, 9 }, new() { [11] = new Vector2(4, 5) });
            var memory = new AreaMemory(directory, log);
            memory.Keep("ABC", snapshot);
            Equal(true, memory.Recall("ABC", map) == snapshot, "back in the same instance: everything returns");

            var restarted = new AreaMemory(directory, log);
            var fromDisk = restarted.Recall("ABC", map);
            Equal(true, fromDisk != null, "after a host restart it returns from disk");
            Equal(exit, fromDisk!.Remembered[7], "with its exits as they were");
            Equal("Campamento", fromDisk.CameFrom, "and the way in");
            Equal(true, fromDisk.Explored.SequenceEqual(snapshot.Explored) && fromDisk.Sighted.SetEquals(snapshot.Sighted), "and the explored ground");
            Equal(new Vector2(4, 5), fromDisk.ClosedBarriers[11], "and the closed barriers");

            Equal(true, restarted.Recall("ABC", AreaMemory.Layout(regenerated, 100, 20000)) == null, "a regenerated map under the same hash starts unexplored");
            Equal(true, new AreaMemory(directory, log).Recall("ABC", map) == null, "and its old file is gone");
            Equal(true, memory.Recall("XYZ", map) == null, "an instance never seen is new");
            System.IO.Directory.Delete(directory, true);
        }

        private static void AreaMemoryCarriesAFixedMapsExits()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "explored-" + Guid.NewGuid().ToString("N"));
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            const string Transition = "Metadata/Terrain/Woods/AreaTransitions/Clearfell_OldForest_Transition_01.tdtx:0-y:0";
            var first = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(1311, 552) } };
            var varied = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_B.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(1311, 552) } };
            var moved = new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) }, [Transition] = new() { new(207, 1173) } };
            var frame = AreaMemory.Frame(first, 100, 20000);
            Equal(true, AreaMemory.Layout(first, 100, 20000) != AreaMemory.Layout(varied, 100, 20000), "Clearfell again: other pieces, another layout");
            Equal(frame, AreaMemory.Frame(varied, 100, 20000), "but its exits where they were: the same frame");
            Equal(true, frame != AreaMemory.Frame(moved, 100, 20000), "a regenerated map moves its exits: another frame");
            Equal(0UL, AreaMemory.Frame(new Dictionary<string, List<Vector2>> { ["Metadata/Terrain/Woods/Stump_A.tdtx:0-y:0"] = new() { new(23, 0) } }, 100, 20000),
                  "a map without transition pieces has no frame");

            var exit = new ScanEntry(7, ScanCategory.Exits, "El bosque singular", 1f, 2f, 3f, "exit");
            var checkpoint = new ScanEntry(8, ScanCategory.Waypoints, "Punto de control", 4f, 5f, 6f, "checkpoint");
            var chest = new ScanEntry(9, ScanCategory.Chests, "Cofre", 7f, 8f, 9f, "chest");
            var death = new ScanEntry(10, ScanCategory.Exits, "Donde moriste", 7f, 8f, 9f);
            var npc = new ScanEntry(11, ScanCategory.Npcs, "Una", 7f, 8f, 9f, "npc");
            var portal = new ScanEntry(12, ScanCategory.Waypoints, "Pueblo de Ogham", 7f, 8f, 9f, ScanEntry.PortalKind);
            Equal(true, portal.CanVanish && chest.CanVanish, "a portal closes as a chest is opened");
            Equal(true, new ScanEntry(13, ScanCategory.PointsOfInterest, "Jaula", 7f, 8f, 9f, MinimapMarks.LandmarkKind, QuestMarked: true).CanVanish, "a quest's object goes once used");
            Equal(false, new ScanEntry(14, ScanCategory.PointsOfInterest, "Pozo", 7f, 8f, 9f, MinimapMarks.LandmarkKind).CanVanish, "a labelled place on the map stays");
            Equal(false, checkpoint.CanVanish || exit.CanVanish || npc.CanVanish, "a checkpoint, an exit and a character stay");
            var left = new AreaMemory.Snapshot(1, new byte[] { 1, 2, 3 }, "Campamento", new() { [7] = exit, [8] = checkpoint, [9] = chest, [10] = death, [11] = npc, [12] = portal },
                                               new() { 7 }, new(), frame);
            new AreaMemory(directory, log).Keep("OLD", left);

            var restarted = new AreaMemory(directory, log);
            Equal(true, restarted.Recall("NEW", AreaMemory.Layout(varied, 100, 20000)) == null, "the new instance itself is unknown");
            var inherited = restarted.Inherit(frame, "NEW");
            Equal("OLD", inherited?.Instance, "but the instance left with the same frame is found on disk");
            Equal("El bosque singular|Punto de control", string.Join("|", AreaMemory.Framed(inherited!.Value.Snapshot).Select(entry => entry.Name)),
                  "and only its exits and checkpoints come along: no chest, death place, character or portal");
            Equal(true, restarted.Inherit(AreaMemory.Frame(moved, 100, 20000), "NEW") == null, "a map whose exits moved inherits nothing");
            Equal(true, restarted.Inherit(frame, "OLD") == null, "nor does the instance itself");
            Equal(true, restarted.Inherit(0, "NEW") == null, "nor a frameless map");
            System.IO.Directory.Delete(directory, true);
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
            Equal(null, Step(850, open, 1f), "open again, but the exit heard holds");
            Equal(null, Step(900, open, 1f), "still held");
            Equal(null, Step(950, open, 1f), "still held, and walking on does not release it");
            Equal(null, Step(2250, open, 1f), "the exit has been gone for the hold: open is a candidate");
            Equal(null, Step(2300, open, 1f), "walking on");
            Equal(null, Step(2350, open, 1f), "walking on");
            Equal(RadarReading.Open, Step(2400, open, 1f), "confirmed after three cells, then said");
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

        private static void RadarBossNamesSpeakTheGamesMonsterName()
        {
            var directory = System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests", "bosses-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var landmarks = System.IO.Path.Join(directory, "landmarks.json");
            System.IO.File.WriteAllText(landmarks, "{}");
            System.IO.File.WriteAllText(System.IO.Path.Join(directory, "monster-names.json"),
                "{\"es\":{\"Beira of the Rotten\":\"Beira, de la manada putrefacta\"},\"en\":{\"Beira of the Rotten\":\"Beira of the Rotten Pack\"}}");
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);
            var table = LandmarkTable.Load(log, landmarks);
            Localization.Detect("es");
            Equal("Beira, de la manada putrefacta (10% de resistencia al frío)", table.Localize("Beira of the Rotten (10% Cold Res)"), "a boss's name and note");
            Equal("Beira, de la manada putrefacta", table.Localize("Beira of the Rotten"), "a boss's name alone");
            Equal("The Moving Bramble (gema de habilidad de nivel 2)", table.Localize("The Moving Bramble (Skill Gem Level 2)"), "no monster: the name kept, the note translated");
            Localization.Detect("fr");
            Equal("Beira of the Rotten", table.Localize("Beira of the Rotten"), "a language without the table keeps the label");
            Localization.Detect("en");
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

        private static void LoopReaderKeepsAPitchedLoopClean()
        {
            const int length = 44100;
            const int hertz = 8000;
            static double Tone(double position) => Math.Sin(2 * Math.PI * hertz * position / length);
            var loop = Enumerable.Range(0, length).Select(i => (float)Tone(i)).ToArray();

            Equal(true, MathF.Abs(LoopReader.At(loop, 5) - loop[5]) < 1e-4f, "on a sample it reads that sample");
            Equal(true, MathF.Abs(LoopReader.At(loop, 3) - loop[3]) < 1e-4f, "by the start it reads round the end");
            Equal(true, MathF.Abs(LoopReader.At(loop, length - 2) - loop[length - 2]) < 1e-4f, "by the end it reads round the start");

            foreach (var pitch in new[] { 0.8, 1.26 })
            {
                var error = 0.0;
                var count = 0;
                for (var position = length - 500.0; position < length + 500.0; position += pitch)
                {
                    var wrapped = position >= length ? position - length : position;
                    var difference = LoopReader.At(loop, wrapped) - Tone(position);
                    error += difference * difference;
                    count++;
                }

                Equal(true, Math.Sqrt(error / count) < 0.003, $"over the seam at pitch {pitch} the error stays 50 dB under a bright tone");
            }
        }

        private static void APileOfLoopsLeavesHeadroom()
        {
            const float near = 0.7f;
            const float far = near * 0.3f;
            Equal(1f, ItemSoundSet.PileScale(near * near, near), "one item by the player keeps its level");
            Equal(1f, ItemSoundSet.PileScale((near * near) + (3 * far * far), near), "nor do three far ones lower it");
            Equal(1f, ItemSoundSet.PileScale(0f, 0f), "with the volume off nothing is scaled");
            var each = near * ItemSoundSet.PileScale(4 * near * near, near);
            Equal(true, MathF.Abs(MathF.Sqrt(4 * each * each) - (1.25f * near)) < 1e-4f, "four near items are a little louder than one");
            Equal(true, each > near / 2f, "and each stays heard");
        }

        private static void DiabloIVSetCoversLootByKindAndRarity()
        {
            string? Sample(string kind, string baseId = "", LootTier tier = LootTier.Normal, string? gameDrop = null) =>
                ItemSoundSet.DiabloIVSample(kind, baseId, tier, gameDrop);

            Equal(ItemSoundSet.Game, ItemSoundSet.Sets[0], "the game's own sounds stay the default");
            Equal("gold", Sample("gold"), "gold");
            Equal("quest item", Sample("quest", gameDrop: "Scroll_Drop_01"), "a quest scroll is a quest item");
            Equal("rune", Sample("currency", "Metadata/Items/Currency/CurrencyUpgradeToRare", LootTier.Valuable), "an orb");
            Equal("scroll", Sample("currency", "Metadata/Items/Currency/CurrencyIdentification", LootTier.Valuable, "Scroll_Drop_01"), "a Scroll of Wisdom");
            Equal("potion", Sample("item", "Metadata/Items/Flasks/FlaskLife1"), "a flask");
            Equal("potion", Sample("item", "Metadata/Items/Flasks/FlaskLife1", LootTier.Magic), "a magic flask stays a flask");
            Equal("gem", Sample("item", "Metadata/Items/Gems/SkillGemFireball", LootTier.Valuable), "a gem");
            Equal("normal equipment", Sample("item", "Metadata/Items/Armours/Helmets/FourHelmetStr1"), "normal gear");
            Equal("magic equipment", Sample("item", "Metadata/Items/Rings/FourRing1", LootTier.Magic), "magic gear");
            Equal("rare equipment", Sample("rare", "Metadata/Items/Rings/FourRing1", LootTier.Valuable), "rare gear");
            Equal("legendary equipment", Sample("unique", "Metadata/Items/Rings/FourRing1", LootTier.Valuable), "unique gear");
            Equal("normal equipment", Sample("item", "Metadata/Items/Maps/MapKeyTier1", LootTier.Valuable), "worth unread: the plain sound");
            Equal("chest", Sample("chest"), "a chest");
            Equal("corpse", Sample("corpse"), "a body");
            Equal("shrine", Sample("shrine"), "a shrine");
            Equal(null, Sample("breakable"), "a breakable keeps the game's sound");
            Equal(null, Sample("waypoint"), "places keep the game's sounds");
        }

        private static void SteamsCommandIsFoundInTheLaunchersArguments()
        {
            const string Game = @"D:\games\steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe";
            string Split(params string[] args)
            {
                var (own, game) = Poe2Access.GameInstall.SplitArguments(args);
                return $"{string.Join('|', own)} > {(game is null ? "none" : string.Join('|', game))}";
            }

            Equal($" > {Game}|--nopatch", Split(Game, "--nopatch"), "%command%: the game's path, then its arguments");
            Equal($"--host|C:\\mod\\app > {Game}|--nopatch", Split("--host", @"C:\mod\app", Game, "--nopatch"), "the launcher's own arguments come first");
            Equal($" > {Game}|--nopatch", Split(@"D:\games\steam\steamapps\common\Path", "of", "Exile", @"2\PathOfExileSteam.exe", "--nopatch"), "a path that came unquoted");
            Equal($"restart > {Game}", Split("restart", Game), "a command before it");
            Equal(" > none", Split(), "a double click");
            Equal("restart|--host|C:\\PathOfExile > none", Split("restart", "--host", @"C:\PathOfExile"), "a folder named like the game");
            Equal("C:\\tools\\notes.exe > none", Split(@"C:\tools\notes.exe"), "another program");
            const string Proton = @"Z:\home\deck\.local\share\Steam\steamapps\common\Path of Exile 2\PathOfExileSteam.exe";
            Equal($" > {Proton}|--nologo", Split(Proton, "--nologo"), "under Proton: the game's Linux path on Wine's Z: drive (poe2access-proton.sh)");
        }

        private static void ItemsSoundUnderTheirGroupsVolume()
        {
            Equal(ItemSoundGroup.Flask, ItemSoundGroup.Of("item", "Metadata/Items/Flasks/FlaskLife1"), "a flask");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("item", "Metadata/Items/Armours/Helmets/FourHelmetStr1"), "a helmet");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("rare", "Metadata/Items/Weapons/OneHandWeapons/Wands/FourWand1"), "a rare wand is equipment");
            Equal(ItemSoundGroup.Equipment, ItemSoundGroup.Of("item", "Metadata/Items/Rings/FourRing1"), "a ring");
            Equal(ItemSoundGroup.Gem, ItemSoundGroup.Of("item", "Metadata/Items/Gems/SkillGemFireball"), "a skill gem");
            Equal(ItemSoundGroup.Gem, ItemSoundGroup.Of("item", "Metadata/items/Gem/SupportGemFasterCast"), "the files' other gem folders");
            Equal("currency", ItemSoundGroup.Of("currency", "Metadata/Items/Currency/CurrencyIdentification"), "currency by its kind");
            Equal("gold", ItemSoundGroup.Of("gold", string.Empty), "gold");
            Equal("quest_item", ItemSoundGroup.Of("quest", "Metadata/Items/QuestItems/Book"), "a quest item");
            Equal(ItemSoundGroup.Other, ItemSoundGroup.Of("item", "Metadata/Items/MapFragments/Key"), "anything else");
            Equal(ItemSoundGroup.Other, ItemSoundGroup.Of("item", string.Empty), "an unread base");
        }

        private static void FightsLastWhileAnEngagingEnemyIsNear()
        {
            var fight = new FightState();
            Equal(false, fight.IsOn(0), "no fight before any enemy");
            Equal(false, fight.Update(false, 1000), "no enemy, no fight");
            Equal(true, fight.Update(true, 2000), "an enemy near");
            Equal(true, fight.Update(false, 2000 + FightState.HoldMs - 1), "held a moment after it is gone");
            Equal(false, fight.Update(false, 2000 + FightState.HoldMs), "then over");
            Equal(true, FightState.Modes.Contains(CullThreshold.Never), "the setting can be turned off");
            Equal("True True True True True", string.Join(" ", new[] { (ScanCategory.Items, "rare"), (ScanCategory.Chests, "chest"), (ScanCategory.Chests, "corpse"), (ScanCategory.Chests, "breakable"), (ScanCategory.Encounters, "ritual") }
                .Select(entry => FightState.QuietsInFight(entry.Item1, entry.Item2))), "loot, chests, bodies, breakables and altars keep quiet");
            Equal("False False False", string.Join(" ", new[] { (ScanCategory.Exits, "exit"), (ScanCategory.Chests, "shrine"), (ScanCategory.Waypoints, "checkpoint") }
                .Select(entry => FightState.QuietsInFight(entry.Item1, entry.Item2))), "exits, shrines and checkpoints still guide");
        }

        private static void AWaitingBossMakesNoFight()
        {
            var roster = new FightRoster();
            Equal(false, roster.Counts(706, false, 1000), "waiting in its arena, never in play");
            Equal(false, roster.Counts(706, false, 600000), "however long the player stays around");
            Equal(true, roster.Counts(706, true, 700000), "awake once the player walks in");
            Equal(true, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs - 1), "between phases it still counts");
            Equal(false, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs), "gone for good, it stops counting");
            roster.Prune(700000 + FightRoster.OutOfPlayMs);
            Equal(false, roster.Counts(706, false, 700000 + FightRoster.OutOfPlayMs - 1), "forgotten: the next area uses the same ids");
        }

        private static void AHiddenMonsterIsToldByItsBuff()
        {
            Equal(true, BuffIds.IsHiddenMonster("hidden_monster_79"), "as GameHelper keys it, read on Count Geonor");
            Equal(true, BuffIds.IsHiddenMonster("hidden_monster"), "without the suffix");
            Equal(false, BuffIds.IsHiddenMonster("kitava_heart_damage_taken_79"), "his other buff");
            Equal(false, BuffIds.IsHiddenMonster("frozen"), "a buff a fought enemy has");
        }

        private static void ABossOfSeveralBodiesIsHeardOnce()
        {
            var repeats = new SpeechRepeats();
            var start = new DateTime(2026, 11, 5, 20, 24, 27, DateTimeKind.Utc);
            Equal(false, repeats.IsRepeat("Geonor: Congelado", start), "the first body");
            repeats.Note("Geonor: Congelado", start);
            Equal(true, repeats.IsRepeat("Geonor: Congelado", start.AddMilliseconds(1)), "the next body in the same look");
            Equal(false, repeats.IsRepeat("Geonor 9 por ciento", start.AddMilliseconds(1)), "another line goes through");
            Equal(false, repeats.IsRepeat("Geonor: Congelado", start.AddMilliseconds(SpeechRepeats.WindowMs)), "frozen again later is news");
        }

        private static void OnlyLinesSaidAloudNearbyAreLocalSpeech()
        {
            Equal(true, ChatDiff.IsNotice("Intercambio aceptado."), "the game's own notice");
            Equal(false, ChatDiff.IsNotice("Renly:Esto es lo que tengo."), "an NPC's line is no notice");
            Equal(false, ChatDiff.IsNotice("@De Dahnte:hola"), "a whisper is no notice");
            Equal(true, ChatDiff.IsLocalSpeech("El rey vudú:¡Arrodillaos!"), "a boss's shout");
            Equal(true, ChatDiff.IsLocalSpeech("Renly:Esto es lo que tengo."), "an NPC's line");
            Equal(false, ChatDiff.IsLocalSpeech("@De Dahnte:hola"), "a whisper");
            Equal(false, ChatDiff.IsLocalSpeech("%yarkey:vamos"), "party");
            Equal(false, ChatDiff.IsLocalSpeech("#Someone:wts"), "global");
            Equal(false, ChatDiff.IsLocalSpeech("Has entrado en Sierraclara."), "a system notice has no speaker");
            Equal(false, ChatDiff.IsLocalSpeech(string.Empty), "nothing");
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

        private static void PlayerMoveFollowsAMessage()
        {
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 10, 28, 12, 35, 9);
            floor.Keep("Vendido: Lanza con punta de hierro, raro, por 124 de oro", true, 2500, start, yieldsToMoves: true);
            Equal(false, floor.Admit("Vacío, columna 2, fila 1", true, start.AddMilliseconds(100)), "the game's refresh waits for the sale");
            var interrupt = true;
            Equal(true, floor.Move("Orbe de aumento, columna 3, fila 1", ref interrupt, start.AddMilliseconds(230)), "the player's move goes out now");
            Equal(false, interrupt, "queued behind the sale, not cutting it");
            Equal(0, floor.Release(start.AddMilliseconds(3000)).Count, "the stale refresh is dropped");
            interrupt = true;
            Equal(true, floor.Move("Pergamino de sabiduría, 5, columna 1, fila 1", ref interrupt, start.AddMilliseconds(600)), "the next move");
            Equal(true, interrupt, "cuts in as usual");

            var lesson = start.AddMinutes(1);
            floor.Keep("Navega a tu arbol de pasivas", true, 2000, lesson);
            interrupt = true;
            Equal(false, floor.Move("Vacío, columna 4, fila 2", ref interrupt, lesson.AddMilliseconds(300)), "a lesson still holds the move");
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

        private static void SpiritValuesNameTheirWeaponSet()
        {
            // The spirit panel (bind screen dump 2026-10-29): [0] entries, [1] caption, [2] one box per set, [3] LB, set icons, RB.
            var parents = new List<int> { -1 };
            var indices = new List<int> { 0 };
            int Node(int parent, int index)
            {
                parents.Add(parent);
                indices.Add(index);
                return parents.Count - 1;
            }

            Node(0, 0);
            var caption = Node(0, 1);
            var boxes = Node(0, 2);
            var setOne = Node(Node(boxes, 0), 0);
            var setTwo = Node(Node(boxes, 1), 0);
            var icons = Node(0, 3);
            Node(Node(icons, 0), 0);
            var iconOne = Node(Node(icons, 1), 0);
            var iconTwo = Node(Node(icons, 2), 0);
            var setIcons = new[] { (iconTwo, 2), (iconOne, 1) };
            var sets = WeaponSetRows.Pair(parents, indices, setIcons, new[] { caption, setOne, setTwo });
            Equal(1, sets.GetValueOrDefault(setOne), "first box, set I");
            Equal(2, sets.GetValueOrDefault(setTwo), "second box, set II");
            Equal(false, sets.ContainsKey(caption), "the caption is no set's");
            Equal(2, WeaponSetRows.Pair(parents, indices, setIcons, new[] { setTwo }).GetValueOrDefault(setTwo), "managing set II shows its box alone");
            Equal(0, WeaponSetRows.Pair(parents, indices, new[] { (iconOne, 1), (iconTwo, 1) }, new[] { setOne, setTwo }).Count, "equipment slots repeat their sets");
            indices[icons] = 5;
            Equal(0, WeaponSetRows.Pair(parents, indices, setIcons, new[] { setOne, setTwo }).Count, "a row away from the icons is not theirs");
            Equal(2, WeaponSetRows.FromTextureKey("Art/Textures/Interface/2D/2DArt/UIImages/InGame/InventoryPanel/InventoryPanelWeaponSwap2Active.dds|0,0,2f,28|0"), "inventory art");
            Equal(1, WeaponSetRows.FromTextureKey("Art/Textures/Interface/2D/2DArt/UIImages/InGame/Cosmetics/WeaponSetIcon1.dds|0,0,0,0|0"), "cosmetics numeral");
        }

        private static void CullingThresholdsByRarity()
        {
            Equal(true, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 35, 100), "normal at 35%");
            Equal(false, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 36, 100), "normal above 35%");
            Equal(false, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Magic, 21, 100), "magic above 20%");
            Equal(true, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Rare, 100, 1000), "rare at 10%");
            Equal(true, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Unique, 2500, 50000), "unique at 5%");
            Equal(false, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Unique, 0, 50000), "dead is not cullable");
            Equal(true, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 42, 100, 20), "35% increased by 20%: 42%");
            Equal(false, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 43, 100, 20), "above 42%");
            Equal(true, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Rare, 15, 100, 0, 50), "rare: 10% increased by 50% against rares");
            Equal(false, CullThreshold.Reached(GameReader.RemoteEnums.Rarity.Magic, 25, 100, 0, 50), "the rare-or-unique increase leaves magic alone");
        }

        private static void CullingSkillsComeFromTheCatalog()
        {
            var skills = new CullSkills(new[] { "# GrantedEffects that cull", "KillingPalmPlayer", " ReapPlayer ", string.Empty });
            Equal(2, skills.Count, "comments and blank lines skipped");
            Equal(true, skills.AnyCulls(new[] { "DodgeRollPlayer", "KillingPalmPlayer" }), "Killing Palm culls");
            Equal(true, skills.Culls("ReapPlayer"), "trimmed");
            Equal(false, skills.AnyCulls(new[] { "DodgeRollPlayer", "ParryPlayer" }), "no culling skill");
            Equal(0, CullSkills.Load(System.IO.Path.Join(System.IO.Path.GetTempPath(), "no-such-cull-skills.txt")).Count, "a missing catalog culls nothing");
            Equal(true, CullThreshold.Modes[0] == CullThreshold.WithSkill, "only with a culling skill by default");
            var gong = CueSynth.Cull(44100);
            Equal(true, gong.Length == (int)(0.75f * 44100) && MathF.Abs(gong.Max(MathF.Abs) - 0.94f) < 0.001f, "the gong: 0.75 s at its level, 7 dB over the sample");
            Equal(true, MathF.Abs(gong[^1]) < 0.01f, "it fades out without a click");
            Equal(true, gong.Length > CueSynth.StateCue(EnemyState.HeavyStunned, 44100).Length, "longer than the heavy-stun gong");
        }

        private static void CrossingAnAreaLeadsToTheWayOn()
        {
            var back = new ScanEntry(1, ScanCategory.Exits, "El bosque singular", 0, 0, 0, QuestMatcher.ExitKind);
            var on = new ScanEntry(2, ScanCategory.Exits, "Cementerio de los eternos", 0, 0, 0, QuestMatcher.ExitKind);
            var waypoint = new ScanEntry(3, ScanCategory.Exits, "Punto de paso", 0, 0, 0, "waypoint");
            var frontier = new ScanEntry(4, ScanCategory.Unexplored, "Sin explorar", 0, 0, 0);
            string Names(IReadOnlyList<ScanEntry> entries, params string[] texts) =>
                string.Join(" | ", QuestMatcher.Targets(entries, texts, "El enredo sombrío", "El bosque singular").Select(entry => entry.Name));
            var crossing = new[] { "La sombra misteriosa", "Sigue a través del Enredo sombrío" };
            Equal("Cementerio de los eternos", Names(new[] { back, on, waypoint, frontier }, crossing), "the way on, not back, not the waypoint");
            Equal("Sin explorar", Names(new[] { back, waypoint, frontier }, crossing), "no way on known yet: the unexplored ground");
            Equal(string.Empty, Names(new[] { back, on, frontier }, "Busca la semilla de la corrupción"), "an objective not about this area");
            Equal(string.Empty, Names(new[] { back, on, frontier }, "Registra el Enredo sombrío para encontrar al Conde"), "a name to find: not the way on");
        }

        private static void ExploredGroundIsKeptAsBytes()
        {
            var map = new ExplorationMap();
            map.Visit(new Vector2(100, 100), 800, 600);
            var copy = new ExplorationMap();
            Equal(true, copy.Load(map.Save()), "a saved map loads");
            Equal(true, copy.IsSeen(new Vector2(100, 100)) && !copy.IsSeen(new Vector2(700, 500)), "the same ground seen");
            Equal(false, copy.Load(new byte[] { 1, 0, 0, 0 }), "too short");
            Equal(false, copy.Load(BitConverter.GetBytes(100000).Concat(BitConverter.GetBytes(100000)).ToArray()), "claims more blocks than any area");
            Equal(true, copy.IsSeen(new Vector2(100, 100)), "a bad load changes nothing");
            Equal("ice_crystal", DeployedObjects.SoundKey("Metadata/MiscellaneousObjects/FrostWall/OrbOfIce"), "the crystal's own sound");
            Equal("tempest_bell", DeployedObjects.SoundKey("Metadata/Effects/Spells/monk_bell/mtx_BellAurora"), "a bell skin tolls the bell");
        }

        private static void PlacedObjectsSoundByDistance()
        {
            Equal(false, DeployedObjects.Moved(new Vector2(100, 100), new Vector2(120, 110)), "a bell standing still");
            Equal(true, DeployedObjects.Moved(new Vector2(100, 100), new Vector2(200, 100)), "a minion walking off");
            Equal(true, DeployedObjects.IntervalMs(100) < DeployedObjects.IntervalMs(1200), "more often near");
            Equal(true, DeployedObjects.IsPlaced("Metadata/MiscellaneousObjects/FrostWall/OrbOfIce"), "Frozen Locus's crystal (seen live)");
            Equal(true, DeployedObjects.IsPlaced("Metadata/Effects/Spells/monk_bell/tempest_bell"), "Tempest Bell");
            Equal(true, DeployedObjects.IsPlaced("Metadata/Effects/Spells/monk_bell/mtx_BellAurora"), "a bell skin");
            Equal(false, DeployedObjects.IsPlaced("Metadata/MiscellaneousObjects/FrostWall/FrostWall"), "a wall segment is not struck");
            Equal(false, DeployedObjects.IsPlaced("Metadata/Monsters/Wolves/FungalWolf1_@6"), "a monster");
            Equal(true, DeployedObjects.GainAt(100) > DeployedObjects.GainAt(1200) && DeployedObjects.GainAt(DeployedObjects.RangeWorld) > 0, "louder near, never silent in range");
            Equal(0f, DeployedObjects.GainAt(DeployedObjects.ReachWorld), "and dying away to nothing past it");
            var bell = CueSynth.Deployed(44100);
            Equal(true, bell.Length < CueSynth.Cull(44100).Length / 2 && bell.Max(MathF.Abs) < 0.3f, "short and soft beside the gong");
        }

        private static void SkillsAreReadyOnceTheirCooldownEnds()
        {
            const uint grenade = 0x80401040, bell = 0x80401050;
            var cooldowns = new SkillCooldowns();
            Dictionary<uint, bool> Usable(bool grenadeUsable, bool bellUsable = true) => new() { [grenade] = grenadeUsable, [bell] = bellUsable };

            Equal(0, cooldowns.Update(Usable(true), 0).Count, "usable from the start is no news");
            Equal(0, cooldowns.Update(Usable(false), 100).Count, "spent");
            Equal(0, cooldowns.Update(Usable(false), 2000).Count, "still recharging");
            Equal(grenade, cooldowns.Update(Usable(true), 3100).Single(), "ready again after its cooldown");
            Equal(0, cooldowns.Update(Usable(true), 3200).Count, "said once");

            cooldowns.Update(Usable(false), 4000);
            Equal(0, cooldowns.Update(Usable(true), 4000 + SkillCooldowns.MinSpentMs - 100).Count, "a short cooldown is not news");

            cooldowns.Update(Usable(false, false), 5000);
            Equal("80401040,80401050", string.Join(",", cooldowns.Update(Usable(true), 9000).Select(id => id.ToString("X8"))), "two ready at once, in order");

            cooldowns.Update(Usable(false), 10000);
            Equal(0, cooldowns.Update(new Dictionary<uint, bool> { [bell] = true }, 11000).Count, "a skill taken off is forgotten");
            Equal(0, cooldowns.Update(Usable(true), 13000).Count, "and not news when it comes back usable");

            cooldowns.Reset();
            Equal(0, cooldowns.Update(Usable(false), 20000).Count, "a new area only remembers what is spent");
            Equal(grenade, cooldowns.Update(Usable(true), 20100).Single(), "spent since before the area: its end is news at once");
        }

        private static void ChargesAreKnownByTheirBuff()
        {
            Equal(ChargeKind.Power, PlayerCharges.FromBuffId("power_charge_79"), "suffix dropped");
            Equal(ChargeKind.Frenzy, PlayerCharges.FromBuffId("frenzy_charge"), "bare id");
            Equal(ChargeKind.Endurance, PlayerCharges.FromBuffId("endurance_charge_79"), "endurance");
            Equal(null, PlayerCharges.FromBuffId("power_charge_on_crit"), "another buff");
            Equal(ChargeKind.ColdInfusion, PlayerCharges.FromBuffId("cold_infusion_79"), "the Sorceress's infusions");
            Equal(SpeechKind.Combo, PlayerCharges.SpeechKindOf(ChargeKind.Combo), "the Combo has its own speech level");
            Equal(SpeechKind.Charges, PlayerCharges.SpeechKindOf(ChargeKind.Rage), "Rage follows the charges");
            Equal("combo", PlayerCharges.RecordedGain(ChargeKind.Combo), "the Combo plays the mod's recorded sound");
            Equal(null, PlayerCharges.RecordedGain(ChargeKind.Frenzy), "frenzy charges keep their ticks");
            Equal(0, PlayerCharges.GainSemitones(ChargeKind.Combo, 1), "the first combo point on the sound's own note");
            Equal(3, PlayerCharges.GainSemitones(ChargeKind.Combo, 4), "a semitone higher per point");
            Equal(PlayerCharges.MaxComboSemitones, PlayerCharges.GainSemitones(ChargeKind.Combo, 40), "at most an octave up");
            Equal(0, PlayerCharges.GainSemitones(ChargeKind.Power, 3), "power charges keep their note");
            Equal(2f, PlayerCharges.GainPitch(ChargeKind.Combo, 13), "twelve semitones play an octave up");
            Equal(1f, PlayerCharges.GainPitch(ChargeKind.Combo, 1), "the first point at the recorded pitch");
            Equal(ChargeKind.ChaosSurge, PlayerCharges.FromBuffId("instilling_chaos"), "a surge");
            Equal(ChargeKind.ArcaneSurge, PlayerCharges.FromBuffId("arcane_surge_79"), "Arcane Surge");
            Equal(ChargeKind.Combo, PlayerCharges.FromBuffId("tempest_bell_combo_tracker_79"), "Tempest Bell's combo (logged live)");
            Equal(null, PlayerCharges.FromBuffId("tempest_bell_active"), "the bell itself is not combo");
            Equal(true, PlayerCharges.RageEdge(29, 30, 30), "Rage fills");
            Equal(null, PlayerCharges.RageEdge(30, 30, 30), "full and still full is no news");
            Equal(null, PlayerCharges.RageEdge(30, 25, 30), "draining is no news");
            Equal(false, PlayerCharges.RageEdge(3, 0, 30), "Rage runs out");
            Equal(null, PlayerCharges.RageEdge(0, 0, 30), "a character without Rage");
            Equal(1, PlayerCharges.Held(0), "a charge buff with no stack count holds one");
            Equal(3, PlayerCharges.Held(3), "the stack count");
            var three = CueSynth.ChargeGained(ChargeKind.Power, 3, 44100);
            var one = CueSynth.ChargeGained(ChargeKind.Power, 1, 44100);
            Equal(true, three.Length > one.Length * 2, "one tick per charge held");
            Equal(true, CueSynth.ChargeGained(ChargeKind.Power, 50, 44100).Length < 44100, "capped under a second");
            Equal(true, CueSynth.ChargeLost(ChargeKind.Endurance, 44100).All(sample => MathF.Abs(sample) <= 0.2f), "the loss is softer");
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
            Equal(false, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 18, 60), "hag at 30%: not yet");
            Equal(true, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 24, 60), "normal at 40%");
            Equal(true, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 54, 60), "hag at 54 of 60 (stunned by the next hit)");
            Equal(false, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Magic, 19, 40), "magic below 50%");
            Equal(true, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Rare, 600, 1000), "rare at 60%");
            Equal(false, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Unique, 690, 1000), "unique below 70%");
            Equal(false, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 10, 0), "no threshold read");
            Equal(false, PrimedThreshold.Reached(GameReader.RemoteEnums.Rarity.Normal, 1_000_000, 60), "not a buildup");
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

        private static void StunBarSpeaksEachStepOnce()
        {
            Equal(50, StunMeter.Percent(30, 60), "buildup over threshold");
            Equal(100, StunMeter.Percent(90, 60), "a bar past full reads full");
            Equal(null, StunMeter.Percent(5, 0), "no threshold: not a reading");
            Equal(null, StunMeter.Percent(-1, 60), "negative: not a reading");
            Equal(null, StunMeter.Percent(500, 60), "far past it: a misread");

            var bar = new StunMeter(10, 50, 75, 90);
            string Feed(params int[] values) => string.Join(" ", values.Select(value => bar.Update(value)?.ToString() ?? "-"));
            Equal("- - 50 - 75 90 -", Feed(0, 30, 55, 60, 80, 95, 100), "each step once on the way up");
            Equal("- -", Feed(70, 82), "not re-armed 5 points below");
            Equal("- 90", Feed(60, 92), "re-armed 10 below; a big blow says only the highest");
            bar.Reset();
            Equal("75", Feed(80), "armed again after a reset");
        }

        private static void ParryCueOnlyForBlowsToParry()
        {
            Equal(true, ParryTiming.Worth(DodgeThreat.Melee, false, false, 100f, 150f), "a normal monster's strike within reach");
            Equal(false, ParryTiming.Worth(DodgeThreat.Melee, false, true, 200f, 150f), "a strike out of reach, even a rare's");
            Equal(true, ParryTiming.Worth(DodgeThreat.Ranged, true, true, 900f, 150f), "a rare's projectile");
            Equal(false, ParryTiming.Worth(DodgeThreat.Ranged, true, false, 900f, 150f), "a normal archer's arrow: no cue");
            Equal(false, ParryTiming.Worth(DodgeThreat.Ranged, false, true, 900f, 150f), "a spell is no projectile");
            Equal(false, ParryTiming.Worth(DodgeThreat.None, false, true, 50f, 150f), "no attack");
            Equal(1000L - 150 - 320, ParryTiming.StartAt(1000, 150, 320), "the swell stops 150 ms before the blow");
            Equal(true, ParryTiming.InTime(1000, 320, 600), "a little late, the stop still comes before the blow");
            Equal(true, ParryTiming.InTime(1000, 320, 680), "stopping on the blow itself");
            Equal(false, ParryTiming.InTime(1000, 320, 681), "a blow too quick for the swell: no cue");
        }

        private static void UnblockableAttacksAreNeverToParry()
        {
            var timings = new DodgeTimings();
            timings.AddHitTimes(new[]
            {
                "Metadata/Monsters/Executioner|71=1200/2500;noblock",
                "Metadata/Monsters/Chimera|40=900/1800/700;noparry",
                "Metadata/Monsters/Miller|10=-;noblock,noparry",
                "Metadata/Monsters/Miller|13=1803/3000",
            });
            Equal(false, timings.CanParry("Metadata/Monsters/Executioner|71"), "cannot be blocked: no parry either");
            Equal(false, timings.CanBlock("Metadata/Monsters/Executioner|71"), "and no block: the extra dodge cue");
            Equal(true, timings.CanBlock("Metadata/Monsters/Chimera|40"), "a shield still blocks what only refuses the parry");
            Equal(1200, timings.Delay("Metadata/Monsters/Executioner|71") ?? 0, "and its time still warns to dodge");
            Equal(false, timings.CanParry("Metadata/Monsters/Chimera|40"), "cannot be parried");
            Equal(700, timings.ProjectileSpeed("Metadata/Monsters/Chimera|40") ?? 0, "flags after a speed");
            Equal(false, timings.CanParry("Metadata/Monsters/Miller|10"), "an untimed charge keeps its flags");
            Equal(true, timings.Delay("Metadata/Monsters/Miller|10") == null, "and stays untimed");
            Equal(true, timings.CanParry("Metadata/Monsters/Miller|13"), "the Miller's slam can be parried");
        }

        private static void HarmfulGroundIsKnownByItsBuff()
        {
            var ground = new HazardGround(new[] { "# comment", "caustic_cloud", "ground_living_blood", "", "vortex" });
            Equal(3, ground.Count, "ids, not comments or blank lines");
            Equal(true, ground.IsHazard("caustic_cloud"), "standing in caustic ground");
            Equal(true, ground.IsHazard("caustic_cloud_79"), "with GameHelper's instance suffix");
            Equal(true, ground.IsHazard("ground_living_blood_damage_79"), "a buff named after the ground");
            Equal(false, ground.IsHazard("ground"), "not a word of one");
            Equal(false, ground.IsHazard("crossbow_should_aim_at_ground_79"), "a buff only mentioning ground");
            Equal(false, ground.IsHazard("ignited_79"), "an ailment is the status warnings' business");
            Equal("chilled", BuffIds.Base("chilled_1A"), "hex instance suffix dropped");
            Equal("mud_slow", BuffIds.Base("mud_slow"), "a word is not a suffix");
        }

        private static void HazardAlarmLoopsWithoutASeam()
        {
            var loop = CueSynth.HazardAlarm(44100);
            Equal(true, loop.Max(MathF.Abs) <= 0.5f && loop.Max(MathF.Abs) > 0.3f, "clear, under the combat warnings");
            Equal(true, MathF.Abs(loop[0]) < 0.01f && MathF.Abs(loop[^1]) < 0.01f, "silent at the seam");
            Equal(true, loop.Skip((int)(0.33f * 44100)).All(sample => sample == 0f), "a rest after the stabs, so it pulses");
            var stab = CueSynth.Unblockable(44100);
            Equal(true, stab.All(sample => MathF.Abs(sample) <= 1f) && stab.Length < CueSynth.Dodge(44100).Length, "the unblockable cue is short and in range");
        }

        private static void ParryCueIsItsOwnSound()
        {
            var parry = CueSynth.Parry(44100);
            var parried = CueSynth.StateCue(EnemyState.Parried, 44100);
            var shield = CueSynth.ShieldUp(44100);
            Equal(true, parry.All(sample => MathF.Abs(sample) <= 1f) && parry.Max(MathF.Abs) > 0.5f, "in range and clear");
            Equal(true, parry.Length > parried.Length && parry.Length > shield.Length, "longer than the parried ting and the shield clank");
            var edge = 441;
            Equal(true, parry.Take(edge).Max(MathF.Abs) < parry.Skip(parry.Length - edge).Max(MathF.Abs) / 4f, "a reverse hit: it swells in");
            Equal(true, parry.Skip(parry.Length - edge).Max(MathF.Abs) > 0.3f, "and stops dead, loud to its last 10 ms");
            Equal(true, PeakLimiter.Drive(parry, parry, 3f, 44100).Left.Max(MathF.Abs) <= 1f, "driven to 300% it stays under full scale");
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
            var targets = map.Frontier(grid, row, new System.Numerics.Vector2(40, 100), 8).Select(place => place.Cell).ToList();
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

        /// <summary>A walled grid with rectangles carved open (x even: two cells per byte).</summary>
        private static byte[] Carved(int width, int height, params (int Left, int Top, int Right, int Bottom)[] rooms)
        {
            var grid = new byte[width / 2 * height];
            foreach (var (left, top, right, bottom) in rooms)
            {
                for (var y = top; y < bottom; y++)
                {
                    Array.Fill(grid, (byte)0x11, (y * width / 2) + (left / 2), (right - left) / 2);
                }
            }

            return grid;
        }

        private static void RadarFindsTheDoorwayToAnotherRoom()
        {
            var limits = new PassageScan.DoorwayLimits(23, 11, 4, 23);
            var east = new System.Numerics.Vector2(1, 0);
            var start = new System.Numerics.Vector2(40, 100);
            System.Numerics.Vector2? Door(byte[] grid, System.Numerics.Vector2 from) => PassageScan.Doorway(grid, 200, from, east, 80, limits);
            // Two halls joined by a gap 12 cells wide through a wall.
            var halls = Carved(400, 200, (10, 40, 80, 160), (80, 94, 92, 106), (92, 40, 200, 160));
            Equal(true, Door(halls, start) is { X: > 70 and < 100, Y: > 94 and < 106 }, "a doorway ahead, at its middle");
            Equal(null, PassageScan.Doorway(halls, 200, start, new System.Numerics.Vector2(0, 1), 80, limits), "toward the plain wall of the hall");
            Equal(null, Door(Carved(400, 200, (10, 40, 300, 160)), start), "one great hall: only floor");
            var corridor = Carved(400, 200, (10, 94, 390, 106));
            Equal(null, Door(corridor, new System.Numerics.Vector2(150, 100)), "along a corridor, nothing but corridor");
            var mouth = Carved(400, 200, (10, 40, 80, 160), (80, 94, 390, 106));
            Equal(true, Door(mouth, start) is { X: > 70 }, "out of a hall into a corridor");
            var opening = Carved(400, 200, (10, 94, 200, 106), (200, 40, 390, 160));
            Equal(true, Door(opening, new System.Numerics.Vector2(160, 100)) is { X: > 170 }, "from a corridor into a hall");
            var pillars = Carved(400, 200, (10, 40, 300, 160));
            for (var y = 60; y < 140; y += 20)
            {
                if (y != 100)
                {
                    pillars = Blocked(pillars, 200, 90, y, 94, y + 6);
                }
            }

            Equal(null, Door(pillars, start), "between pillars the ground runs on either side");
            var shallow = Carved(400, 200, (10, 40, 80, 160), (80, 94, 86, 106));
            var depth = PassageScan.Scan(shallow, 200, start, east, 2, 80, 23).Depth;
            Equal(null, PassageScan.Doorway(shallow, 200, start, east, depth, limits), "a nook no deeper than its gap: the way must go on past it");
        }

        private static byte[] Blocked(byte[] grid, int bytesPerRow, int left, int top, int right, int bottom)
        {
            for (var y = top; y < bottom; y++)
            {
                Array.Fill(grid, (byte)0, (y * bytesPerRow) + (left / 2), (right - left) / 2);
            }

            return grid;
        }

        private static void MapCornerFollowsTheScreenNotTheGrid()
        {
            const int width = 400, height = 400;
            var grid = Carved(width, height, (0, 0, width, height));
            var flatX = new System.Numerics.Vector2(1, 0);
            var flatY = new System.Numerics.Vector2(0, 1);
            (int, int)? Flat(float x, float y) => MapCorner.Of(grid, width / 2, new System.Numerics.Vector2(x, y), flatX, flatY);
            Equal(((int, int)?)(-1, 1), Flat(20, 380), "bottom left, a flat camera");
            Equal(((int, int)?)(1, -1), Flat(380, 20), "top right");
            Equal(((int, int)?)(0, 0), Flat(200, 200), "the middle");
            Equal(((int, int)?)(-1, 0), Flat(20, 200), "the left edge");
            // The game's camera: grid +X draws up and right, +Y down and right, so the grid's corners are the screen's edges.
            var isoX = new System.Numerics.Vector2(1, -0.5f);
            var isoY = new System.Numerics.Vector2(1, 0.5f);
            (int, int)? Iso(float x, float y) => MapCorner.Of(grid, width / 2, new System.Numerics.Vector2(x, y), isoX, isoY);
            Equal(((int, int)?)(-1, 0), Iso(10, 10), "the grid's first corner is the screen's left");
            Equal(((int, int)?)(1, 0), Iso(390, 390), "its opposite, the right");
            Equal(((int, int)?)(0, -1), Iso(390, 10), "up");
            Equal(((int, int)?)(0, 1), Iso(10, 390), "down");
            Equal(((int, int)?)(-1, 1), Iso(10, 200), "between left and down, a corner of the screen");
            var stray = Carved(1000, 400, (0, 0, 400, 400), (990, 0, 1000, 8));
            Equal(((int, int)?)(1, 0), MapCorner.Of(stray, 500, new System.Numerics.Vector2(380, 200), flatX, flatY), "a stray patch far out is no edge");
            Equal(null, MapCorner.Of(new byte[200 * 400], 200, new System.Numerics.Vector2(10, 10), flatX, flatY), "no ground");
        }

        private static void RadarHearsAnExitInItsWallAndOffItsLane()
        {
            var sensor = new RadarSensor(new RadarThresholds(20, 15, 3, 200, 100, 500));
            var east = new System.Numerics.Vector2(1, 0);
            var wall = new PassageScan.Result(12, false);
            sensor.Update(0, System.Numerics.Vector2.Zero, east, wall, false);
            RadarReading? heard = null;
            for (var step = 1; step <= 6 && heard == null; step++)
            {
                heard = sensor.Update(step * 100, new System.Numerics.Vector2(step, 0), east, wall, true);
            }

            Equal(RadarReading.Exit, heard, "a door in the wall ahead is the exit, not the wall");
            var south = new System.Numerics.Vector2(0, 1);
            var start = new System.Numerics.Vector2(30, 50);
            var door = new System.Numerics.Vector2(62, 40);
            var field = Carved(100, 100, (0, 0, 60, 100));
            Equal(true, PassageScan.ExitToward(field, 50, start, east, door, 69, 23), "off the lane, in the wall, inside the ray's quarter");
            Equal(false, PassageScan.ExitToward(field, 50, start, south, door, 69, 23), "not on another ray");
            Equal(false, PassageScan.ExitToward(field, 50, start, east, door, 20, 23), "out of reach");
            var walled = Carved(100, 100, (0, 0, 40, 100), (70, 0, 100, 100));
            Equal(false, PassageScan.ExitToward(walled, 50, start, east, new System.Numerics.Vector2(75, 50), 69, 23), "behind a wall of its own");
        }

        private static void FrontierTellsNooksFromGroundThatGoesOn()
        {
            const int width = 400, height = 200;
            var player = new System.Numerics.Vector2(60, 100);
            // A hall; a corridor east to a great room; a short dead end south.
            var grid = Carved(width, height, (20, 80, 100, 120), (100, 96, 300, 104), (300, 10, 390, 190), (60, 120, 68, 190));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var places = map.Frontier(grid, width / 2, player, 8);
            Equal(2, places.Count, "the corridor and the dead end");
            Equal(false, places[0].Pocket, "ground that goes on comes first");
            Equal(true, places[0].Cell.X > 100, "it is the corridor");
            Equal(true, places[1].Pocket, "the dead end is a nook");
            Equal(true, places[1].Cell.Y > 120, "south of the hall");
        }

        private static void QuestWayLeavesTheSeenGroundTowardTheGoal()
        {
            const int width = 800, height = 200;
            var player = new System.Numerics.Vector2(60, 100);
            var goal = new System.Numerics.Vector2(750, 100);
            // A hall with two ways on: north to one room, east to the goal's; a walled-off room apart.
            var grid = Carved(width, height, (20, 80, 100, 120), (40, 30, 48, 80), (40, 2, 390, 30), (100, 96, 700, 104), (700, 40, 790, 160), (200, 170, 240, 195));
            var map = new ExplorationMap();
            map.Visit(player, width, height);
            var way = map.WayOn(grid, width / 2, player, goal);
            Equal(true, way is { X: > 100 and < 140, Y: > 94 and < 106 }, "the east corridor, at the edge of the seen ground, not the north way nor the goal");
            Equal(null, map.WayOn(grid, width / 2, player, new System.Numerics.Vector2(220, 180)), "no ground leads to a walled-off room");
            var further = new System.Numerics.Vector2(200, 100);
            map.Visit(further, width, height);
            Equal(true, map.WayOn(grid, width / 2, further, goal) is { X: > 240 and < 280 }, "the way moves on with the player");
            var seen = map.SeenBlocks;
            map.Visit(goal, width, height);
            Equal(true, map.SeenBlocks > seen, "new ground counts");
            seen = map.SeenBlocks;
            map.Visit(goal, width, height);
            Equal(seen, map.SeenBlocks, "ground seen twice does not");
            Equal(null, map.WayOn(grid, width / 2, further, goal), "ground seen: the place itself is known");
        }

        private static void MinimapMarksNameBossRoomsAndLandmarks()
        {
            Equal("ritual", MinimapMarks.DeviceKind(MinimapMarks.RitualIcon), "an altar still to do sounds");
            Equal(MinimapMarks.RitualDoneKind, MinimapMarks.DeviceKind(MinimapMarks.RitualDoneIcon), "a finished one has its own kind, out of the sounds");
            Equal("encounter", MinimapMarks.DeviceKind("ExpeditionDevice"), "another league's device stays an encounter");

            const string crowbellRoom = "Metadata/Monsters/Crowbell/Objects/BossRoomMinimapIcon";
            Equal((MinimapMarks.BossRoomKind, "El pilar de caza", false),
                  MinimapMarks.PointOfInterest(crowbellRoom, "CrowbellActive", "El pilar de caza"), "a campaign boss's arena by its label");
            Equal((MinimapMarks.BossRoomKind, "El pilar de caza", true),
                  MinimapMarks.PointOfInterest(crowbellRoom, "CrowbellInactive", "El pilar de caza"), "slain: its grey icon, the active row's label");
            Equal((MinimapMarks.BossRoomKind, string.Empty, false),
                  MinimapMarks.PointOfInterest("Metadata/MiscellaneousObjects/BossRoomMinimapIcon", "LootFilterLargeRedUpsideDownHouse", string.Empty), "a map boss's red house has no label");
            Equal((MinimapMarks.BossRoomKind, string.Empty, true),
                  MinimapMarks.PointOfInterest("Metadata/MiscellaneousObjects/BossRoomMinimapIcon", "LootFilterLargeGreyUpsideDownHouse", string.Empty), "and turns grey");
            Equal((MinimapMarks.LandmarkKind, "Ritual driádico", false),
                  MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act1/1_11/Objects/SecretRoomMinimapIcon", "HuntingGroundsSecretActive", "Ritual driádico"), "a secret by its label");
            Equal(null, MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act1/1_11/Objects/SecretRoomMinimapIcon", "HuntingGroundsSecretInactive", string.Empty), "a secret found is gone");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.PointOfInterest("Metadata/Terrain/Gallows/Act2/2_10_2/Objects/RisuCage", "QuestObject", "Jaula de Risu")?.Kind,
                  "what the quest's icon marks sounds as a quest object");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.KindUnder("QuestObject", string.Empty), "a named object under the quest's icon sounds as a quest object");
            Equal("chest", MinimapMarks.KindUnder("QuestObject", "chest"), "a chest there keeps its kind, the quest sound goes over its own");
            Equal(string.Empty, MinimapMarks.KindUnder("Stash", string.Empty), "and another icon gives no kind");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.DeviceKind("QuestObject"), "a league's summon under the quest's icon too");
            Equal("encounter", MinimapMarks.DeviceKind("Strongbox"), "while another device stays an encounter");
            Equal(true, MinimapMarks.IsStoryBarrier("Metadata/Terrain/Gallows/Act2/2_2/Objects/BossDoor"), "the Ancient Seal is a campaign area's own barrier");
            Equal(false, MinimapMarks.IsStoryBarrier("Metadata/MiscellaneousObjects/Door"), "a dungeon's plain door is not");
            Equal(false, MinimapMarks.IsStoryBarrier("Metadata/Terrain/Gallows/Leagues/Incursion/Objects/RoomDoor"), "nor a league's");
            Equal(true, MinimapMarks.IsCage("Metadata/Terrain/Gallows/Act4/4_7/Objects/KaruiElderCage"), "a cage by its file");
            Equal(false, MinimapMarks.IsCage("Metadata/Terrain/Doodads/Desert/DreadnoughtVanguard/BossRoomCageDoor"), "and only by its end");
            Equal("Risu", MinimapMarks.BareName("Risu, Desertora faridun"), "a cage says who is inside without the title");
            Equal("Una", MinimapMarks.BareName("Una"), "a name with no title stays");
            Equal(null, MinimapMarks.PointOfInterest("Metadata/NPC/Four_Act1/HuntingGroundsSignpostGlyph", MinimapMarks.StoryGlyph, string.Empty), "a sign is no point of interest");
            Equal(true, MinimapMarks.IsQuestIcon("QuestObject") && MinimapMarks.IsQuestIcon("QuestItem") && !MinimapMarks.IsQuestIcon("NPC"), "the quest's own icons");
            Equal(true, MinimapMarks.IsSpent("TitanGrottoEntranceInActive"), "the table's other spelling of inactive");
            Equal("Legends Of Wraeclast", CardText.ShopImageTitle("C:/Users/x/AppData/Local/Path of Exile 2/ShopImages/public_news_2026-10-01_LegendsOfWraeclast_small.png|0,0,0,0|1"), "a featured banner by its image file");
            Equal("Cot A Box", CardText.ShopImageTitle("C:/Users/x/AppData/Local/Path of Exile 2/ShopImages/public_news_2026-09-21_CotABox_small.png|0,0,0,0|1"), "capitals in a run split before the small letters");
            Equal(string.Empty, CardText.ShopImageTitle("Art/2DArt/UIImages/InGame/Cross.dds|0"), "no shop image, no title");
            Equal("Early Access Supporter Packs", CardText.ShopImageTitle("C:/x/ShopImages/client_shop_assets_PoE2EarlyAccessSupporterPacks_Thumb.png|0"), "the file's own words dropped");
            Equal("On Sale", CardText.ShopImageTitle("C:/x/ShopImages/public_client_shop_OnSale_Thumb.png|0"), "and the folder-like prefix");

            const string tomb = "Metadata/Terrain/Woods/GraveyardDungeons/Feature/TombBoss_ArenaFloor.tdtx:2-y:2";
            Equal("TombBoss_ArenaFloor", MinimapMarks.TileFile(tomb), "a tile key's file");
            Equal("Asinia", MinimapMarks.ArenaBoss(tomb, "Asinia"), "the Tomb's arena names its boss");
            Equal(string.Empty, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/GraveyardDungeons/Feature/MausoleumBoss_ArenaFloor.tdtx:0-y:0", "MausoleumBoss_ArenaFloor"),
                  "the Mausoleum's arena, labelled with its own file name, names no one");
            Equal("Crowbell Arena (+2 Passive)", MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/Features_Cairns/DolmenDeAzutan_03.tdtx:0-y:9", "Crowbell Arena (+2 Passive)"),
                  "an arena known by its label");
            Equal("Captain Hartlin", MinimapMarks.ArenaBoss("Metadata/Terrain/Islands/Tiles/Island/JourneysEnd/BossArena_02.tdtx:0-y:0", "Boss - Captain Hartlin"), "the label's boss after its prefix");
            Equal(string.Empty, MinimapMarks.ArenaBoss("Metadata/Terrain/Interlude/Part2/P2_1/Salt_Waste_Bossroom.tdtx:14-y:11", "Bosses"), "a bare label");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/Village/Huts/VillageArena_02_courtyard_side.tdtx:0-y:0", "Entrance"), "the way into an arena");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Maps/Derelict/Tiles/HoltenBossStairsDown_01.tdtx:0-y:0", "Boss stairs"), "stairs down to a boss");
            Equal(null, MinimapMarks.ArenaBoss("Metadata/Terrain/Woods/HuntingGrounds/RitualClearing01.tdtx:0-y:4", "Ritual Site (Level 4 Skill Gem)"), "no arena");
            Equal(true, MinimapMarks.IsArenaTile("Metadata/Terrain/Interlude/Part2/P2_7/Qimah_bossroom.tdtx:3-y:1"), "an arena tile without a label");
            Equal(false, MinimapMarks.IsArenaTile("Metadata/Terrain/Woods/Mudburrow/Path/path_St_Spawner_v02_01.tdtx:1-y:0"), "a burrow path");
            var arenas = MinimapMarks.Groups(new[] { new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(400, 400), new System.Numerics.Vector2(46, 0), new System.Numerics.Vector2(23, 0) }, 46);
            Equal("0 2 3 | 1", string.Join(" | ", arenas.Select(group => string.Join(' ', group.OrderBy(member => member)))), "one arena's pieces side by side, another far off");
            Equal("Beira of the Rotten", MinimapMarks.WithoutNote("Beira of the Rotten (10% Cold Res)"), "the reward note left out");
            var notes = QuestMatcher.Targets(new[]
                {
                    new ScanEntry(5, ScanCategory.PointsOfInterest, "Sala del jefe: Arena o coto de caza", 0, 0, 0, MinimapMarks.BossRoomKind, Subject: "Arena o coto de caza"),
                    new ScanEntry(6, ScanCategory.PointsOfInterest, "Sala del jefe", 0, 0, 0, MinimapMarks.BossRoomKind),
                },
                new[] { "La caza", "Ve al Coto de caza y encuentra la Arena" });
            Equal(0, notes.Count, "a boss room's note sharing words with the objective is no target, nor an unnamed room");
            var bossTargets = QuestMatcher.Targets(new[] { new ScanEntry(4, ScanCategory.PointsOfInterest, "Sala del jefe: Asinia", 0, 0, 0, MinimapMarks.BossRoomKind, Subject: "Asinia") },
                                                   new[] { "La consorte", "Asinia te espera en la Tumba de la Consorte" });
            Equal(1, bossTargets.Count, "the quest naming the boss leads to the boss room");
        }

        private static void SignsAreNeverQuestTargetsAndQuestIconsAlways()
        {
            var entries = new[]
            {
                new ScanEntry(1, ScanCategory.Objects, "Letrero: Pueblo de Ogham", 0, 0, 0, MinimapMarks.SignKind),
                new ScanEntry(2, ScanCategory.Exits, "Tierras de cultivo de Ogham", 0, 0, 0),
                new ScanEntry(3, ScanCategory.Objects, "Mecanismo antiguo", 0, 0, 0, QuestMarked: true),
            };
            var targets = QuestMatcher.Targets(entries, new[] { "El lobo rabioso de Ogham", "Infíltrate en la Mansión más allá del pueblo de Ogham" });
            Equal("Mecanismo antiguo | Tierras de cultivo de Ogham", string.Join(" | ", targets.Select(entry => entry.Name)),
                  "the quest's icon first, the exit by its proper noun, never the sign that names the village (user 2026-10-28)");
            Equal("Mecanismo antiguo", string.Join(" | ", QuestMatcher.Targets(entries, Array.Empty<string>()).Select(entry => entry.Name)), "the quest icon needs no tracker text");
        }

        private static void MapHintsAreSaidOncePerPlace()
        {
            var memory = new MapHintMemory();
            var start = new System.Numerics.Vector2(100, 100);
            Equal(true, memory.Note("mushrooms", start), "the first time");
            Equal(false, memory.Note("mushrooms", start + new System.Numerics.Vector2(MapHintMemory.AgainCells - 1, 0)), "the same patch, walking through it");
            Equal(true, memory.Note("roots", start), "another cue is news");
            Equal(true, memory.Note("mushrooms", start + new System.Numerics.Vector2(MapHintMemory.AgainCells, 0)), "far on, it may go another way");
            Equal(false, memory.Note("mushrooms", start), "back where it was said");

            var table = MapHintTable.Parse("{ 'G1_4': [ { 'match': 'FungiCluster', 'hint': 'mushrooms', 'goal': 'The Grim Tangle' }, { 'tile': 'Woods/RedvaleRiver/', 'hint': 'river' } ] }");
            Equal(2, table.For("G1_4").Count, "an area's rows");
            Equal(0, table.For("G1_5").Count, "an area with none");
            Equal("mushrooms", MapHintTable.Matching(table.For("G1_4"), "Metadata/Doodads/fungicluster03.ao")?.Hint, "a model, in any case");
            Equal(null, MapHintTable.Matching(table.For("G1_4"), "Metadata/Terrain/Woods/RedvaleRiver/ccmm_01.tdt"), "a tile row is not an entity's");
        }

        private static void ShippedMapHintsAreWellFormed()
        {
            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "map-hints.json")))
            {
                directory = System.IO.Path.GetDirectoryName(directory.TrimEnd(System.IO.Path.DirectorySeparatorChar))!;
            }

            var table = MapHintTable.Parse(System.IO.File.ReadAllText(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "map-hints.json")));
            foreach (var area in table.Areas)
            {
                foreach (var row in table.For(area))
                {
                    var sources = new[] { row.Match, row.Tile, row.Sign }.Count(source => source.Length > 0);
                    Equal(1, sources, $"{area}: found one way");
                    Equal(true, row.Sign.Length > 0 || row.Hint.Length > 0, $"{area}: a cue has its line");
                    Equal(true, !row.Reveals || row.Goal.Length > 0, $"{area}: reveals something");
                }
            }
        }

        private static void LostMeansWalkingWithoutNewGround()
        {
            var watch = new LostWatch();
            const int after = 3000;
            bool Polls(long from, long to, int seen, bool walking)
            {
                var due = false;
                for (var now = from; now <= to; now += 250)
                {
                    due |= watch.Update(now, seen, walking, after);
                }

                return due;
            }

            Equal(false, Polls(1000, 3000, 5, true), "the count starts with the area's ground");
            Equal(true, Polls(3250, 4500, 5, true), "walking that long without new ground");
            Equal(false, Polls(4750, 6000, 5, true), "then it counts again from nothing");
            Equal(false, Polls(6250, 9000, 6, true) | Polls(9250, 9500, 7, true), "new ground: not lost");
            Equal(false, Polls(9750, 60000, 7, false), "standing still or fighting is not being lost");
            Equal(true, Polls(60250, 63500, 7, true), "only walking counts, and it adds up");
            watch.Reset();
            Equal(false, watch.Update(70000, 7, true, after), "a new area starts again");
            Equal(false, watch.Update(90000, 7, true, after), "a gap is not walking");
        }

        private static void RadarMarksTheWayToUnseenGround()
        {
            var from = new System.Numerics.Vector2(100, 100);
            var east = new System.Numerics.Vector2(170, 100);
            var north = new System.Numerics.Vector2(100, 40);
            var right = new System.Numerics.Vector2(1, 0);
            Equal(true, ExplorationMap.AnyToward(from, right, new[] { east }, ExplorationMap.MarkReachCells), "unseen ground ahead marks the ray");
            Equal(true, ExplorationMap.AnyToward(from, right, new[] { new System.Numerics.Vector2(160, 150) }, ExplorationMap.MarkReachCells), "within the ray's quarter");
            Equal(false, ExplorationMap.AnyToward(from, right, new[] { north }, ExplorationMap.MarkReachCells), "not off to the side");
            Equal(false, ExplorationMap.AnyToward(from, right, new[] { new System.Numerics.Vector2(100 + ExplorationMap.MarkReachCells + 1, 100) }, ExplorationMap.MarkReachCells), "not beyond reach");
            const int rate = 44100;
            var open = RadarSynth.Cue(RadarReading.Open, RadarSynth.Direction.Up, rate);
            var marked = RadarSynth.WithUnexploredTail(open, RadarSynth.Direction.Up, rate);
            Equal(true, marked.Length > open.Length + (rate / 10) && marked.Take(open.Length).SequenceEqual(open), "the cue untouched, then the pips");
            Equal(true, marked.Skip(open.Length).Max(Math.Abs) is > 0.1f and <= 1f, "the pips are heard and do not clip");
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

        private static void BuildFilesAreReadWithTheirNotes()
        {
            // The GGG docs' example shape: plain ids and objects with a weapon set and a note.
            var plan = BuildPlan.Parse("{\"name\":\"Titan\",\"passives\":[\"melee17\",{\"id\":\"strength17\",\"additional_text\":\"<m>{<red>{Strength +5 is recommended}}\"},{\"id\":\"melee25\",\"weapon_set\":2}],\"skills\":[]}");
            Equal("Titan 3", $"{plan?.Name} {plan?.Passives.Count}", "name and passives");
            Equal("strength17 1 0", $"{plan?.Passives[1].Id} {plan?.Passives[1].Order} {plan?.Passives[1].WeaponSet}", "an object entry keeps its place");
            Equal(2, plan?.Passives[2].WeaponSet ?? 0, "weapon set");
            Equal(true, BuildPlan.Parse("not json") == null && BuildPlan.Parse("{\"name\":\"x\"}") == null, "no build file");
            Equal("Strength +5 is recommended", BuildNotes.Plain("<m>{<red>{Strength +5 is recommended}}"), "markup out");
            var words = new[] { "Strength", "Dexterity", "Intelligence", "Fuerza", "Destreza", "Inteligencia" };
            Equal(true, BuildNotes.ColourOnly("<m>{<red>{Strength +5 is recommended}}", words) == null, "a note naming the attribute needs nothing added");
            Equal(BuildNotes.Attribute.Strength, BuildNotes.ColourOnly("<red>{Attribute is recommended}", words), "red alone means strength");
            Equal(BuildNotes.Attribute.Dexterity, BuildNotes.ColourOnly("<green>{+5}", words), "green dexterity");
            Equal(true, BuildNotes.ColourOnly("<grey>{Point #1}", words) == null, "other colours say nothing");
            Equal("True True True False", string.Join(" ", new[] { "strength89", "intelligence36_", "attributes65", "melee17" }.Select(BuildNotes.IsAttributeNode)), "attribute nodes by id");
        }

        private static void MinionsAreCountedByKind()
        {
            Equal(string.Empty, MinionGroups.Describe(Array.Empty<(string, int)>()), "none: nothing said");
            var said = MinionGroups.Describe(new[] { ("Arquero", 100), ("Lobo", 45), ("Arquero", 60), ("Arquero", 80) });
            Equal(true, said.EndsWith("3 Arquero, 80 percent; Lobo, 45 percent") || said.EndsWith("3 Arquero, 80 por ciento; Lobo, 45 por ciento"), $"the most first, life averaged: {said}");
        }

        private static void PassiveStepsCountThePointsAlongTheLinks()
        {
            // 0 - 1 - 2 - 3, with 4 linked to nothing.
            var links = new[] { new[] { 1 }, new[] { 0, 2 }, new[] { 1, 3 }, new[] { 2 }, Array.Empty<int>() };
            var steps = PassiveMap.Steps(links, new[] { 0 });
            Equal(3, steps[3], "three links from what is taken");
            Equal(0, steps[0], "taken costs nothing");
            Equal(PassiveMap.NoPath, steps[4], "no link leads there");
            Equal(1, PassiveMap.Steps(links, new[] { 0, 2 })[3], "the nearest taken node counts");
        }

        private static void PassiveZoneNamesTheSideAndRing()
        {
            var positions = new[] { new Vector2(-100, 0), new Vector2(100, 0), new Vector2(0, -100), new Vector2(0, 100), new Vector2(10, 0), new Vector2(50, -50) };
            Equal((0, 2), PassiveMap.Zone(positions, 1), "the far right edge: right, outer");
            Equal((2, 2), PassiveMap.Zone(positions, 2), "screen y grows down: the top is up");
            Equal((0, 0), PassiveMap.Zone(positions, 4), "beside the centre: inner");
            Equal((1, 2), PassiveMap.Zone(positions, 5), "up right, at 0.7 of the radius: outer");
        }

        private static void PassiveFinderWalksItsLists()
        {
            var (keystone, nearNotable, farNotable, reachable) = ((IntPtr)1, (IntPtr)2, (IntPtr)3, (IntPtr)4);
            IReadOnlyList<IntPtr> Members(PassiveCategory category) => category switch
            {
                PassiveCategory.Keystones => new[] { keystone },
                PassiveCategory.Notables => new[] { nearNotable, farNotable },
                PassiveCategory.Reachable => new[] { reachable },
                _ => Array.Empty<IntPtr>(),
            };

            var finder = new PassiveFinder();
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Keystones, finder.Category, "down: the first list");
            Equal(keystone, finder.Target, "the list's nearest is chosen at once");
            finder.ChangeCategory(1, Members);
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(farNotable, finder.Target, "right: the next entry");
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(nearNotable, finder.Target, "and round");
            finder.ChangeEntry(-1, new[] { farNotable, nearNotable });
            Equal(farNotable, finder.Target, "the list re-sorted under the choice: it moves from where the choice is now");
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Reachable, finder.Category, "an empty list is passed over");
            finder.ChangeCategory(1, Members);
            Equal(PassiveCategory.Position, finder.Category, "the lists wrap to the position");
            Equal(IntPtr.Zero, finder.Target, "which lets the target go");

            finder.ChangeCategory(1, Members);
            Equal(false, finder.Arrive(reachable, true), "another node");
            Equal(keystone, finder.Target, "keeps the guide");
            Equal(true, finder.Arrive(keystone, false), "landing on a target taken");
            Equal(IntPtr.Zero, finder.Target, "ends the guide");
            Equal(false, finder.Arrive(keystone, false), "once");

            finder.ChangeCategory(-1, Members);
            finder.ChangeCategory(1, Members);
            Equal(true, finder.Arrive(keystone, true), "landing on a target not taken");
            Equal((keystone, true), (finder.Target, finder.Taking), "leads on to the nodes to take for it");
            Equal(false, finder.Arrive(keystone, true), "said once");
            finder.ChangeEntry(1, Members(finder.Category));
            Equal(false, finder.Taking, "another choice is looked at first");
            finder.Drop();
            Equal((IntPtr.Zero, false), (finder.Target, finder.Taking), "taken: the guide ends");
        }

        private static void TheTreeRemembersWhereEachCharacterLeftOff()
        {
            var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"passive-resume-{Guid.NewGuid():N}.txt");
            try
            {
                var book = new PassiveResume(file);
                Equal(true, book.Get("yarkey") == null, "nothing yet");
                book.Set("yarkey", "attributes20");
                book.Set("malgdhler", "spell_criticals7");
                book.Set("yarkey", "jewel_slot1970");
                var again = new PassiveResume(file);
                Equal("jewel_slot1970", again.Get("yarkey"), "the last place, after a restart");
                Equal("spell_criticals7", again.Get("malgdhler"), "each character its own");
            }
            finally
            {
                System.IO.File.Delete(file);
            }

            var finder = new PassiveFinder();
            var place = (IntPtr)7;
            finder.Return(place);
            Equal((place, true, PassiveCategory.Position), (finder.Target, finder.Returning, finder.Category), "led back from the start");
            Equal(true, finder.Arrive(place, true), "landing there");
            Equal((IntPtr.Zero, false, false), (finder.Target, finder.Returning, finder.Taking), "ends the guide: nothing to take there");
        }

        private static void APassiveTrailTakesTheFewestPointsAlongTheBuild()
        {
            // 0 (taken) - 1 - 2 - 5 (target), and 0 - 3 - 4 - 5: two trails of three points; 6 hangs off 5, 7 is linked to nothing.
            var links = new[] { new[] { 1, 3 }, new[] { 0, 2 }, new[] { 1, 5 }, new[] { 0, 4 }, new[] { 3, 5 }, new[] { 2, 4, 6 }, new[] { 5 }, Array.Empty<int>() };
            int Plain(int node) => PassiveMap.TrailCost(false, false, false);
            Equal(3, PassiveMap.Trail(links, new[] { 0 }, 5, Plain)!.Count, "the fewest points");
            Equal("1,2,5", string.Join(",", PassiveMap.Trail(links, new[] { 0 }, 5, node => PassiveMap.TrailCost(false, node == 2, false))!), "a notable on one of two trails as long");
            Equal("3,4,5", string.Join(",", PassiveMap.Trail(links, new[] { 0 }, 5, node => PassiveMap.TrailCost(node is 3 or 4, node == 2, false))!), "the build's nodes before a notable");
            Equal("2,5,6", string.Join(",", PassiveMap.Trail(links, new[] { 0, 1 }, 6, Plain)!), "from the nearest taken node, in the order they can be taken");
            Equal(0, PassiveMap.Trail(links, new[] { 0, 1 }, 1, Plain)!.Count, "taken: nothing to take");
            Equal(true, PassiveMap.Trail(links, new[] { 0 }, 7, Plain) == null, "no link leads there");

            // 0 - 1 - 2 (target): one point off the build, or 0 - 3 - 4 - 5 - 2 along it.
            var detour = new[] { new[] { 1, 3 }, new[] { 0, 2 }, new[] { 1, 5 }, new[] { 0, 4 }, new[] { 3, 5 }, new[] { 4, 2 } };
            Equal("3,4,5,2", string.Join(",", PassiveMap.Trail(detour, new[] { 0 }, 2, node => PassiveMap.TrailCost(node is 2 or 3 or 4 or 5, false, true))!), "a build's node is reached along the build");
            Equal("1,2", string.Join(",", PassiveMap.Trail(detour, new[] { 0 }, 2, node => PassiveMap.TrailCost(node is 3 or 4 or 5, false, false))!), "another node by the fewest points");
        }

        private static void ObjectSoundsDieAwayPastTheirRange()
        {
            const float Range = 1300f;
            const float Far = 0.3f;
            Equal(1f, ProximityFade.Gain(0f, Range, Far), "on it");
            Equal(Far, ProximityFade.Gain(Range, Range, Far), "the edge of the range keeps its level");
            Equal(0f, ProximityFade.Gain(Range + ProximityFade.TailWorld, Range, Far), "silent where it stops");
            var previous = 2f;
            for (var distance = 0f; distance <= Range + ProximityFade.TailWorld; distance += 10f)
            {
                var gain = ProximityFade.Gain(distance, Range, Far);
                Equal(true, gain < previous, $"falls all the way ({distance})");
                Equal(true, previous - gain < 0.02f || previous == 2f, $"with no step ({distance})");
                previous = gain;
            }
        }

        private static void SettingsMenuOpensGroupsAndGoesBack()
        {
            static SettingsCursor.Node Menu(int settings, params SettingsCursor.Node[] submenus) =>
                new(Enumerable.Repeat(SettingsCursor.Node.Setting, settings).Concat(submenus).ToArray());

            // Top level: a group of 5, one of 12, and Sound: one volume, then a submenu of 3 and one of 2.
            var cursor = new SettingsCursor(Menu(0, Menu(5), Menu(12), Menu(1, Menu(3), Menu(2))));
            Equal(false, cursor.InGroup, "opens on the groups");
            Equal((1, 3), cursor.Place, "the top level lists the groups");
            cursor.Move(1);
            Equal(true, cursor.Enter(), "a group opens");
            Equal((true, 1, 0), (cursor.InGroup, cursor.Path[0], cursor.Path[1]), "a group opens on its first entry");
            Equal((1, 12), cursor.Place, "within a group, the place is the group's");
            Equal(false, cursor.Enter(), "a setting is no submenu");
            cursor.Move(-1);
            Equal(11, cursor.Path[1], "a group wraps");
            Equal(true, cursor.Back(), "back");
            Equal((false, 1), (cursor.InGroup, cursor.Path[0]), "back lands on the group left");
            Equal(false, cursor.Back(), "nothing above the top level");
            cursor.Move(-2);
            Equal(2, cursor.Path[0], "the groups wrap");
            cursor.Move(-1);
            cursor.Enter();
            Equal(11, cursor.Path[1], "a group opens on the entry it was left on");
            cursor.Back();
            cursor.Move(1);
            cursor.Enter();
            Equal(false, cursor.OnSubmenu, "Sound opens on its volume");
            cursor.Move(2);
            Equal(true, cursor.OnSubmenu, "then its submenus");
            cursor.Enter();
            Equal((3, 0, 2), (cursor.Path.Count, cursor.Place.Position - 1, cursor.Place.Count), "a nested submenu opens a level deeper");
            cursor.Move(1);
            cursor.Back();
            Equal((2, 2), (cursor.Path.Count, cursor.Path[1]), "back one level, on the submenu left");
            cursor.Back();
            cursor.Enter();
            Equal(2, cursor.Path[1], "each submenu reopens where it was left");
            cursor.Enter();
            Equal(1, cursor.Path[2], "nested ones too");
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
            output.Say(SpeechKind.Charges, "g");
            output.Say(SpeechKind.Combo, "h");
            Equal("urge a|b False|urge c|mention d|e True|f False|g False|h False", string.Join("|", output.Calls), "each kind at its level, urgent news holds, a waiting life warning keeps its turn, defaults for unknown values, charges and combo wait");
        }

        private sealed class RecordingOutput : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"{text} {interrupt}");

            public void Urge(string text, int holdMs = 1500) => this.Calls.Add($"urge {text}");

            public void Keep(string text, int holdMs, bool interrupt = false, bool yieldsToMoves = false) => this.Calls.Add($"keep {text} {interrupt}");

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

        private static void StashOpenedOnInventoryNamesTheInventory()
        {
            // Live 2026-11-08: the stash opened with the cursor on the inventory's first item and the stash's selected
            // tab "1" was said as its place ("pestaña 1. Orbe de transmutación, columna 1, fila 1"), where a helmet lay.
            Equal("Inventario", FocusDetails.PagePlace("Inventario", "1"), "an inventory item beside the stash is said in the inventory");
            Equal(Announcements.TabTitle("1"), FocusDetails.PagePlace("Alijo. " + Announcements.TabTitle("1"), "1"), "a stash item keeps its tab");
            Equal(Announcements.TabTitle("1"), FocusDetails.PagePlace(string.Empty, "1"), "a page focus with no part of its own (options) takes the tab");
            Equal("Gráficos", FocusDetails.PagePlace(string.Empty, "Gráficos"), "a named tab keeps its name");

            // Live 2026-11-10: a merchant opened on the inventory's side said "Inventario. Orbe de caos…" and never its tab.
            Equal("Armas", FocusDetails.PageBeside("Inventario", "Armas", string.Empty), "a merchant's first tab is named beside the inventory");
            Equal("Alijo, " + Announcements.TabTitle("1"), FocusDetails.PageBeside("Inventario", "1", "Alijo"), "a numbered tab with its panel's title");
            Equal(string.Empty, FocusDetails.PageBeside("Alijo. " + Announcements.TabTitle("1"), "1", "Alijo"), "a focus on the page itself is placed by its tab alone");
            Equal(string.Empty, FocusDetails.PageBeside(string.Empty, "Gráficos", string.Empty), "as is one with no part of its own");
            Equal("Armas", FocusDetails.PageBeside("Inventario", "Armas", "Inventario"), "the inventory is not the panel beside itself");
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
            var stamp = Poe2Access.GameDataStamp.Parse(null).With(install, "sounds", 2).With(install, "places", 1);
            var read = Poe2Access.GameDataStamp.Parse(stamp.ToString().Replace("\n", "\r\n"));
            Equal(install, read.Install, "the install lines survive the file");
            Equal(true, read.IsCurrent(install, "sounds", 2), "a step done at its version is current");
            Equal(true, read.IsCurrent(install, "places", 1), "the other steps are kept");
            Equal(false, read.IsCurrent(install, "sounds", 3), "a raised version reruns that step");
            Equal(false, read.IsCurrent(install, "area names", 1), "a step never done runs");
            Equal(false, read.IsCurrent("index=11:20\nexe=30:40\nfmod=2.03.06\n", "places", 1), "a game patch reruns every step");
            Equal(1, read.With("index=11:20\n", "sounds", 2).Steps.Count, "a step done on a patched install forgets the others");
        }

        private static void GameDataStepsAreEachListedOnce()
        {
            // A step listed twice ran twice in one batch and the second run failed on the first's output (2026-11-08).
            var steps = Poe2Access.GameDataSteps.All;
            Equal(steps.Length, steps.Select(step => step.Name).Distinct().Count(), "each step name once");
            Equal(steps.Count(step => step.Output != null), steps.Where(step => step.Output != null).Select(step => step.Output).Distinct().Count(), "each output once");
            Equal(steps.Count(step => step.Command != null), steps.Where(step => step.Command != null).Select(step => step.Command).Distinct().Count(), "each command once");
        }

        private static void OldGameDataStampCarriesOver()
        {
            const string install = "index=10:20\nexe=30:40\nfmod=2.03.06\n";
            string[] steps = { "item sounds", "places", "sounds" };
            var current = Poe2Access.GameDataStamp.FromLegacy("format=7\r\n" + install.Replace("\n", "\r\n"), install, steps, "sounds");
            Equal(3, current.Steps.Count, "the last format vouches for every step");
            Equal(true, current.IsCurrent(install, "sounds", Poe2Access.GameDataStamp.LegacyVersion), "at the first version");
            var older = Poe2Access.GameDataStamp.FromLegacy("format=5\n" + install, install, steps, "sounds");
            Equal(false, older.Steps.ContainsKey("sounds"), "an earlier format reruns the sounds");
            Equal(true, older.IsCurrent(install, "places", 1), "and keeps the catalogs it covered");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy("format=2\n" + install, install, steps, "sounds").Steps.Count, "a format before hit times and places covers nothing");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy("format=7\nindex=9:20\nexe=30:40\nfmod=2.03.06\n", install, steps, "sounds").Steps.Count, "another install covers nothing");
            Equal(0, Poe2Access.GameDataStamp.FromLegacy(null, install, steps, "sounds").Steps.Count, "no old stamp covers nothing");
        }

        private static void GameContentIsFoundInEitherLayout()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poe2-install-" + Guid.NewGuid().ToString("N"));
            var ggpk = System.IO.Path.Combine(root, "Content.ggpk");
            var index = System.IO.Path.Combine(root, "Bundles2", "_.index.bin");
            try
            {
                System.IO.Directory.CreateDirectory(root);
                Equal(null, Poe2Access.GameInstall.ContentFile(root), "a folder without game content is no install");
                System.IO.File.WriteAllText(ggpk, string.Empty);
                Equal(ggpk, Poe2Access.GameInstall.ContentFile(root), "the Kakao Games client keeps everything in Content.ggpk");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(index)!);
                System.IO.File.WriteAllText(index, string.Empty);
                Equal(index, Poe2Access.GameInstall.ContentFile(root), "a Steam install's bundle index comes first");
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        /// <summary>The Druid's tree shows "Regeneración de vida" where the node record keeps "Daño crítico de hechizos" (2026-11-09).</summary>
        private static void AClassSeesItsOwnPassives()
        {
            var overrides = PassiveOverrides.Parse(new[]
            {
                "# character id\treplaced passive id\trow\tshown passive id\trow",
                "row-size\t487",
                "Metadata/Characters/StrInt/StrIntFourb\tspell_criticals7\t4709\tdruid_class_start22\t5699",
                "Metadata/Characters/Int/IntFour\tspells18\t2779\tspell_and_minion_damage1\t4935",
            });
            Equal(2, overrides.Count, "both overrides read");
            Equal("druid_class_start22", overrides.Find("Metadata/Characters/StrInt/StrIntFourb", "spell_criticals7")?.ShownId, "the Druid's node in its place");
            Equal("druid_class_start22", overrides.Find("metadata/characters/strint/strintfourb", "spell_criticals7")?.ShownId, "the player's path in any case");
            Equal((5699L - 4709L) * 487L, overrides.Step(overrides.Find("Metadata/Characters/StrInt/StrIntFourb", "spell_criticals7")!.Value), "rows apart times the row size");
            Equal(false, overrides.Find("Metadata/Characters/Dex/DexFour", "spell_criticals7").HasValue, "another class sees the node itself");
            Equal(0, PassiveOverrides.Parse(new[] { "Metadata/Characters/Int/IntFour\tspells18\t2779\tspell_and_minion_damage1\t4935" }).Count, "no row size: nothing swapped");
        }

        private static void KakaoClientIsAGameProcess()
        {
            Equal(true, Poe2Access.GameInstall.ProcessNames.Contains("PathOfExile_KG"), "the Korean (Kakao Games) client's process");
            Equal(true, Poe2Access.GameInstall.ProcessNames.Contains("PathOfExileSteam"), "the Steam client's process");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator("PathOfExile_KG", false, false), "the Korean client is read only by an administrator");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExile_KG", false, true), "an administrator launcher carries on");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, false), "the Steam client needs no administrator");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, false, gameReadDenied: true), "unless it runs as administrator (Steam run so)");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator("PathOfExileSteam", false, true, gameReadDenied: true), "denied to an administrator too: elevating again would not help");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator(null, false, false), "no game running needs no administrator");
            Equal(true, Poe2Access.GameInstall.NeedsAdministrator(null, true, false), "an administrator host left after the Korean client closed");
            Equal(false, Poe2Access.GameInstall.NeedsAdministrator(null, true, true), "an administrator launcher stops it itself");
        }

        private static void SteamGameIsStartedBeforeTheKakaoClient()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poe2-steam-" + Guid.NewGuid().ToString("N"));
            var steam = System.IO.Path.Combine(root, "Steam");
            var library = System.IO.Path.Combine(root, "Games");
            var kakao = System.IO.Path.Combine(root, "Kakaogames");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(steam, "steamapps"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(library, "steamapps"));
                Equal(false, Poe2Access.GameInstall.SteamHasGame(null), "no Steam has no game");
                Equal(false, Poe2Access.GameInstall.SteamHasGame(steam), "Steam without the game");

                // libraryfolders.vdf escapes the backslashes of each library's path.
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(steam, "steamapps", "libraryfolders.vdf"),
                    "\"libraryfolders\"\n{\n\t\"0\"\n\t{\n\t\t\"path\"\t\t\"" + steam.Replace("\\", "\\\\") + "\"\n\t}\n\t\"1\"\n\t{\n\t\t\"path\"\t\t\"" +
                    library.Replace("\\", "\\\\") + "\"\n\t\t\"apps\"\n\t\t{\n\t\t\t\"2694490\"\t\t\"1\"\n\t\t}\n\t}\n}\n");
                Equal(false, Poe2Access.GameInstall.SteamHasGame(steam), "a library listing the game but holding no manifest");
                System.IO.File.WriteAllText(System.IO.Path.Combine(library, "steamapps", "appmanifest_2694490.acf"), string.Empty);
                Equal(true, Poe2Access.GameInstall.SteamHasGame(steam), "the game in a second library");

                Equal(false, Poe2Access.GameInstall.StartsKakaoClient(true, kakao), "both installed: Steam starts the game");
                Equal(true, Poe2Access.GameInstall.StartsKakaoClient(false, kakao), "only the Korean client installed");
                Equal(false, Poe2Access.GameInstall.StartsKakaoClient(false, null), "neither found: Steam, as before");
            }
            finally
            {
                System.IO.Directory.Delete(root, true);
            }
        }

        private static void ReleaseTagsOrderByDayThenNumber()
        {
            Equal(new Version(2026, 10, 28, 1), Poe2Access.ReleaseTag.Parse("v2026.10.28"), "an unnumbered tag is the day's first release");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.28-2") > Poe2Access.ReleaseTag.Parse("v2026.10.28"), "-2 follows the day's first");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.29") > Poe2Access.ReleaseTag.Parse("v2026.10.28-3"), "a later day follows any number");
            Equal(true, Poe2Access.ReleaseTag.Parse("v2026.10.28-10") > Poe2Access.ReleaseTag.Parse("v2026.10.28-9"), "numbers compare as numbers");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("latest"), "not a dated tag");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("v2026.10"), "a tag without its day");
            Equal<Version?>(null, Poe2Access.ReleaseTag.Parse("v2026.10.28-0"), "releases are numbered from 2");
        }

        private static void ReleaseManifestPinsThePackageToItsTag()
        {
            var sha256 = new string('A', 64);
            var release = Poe2Access.ReleaseManifest.Parse($"{{\"tag\": \"v2026.10.30-2\", \"sha256\": \"{sha256}\", \"notes\": \" Changes \"}}");
            Equal("https://github.com/Ali-Bueno/poe2-access/releases/download/v2026.10.30-2/Poe2Access.zip", release.ZipUrl, "the package of that release, not of whatever is latest");
            Equal(new Version(2026, 10, 30, 2), release.Version, "the tag as a version");
            Equal(sha256, release.Sha256, "the checksum as written");
            Equal("Changes", release.Notes, "notes trimmed");

            string Refused(string json)
            {
                try
                {
                    Poe2Access.ReleaseManifest.Parse(json);
                    return "accepted";
                }
                catch (System.IO.InvalidDataException)
                {
                    return "refused";
                }
            }

            Equal("refused", Refused("{\"tag\": \"v2026.10.30\", \"sha256\": \"abc\"}"), "a short checksum");
            Equal("refused", Refused($"{{\"tag\": \"v2026.10.30\", \"sha256\": \"{new string('g', 64)}\"}}"), "a checksum that is not hex");
            Equal("refused", Refused($"{{\"tag\": \"latest\", \"sha256\": \"{sha256}\"}}"), "a tag that is not a date");
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
            Equal(string.Empty, Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Orbe, 6") },
                                                    new List<(InventoryName, string)> { (main, "Orbe, 5"), (InventoryName.Cursor1, "Orbe") }, Announcements.SlotName, true, 0, "")),
                  "put back on its stack: the stack that grew was not used up");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Orbe") }, Announcements.SlotName, false, 0, "")),
                  "during play the pick-up watch speaks");
            Equal("TakenFrom Runa Alijo", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Runa") }, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") },
                                                              Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "the hand picked it up in the stash and set it down in the inventory");
            Equal(string.Empty, Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (InventoryName.Cursor1, "Runa") }, Announcements.SlotName, true, 0, "Alijo", "Alijo")),
                  "picked up and set down in the stash: rearranging, not news");
            Func<string, bool> gemsOnly = name => name == "Furia I";
            Equal("DroppedOrUsed Orbe | MovedTo Furia I Habilidades", Said(ItemMoves.Read(none, new List<(InventoryName, string)> { (main, "Orbe"), (main, "Furia I") },
                                                                                          Announcements.SlotName, true, 0, "Habilidades", "", gemsOnly)),
                  "beside the skills only a gem goes into them; an orb was used or dropped");
            Equal("Obtained Arco | TakenFrom Furia I Habilidades", Said(ItemMoves.Read(new List<(InventoryName, string)> { (main, "Arco"), (main, "Furia I") }, none,
                                                                                          Announcements.SlotName, true, 0, "Habilidades", "", gemsOnly)),
                  "and only a gem comes out of them");
            Equal("Orbe de aumento", ItemMoves.Title("Orbe de aumento, 3"), "the title without the stack");
        }

        private static void SalvageSaysWhatWentAndWhatCame()
        {
            string Said(SalvageOutcome outcome) =>
                string.Join(" | ", outcome.Salvaged) + " -> " + string.Join(" | ", outcome.Received.Select(item => $"{item.Title} {item.Count}"));
            var helmet = ("Corona con cuernos, raro", 1);
            var scrolls = ("Pergamino de sabiduría, 23", 23);
            Equal("Corona con cuernos, raro -> Esquirla del artesano 1",
                  Said(SalvageOutcome.Read(new[] { helmet, scrolls }, new[] { scrolls, ("Esquirla del artesano", 1) })),
                  "the item went and a new stack came");
            Equal("Corona con cuernos, raro -> Chatarra de armero 2",
                  Said(SalvageOutcome.Read(new[] { helmet, ("Chatarra de armero, 3", 3) }, new[] { ("Chatarra de armero, 5", 5) })),
                  "a stack that grew counts only what came");
            Equal(" -> ", Said(SalvageOutcome.Read(new[] { helmet, scrolls }, new[] { scrolls, helmet })), "moved within the inventory: nothing");
            Equal("Gorro de fieltro -> ", Said(SalvageOutcome.Read(new[] { ("Gorro de fieltro", 1), ("Gorro de fieltro, mágico", 1) },
                                                                    new[] { ("Gorro de fieltro, mágico", 1) })),
                  "of two items of one base, the one that went");
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
            var ramparts = new[]
            {
                new ScanEntry(14, ScanCategory.Exits, "Mansión de Ogham", 0, 0, 0),
                new ScanEntry(15, ScanCategory.Exits, "Camino de vuelta", 0, 0, 0, QuestMatcher.TileNote),
            };
            Equal("Mansión de Ogham", string.Join(" | ", QuestMatcher.Targets(ramparts, new[] { "El lobo rabioso de Ogham", "Infíltrate en la Mansión más allá del pueblo. Encuentra al Conde Geonor" },
                                                                               "Las murallas de la mansión", "Pueblo de Ogham").Select(entry => entry.Name)),
                  "the Manor from its ramparts, never the way back (user 2026-11-05)");
            var altars = new[]
            {
                new ScanEntry(11, ScanCategory.Encounters, "Altar de ritual", 0, 0, 0, "ritual"),
                new ScanEntry(12, ScanCategory.Encounters, "Altar de ritual del jefe", 0, 0, 0, "ritual"),
                new ScanEntry(13, ScanCategory.Encounters, "Altar de ritual, completado", 0, 0, 0, "ritual"),
            };
            Equal("Altar de ritual", string.Join(" | ", QuestMatcher.Targets(altars, new[] { "Altares siniestros", "Busca los altares de ritual y purifícalos" }).Select(entry => entry.Name)),
                  "the objective's plural names the altars still to do, not the boss's nor a done one (Freythorn, 2026-10-29)");
        }

        private static void QuestTrackerPageIsSaidWhenItChanges()
        {
            var page = new QuestTrackerPage();
            Equal("Altares siniestros. Busca Villafrey", page.News(new[] { "Altares siniestros. Busca Villafrey" }), "the first page");
            Equal(string.Empty, page.News(Array.Empty<string>()), "the tracker hidden by a panel");
            Equal(string.Empty, page.News(new[] { "Altares siniestros. Busca Villafrey" }), "back the same: no news");
            Equal("Altares siniestros. Busca los altares de ritual", page.News(new[] { "Altares siniestros. Busca los altares de ritual" }), "the objective changed on entering the area");
            Equal("El rastro de la corrupción. Viaja a Ogham", page.News(new[] { "El rastro de la corrupción. Viaja a Ogham" }), "another page");
            Equal("Altares siniestros. Busca los altares de ritual", page.News(new[] { "Altares siniestros. Busca los altares de ritual" }), "paged back to it");
        }

        private static void QuestPageSaysOnlyTheQuestThatChanged()
        {
            var page = new QuestTrackerPage();
            var (rising, trail) = ("Ascenso al poder. Encuentra un Altar de reliquias", "El rastro de la corrupción. Vuelve sobre los pasos de Orbala");
            Equal($"{rising}. {trail}", page.News(new[] { rising, trail }), "the first page, whole");
            var placed = "Ascenso al poder. Coloca tu barya en el Altar de reliquias";
            Equal(placed, page.News(new[] { placed, trail }), "one objective changed: only that quest");
            var city = "La ciudad de los siete ríos. Usa el Mapa del desierto";
            Equal(city, page.News(new[] { placed, city }), "paged on: the quest in hand opens every page, so only the other");
            Equal(string.Empty, page.News(new[] { placed }), "a quest gone leaves nothing new");
            var ivory = "Un robo de marfil. Viaja al Páramo";
            var crown = "Una corona de piedra. Encuentra la Gruta";
            Equal($"{ivory}. {crown}", page.News(new[] { ivory, crown }), "a page with nothing in common is said whole");
            Equal(string.Empty, page.News(new[] { ivory, crown }, inMenu: true), "the same page in a menu is no news");
            Equal(string.Empty, page.News(new[] { ivory, placed }, inMenu: true), "a change waits for the menu");
            Equal(placed, page.News(new[] { ivory, placed }), "and says only itself");
        }

        private static void TurnedQuestPageIsToldFromTheObjectivesOwnChange()
        {
            var page = new QuestTrackerPage();
            var (rising, trail, city) = ("Ascenso al poder. Busca el altar", "El rastro. Sigue a Orbala", "La ciudad. Viaja a Keth");
            page.News(new[] { rising, trail }, legend: "Página de misiones 1/5 LT + cruceta derecha");
            Equal(false, page.Turned, "the first page is no turn");
            Equal(city, page.News(new[] { rising, city }, legend: "Página de misiones 2/5 LT + cruceta derecha"), "the next page");
            Equal(true, page.Turned, "its number changed: the player turned it");
            var placed = "Ascenso al poder. Coloca tu barya";
            Equal(placed, page.News(new[] { placed, city }, legend: "Página de misiones 2/5 LT + cruceta derecha"), "an objective on the same page");
            Equal(false, page.Turned, "is no turn");
            Equal(string.Empty, page.News(new[] { placed, city }, legend: "Página de misiones 3/5 LT + cruceta derecha"), "the legend is read after the pages, a turn can show there first");
            Equal(false, page.Turned, "with nothing to say yet");
            Equal(trail, page.News(new[] { placed, trail }, legend: "Página de misiones 3/5 LT + cruceta derecha"), "the page follows");
            Equal(true, page.Turned, "and is that turn");
            Equal(string.Empty, page.News(new[] { placed, trail }, legend: "Página de misiones 3/6 LT + cruceta derecha"), "a new quest only raises the total");
            Equal(placed, page.News(new[] { placed }, legend: "Página de misiones 4/6 LT + cruceta derecha"), "a page turned to with nothing new is said whole: the player asked");
            Equal(true, page.Turned, "and turned");
        }

        private static void AFallingClockIsSaidAtItsMarks()
        {
            var watch = new CountdownWatch();
            Equal(null, watch.Heard("00:20"), "a clock first seen may not be falling");
            Equal(19, watch.Heard("00:19"), "falling: said at once");
            Equal(null, watch.Heard("00:19"), "the same second is no news");
            Equal(null, watch.Heard("00:18"), "nor any second");
            Equal(null, watch.Heard("00:11"), "before a mark");
            Equal(10, watch.Heard("00:10"), "the mark of ten");
            Equal(4, watch.Heard("00:04"), "a mark jumped over is still said, with the time left");
            Equal(null, watch.Heard(null), "gone");
            Equal(null, watch.Heard("00:01"), "a clock that counts up");
            Equal(null, watch.Heard("00:02"), "is never said");
            Equal(true, CountdownWatch.IsClock("00:19") && CountdownWatch.IsClock("01:05"), "minutes and seconds");
            Equal(false, CountdownWatch.IsClock("9") || CountdownWatch.IsClock("1/5"), "a flask's charges and a page are no clock");
            Equal(false, CountdownWatch.IsClock("0:06"), "nor a buff's duration");
            Equal(true, MinimapMarks.IsQuestIcon("SanctumFountain"), "the Death Crystal is what the Trial's room wants");
            Equal(MinimapMarks.QuestObjectKind, MinimapMarks.DeviceKind("MarakethSanctumRoomSelector"), "and a Trial's reward, under Leagues, sounds as a quest object");
        }

        private static void CameraViewIsASixteenByNineScreenAroundThePlayer()
        {
            // World +X draws right and up, +Y left and up (2560x1440: 85.3 px across and 63.1 px up per 100 units of either).
            Equal(true, CameraView.Offset(100, 0).X > 0 && CameraView.Offset(100, 0).Y < 0, "world +X: right and up");
            Equal(true, CameraView.Offset(0, 100).X < 0 && CameraView.Offset(0, 100).Y < 0, "world +Y: left and up");
            Equal(true, CameraView.InView(0, 0), "the player");
            Equal(true, CameraView.InView(600, -600), "0.71 heights right: on screen");
            Equal(false, CameraView.InView(800, -800), "0.95 heights right: past a 16:9 edge at 0.89");
            Equal(true, CameraView.InView(-600, -600), "below, 0.47 from the centre");
            Equal(false, CameraView.InView(-700, -700), "lower, 0.56 from the centre");
            Equal(true, CameraView.InView(480, 480), "above, 0.42 heights: the player stands high, so less is seen above");
            Equal(false, CameraView.InView(530, 530), "higher, 0.46: past the top edge at 0.44");
            Equal(1f, MathF.Round(CameraView.HalfScreens(750.6f, -750.6f).X, 2), "a 16:9 screen's right edge is one half screen");
            Equal(false, CameraView.IsUsable(System.Numerics.Vector2.Zero, System.Numerics.Vector2.Zero), "no window: nothing drawn");
            Equal(false, CameraView.IsUsable(new System.Numerics.Vector2(float.NaN, 1), new System.Numerics.Vector2(1, 1)), "a camera not drawn yet");
            Equal(true, CameraView.IsUsable(CameraView.Offset(100, 0), CameraView.Offset(0, 100)), "the measured camera itself");
        }

        private static void CameraViewDrawsFromTheMatrixWhateverTheWindow()
        {
            // A camera like the game's: above and behind the player, looking down at it; the game widens the view with the
            // window (the same vertical angle), so its matrix differs per aspect while the place drawn per height must not.
            var target = new System.Numerics.Vector3(5000, 3000, 0);
            var eye = target + new System.Numerics.Vector3(-700, -700, 1200);
            var view = System.Numerics.Matrix4x4.CreateLookAt(eye, target, System.Numerics.Vector3.UnitZ);
            System.Numerics.Matrix4x4 Camera(float aspect) => view * System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(0.6f, aspect, 10f, 20000f);
            var point = target + new System.Numerics.Vector3(300, -200, 0);
            var wide = CameraView.Draw(Camera(16f / 9f), point);
            var square = CameraView.Draw(Camera(5f / 4f), point);
            var ultra = CameraView.Draw(Camera(21f / 9f), point);
            Equal(true, wide is { } w && square is { } s && ultra is { } u && System.Numerics.Vector2.Distance(w, s) < 1e-5f && System.Numerics.Vector2.Distance(w, u) < 1e-5f,
                  "the same place at 16:9, 5:4 and 21:9");
            Equal(true, CameraView.Draw(Camera(16f / 9f), target) is { } centre && centre.Length() < 1e-5f, "what the camera looks at: the centre");

            // What GameHelper's WorldToScreen gives on a 1280x1024 window, in heights from its centre: the same numbers.
            System.Numerics.Vector2 OnWindow(System.Numerics.Matrix4x4 camera, float width, float height)
            {
                var clip = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(point, 1f), camera);
                var (x, y) = ((clip.X / clip.W + 1f) * width / 2f, (1f - (clip.Y / clip.W)) * height / 2f);
                return new System.Numerics.Vector2(x - (width / 2f), y - (height / 2f)) / height;
            }

            Equal(true, square is { } s2 && System.Numerics.Vector2.Distance(s2, OnWindow(Camera(5f / 4f), 1280, 1024)) < 1e-5f, "as a 5:4 window draws it");

            // A view shifted off its centre (as a camera keeping the player high on the screen could be) keeps its aspect.
            var (near, half) = (10f, 10f * MathF.Tan(0.3f));
            var shifted = view * System.Numerics.Matrix4x4.CreatePerspectiveOffCenter(-half * 16f / 9f, half * 16f / 9f, -half * 0.9f, half * 1.1f, near, 20000f);
            Equal(true, CameraView.Draw(shifted, point) is { } off && System.Numerics.Vector2.Distance(off, OnWindow(shifted, 2560, 1440)) < 1e-5f, "an off-centre view");

            Equal(false, CameraView.Draw(Camera(16f / 9f), eye + (eye - target)).HasValue, "behind the camera: not drawn");
            Equal(false, CameraView.Draw(System.Numerics.Matrix4x4.Identity, point).HasValue, "GameHelper's placeholder before the first frame draws nothing");
            Equal(true, wide is { } w2 && CameraView.InView(w2), "a place near the player is in view");
            Equal(false, CameraView.Draw(Camera(16f / 9f), target + new System.Numerics.Vector3(4000, -4000, 0)) is { } far && CameraView.InView(far), "far off: out of view");
        }

        private static void TrialHudCountsWaterAndKeys()
        {
            Equal(new TrialCounters(39, 7, 1, 0, 0), TrialHud.Parse(new[] { "39(+7)", "1", "0", "0" }), "the HUD of 2026-11-07");
            Equal(new TrialCounters(46, 0, 0, 0, 2), TrialHud.Parse(new[] { "46", "0", "0", "2" }), "water without what is to come");
            Equal(null, TrialHud.Parse(new[] { "39(+7)", "1" }), "not all the counters");
            Equal(null, TrialHud.Parse(new[] { "x", "1", "0", "0" }), "no number");
            Equal((474, 513), TrialHud.HonourOf("Honor: 474/513"), "the honour line");
            Equal(null, TrialHud.HonourOf("Honor"), "with no amount");

            var watch = new TrialCounterWatch();
            Equal(default, watch.Heard(new TrialCounters(39, 0, 0, 0, 0), 0), "the first reading is no news");
            Equal(default, watch.Heard(new TrialCounters(41, 0, 0, 0, 0), 100), "water still falling into the purse");
            Equal(default, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 800), "more of it");
            Equal(46, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 2400).Water, "said once it settles");
            Equal(null, watch.Heard(new TrialCounters(46, 0, 0, 0, 0), 4000).Water, "and only once");
            Equal(1, watch.Heard(new TrialCounters(46, 0, 1, 0, 0), 4100).Bronze, "a key at once");
            Equal(default, watch.Heard(new TrialCounters(30, 0, 1, 0, 0), 4200), "water spent is no news");
            Equal(null, watch.Heard(new TrialCounters(30, 0, 1, 0, 0), 6000).Water, "nor once settled");
            Equal(31, watch.Heard(new TrialCounters(31, 0, 1, 0, 0), 6100) is var _ ? watch.Heard(new TrialCounters(31, 0, 1, 0, 0), 7700).Water : null, "the next gain counts from there");
        }

        private static void TrialTrapsAreToldByTheirFiles()
        {
            const string Hazards = "Metadata/Monsters/MarakethSanctumTrial/Hazards/";
            Equal(TrialTrap.Spikes, TrialTraps.KindOf(Hazards + "GroundSpike"), "a floor's spikes");
            Equal(TrialTrap.Laser, TrialTraps.KindOf(Hazards + "LaserCrossBeamTurret@22"), "whatever its level");
            Equal(null, TrialTraps.KindOf(Hazards + "GroundSpikeSpawner"), "what spawns them is no trap");
            Equal(null, TrialTraps.KindOf(Hazards + "HourglassLethal"), "the Death Crystal is its own object");
            Equal(null, TrialTraps.KindOf("Metadata/Monsters/Wolves/Wolf"), "a monster");
            Equal(false, TrialTraps.IsSeenBeforeItStrikes(TrialTrap.Spikes), "spikes lie hidden");
            Equal(true, TrialTraps.IsSeenBeforeItStrikes(TrialTrap.Laser), "a turret stands in the open");
            Equal(true, TrialTraps.IsFiring(Hazards + "GroundSpike", new[] { ("rumble", 1L), ("spike", 0L) }), "the ground rumbling warns of them");
            Equal(false, TrialTraps.IsFiring(Hazards + "GroundSpike", new[] { ("rumble", 0L), ("spike", 0L) }), "at rest");
            Equal(false, TrialTraps.IsFiring(Hazards + "LaserCrossBeamTurret", new[] { ("active", 1L), ("fire", 0L) }), "armed is not firing");
            Equal(true, TrialTraps.IsFiring(Hazards + "LaserCrossBeamTurret", new[] { ("fire", 1L) }), "firing");
        }

        private static void SheetWidgetsAreToldByTheirTooltipsKeys()
        {
            Equal("chaos_damage_resistance_%", SheetStats.FromMarkup("<valuedefault>{Chaos Resistance}\nYou take this much less [Chaos] damage.\nMaximum [Resistances|Resistance] is capped at 75%. The number in brackets is the [UncappedResist|Uncapped] value.")?.Stat, "a resistance in English");
            Equal("uncapped_fire_damage_resistance_%", SheetStats.FromMarkup("<valuedefault>{Resistencia al fuego}\nRecibes esta cantidad menos de daño de [Fire|fuego].\n[Resistances|Resistencia] máxima [UncappedResist|sin tope]")?.Uncapped, "the keys stay English in Spanish");
            Equal(BuildNotes.Attribute.Strength, SheetStats.FromMarkup("1 [Strength|Strength] = +2 Life\n5 [Strength|Strength] = +1 Strength [SupportGem|Support Gem] Capacity")?.Attribute, "an attribute");
            Equal("intelligence", SheetStats.FromMarkup("1 [Intelligence] = +2 Mana\n5 [Intelligence|Intelligence] = +1 Intelligence [SupportGem|Support Gem] Capacity")?.Stat, "a key without its shown half");
            Equal(null, SheetStats.FromMarkup("<valuedefault>{Estimated Armour Mitigation}\n[Armour] reduces the [Physical|Physical Damage] you take"), "any other tooltip");
            Equal(null, SheetStats.FromMarkup("+10 to [Strength]\n+20% to [Fire] [Resistances|Resistance]"), "an item's lines are no sheet widget");
            Equal(null, SheetStats.FromMarkup(string.Empty), "no tooltip");
        }

        private static void TrialMapIsColumnsFromEntranceToBoss()
        {
            Equal(true, TrialMap.IsMap(new[] { 1, 3, 4, 3, 5, 3, 6, 1 }), "the floor seen on 2026-11-07");
            Equal(false, TrialMap.IsMap(new[] { 3, 3, 3, 3 }), "a grid of buttons has no entrance");
            Equal(false, TrialMap.IsMap(new[] { 1, 2, 1 }), "too few columns");
            Equal(false, TrialMap.IsMap(new[] { 1, 0, 3, 1 }), "and every column holds rooms");
            Equal(TrialRoomStanding.Open, TrialMap.Standing(1, true, 1), "the room offers to enter");
            Equal(TrialRoomStanding.Here, TrialMap.Standing(0, false, 1), "the column before the first to enter is the player's");
            Equal(TrialRoomStanding.Behind, TrialMap.Standing(1, false, 3), "before it, left behind");
            Equal(TrialRoomStanding.Ahead, TrialMap.Standing(2, false, 1), "after it, not reachable");
            Equal(TrialRoomStanding.Ahead, TrialMap.Standing(1, false, 1), "nor is a room beside the open ones that does not offer");
            Equal(TrialRoomStanding.Ahead, TrialMap.Standing(0, false, -1), "with nothing open nothing can be told");
        }

        private static void TheGuideGoesFromCrystalToCrystal()
        {
            var guide = new TimedGuide();
            Equal((null, false), guide.Look(Array.Empty<uint>(), null), "no crystal, nothing to do");
            Equal((7u, false), guide.Look(new uint[] { 7, 8, 9 }, null), "the nearest first");
            Equal((null, false), guide.Look(new uint[] { 8, 7, 9 }, 7), "while it stands the guide stays, whichever is now nearer");
            Equal((null, false), guide.Look(new uint[] { 7, 8, 9 }, null), "arrived or released, not again until it is disabled");
            Equal((8u, false), guide.Look(new uint[] { 8, 9 }, null), "disabled: the next");
            Equal((9u, false), guide.Look(new uint[] { 9 }, 8), "and straight on from the one gone");
            Equal((null, false), guide.Look(Array.Empty<uint>(), 9), "the room done: a page turned hides the objective too, so not at once");
            Equal((null, false), guide.Look(Array.Empty<uint>(), 9), "nor on the second look");
            Equal((null, true), guide.Look(Array.Empty<uint>(), 9), "then the guide lets go of the last crystal");
            Equal((null, false), guide.Look(Array.Empty<uint>(), null), "once");
            var paged = new TimedGuide();
            paged.Look(new uint[] { 7 }, null);
            paged.Look(Array.Empty<uint>(), 7);
            Equal((null, false), paged.Look(new uint[] { 7 }, 7), "the objective back in a moment: the guide stays");
            paged.Look(Array.Empty<uint>(), 7);
            paged.Look(Array.Empty<uint>(), 7);
            Equal((null, true), paged.Look(Array.Empty<uint>(), 7), "and the looks count from there");
            var walked = new TimedGuide();
            Equal((7u, false), walked.Look(new uint[] { 7, 8 }, null), "the nearest in a straight line");
            walked.Aim(8);
            Equal((null, false), walked.Look(new uint[] { 7, 8 }, 8), "the one chosen for its route is the one that stands");
            Equal((7u, false), walked.Look(new uint[] { 7 }, 8), "until it is disabled");
            var other = new TimedGuide();
            Equal((null, false), other.Look(new uint[] { 7 }, 42), "the player's own target is left alone");
            Equal((7u, false), other.Look(new uint[] { 7 }, null), "until released");
            other.Look(Array.Empty<uint>(), 42);
            other.Look(Array.Empty<uint>(), 42);
            Equal((null, false), other.Look(Array.Empty<uint>(), 42), "and it is not let go when the room ends");
            var next = new TimedGuide();
            next.Look(new uint[] { 7 }, null);
            next.Reset();
            Equal((7u, false), next.Look(new uint[] { 7 }, null), "in the next room the same id is another crystal: it is aimed at anew");
        }

        private static void HonourIsSaidAsItFalls()
        {
            var watch = new HonourWatch();
            Equal(null, watch.Heard("Honor: 474/513"), "the first reading is no fall");
            Equal(null, watch.Heard("Honor: 470/513"), "91%: no mark passed");
            Equal(77, watch.Heard("Honor: 400/513"), "past 90 and 80, said once");
            Equal(null, watch.Heard("Honor: 370/513"), "not again above the next");
            Equal(24, watch.Heard("Honor: 125/513"), "marks passed at one blow say the amount once");
            Equal(null, watch.Heard("Honor: 400/513"), "a fountain raises it");
            Equal(49, watch.Heard("Honor: 255/513"), "and the marks count again");
            Equal(null, watch.Heard("Honor"), "a line with no amount says nothing");
            Equal(9, watch.Heard("Honor: 50/513"), "and leaves the last reading");
            Equal(0, watch.Heard("Honor: 0/513"), "running out is said");
            Equal(null, watch.Heard("Honor: 0/513"), "once, and the hub's stale line after the failed run stays silent");
        }

        private static void TakingTheFloorCutsAKeptLine()
        {
            var now = new DateTime(2026, 11, 7, 10, 0, 0, DateTimeKind.Utc);
            var floor = new SpeechFloor();
            floor.Keep("Misión: una página larga", false, 5000, now);
            Equal(true, floor.Held(now.AddSeconds(1)), "a kept line holds");
            Equal(false, floor.Admit("Oro recogido", false, now.AddSeconds(1)), "and what comes waits");
            floor.Take("Misión: la página siguiente", now.AddSeconds(2));
            Equal(false, floor.Held(now.AddSeconds(2)), "the player's turn takes the floor");
            Equal(0, floor.Release(now.AddSeconds(2)).Count, "and what waited is stale");
        }

        private static void QuestPageWaitsForTheMenuToClose()
        {
            var page = new QuestTrackerPage();
            page.News(new[] { "Noticias graves. Habla con Leitis" });
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Encuentra al Conde Geonor" }, inMenu: true), "accepted at the NPC: not over its options");
            Equal(true, page.Waiting, "it waits");
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }, inMenu: true), "changed again in the same conversation");
            Equal("El lobo rabioso de Ogham. Entra en la mansión", page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }), "the menu closed: the latest page, whole");
            Equal(false, page.Waiting, "said");
            Equal(string.Empty, page.News(new[] { "El lobo rabioso de Ogham. Entra en la mansión" }, inMenu: true), "the same page in a menu is no news");
            Equal(false, page.Waiting, "and nothing waits");
        }

        private static void WorldIsBackOnlyOnceNothingCoversIt()
        {
            var gate = new WorldGate();
            Equal(true, gate.InFront(false, 1000), "playing from the start");
            Equal(false, gate.InFront(true, 2000), "a panel opened: quiet at once");
            Equal(false, gate.InFront(false, 2100), "between two panels, before the snapshots show the next one");
            Equal(false, gate.InFront(true, 2200), "the confirmation box shows");
            Equal(false, gate.InFront(false, 2200 + WorldGate.ReturnMs - 1), "the box answered, a moment later");
            Equal(true, gate.InFront(false, 2200 + WorldGate.ReturnMs), "the world again");
        }

        private static void PanelOverTheGameIsTheTopLayerThatIsNotTheHud()
        {
            IntPtr hud = 12, tutorial = 30, panel = 57;
            ScreenLayer Layer(IntPtr address, bool controls = true, bool hints = false, bool isBase = false) =>
                new(address, (int)address, Array.Empty<ScreenText>(), controls, hints, isBase);
            ScreenLayer? Over(params ScreenLayer[] layers) => new ScreenSnapshot(1, layers).PanelOver(hud, tutorial);
            Equal(null, Over(Layer(hud))?.Address, "playing: the HUD on top");
            Equal(null, Over(Layer(hud, isBase: true))?.Address, "playing: the HUD taken as a base layer");
            Equal(null, Over(Layer(hud), Layer(tutorial))?.Address, "the tutorial box covers nothing");
            Equal(null, Over(Layer(hud), Layer(panel, controls: false))?.Address, "a box to read without controls (radial menu) is not a panel");
            Equal(null, Over(Layer(hud), Layer(panel, hints: true))?.Address, "nor a hint bar");
            Equal(panel, Over(Layer(hud), Layer(panel))?.Address, "a panel, focus or not");
            Equal(null, new ScreenSnapshot(1, new[] { Layer(panel) }).PanelOver(IntPtr.Zero, IntPtr.Zero)?.Address, "HUD not found: never silence the game on a guess");
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

        private static void TownsAndHideoutsAreToldByTheirRow()
        {
            Equal(true, AreaKind.IsTownOrHideout("G1_town", true), "the encampment: its row's town flag");
            Equal(false, AreaKind.IsTownOrHideout("G1_2", false), "Clearfell");
            Equal(true, AreaKind.IsTownOrHideout("HideoutFelled", false), "a hideout by its Id");
            Equal(false, AreaKind.IsTownOrHideout("MapHideoutFelled", false), "a hideout's map is a map");
        }

        private static void ALayoutLostTooEarlyIsJudgedAgain()
        {
            // 13:27:15 cold start: the PreGameState root had no children yet; the login's root a second later read every field.
            Equal(true, LayoutVerdict.Settled(false, 0, 0), "found at once: kept");
            Equal(false, LayoutVerdict.Settled(true, 1000, 1000), "lost on the first root: judged again on the next");
            Equal(true, LayoutVerdict.Settled(false, 1000, 2000), "found on the login's root: kept, no warning");
            Equal(false, LayoutVerdict.Settled(true, 1000, 1000 + LayoutVerdict.LostWaitMs - 1), "still waiting");
            Equal(true, LayoutVerdict.Settled(true, 1000, 1000 + LayoutVerdict.LostWaitMs), "lost for good: reported");
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

        private static void TheHoveredPinIsTheKeyThatChangesWithTheCursor()
        {
            var log = new PluginLog(System.IO.Path.Join(System.IO.Path.GetTempPath(), "poe2access-tests"), false);

            // Keys into MapPins on the canvas, by shift from where the hovered one was measured: the current area's 0x20 before it.
            static Func<int, long> Canvas(int hoveredAt, long hoveredRow) => shift => shift == hoveredAt ? hoveredRow : shift == hoveredAt - 0x20 ? 0x5000 : 0;
            var measured = new ControlFields(new CompatibilityReport(log), () => 0);
            Equal(0, measured.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0, 0x6000)), "read where measured until judged");
            Equal(0, measured.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0, 0x6100)), "the cursor moves to another pin");
            Equal(true, measured.IsKnown(ControlGroup.HoveredPin), "proved where measured on the first change");

            var moved = new ControlFields(new CompatibilityReport(log), () => 0);
            var rows = new long[] { 0x6000, 0x6100, 0, 0x6200, 0x6300 };
            var shifts = rows.Select(row => moved.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(8, row))).ToList();
            Equal("0,0,0,0,8", string.Join(",", shifts), "after a patch, the key that changed twice from pin to pin (to and from no pin does not count)");
            Equal(8, moved.Current(ControlGroup.HoveredPin), "kept");

            var still = new ControlFields(new CompatibilityReport(log), () => 0);
            foreach (var row in rows)
            {
                still.ChangingAt(ControlGroup.HoveredPin, 1, Canvas(0x40, 0x6000));
            }

            Equal(false, still.IsKnown(ControlGroup.HoveredPin), "a cursor resting on one pin proves nothing, nor the current area's key");
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
            Equal(DodgeThreat.None, DodgeThreats.Of(99, NameOf), "an id the table lacks (a game update) stays quiet and is logged");
            Equal(DodgeThreat.None, DodgeThreats.Of(0, NameOf), "no animation");
        }

        private static void BuffIdsNameAilments()
        {
            Equal(StatusAilment.Chilled, StatusAilments.FromBuffId("chilled") ?? StatusAilment.Cursed, "chill");
            Equal(StatusAilment.StopMoving, StatusAilments.FromBuffId("voodoo_king_punishment_moving") ?? StatusAilment.Cursed, "punished for moving: stand still");
            Equal(StatusAilment.KeepMoving, StatusAilments.FromBuffId("voodoo_king_punishment_stationary") ?? StatusAilment.Cursed, "punished for standing: keep moving");
            Equal(StatusAilment.Frozen, StatusAilments.FromBuffId("frozen") ?? StatusAilment.Cursed, "freeze");
            Equal(StatusAilment.Poisoned, StatusAilments.FromBuffId("poisoned") ?? StatusAilment.Cursed, "poison");
            Equal(StatusAilment.Electrocuted, StatusAilments.FromBuffId("electrocuted") ?? StatusAilment.Cursed, "electrocution before shock");
            Equal(true, StatusAilments.FromBuffId("chill_immunity") == null, "immunities are not ailments");
            Equal(true, StatusAilments.FromBuffId("flask_effect_life") == null, "flasks are not ailments");
            Equal(true, StatusAilments.FromBuffId("flammability_ignite_chance_buildup_79") == null, "build-up is not the ailment yet");
            Equal(StatusAilment.Overheated, StatusAilments.FromBuffId("overheated_79") ?? StatusAilment.Cursed, "the crossbow at maximum heat");
            Equal(true, StatusAilments.FromBuffId("heat_79") == null, "heat still building is not overheated");
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

        private static void CodeFieldsNeedTwoSignaturesThatAgree()
        {
            var one = GameReader.Utils.CodeFieldSearch.Parse("field", "AA BB ^ ?? ?? ?? ?? CC", 4);
            var two = GameReader.Utils.CodeFieldSearch.Parse("field", "DD EE ^ ?? CC", 1);
            var absent = GameReader.Utils.CodeFieldSearch.Parse("field", "77 88 ^ ?? 99", 1);
            byte[] Code(byte first, byte second) => new byte[] { 0xAA, 0xBB, first, 0, 0, 0, 0xCC, 0x11, 0xDD, 0xEE, second, 0xCC };
            Equal(0x40, GameReader.Utils.CodeFieldSearch.Agreed(Code(0x40, 0x40), new[] { one, two }), "two agree");
            Equal(0x40, GameReader.Utils.CodeFieldSearch.Agreed(Code(0x40, 0x40), new[] { one, two, absent }), "a miss does not stop two that agree");
            Equal(null, GameReader.Utils.CodeFieldSearch.Agreed(Code(0x40, 0x48), new[] { one, two }), "two that differ say nothing");
            Equal(null, GameReader.Utils.CodeFieldSearch.Agreed(Code(0x40, 0x40), new[] { one, absent }), "one alone says nothing");
            var doubled = Code(0x40, 0x40).Concat(Code(0x48, 0x40)).ToArray();
            Equal(null, GameReader.Utils.CodeFieldSearch.Agreed(doubled, new[] { one, two }), "a second hit voids a signature");

            var biased = new CodeFieldEntry("Render.Bounds.Y", "RenderOffsets:CharactorModelBounds:4", 0x148, new[] { one, two });
            Equal(0x3C, CodeFieldCatalog.Read(Code(0x40, 0x40), new[] { biased })[0].Found, "the bias comes off");
            Equal("CharactorModelBounds", biased.Field, "the field");
            var far = new CodeFieldEntry("Far", "report", 0x4000, new[] { one, two });
            Equal(null, CodeFieldCatalog.Read(Code(0x40, 0x40), new[] { far })[0].Found, "too far off to be the field");
            Equal("RenderOffsets", biased.Struct, "the struct");
        }

        private static void TheStunRecordIsFoundByItsHead()
        {
            byte[] Actor(int head, int threshold)
            {
                var block = new byte[0x100];
                BitConverter.TryWriteBytes(block.AsSpan(head), 5);
                BitConverter.TryWriteBytes(block.AsSpan(head + 4), 8f);
                BitConverter.TryWriteBytes(block.AsSpan(head + PoiseSearch.ThresholdFromHead), threshold);
                return block;
            }

            Equal(0x40, PoiseSearch.Heads(Actor(0x40, 60)).Single(), "the head with a threshold");
            Equal(0, PoiseSearch.Heads(Actor(0x40, 0)).Count, "no threshold, no record");
            var five = Enumerable.Repeat(PoiseSearch.Heads(Actor(0x48, 24)), 5).ToList();
            Equal(0x48, PoiseSearch.Agreed(five, 0x40), "five actors agree on a moved record");
            Equal(null, PoiseSearch.Agreed(five.Take(4).ToList(), 0x40), "four are too few");
            var split = five.Take(3).Concat(Enumerable.Repeat(PoiseSearch.Heads(Actor(0x60, 24)), 2)).ToList();
            Equal(null, PoiseSearch.Agreed(split, 0x40), "three of five do not agree");
            IReadOnlyList<int> both = new[] { 0x48, 0x64 };
            Equal(0x48, PoiseSearch.Agreed(Enumerable.Repeat(both, 5).ToList(), 0x40), "of two shared places, the nearest");
        }

        private static void TheGridMustHoldWhereThePlayerWalks()
        {
            var check = new WalkableCheck();
            bool? verdict = null;
            for (var x = 0; x < 30; x++)
            {
                verdict = check.Add(x, 0, x % 10 != 0);
            }

            Equal(true, verdict, "a few cells at a wall's edge do not count against it");
            Equal(null, check.Add(1, 1, true), "the count starts again");
            Equal(null, check.Add(1, 1, false), "the same cell twice is one cell");
            check = new WalkableCheck();
            for (var x = 0; x < 30; x++)
            {
                verdict = check.Add(x, 0, x % 2 == 0);
            }

            Equal(false, verdict, "half the path closed: the grid no longer means that");
        }

        private static void CodeFieldCatalogParses()
        {
            var lines = new[]
            {
                "# a comment",
                "Player.Level\tPlayerOffsets:Level\t0x204\t4\tAA ^ ?? ?? ?? ?? BB\tCC ^ ?? ?? ?? ?? DD",
                "Targetable.IsTargeted\tlayout:IsTargeted\t0x6b\t1\tAA ^ ?? BB\tCC ^ ?? DD",
                "Shrine.IsUsed\treport\t0x24\t1\tAA ^ ?? BB\tCC ^ ?? DD",
                "broken\tPlayerOffsets:Level\t204\t4\tAA ^ ?? BB",
                "short\treport",
            };
            var entries = CodeFieldCatalog.Parse(lines);
            Equal(3, entries.Count, "comments and broken lines are left out");
            Equal(0x204, entries[0].Value, "the value");
            Equal(2, entries[0].Signatures.Count, "the signatures");
            Equal("IsTargeted", entries[1].Layout, "a plugin offset");
            Equal(null, entries[1].Struct, "not a host struct");
            Equal(true, entries[2].IsReport, "only told");

            var directory = AppContext.BaseDirectory;
            while (!System.IO.File.Exists(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "code-fields.txt")))
            {
                directory = System.IO.Path.GetDirectoryName(directory) ?? throw new System.IO.FileNotFoundException("code-fields.txt is not under any folder above the tests");
            }

            var shipped = CodeFieldCatalog.Parse(System.IO.File.ReadLines(System.IO.Path.Join(directory, "Poe2Accessibility", "catalog", "code-fields.txt")));
            Equal(52, shipped.Count, "every shipped line parses");
            Equal(true, shipped.All(entry => entry.Signatures.Count >= 2), "two can agree on each");
        }

        private static void CodeFieldsSettleEachFieldApart()
        {
            var none = Array.Empty<GameReader.Utils.CodeField>();
            var code = new (CodeFieldEntry, int?)[]
            {
                (new CodeFieldEntry("Mods.Ptr", "report", 0x28, none), 0x28),
                (new CodeFieldEntry("Mods.Size", "report", 0x40, none), 0x40),
                (new CodeFieldEntry("Area.Player", "AreaInstanceOffsets:PlayerInfo:8", 0x5C0, none), 0x5B8),
            };
            var semantic = new (CodeFieldEntry, int?)[]
            {
                (new CodeFieldEntry("Area.Player", "AreaInstanceOffsets:PlayerInfo", 0x5B8, none), 0x5B8),
            };
            var (settled, disagree) = CodeFieldCatalog.Settle(code, semantic);
            Equal(0, disagree.Count, "two reports are two fields, and a bias does not make a second field");
            Equal(3, settled.Count, "each field once");
            Equal(0x40, settled.Single(field => field.Entry.Name == "Mods.Size").Found, "a report keeps its own value");

            var moved = new (CodeFieldEntry, int?)[] { (new CodeFieldEntry("Area.Player", "AreaInstanceOffsets:PlayerInfo", 0x5B8, none), 0x5C0) };
            Equal(1, CodeFieldCatalog.Settle(code, moved).Disagree.Count, "the code and the rules reading one field apart");
            Equal(0, CodeFieldCatalog.Unsettled(settled).Count, "every field read: nothing stands unchecked");
            Equal("Area.Player", string.Join(",", CodeFieldCatalog.Unsettled(CodeFieldCatalog.Settle(code, moved).Settled)), "two readings apart settle nothing");

            // One source misses and the other reads: settled. Both miss: the constant stands unchecked.
            var missed = new (CodeFieldEntry, int?)[] { (new CodeFieldEntry("Area.Player", "AreaInstanceOffsets:PlayerInfo", 0x5B8, none), null) };
            Equal(0, CodeFieldCatalog.Unsettled(CodeFieldCatalog.Settle(code, missed).Settled).Count, "the signatures read what the rule missed");
            var blind = new (CodeFieldEntry, int?)[] { (new CodeFieldEntry("Chest.Opened", "ChestOffsets:IsOpened", 0x168, none), null) };
            Equal("Chest.Opened", string.Join(",", CodeFieldCatalog.Unsettled(CodeFieldCatalog.Settle(blind, missed).Settled.Where(field => field.Entry.Name != "Area.Player"))), "read by nothing");
        }

        private static void LayoutOverridesCountOnlyForTheirBuild()
        {
            var lines = new[]
            {
                "# fixes by hand",
                "build 100",
                "AreaInstanceOffsets:PlayerInfo 0x5B8 0x5C0",
                "build 200",
                "AreaInstanceOffsets:PlayerInfo 0x5B8 0x5D0  # this build",
                "layout:MapOverlayOffset 0xB90 0xB98",
                "report 0x24 0x28",
                "PlayerOffsets:Level 0x204",
                "PlayerOffsets:Level:4 0x204 0x20C",
            };
            var fields = LayoutOverrides.Parse(lines, 200, out var otherBuilds, out var rejected);
            Equal(2, fields.Count, "only this build's lines");
            Equal(1, otherBuilds, "the other build's line is counted, not used");
            Equal(3, rejected.Count, "a report, a missing value and a bias are refused");
            Equal(0x5D0, fields[0].Found, "where this build has it");
            Equal(0x5B8, fields[0].Entry.Value, "where the mod has it");
            Equal("AreaInstanceOffsets", fields[0].Entry.Struct, "a host struct");
            Equal("MapOverlayOffset", fields[1].Entry.Layout, "a plugin offset");
            Equal(0, LayoutOverrides.Parse(lines, 300, out _, out _).Count, "a new build inherits nothing");

            var code = new (CodeFieldEntry, int?)[]
            {
                (new CodeFieldEntry("Area.Player", "AreaInstanceOffsets:PlayerInfo:8", 0x5C0, Array.Empty<GameReader.Utils.CodeField>()), 0x5B8),
                (new CodeFieldEntry("Area.Level", "AreaInstanceOffsets:CurrentAreaLevel", 0xC4, Array.Empty<GameReader.Utils.CodeField>()), 0xC4),
            };
            var merged = LayoutOverrides.Apply(code, fields);
            Equal(3, merged.Count, "the overridden reading is replaced, the others stay");
            Equal(0x5D0, merged.Single(field => field.Entry.Field == "PlayerInfo").Found, "the hand wins over the code, bias or not");
        }

        private static void ObjectsPlayTheSoundTheGameGivesThem()
        {
            Breakables.UseObjectSounds(new[] { "Metadata/Chests/Badlands/Effigy_01\tEffigies_Open_01", "Metadata/Chests/G2_1/SnakeLadyPotionChest\tCorpses_Open", "not a line" });
            Equal("Effigies_Open_01", Breakables.Material("Metadata/Chests/Badlands/Effigy_01"), "an object no word names");
            Equal("Effigies_Open_01", Breakables.Material("Metadata/Chests/Badlands/Effigy_01@73"), "an instance's level is not part of its name");
            Equal("Corpses_Open", Breakables.Material("Metadata/Chests/G2_1/SnakeLadyPotionChest"), "a potion is no pot");
            Equal("urn", Breakables.Material("Metadata/Chests/Urns/Urn1"), "a word keeps its tuned sound");
            Equal("", Breakables.Material("Metadata/Chests/Gold/GoldPileSmall"), "the game gives it none: the common sound");
            Breakables.UseObjectSounds(Array.Empty<string>());
        }

        private static void OpeningSoundsComeFromTheOpeningAnimation()
        {
            const string Lid = "Audio/Sound Effects/Chests/Keth/Lid";
            const string Sounds = "SoundEvents\n{\n animations = '[{\"name\": \"idle\", \"events\": [{\"time\": 0.0, \"filename\": \"Audio/Sound Effects/Hum\"}]},"
                + "{\"name\": \"open\", \"events\": [{\"time\": 0.6, \"filename\": \"Audio/Sound Effects/Chests/Keth/Impact\"}, {\"time\": 0.0, \"filename\": \"" + Lid + "\"}]}]'\n}\n";
            const string Particles = "ParticleEffects\n{\n animations = '[{\"name\": \"open\", \"events\": [{\"time\": 0.0, \"filename\": \"Metadata/fx/dust.pet\"}]}]'\n}\n";
            Equal(Lid, AoSoundEvents.OpeningEvent(Particles + Sounds), "the earliest sound of the open animation, past other blocks' lists");
            Equal(null, AoSoundEvents.OpeningEvent("SoundEvents\n{\n}\n" + Particles), "an empty block does not borrow the next block's list");
            Equal(null, AoSoundEvents.OpeningEvent("SkinMesh\n{\n}\n"), "no sounds");
            Equal(null, AoSoundEvents.OpeningEvent("SoundEvents\n{\n animations = '[{broken]'\n}\n"), "a broken list");

            var files = new Dictionary<string, string> { ["Metadata/Child.ao"] = "version 3\nextends \"Metadata/Base\"\n", ["Metadata/Base.ao"] = Sounds };
            Equal(Lid, AoSoundEvents.OpeningEventOf("Metadata/Child.ao", path => files.GetValueOrDefault(path)), "a parent's sounds");
            Equal(null, AoSoundEvents.OpeningEventOf("Metadata/None.ao", path => files.GetValueOrDefault(path)), "a missing file");

            Equal("event:/Sound Effects/Chests/Keth/LidSG", AoSoundEvents.EventPaths(Lid)[0], "the event name");
            Equal("event:/Sound Effects/Chests/Spawn.ogg", AoSoundEvents.EventPaths("Audio/Sound Effects/Chests/Spawn.ogg%70")[1], "an older name, its volume dropped");
            Equal("Chests_4_0_G2_5_1_Effigies_Open_01", AoSoundEvents.FileName("Audio/Sound Effects/Chests/4.0/G2_5_1/Effigies/Open_01"), "a file name");
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

        private static void ATooltipLineSwappedInPlaceIsSaid()
        {
            var bear = new[] { "Talismán", "Forma de oso", "Equipado: Set de armas" };
            var wolf = new[] { "Talismán", "Forma de hombre lobo", "Equipado: Set de armas" };
            const string focused = "Talismán cambiaformas, Talismán. Forma de oso. Equipado: Set de armas";
            string New(IReadOnlyList<string> said, IReadOnlyList<string> tooltip, string speech) =>
                string.Join(" | ", FocusDetails.NewTooltipLines(said, tooltip, speech));

            Equal("Forma de hombre lobo", New(bear, wolf, focused), "the form changed");
            Equal("Forma de oso", New(wolf, bear, focused), "and changed back, though the focus said it");
            Equal(string.Empty, New(bear, bear, focused), "nothing changed");
            Equal(string.Empty, New(Array.Empty<string>(), bear, focused), "built after the focus: all of it was said");
            Equal("Daño", New(new[] { "Talismán" }, new[] { "Talismán", "Daño" }, "Talismán"), "still building: only what the focus did not hold");
        }

        private static void OnlyASwappedLineCutsIn()
        {
            var bear = new[] { "Talismán", "Forma de oso" };
            Equal(true, FocusDetails.LineSwapped(bear, new[] { "Talismán", "Forma de hombre lobo" }), "another form");
            Equal(false, FocusDetails.LineSwapped(new[] { "Frasco", "Actualmente tiene 6 cargas" }, new[] { "Frasco", "Actualmente tiene 7 cargas" }), "a number ticking");
            Equal(false, FocusDetails.LineSwapped(new[] { "Talismán" }, bear), "still being built");
            Equal(false, FocusDetails.LineSwapped(bear, bear), "nothing changed");
        }

        private static void AGameNoticeLetsTheCursorThroughAndIsNotSaidTwice()
        {
            const string notice = "malgdhler ha recibido +10% a la resistencia al hielo.";
            const string copy = "+10% a la resistencia al hielo";
            var floor = new SpeechFloor();
            var start = new DateTime(2026, 11, 6, 23, 5, 4, DateTimeKind.Utc);
            floor.Sent(new string('x', 200), true, start); // an item's long text, 9 s
            var hold = SpeechFloor.MessageHoldMs(notice);
            Equal(true, floor.Keep(notice, true, hold, start.AddMilliseconds(400), yieldsToMoves: true), "the notice cuts the long text");
            Equal(true, floor.Echoes(copy, start.AddMilliseconds(600)), "the copy on screen is no news");
            Equal(false, floor.Echoes("Inventario lleno", start.AddMilliseconds(600)), "another message is");
            var moved = true;
            Equal(true, floor.Move("Grebas rústicas", ref moved, start.AddMilliseconds(900)), "the cursor goes out at once");
            Equal(false, moved, "behind the notice, not over it");
            Equal(true, floor.Echoes(copy, start.AddMilliseconds(1000)), "a move does not bring the copy back");
            Equal(false, floor.Echoes(copy, start.AddMilliseconds(400 + hold + 1)), "said again once it is over");
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
