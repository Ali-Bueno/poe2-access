namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Numerics;
    using Poe2Accessibility.Services;

    /// <summary>The dodge warning's timing, aim, reach, sight and field checks (docs/research/dodge-timing.md).</summary>
    public static class DodgeGateTests
    {
        private const uint PlayerId = 6538;
        private const int LeadMs = 500;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheClockFollowsTheSpeedTheClipPlaysAt), TheClockFollowsTheSpeedTheClipPlaysAt),
            (nameof(TheGlobalSpeedCountsUnlessItsFlagSaysOne), TheGlobalSpeedCountsUnlessItsFlagSaysOne),
            (nameof(OnlyABlowAheadOfThePositionIsNext), OnlyABlowAheadOfThePositionIsNext),
            (nameof(AContactCoveredByTheLastWarningPassesTheTurnToTheNext), AContactCoveredByTheLastWarningPassesTheTurnToTheNext),
            (nameof(TheFilesNominalHitIsTimedFromWhereTheClipIs), TheFilesNominalHitIsTimedFromWhereTheClipIs),
            (nameof(EventAndActionIdsAreTheGamesHashes), EventAndActionIdsAreTheGamesHashes),
            (nameof(AnImplausibleReadingIsNoClock), AnImplausibleReadingIsNoClock),
            (nameof(ARestartedClipIsANewSwing), ARestartedClipIsANewSwing),
            (nameof(AnAttackOnThePlayerIsAimedAtThePlayer), AnAttackOnThePlayerIsAimedAtThePlayer),
            (nameof(AnAttackOnAMinionAwayFromThePlayerIsAimedElsewhere), AnAttackOnAMinionAwayFromThePlayerIsAimedElsewhere),
            (nameof(AGroundTargetCatchesThePlayerOnlyWithinItsArea), AGroundTargetCatchesThePlayerOnlyWithinItsArea),
            (nameof(AProjectilePathCatchesThePlayerAheadOfTheShooter), AProjectilePathCatchesThePlayerAheadOfTheShooter),
            (nameof(NoTargetReadIsUnknownAim), NoTargetReadIsUnknownAim),
            (nameof(APatternReachesItsOffsetAndItsLength), APatternReachesItsOffsetAndItsLength),
            (nameof(AnUnbelievablePatternReachesNothing), AnUnbelievablePatternReachesNothing),
            (nameof(ABlowReachesWithinItsReachPlusTheBody), ABlowReachesWithinItsReachPlusTheBody),
            (nameof(AnAttackerPartlyOnScreenIsSeen), AnAttackerPartlyOnScreenIsSeen),
            (nameof(AProjectileFromOffScreenIsSeenWhereItEntersTheView), AProjectileFromOffScreenIsSeenWhereItEntersTheView),
            (nameof(ACalmOrMisaimedAttackIsSilent), ACalmOrMisaimedAttackIsSilent),
            (nameof(ATimedAttackWarnsALeadBeforeItsBlow), ATimedAttackWarnsALeadBeforeItsBlow),
            (nameof(AnUnknownAimKeepsTheOldReachRule), AnUnknownAimKeepsTheOldReachRule),
            (nameof(AMeleeBlowAimedAtThePlayerStillNeedsReach), AMeleeBlowAimedAtThePlayerStillNeedsReach),
            (nameof(AnAttackerOffScreenWarnsOnlyOnceItsProjectileShows), AnAttackerOffScreenWarnsOnlyOnceItsProjectileShows),
            (nameof(AClockThatAdvancesAtItsRateIsIntact), AClockThatAdvancesAtItsRateIsIntact),
            (nameof(AClockRunningOffItsRateIsLost), AClockRunningOffItsRateIsLost),
            (nameof(AClockTheGameStepsSlowerThanTheHostReadsIsIntact), AClockTheGameStepsSlowerThanTheHostReadsIsIntact),
            (nameof(ARestartAPauseOrNoClipSaysNothing), ARestartAPauseOrNoClipSaysNothing),
            (nameof(EventsAndActionsCheckThemselves), EventsAndActionsCheckThemselves),
            (nameof(ASweepThatReachesThePlayerWarnsWhateverItAimsAt), ASweepThatReachesThePlayerWarnsWhateverItAimsAt),
            (nameof(AGroundTelegraphDecidesSightReachAndTime), AGroundTelegraphDecidesSightReachAndTime),
            (nameof(ASkillAreaIsReadFromItsCatalogLine), ASkillAreaIsReadFromItsCatalogLine),
            (nameof(AnAreaCatchesThePlayerOnlyInsideItsShape), AnAreaCatchesThePlayerOnlyInsideItsShape),
            (nameof(TheAttackCatalogsGiveReachAreasAndActionNames), TheAttackCatalogsGiveReachAreasAndActionNames),
            (nameof(TheTablesAreaSizesAreGridCells), TheTablesAreaSizesAreGridCells),
            (nameof(ALearntBlowFarFromTheFilesIsNotBelieved), ALearntBlowFarFromTheFilesIsNotBelieved),
        };

        private static AnimationReading Clip(float position, float speed = 1f, float global = 1f, bool off = false, float end = 2f, int clip = 7) =>
            new(clip, position, speed, global, off, 0f, end);

        private static void TheClockFollowsTheSpeedTheClipPlaysAt()
        {
            var events = new[] { new ClipEvent(AttackClock.Contact, 1.0f) };
            Equal(500, AttackClock.MsToNext(Clip(0.5f), events, AttackClock.Contact), "speed 1");
            Equal(400, AttackClock.MsToNext(Clip(0.5f, speed: 1.25f), events, AttackClock.Contact), "hasted a quarter");
            Equal(1000, AttackClock.MsToNext(Clip(0.5f, speed: 0.5f), events, AttackClock.Contact), "slowed by half");
        }

        private static void TheGlobalSpeedCountsUnlessItsFlagSaysOne()
        {
            Near(0.5f, AttackClock.Rate(Clip(0f, global: 0.5f)), "global half");
            Near(1.5f, AttackClock.Rate(Clip(0f, speed: 1.5f, global: 0.5f, off: true)), "flag: global taken as 1");
            Near(1f, AttackClock.Rate(Clip(0f, speed: 0f)), "a stopped rate counts as 1, as the game does");
        }

        private static void OnlyABlowAheadOfThePositionIsNext()
        {
            var events = new[] { new ClipEvent(AttackClock.Contact, 0.9f), new ClipEvent("footstep", 0.6f), new ClipEvent(AttackClock.Contact, 0.3f) };
            Equal(400, AttackClock.MsToNext(Clip(0.5f), events, AttackClock.Contact), "the second contact");
            Equal(null, AttackClock.MsToNext(Clip(0.95f), events, AttackClock.Contact), "none left in this swing");
        }

        private static void AContactCoveredByTheLastWarningPassesTheTurnToTheNext()
        {
            // Beira's ice_spear_combo_01 (Clearfell, 2026-11-08): five spears; the 0.0 contact of a cast is no blow.
            var events = new[]
            {
                new ClipEvent(AttackClock.Contact, 0.0f), new ClipEvent(AttackClock.Contact, 0.5018f), new ClipEvent("face", 0.7224f),
                new ClipEvent(AttackClock.Contact, 0.95f), new ClipEvent(AttackClock.Contact, 1.4f), new ClipEvent(AttackClock.Contact, 1.7037f),
                new ClipEvent(AttackClock.Contact, 2.1712f),
            };
            Near(0.5018f, AttackClock.NextBlow(0f, events, null), "the first spear, not the cast mark");
            Near(0.95f, AttackClock.NextBlow(0.51f, events, null), "the second spear once the first passed");
            Near(1.4f, AttackClock.NextBlow(0.51f, events, 0.95f), "the second covered: the third is next");
            Near(2.1712f, AttackClock.NextBlow(1.41f, events, 1.7037f), "the fourth covered: the fifth");
            Equal(null, AttackClock.NextBlow(2.2f, events, null), "none left");
            Equal(null, AttackClock.NextBlow(1.8f, events, 2.1712f), "the last one covered: none left to warn");
        }

        private static void TheFilesNominalHitIsTimedFromWhereTheClipIs()
        {
            Equal(300, AttackClock.MsToNominal(Clip(0.2f, speed: 2f), 800), "0.6 s of clip at double speed");
            Equal(null, AttackClock.MsToNominal(Clip(0.9f), 800), "already behind");
        }

        private static void EventAndActionIdsAreTheGamesHashes()
        {
            // Read live 2026-11-08: the player's aim_crossbow events, a player's actions, and ActionTypes.dat +0x08.
            Equal((ushort)0x858E, AttackClock.EventId("DownContact"), "DownContact");
            Equal((ushort)0xD10A, AttackClock.EventId("UpContact"), "UpContact");
            Equal(AttackClock.EventId(AttackClock.Contact), AttackClock.EventId("contact#2"), "a '#' suffix is not hashed");
            Equal((ushort)0xED4C, AttackClock.ActionId("DodgeRoll"), "DodgeRoll");
            Equal((ushort)0xC266, AttackClock.ActionId("Interaction"), "Interaction");
            Equal((ushort)(85057 & 0xFFFF), AttackClock.ActionId("GroundSlam"), "GroundSlam, ActionTypes row 0");
        }

        private static void AnImplausibleReadingIsNoClock()
        {
            Equal(true, AttackClock.IsPlausible(Clip(1.2f)), "a playing clip");
            Equal(false, AttackClock.IsPlausible(Clip(float.NaN)), "not a number");
            Equal(false, AttackClock.IsPlausible(Clip(3f)), "past the clip's end");
            Equal(false, AttackClock.IsPlausible(Clip(0.5f, clip: -1)), "no clip");
            Equal(false, AttackClock.IsPlausible(Clip(0.5f, speed: 50f)), "a rate no monster plays at");
            Equal(false, AttackClock.IsPlausible(Clip(0.5f, end: 0f)), "an empty clip");
        }

        private static void ARestartedClipIsANewSwing()
        {
            Equal(true, AttackClock.NewSwing(Clip(1.4f), Clip(0.1f)), "the same clip from its start");
            Equal(true, AttackClock.NewSwing(Clip(0.4f), Clip(0.5f, clip: 8)), "another clip");
            Equal(false, AttackClock.NewSwing(Clip(0.4f), Clip(0.5f)), "the swing goes on");
            Equal(false, AttackClock.LeftStartClip(7, Clip(0.03f), acting: false), "the clip it started with, started over or come back to: the files' time still holds");
            Equal(false, AttackClock.LeftStartClip(7, Clip(0.03f, clip: 8), acting: true), "a skeleton's alternate swing while its action goes on");
            Equal(true, AttackClock.LeftStartClip(7, Clip(0.03f, clip: 8), acting: false), "another clip with no action: a walk under the last animation id");
            Equal(80, AttackClock.MsToNominal(Clip(0.03f), 110), "a restarted hag swing is timed from its new start");
            Equal(false, AttackClock.IsBlow(new ClipEvent(AttackClock.Contact, 0f)), "the hag's spellcast contact at 0.0 marks the cast");
            Equal(true, AttackClock.IsBlow(new ClipEvent(AttackClock.Contact, 0.697f)), "the hag's swing lands at its contact");
            Equal(false, AttackClock.IsBlow(new ClipEvent("slamtrigger", 0.8f)), "only a contact times a blow");
        }

        private static void AnAttackOnThePlayerIsAimedAtThePlayer() =>
            Equal(AimKind.Player, Aim(new ActionAim(PlayerId, new Vector2(900, 0))), "the player's body, wherever the place points");

        private static void AnAttackOnAMinionAwayFromThePlayerIsAimedElsewhere()
        {
            Equal(AimKind.Elsewhere, Aim(new ActionAim(77, new Vector2(600, 0))), "a minion three hundred units away");
            Equal(AimKind.Elsewhere, Aim(new ActionAim(77, null)), "a minion, no place read");
            Equal(AimKind.PlayerArea, Aim(new ActionAim(77, new Vector2(60, 0)), areaRadius: 30), "a slam on a minion beside the player");
        }

        private static void AGroundTargetCatchesThePlayerOnlyWithinItsArea()
        {
            Equal(AimKind.PlayerArea, Aim(new ActionAim(0, new Vector2(90, 0)), areaRadius: 25), "25 + body 25 + slack 40");
            Equal(AimKind.Elsewhere, Aim(new ActionAim(0, new Vector2(100, 0)), areaRadius: 25), "just beyond");
        }

        private static void AProjectilePathCatchesThePlayerAheadOfTheShooter()
        {
            // Shooter at (-500, 0) firing at ground behind the player: the arrow crosses the player.
            Equal(AimKind.PlayerArea, Aim(new ActionAim(0, new Vector2(300, 20)), projectile: true), "on the way");
            Equal(false, AttackAim.OnPath(new Vector2(-500, 0), new Vector2(300, 0), new Vector2(0, 120), 75), "to one side");
            Equal(false, AttackAim.OnPath(new Vector2(-500, 0), new Vector2(-900, 0), Vector2.Zero, 75), "shot away from the player");
        }

        private static void NoTargetReadIsUnknownAim() => Equal(AimKind.Unknown, Aim(new ActionAim(0, null)), "nothing read");

        private static AimKind Aim(ActionAim aim, float areaRadius = 0f, bool projectile = false) =>
            AttackAim.Of(aim, PlayerId, Vector2.Zero, 25f, new Vector2(-500, 0), areaRadius, projectile);

        private static void APatternReachesItsOffsetAndItsLength()
        {
            // The Bloated Miller's attack_03 cone and attack_01 rectangle (swollenmiller.ao, Melee).
            Near(MathF.Sqrt((40 * 40) + (25 * 25)) + 173.41f, AttackReach.Farthest(new DamagePattern(PatternShape.Cone, 173.41f, 0f, 2.69f, new Vector2(40, 25))), "cone");
            Near(MathF.Sqrt((283.24f * 283.24f) + (83.815f * 83.815f)), AttackReach.Farthest(new DamagePattern(PatternShape.Rectangle, 283.24f, 167.63f, 0f, Vector2.Zero)), "rectangle's far corner");
        }

        private static void AnUnbelievablePatternReachesNothing()
        {
            Equal(null, AttackReach.Farthest(new DamagePattern(PatternShape.Cone, float.NaN, 0f, 1f, Vector2.Zero)), "not a number");
            Equal(null, AttackReach.Farthest(new DamagePattern(PatternShape.Cone, 0f, 0f, 1f, Vector2.Zero)), "empty");
            Equal(null, AttackReach.Farthest(new DamagePattern(PatternShape.Cone, 1e7f, 0f, 1f, Vector2.Zero)), "a pointer read as a length");
            var clip = new[] { new DamagePattern(PatternShape.Cone, 1e7f, 0f, 1f, Vector2.Zero), new DamagePattern(PatternShape.Circle, 120f, 0f, 0f, Vector2.Zero) };
            Near(120f, AttackReach.Farthest(clip), "the believable one");
        }

        private static void ABlowReachesWithinItsReachPlusTheBody()
        {
            Equal(true, AttackReach.MeleeReaches(200f, Vector2.Zero, new Vector2(250, 0), 25f, 40f), "within reach + slack after the body");
            Equal(false, AttackReach.MeleeReaches(200f, Vector2.Zero, new Vector2(300, 0), 25f, 40f), "a step too far");
            Equal(true, AttackReach.AreaCatches(new Vector2(0, 247), 60f, new Vector2(0, 340), 25f, 10f), "the slam's circle catches the edge of the body");
        }

        private static void AnAttackerPartlyOnScreenIsSeen()
        {
            Equal(true, AttackSight.BodySeen(new Vector2(0.3f, -0.2f), 0.02f), "on screen");
            Equal(true, AttackSight.BodySeen(new Vector2(0.95f, 0f), 0.1f), "half its body past the right edge");
            Equal(false, AttackSight.BodySeen(new Vector2(0.95f, 0f), 0.03f), "wholly past it");
        }

        private static void AProjectileFromOffScreenIsSeenWhereItEntersTheView()
        {
            var entry = AttackSight.EntryFraction(new Vector2(1.6f, 0f), Vector2.Zero);
            Near((1.6f - (8f / 9f)) / 1.6f, entry, "where it crosses the right edge of a 16:9 screen", 0.002f);
            Equal(0f, AttackSight.EntryFraction(new Vector2(0.2f, 0f), Vector2.Zero), "shot from on screen");
            Equal(null, AttackSight.EntryFraction(new Vector2(1.6f, 0f), new Vector2(1.2f, 0f)), "never on screen");
            Equal(1250L, AttackSight.SeenAt(1000, 500, 0.5f), "half way through its flight");
        }

        private static DodgeFacts Facts(DodgeThreat threat = DodgeThreat.Ranged, AimKind aim = AimKind.Player, bool? inReach = true, bool fallback = true,
                                        bool seen = true, int? seenIn = null, int? blowIn = 900, BlowTiming timing = BlowTiming.Live) =>
            new(threat, aim, inReach, fallback, seen, seenIn, blowIn, timing);

        private static void ACalmOrMisaimedAttackIsSilent()
        {
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(threat: DodgeThreat.None), LeadMs).Verdict, "calm");
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(aim: AimKind.Elsewhere, inReach: null), LeadMs).Verdict, "at a minion");
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(blowIn: 0), LeadMs).Verdict, "its blow passed");
        }

        private static void ATimedAttackWarnsALeadBeforeItsBlow()
        {
            Equal(new DodgeDecision(DodgeVerdict.Wait, 400, "timed live"), DodgeGate.Decide(Facts(), LeadMs), "900 ms ahead");
            Equal(new DodgeDecision(DodgeVerdict.Warn, 0, "timed nominal"), DodgeGate.Decide(Facts(blowIn: 300, timing: BlowTiming.Nominal), LeadMs), "sooner than the lead");
            Equal(new DodgeDecision(DodgeVerdict.Warn, 0, "wind-up"), DodgeGate.Decide(Facts(blowIn: null, timing: BlowTiming.None), LeadMs), "untimed");
        }

        private static void AnUnknownAimKeepsTheOldReachRule()
        {
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(aim: AimKind.Unknown, inReach: null, fallback: false), LeadMs).Verdict, "a rare too far by the old rule");
            Equal(DodgeVerdict.Wait, DodgeGate.Decide(Facts(aim: AimKind.Unknown, inReach: null, fallback: true), LeadMs).Verdict, "within the old rule");
        }

        private static void AMeleeBlowAimedAtThePlayerStillNeedsReach()
        {
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(threat: DodgeThreat.Melee, inReach: false), LeadMs).Verdict, "out of its patterns' reach");
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(threat: DodgeThreat.Melee, inReach: null, fallback: false), LeadMs).Verdict, "no pattern: the old gap rule");
            Equal(DodgeVerdict.Wait, DodgeGate.Decide(Facts(threat: DodgeThreat.Ranged, inReach: null, fallback: false), LeadMs).Verdict, "a ranged attack at the player reaches");
        }

        private static void AnAttackerOffScreenWarnsOnlyOnceItsProjectileShows()
        {
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(seen: false), LeadMs).Verdict, "nothing of it on screen");
            Equal(new DodgeDecision(DodgeVerdict.Wait, 200, "timed live"), DodgeGate.Decide(Facts(seen: false, seenIn: 200, blowIn: 600), LeadMs), "as the arrow comes into view");
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(seen: false, seenIn: 700, blowIn: 600), LeadMs).Verdict, "seen only after it landed");
        }

        private static void AClockThatAdvancesAtItsRateIsIntact()
        {
            var check = new AttackFieldCheck();
            for (var i = 0; i < AttackFieldCheck.MinSamples; i++)
            {
                check.Observe(AttackFieldCheck.ClockAdvances(Clip(0.1f, speed: 1.3f), Clip(0.1f + (0.016f * 1.3f), speed: 1.3f), 16));
            }

            Equal(FieldTrust.Intact, check.Trust, "twenty frames at its rate");
        }

        private static void AClockRunningOffItsRateIsLost()
        {
            var check = new AttackFieldCheck();
            for (var i = 0; i < AttackFieldCheck.MinSamples - 1; i++)
            {
                check.Observe(AttackFieldCheck.ClockAdvances(Clip(0.1f), Clip(0.15f), 16));
            }

            Equal(FieldTrust.Unjudged, check.Trust, "not enough frames yet");
            check.Observe(AttackFieldCheck.ClockAdvances(Clip(0.1f), Clip(0.15f), 16));
            Equal(FieldTrust.Lost, check.Trust, "a position running three times its rate is not a position");
            Equal(false, AttackFieldCheck.ClockAdvances(Clip(0.1f), Clip(float.NaN), 16), "garbage");
        }

        /// <summary>
        ///     A monster's clip the game moves every 50 ms, read by the host every 16.7 ms on a clock of 15.6 ms ticks: frame-to-frame
        ///     pairs run at none or several times the rate, pairs 200 ms apart keep it (the clock judged lost live, 2026-11-08).
        /// </summary>
        private static void AClockTheGameStepsSlowerThanTheHostReadsIsIntact()
        {
            var frameByFrame = new AttackFieldCheck();
            var baseline = new AttackFieldCheck();
            var pairs = new ClockBaseline();
            AnimationReading? previous = null;
            long previousAt = 0;
            for (var frame = 0; frame < 400; frame++)
            {
                var real = frame * 1000.0 / 60;
                var now = (long)(Math.Floor(real / 15.625) * 15.625);
                var reading = Clip((float)(Math.Floor(real / 50) * 0.05), end: 10f);
                if (previous is { } before)
                {
                    frameByFrame.Observe(AttackFieldCheck.ClockAdvances(before, reading, now - previousAt));
                }

                (previous, previousAt) = (reading, now);
                baseline.Observe(pairs.Offer(42, reading, now));
            }

            Equal(true, frameByFrame.Trust != FieldTrust.Intact, "frame to frame the steps alias");
            Equal(FieldTrust.Intact, baseline.Trust, "200 ms apart the rate holds");
            var garbage = new ClockBaseline();
            Equal(null, garbage.Offer(1, Clip(0.1f), 0), "a first reading closes nothing");
            Equal(null, garbage.Offer(1, Clip(0.2f), 100), "too soon");
            Equal(false, garbage.Offer(1, Clip(0.9f), 250), "running three times its rate");
        }

        private static void ARestartAPauseOrNoClipSaysNothing()
        {
            Equal(null, AttackFieldCheck.ClockAdvances(Clip(1.5f), Clip(0.01f), 16), "started over");
            Equal(null, AttackFieldCheck.ClockAdvances(Clip(0.5f), Clip(0.5f), 16), "paused");
            Equal(null, AttackFieldCheck.ClockAdvances(Clip(0.5f, clip: -1), Clip(0.6f, clip: -1), 100), "nothing playing");
            Equal(null, AttackFieldCheck.ClockAdvances(Clip(0.5f), Clip(0.6f), 2), "too close together");
        }

        private static void EventsAndActionsCheckThemselves()
        {
            Equal(true, AttackFieldCheck.EventLayoutHolds(AttackClock.Contact, AttackClock.EventId(AttackClock.Contact)), "a contact read where it should be");
            Equal(false, AttackFieldCheck.EventLayoutHolds(AttackClock.Contact, 0x1234), "a name beside another event's id");
            Equal(false, AttackFieldCheck.EventLayoutHolds(null, 0x9B8D), "no name");
            Equal(null, AttackFieldCheck.EventLayoutHolds(null, 0), "a chain's end marker says nothing");
            Equal(null, AttackFieldCheck.EventLayoutHolds(string.Empty, 0), "an empty end marker says nothing");
            var events = new AttackFieldCheck();
            for (var clip = 0; clip < AttackFieldCheck.MinSamples; clip++)
            {
                // Each clip as read live in Clearfell: one contact, then the end marker.
                events.Observe(AttackFieldCheck.EventLayoutHolds(AttackClock.Contact, AttackClock.EventId(AttackClock.Contact)));
                events.Observe(AttackFieldCheck.EventLayoutHolds(null, 0));
            }

            Equal(FieldTrust.Intact, events.Trust, "clips of one contact and an end marker judge the events intact");
            var few = new AttackFieldCheck(AttackFieldCheck.EventSamples);
            for (var clip = 0; clip < AttackFieldCheck.EventSamples - 1; clip++)
            {
                few.Observe(true);
            }

            Equal(FieldTrust.Unjudged, few.Trust, "five events are not yet a verdict");
            few.Observe(true);
            Equal(FieldTrust.Intact, few.Trust, "six events whose ids are their names' hashes settle the layout");
            var types = new HashSet<ushort> { AttackClock.ActionId("GroundSlam"), AttackClock.ActionId("DodgeRoll") };
            Equal(true, AttackFieldCheck.ActionTypeKnown(0xED4C, types), "DodgeRoll");
            Equal(false, AttackFieldCheck.ActionTypeKnown(0x1111, types), "no such action type");
            Equal(true, AttackFieldCheck.TargetPointPlausible(new Vector2(300, 0), Vector2.Zero, 3000), "near its monster");
            Equal(false, AttackFieldCheck.TargetPointPlausible(new Vector2(1e9f, 0), Vector2.Zero, 3000), "a pointer read as a place");
        }

        private static void ASweepThatReachesThePlayerWarnsWhateverItAimsAt()
        {
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(threat: DodgeThreat.Melee, aim: AimKind.Elsewhere, inReach: null), LeadMs).Verdict, "at a minion, reach unknown");
            Equal(DodgeVerdict.Wait, DodgeGate.Decide(Facts(threat: DodgeThreat.Melee, aim: AimKind.Elsewhere, inReach: true), LeadMs).Verdict, "at a minion, but its sweep reaches the player");
        }

        private static void AGroundTelegraphDecidesSightReachAndTime()
        {
            var under = new AttackTelegraph(true, true, 700);
            Equal(new DodgeDecision(DodgeVerdict.Wait, 200, "timed by its telegraph"),
                DodgeGate.Decide(Facts(aim: AimKind.Elsewhere, inReach: false, seen: false, blowIn: null, timing: BlowTiming.None) with { Telegraph = under }, LeadMs),
                "an area drawn under the player, its caster off screen and aiming elsewhere");
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(aim: AimKind.Unknown, inReach: null) with { Telegraph = new AttackTelegraph(true, false, 700) }, LeadMs).Verdict,
                "an area drawn away from the player");
            Equal(new DodgeDecision(DodgeVerdict.Wait, 400, "timed live"), DodgeGate.Decide(Facts() with { Telegraph = under }, LeadMs), "the clip's own time stays first");
        }

        private static void ASkillAreaIsReadFromItsCatalogLine()
        {
            Equal(new AttackArea(AttackAreas.Cone, 10f, 0f, 100f, 85f, 33), AttackAreas.Parse("geometry", "shape=cone;length=10;angle=100;delay=33;range=85"), "a geometry cone");
            Equal(new AttackArea(AttackAreas.Circle, 20f, 0f, 360f, 25f, 0), AttackAreas.Parse("trigger", "count=1;range=25;radius=20;angle=360"), "a trigger's circle");
            Equal(null, AttackAreas.Parse("effect", string.Empty), "an effect line has no area of its own");
            Equal(null, AttackAreas.Parse("geometry", "shape=circle;length=0"), "no size");
        }

        private static void AnAreaCatchesThePlayerOnlyInsideItsShape()
        {
            var monster = new Vector2(-300, 0);
            var circle = new AttackArea(AttackAreas.Circle, 60f, 0f, 0f, 0f, 0);
            Equal(true, AttackAreas.Catches(circle, monster, new Vector2(50, 0), Vector2.Zero, 25f, 10f), "a circle at a place beside the player");
            Equal(false, AttackAreas.Catches(circle, monster, new Vector2(200, 0), Vector2.Zero, 25f, 10f), "far from it");
            Equal(true, AttackAreas.Catches(circle with { Range = 280f }, monster, new Vector2(600, 0), Vector2.Zero, 25f, 10f), "a place beyond its range lands at its range");
            var cone = new AttackArea(AttackAreas.Cone, 400f, 0f, 60f, 0f, 0);
            Equal(true, AttackAreas.Catches(cone, monster, Vector2.Zero, Vector2.Zero, 25f, 10f), "a cone toward the player");
            Equal(false, AttackAreas.Catches(cone, monster, new Vector2(-300, 400), Vector2.Zero, 25f, 10f), "a cone turned away");
            var line = new AttackArea(AttackAreas.Rectangle, 400f, 60f, 0f, 0f, 0);
            Equal(true, AttackAreas.Catches(line, monster, new Vector2(300, 0), new Vector2(0, 50), 25f, 10f), "a rectangle's edge on the body");
            Equal(false, AttackAreas.Catches(line, monster, new Vector2(300, 0), new Vector2(0, 100), 25f, 10f), "beside it");
        }

        /// <summary>Live 2026-11-08: hits that landed farther than the tables' sizes read as world units allow.</summary>
        private static void TheTablesAreaSizesAreGridCells()
        {
            var data = new AttackData();
            data.AddTelegraphSkills(new[]
            {
                "GAMudBurrowerHeadSlam|geometry|shape=circle;length=18;delay=33;range=100|Metadata/Effects/Spells/monsters_effects/Act1_FOUR/MudBurrower/bite_vomit.ao|",
                "MudGolemSlam|geometry|shape=circle;length=15;delay=33;range=66|Metadata/Effects/Spells/monsters_effects/Gallows/Act1/MudGolem/slam.ao|",
            });
            Near(18f * 250f / 23f, data.Area("GAMudBurrowerHeadSlam")?.Length, "a radius of 18 cells");
            Near(100f * 250f / 23f, data.Area("GAMudBurrowerHeadSlam")?.Range, "a range of 100 cells");

            // The Devourer at (1832, 2364) slammed the player's place (2076, 2609); the player stood at (2082, 2614): 41 life.
            var devourer = new Vector2(1832, 2364);
            var target = new Vector2(2076, 2609);
            var player = new Vector2(2082, 2614);
            Equal(true, AttackAreas.Catches(data.Area("GAMudBurrowerHeadSlam")!.Value, devourer, target, player, 30f, 40f), "the slam that hit at 346 units");
            Equal(false, AttackAreas.Catches(AttackAreas.Parse("geometry", "shape=circle;length=18;delay=33;range=100")!.Value, devourer, target, player, 30f, 40f),
                "read as world units it could not have");

            // A Mud Golem 217 units from the player, its slam aimed at the player's place: hit live.
            var golem = new Vector2(0, 0);
            var standing = new Vector2(217, 0);
            Equal(true, AttackAreas.Catches(data.Area("MudGolemSlam")!.Value, golem, standing, standing, 30f, 40f), "a golem slam at 217 units");
            Equal(false, AttackAreas.Catches(data.Area("MudGolemSlam")!.Value, golem, new Vector2(900, 0), new Vector2(1300, 0), 30f, 40f),
                "still nothing far past the slam's range");
        }

        private static void TheAttackCatalogsGiveReachAreasAndActionNames()
        {
            var data = new AttackData();
            data.AddReaches(new[] { "# header", "metadata/monsters/swollenmiller/swollenmiller.ao|attack_01=296/107", "metadata/monsters/x/x.ao|slam_01=0/247", "broken" });
            data.AddTelegraphSkills(new[] { "# header", "BalbalaAttack1TriggeredSlash1|geometry|shape=cone;length=10;angle=100;delay=33;range=85||", "SomeMeteor|effect||Metadata/Effects/m.ao|" });
            data.AddActionTypes(new[] { "# header", "GroundSlam", "DodgeRoll" });
            Equal(296, data.Reach("Metadata/Monsters/SwollenMiller/SwollenMiller.ao", "attack_01"), "a clip's reach, whatever the path's case");
            Equal(247, data.Reach("metadata/monsters/x/x.ao", "slam_01"), "a slam lands where its contact is");
            Equal(null, data.Reach("metadata/monsters/x/x.ao", "idle_01"), "a clip the files do not reach with");
            Equal(AttackAreas.Cone, data.Area("BalbalaAttack1TriggeredSlash1")?.Shape, "a skill's area");
            Equal(null, data.Area("SomeMeteor"), "an effect-driven skill has its shapes in its effect file");
            Equal("DodgeRoll", data.ActionName(0xED4C), "an action named by its id");
            Equal(2, data.ActionTypeIds.Count, "the ids the action check knows");
        }

        private static void ALearntBlowFarFromTheFilesIsNotBelieved()
        {
            const string swing = "Metadata/Monsters/FungusBehemoth|1";
            const string spell = "Metadata/Monsters/FungusBehemoth|35";
            var timings = new DodgeTimings();
            timings.AddHitTimes(new[] { $"{swing}=576/1500;melee", $"{spell}=1300/3000;ranged" });
            Equal(false, timings.Learn(swing, 31), "a hit the moment it starts is another's");
            Equal(false, timings.Learn(swing, 1078), "half a second late is another blow");
            Equal(true, timings.Learn(swing, 625), "near the files' time");
            Equal(625, timings.Delay(swing), "only what was believed counts");
            Equal(false, timings.Learn(spell, 125), "a spell's projectile cannot land before it is released");
            Equal(true, timings.Learn(spell, 2578), "its flight, of unknown speed, may land it later");
            var polluted = DodgeTimings.FromLines(new[] { $"{swing}=609,625,594,1078,31,110" });
            polluted.AddHitTimes(new[] { $"{swing}=576/1500;melee" });
            Equal(609, polluted.Delay(swing), "blows saved before the rule are judged again when asked");
        }

        private static void Equal<T>(T expected, T actual, string what)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{what}: expected {expected}, got {actual}");
            }
        }

        private static void Near(float expected, float? actual, string what, float tolerance = 0.01f)
        {
            if (actual is not { } value || MathF.Abs(value - expected) > tolerance)
            {
                throw new Exception($"{what}: expected {expected}, got {actual}");
            }
        }
    }
}
