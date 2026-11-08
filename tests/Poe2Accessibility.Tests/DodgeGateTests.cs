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
            (nameof(ARestartAPauseOrNoClipSaysNothing), ARestartAPauseOrNoClipSaysNothing),
            (nameof(EventsAndActionsCheckThemselves), EventsAndActionsCheckThemselves),
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
            Equal(AimKind.PlayerArea, Aim(new ActionAim(0, new Vector2(300, 20)), pathHalfWidth: 10), "on the way");
            Equal(false, AttackAim.OnPath(new Vector2(-500, 0), new Vector2(300, 0), new Vector2(0, 120), 75), "to one side");
            Equal(false, AttackAim.OnPath(new Vector2(-500, 0), new Vector2(-900, 0), Vector2.Zero, 75), "shot away from the player");
        }

        private static void NoTargetReadIsUnknownAim() => Equal(AimKind.Unknown, Aim(new ActionAim(0, null)), "nothing read");

        private static AimKind Aim(ActionAim aim, float areaRadius = 0f, float pathHalfWidth = 0f) =>
            AttackAim.Of(aim, PlayerId, Vector2.Zero, 25f, new Vector2(-500, 0), areaRadius, pathHalfWidth);

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
            Equal(DodgeVerdict.Silent, DodgeGate.Decide(Facts(aim: AimKind.Elsewhere), LeadMs).Verdict, "at a minion");
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
            var types = new HashSet<ushort> { AttackClock.ActionId("GroundSlam"), AttackClock.ActionId("DodgeRoll") };
            Equal(true, AttackFieldCheck.ActionTypeKnown(0xED4C, types), "DodgeRoll");
            Equal(false, AttackFieldCheck.ActionTypeKnown(0x1111, types), "no such action type");
            Equal(true, AttackFieldCheck.TargetPointPlausible(new Vector2(300, 0), Vector2.Zero, 3000), "near its monster");
            Equal(false, AttackFieldCheck.TargetPointPlausible(new Vector2(1e9f, 0), Vector2.Zero, 3000), "a pointer read as a place");
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
