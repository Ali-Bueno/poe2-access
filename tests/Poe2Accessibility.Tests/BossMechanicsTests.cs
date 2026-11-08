namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;

    /// <summary>Boss fights' turns, the way out of danger areas, the boss catalog and its extractor's reading of scripts (docs/research/boss-mechanics.md).</summary>
    public static class BossMechanicsTests
    {
        private const string Wolf = "Metadata/Monsters/Baron/BaronBossCorruptedWolfForm";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ACircleOnThePlayerIsLeftAnyWay), ACircleOnThePlayerIsLeftAnyWay),
            (nameof(AnOffCentreCircleIsLeftAwayFromItsCentre), AnOffCentreCircleIsLeftAwayFromItsCentre),
            (nameof(ADonutSendsToTheNearerSafety), ADonutSendsToTheNearerSafety),
            (nameof(ALineIsSidesteppedOnTheNearerSide), ALineIsSidesteppedOnTheNearerSide),
            (nameof(AConeIsLeftAcrossTheNearerEdgeOrBehind), AConeIsLeftAcrossTheNearerEdgeOrBehind),
            (nameof(ScatteredMarkersSayMove), ScatteredMarkersSayMove),
            (nameof(ATriggerLineKeepsItsMarkerCount), ATriggerLineKeepsItsMarkerCount),
            (nameof(AFlickerIsNoNewsALastingHideIs), AFlickerIsNoNewsALastingHideIs),
            (nameof(NothingIsSaidBeforeTheFightIsOn), NothingIsSaidBeforeTheFightIsOn),
            (nameof(InvulnerableEnragedAndPhaseAreSaidOnce), InvulnerableEnragedAndPhaseAreSaidOnce),
            (nameof(AReplacedBossSaysNothingMore), AReplacedBossSaysNothingMore),
            (nameof(TheCatalogReadsItsKinds), TheCatalogReadsItsKinds),
            (nameof(AMechanicLineIsKnownInAnyLanguageAndMarkup), AMechanicLineIsKnownInAnyLanguageAndMarkup),
            (nameof(TheFilesMovementRulesBecomeAilments), TheFilesMovementRulesBecomeAilments),
            (nameof(CounterplayWordsAreShort), CounterplayWordsAreShort),
            (nameof(AScriptGivesHidesPhasesEnragesLinesAndPreloads), AScriptGivesHidesPhasesEnragesLinesAndPreloads),
            (nameof(OnlyLinesOfAFightsTurnAreMechanics), OnlyLinesOfAFightsTurnAreMechanics),
            (nameof(ARandomLinePicksAmongItsNumberedSiblings), ARandomLinePicksAmongItsNumberedSiblings),
        };

        private static Counterplay? Answer(AttackArea area, Vector2 target, Vector2 player, Vector2? monster = null) =>
            BossCounterplay.Of(area, monster ?? Vector2.Zero, target, player, 0f, 0f);

        private static void ACircleOnThePlayerIsLeftAnyWay()
        {
            var slam = new AttackArea(AttackAreas.Circle, 50f, 0f, 0f, 0f, 33);
            Equal(CounterplayKind.Out, Answer(slam, new Vector2(300, 0), new Vector2(305, 0))?.Kind, "a slam under the player");
            Equal(null, BossCounterplay.Of(slam, Vector2.Zero, new Vector2(300, 0), new Vector2(410, 0), 10f, 40f), "outside the slam and its slack");
        }

        private static void AnOffCentreCircleIsLeftAwayFromItsCentre()
        {
            var answer = Answer(new AttackArea(AttackAreas.Circle, 50f, 0f, 0f, 0f, 33), new Vector2(300, 0), new Vector2(330, 0));
            Equal(CounterplayKind.Step, answer?.Kind, "near the rim");
            Near(1f, answer?.Way.X, "away from the centre");
        }

        private static void ADonutSendsToTheNearerSafety()
        {
            var ring = new AttackArea(AttackAreas.Donut, 100f, 40f, 0f, 0f, 33);
            var inner = Answer(ring, Vector2.Zero, new Vector2(50, 0));
            Equal(CounterplayKind.Centre, inner?.Kind, "just outside the hole");
            Near(-1f, inner?.Way.X, "toward the hub");
            Equal(CounterplayKind.Step, Answer(ring, Vector2.Zero, new Vector2(90, 0))?.Kind, "near the outer rim: out");
            Equal(null, BossCounterplay.Of(ring, Vector2.Zero, Vector2.Zero, new Vector2(10, 0), 10f, 0f), "in the hole: safe");
        }

        private static void ALineIsSidesteppedOnTheNearerSide()
        {
            var beam = new AttackArea(AttackAreas.Rectangle, 200f, 40f, 0f, 0f, 33);
            var target = new Vector2(100, 0);
            Near(1f, Answer(beam, target, new Vector2(50, 10))?.Way.Y, "left of the beam: step left");
            Near(-1f, Answer(beam, target, new Vector2(50, -10))?.Way.Y, "right of it: step right");
            Near(1f, Answer(beam, target, new Vector2(195, 0))?.Way.X, "by its far end: out of the end");
            Equal(null, Answer(beam, target, new Vector2(50, 60)), "beside it");
        }

        private static void AConeIsLeftAcrossTheNearerEdgeOrBehind()
        {
            var breath = new AttackArea(AttackAreas.Cone, 100f, 0f, 90f, 0f, 33);
            var side = Answer(breath, new Vector2(100, 0), new Vector2(50, 10));
            Equal(CounterplayKind.Step, side?.Kind, "inside a quarter-turn cone");
            Equal(true, side?.Way.Y > 0f && side?.Way.X < 0f, "squared off the nearer (left) edge");
            var sweep = new AttackArea(AttackAreas.Cone, 100f, 0f, 180f, 0f, 60);
            var behind = Answer(sweep, new Vector2(100, 0), new Vector2(20, 0));
            Equal(CounterplayKind.Behind, behind?.Kind, "close to a half-turn sweep");
            Near(-1f, behind?.Way.X, "behind the caster");
            Equal(CounterplayKind.Step, Answer(sweep, new Vector2(100, 0), new Vector2(90, 0))?.Kind, "at its reach: out past it");
        }

        private static void ScatteredMarkersSayMove()
        {
            var meteors = new AttackArea(AttackAreas.Circle, 15f, 0f, 360f, 100f, 2000) { Count = 5 };
            Equal(CounterplayKind.Move, Answer(meteors, Vector2.Zero, new Vector2(80, 0), new Vector2(-300, 0))?.Kind, "within the spread");
            Equal(null, Answer(meteors, Vector2.Zero, new Vector2(200, 0), new Vector2(-300, 0)), "beyond it");
        }

        private static void ATriggerLineKeepsItsMarkerCount()
        {
            Equal(10, AttackAreas.Parse("trigger", "count=10;range=280;radius=140;angle=360")?.Count, "GTCyclopsSpiderBossDangerZone's ten");
            Equal(1, AttackAreas.Parse("geometry", "shape=circle;length=15;delay=33;range=66")?.Count, "one slam");
        }

        private static void AFlickerIsNoNewsALastingHideIs()
        {
            var tracker = new BossPhaseTracker();
            Equal(0, tracker.Update(Facts(gone: true), 0).Count, "it just vanished");
            Equal(0, tracker.Update(Facts(gone: false), 300).Count, "back within a teleport's time");
            Equal(0, tracker.Update(Facts(gone: true), 1000).Count, "vanished again");
            Equal(BossNews.Gone, tracker.Update(Facts(gone: true), 1000 + BossPhaseTracker.SettleMs).Single(), "it stayed away");
            Equal(0, tracker.Update(Facts(gone: true), 5000).Count, "said once");
            Equal(0, tracker.Update(Facts(gone: false), 6000).Count, "back, not yet settled");
            Equal(BossNews.Back, tracker.Update(Facts(gone: false), 6000 + BossPhaseTracker.SettleMs).Single(), "back for good");
        }

        private static void NothingIsSaidBeforeTheFightIsOn()
        {
            var tracker = new BossPhaseTracker();
            Equal(0, tracker.Update(Facts(engaged: false, gone: true, phase: 0), 0).Count, "waiting hidden in its arena");
            Equal(0, tracker.Update(Facts(engaged: false, gone: false, phase: 1), 2000).Count, "emerging, its music starting");
            Equal(0, tracker.Update(Facts(gone: false, phase: 1), 4000).Count, "the first hit is no news");
        }

        private static void InvulnerableEnragedAndPhaseAreSaidOnce()
        {
            var tracker = new BossPhaseTracker();
            tracker.Update(Facts(phase: 1), 0);
            tracker.Update(Facts(invulnerable: true, phase: 1), 100);
            Equal(BossNews.Invulnerable, tracker.Update(Facts(invulnerable: true, phase: 1), 100 + BossPhaseTracker.SettleMs).Single(), "a lasting shield");
            tracker.Update(Facts(phase: 1), 2000);
            Equal(BossNews.Vulnerable, tracker.Update(Facts(phase: 1), 2000 + BossPhaseTracker.SettleMs).Single(), "the shield fell");
            Equal(BossNews.NextPhase, tracker.Update(Facts(phase: 2), 4000).Single(), "its music's next part");
            Equal(BossNews.Enraged, tracker.Update(Facts(phase: 2, enraged: true), 5000).Single(), "enraged");
            Equal(0, tracker.Update(Facts(phase: 2, enraged: true), 6000).Count, "said once");
        }

        private static void AReplacedBossSaysNothingMore()
        {
            var tracker = new BossPhaseTracker(3000) { Replaced = true };
            Equal(0, tracker.Update(Facts(gone: true), 0).Count + tracker.Update(Facts(gone: true), 10000).Count, "the Count after his wolf took over");
        }

        private static BossFacts Facts(bool engaged = true, bool gone = false, bool invulnerable = false, bool enraged = false, long? phase = null) =>
            new(engaged, gone, invulnerable, enraged, phase);

        private static BossMechanicsCatalog Sample()
        {
            var catalog = new BossMechanicsCatalog();
            catalog.Add(new[]
            {
                "# header|x|y",
                $"boss|{Wolf}|Geonor, the Putrid Wolf",
                $"hide|{Wolf}|faded=1",
                $"phase|{Wolf}|audio_param",
                $"enrage|{Wolf}|enraged",
                $"add|{Wolf}|Metadata/Monsters/Baron/BaronWerewolfSummon",
                $"form|Metadata/Monsters/Baron/BaronBossHumanForm|{Wolf}",
                "buff|god_mode|invulnerable",
                "buff|enrage_buff|enrage",
                "move|voodoo_king_punishment_moving|stop",
                "move|grasping_vines_buff|keep",
                $"line|en|Chilling frost.|{Wolf}|GSBaronBossColdBreathChannel|CorruptWolf_Skill_IceBreath_2",
                "line|es|<i>{Rith}!|Metadata/Monsters/VoodooKing/VoodooKingBoss2|-|TheKingInTheMists_StationaryHex2",
            });
            return catalog;
        }

        private static void TheCatalogReadsItsKinds()
        {
            var catalog = Sample();
            Equal(true, catalog.IsBoss(BossMechanicsCatalog.MonsterId(Wolf + "@15")), "a boss by its entity path");
            Equal(true, catalog.Hides(Wolf, "faded", 1) && !catalog.Hides(Wolf, "faded", 0), "the fog's fade hides it");
            Equal(true, catalog.IsPhaseState(Wolf, "audio_param") && catalog.IsEnrageState(Wolf, "enraged"), "phase and enrage states");
            Equal(true, catalog.IsAdd(Wolf, "Metadata/Monsters/Baron/BaronWerewolfSummon"), "its werewolves");
            Equal(true, catalog.IsForm("Metadata/Monsters/Baron/BaronBossHumanForm", Wolf) && catalog.HasForms("Metadata/Monsters/Baron/BaronBossHumanForm"), "the Count turns into it");
            Equal(true, catalog.IsUnhurtable("god_mode") && catalog.IsEnrage("enrage_buff") && !catalog.IsUnhurtable("enrage_buff"), "buff kinds");
            Equal(2, catalog.Lines, "two lines");
        }

        private static void AMechanicLineIsKnownInAnyLanguageAndMarkup()
        {
            var catalog = Sample();
            Equal(true, catalog.IsMechanicLine("Geonor, the Putrid Wolf: Chilling frost."), "the chat's speaker and text");
            Equal(true, catalog.IsMechanicLine("El Rey de la Niebla: Rith!"), "the markup the chat does not show");
            Equal(false, catalog.IsMechanicLine("Geonor, the Putrid Wolf: I... Hunger."), "a taunt stays muted");
            Equal("rith", BossMechanicsCatalog.LineKey("<i>{Rith}!</i>"), "letters only");
        }

        private static void TheFilesMovementRulesBecomeAilments()
        {
            var before = BossMechanicsCatalog.Shared;
            try
            {
                BossMechanicsCatalog.Shared = Sample();
                Equal(StatusAilment.KeepMoving, StatusAilments.FromBuffId("grasping_vines_buff_79"), "moving breaks the vines");
                Equal(StatusAilment.StopMoving, StatusAilments.FromBuffId("voodoo_king_punishment_moving"), "the Ritual of Meditation");
                Equal(StatusAilment.Chilled, StatusAilments.FromBuffId("chilled_79"), "other states as before");
            }
            finally
            {
                BossMechanicsCatalog.Shared = before;
            }
        }

        private static void CounterplayWordsAreShort()
        {
            try
            {
                Localization.Detect("es");
                Equal("¡Izquierda!", Announcements.Counterplay(CounterplayKind.Step, 4), "a stick octant");
                Equal("¡Al centro!", Announcements.Counterplay(CounterplayKind.Centre, 0), "a donut");
                Equal("Fase 2: Geonor", Announcements.BossTurn(BossNews.NextPhase, "Geonor", phase: 2), "a new form");
                Equal("Invulnerable, 3 esbirros", Announcements.BossTurn(BossNews.Invulnerable, null, 3), "a shield and its adds");
            }
            finally
            {
                Localization.Detect("en");
            }
        }

        private const string Script = """
            version 3
            extends "Metadata/Monsters/Monster"
            extends "Metadata/Monsters/BossBase"
            StateMachine
            {
                define_shared_state = "faded; moonlight; enraged; enrage_time; audio_param = 1;"
                on_or_create_state_tele_hide_1 = { RemoveCollidability(); }
            }
            client
            {
                StateMachine
                {
                    on_state_faded_1 =
                    {
                        AddEffectPack( 'smoke.epk' );
                        DisableRendering();
                    }
                    on_or_create_state_audio_param_1 = { SetGlobalAudioParam( BaronPhase, 1 ); }
                }
                Animated
                {
                    on_effect_start_GSBaronBossColdBreathChannel = { PlayTextAudio( CorruptWolf_Skill_IceBreath_Random, 'Metadata/Monsters/Baron/BaronCorruptWolfForm' ); }
                    on_moon_beam_01 = { PlayTextAudio( CorruptWolf_Skill_IceSkyBeams_Random, 'Metadata/Monsters/Baron/BaronCorruptWolfForm' ); }
                }
            }
            Preload
            {
                preload_monster_variety = "Metadata/Monsters/Baron/BaronWerewolfSummon"
                preload_object = "Metadata/Monsters/Baron/Objects/BaronWolfBloodOrb"
            }
            """;

        private static void AScriptGivesHidesPhasesEnragesLinesAndPreloads()
        {
            var script = BossScripts.Parse(Script);
            Equal("Metadata/Monsters/Monster,Metadata/Monsters/BossBase", string.Join(",", script.Extends), "both parents");
            Equal("faded=1", string.Join(",", script.Hides.Select(hide => $"{hide.State}={hide.Value}")), "the fade draws nothing, the teleport only stops colliding");
            Equal("audio_param", string.Join(",", script.PhaseStates), "the state setting BaronPhase");
            Equal("enraged", string.Join(",", script.EnrageStates), "not the enrage timer");
            Equal(true, script.Lines.Contains(("on_effect_start_GSBaronBossColdBreathChannel", "CorruptWolf_Skill_IceBreath_Random")), "a line with its skill");
            Equal("Metadata/Monsters/Baron/BaronWerewolfSummon", script.Monsters.Single(), "its add");
            Equal("Metadata/Monsters/Baron/Objects/BaronWolfBloodOrb", script.Objects.Single(), "its object");
            Equal("GSBaronBossColdBreathChannel", BossScripts.SkillOf("on_effect_start_GSBaronBossColdBreathChannel"), "the skill of a handler");
        }

        private static void OnlyLinesOfAFightsTurnAreMechanics()
        {
            Equal(true, BossScripts.AnnouncesMechanic("on_effect_start_GSBaronBossColdBreathChannel", "CorruptWolf_Skill_IceBreath_Random", "geometry_spell"), "a channelled breath");
            Equal(true, BossScripts.AnnouncesMechanic("succeeded", "Kabala_Summon_Random", null), "a summon from a function");
            Equal(true, BossScripts.AnnouncesMechanic("on_effect_start_SSMDaGok", "BoneCultistDeathlordDaGokRandom", "summon_specific_monsters_on_target"), "a summon by its kind");
            Equal(true, BossScripts.AnnouncesMechanic("on_1h_to_2h", "Executioner_PhaseChange_Random", null), "a phase change");
            Equal(false, BossScripts.AnnouncesMechanic("on_moon_beam_01", "CorruptWolf_Skill_IceSkyBeams_Random", null), "an attack cry");
            Equal(false, BossScripts.AnnouncesMechanic("on_effect_start_CTS1BossFightStart", "Boss_FightStart_Random", "change_to_stance1"), "the fight's opening");
            Equal(false, BossScripts.AnnouncesMechanic("on_death", "CarrionCroneDeath", null), "a death");
            Equal(false, BossScripts.AnnouncesMechanic("on_effect_start_SSMWolves", "CarrionCroneAggroRandom", "summon_specific_monsters"), "flavour wins over the kind");
        }

        private static void ARandomLinePicksAmongItsNumberedSiblings()
        {
            Equal(true, BossScripts.IsVariant("CorruptWolf_Skill_IceBreath_Random", "CorruptWolf_Skill_IceBreath_3"), "underscored");
            Equal(true, BossScripts.IsVariant("SwollenMillerAttackARandom", "SwollenMillerAttackA2"), "run together");
            Equal(false, BossScripts.IsVariant("SwollenMillerAttackARandom", "SwollenMillerAttackB2"), "another line");
            Equal(false, BossScripts.IsVariant("SwollenMillerAttackC", "SwollenMillerAttackC2"), "a fixed line is only itself");
            Equal(BossScripts.Stem("CorruptWolf_Skill_IceBreath_Random"), BossScripts.Stem("CorruptWolf_Skill_IceBreath_12"), "one stem");
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
