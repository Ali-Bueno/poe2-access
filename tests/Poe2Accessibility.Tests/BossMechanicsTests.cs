namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Boss fights' turns, the way out of danger areas, the boss catalog and its extractor's reading of scripts (docs/research/boss-mechanics.md).</summary>
    public static class BossMechanicsTests
    {
        private const string Wolf = "Metadata/Monsters/Baron/BaronBossCorruptedWolfForm";
        private const string Balbala = "Metadata/Monsters/Balbala/Balbala";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ACircleOnThePlayerIsLeftAnyWay), ACircleOnThePlayerIsLeftAnyWay),
            (nameof(AMortarAimedAtThePlayerSaysMove), AMortarAimedAtThePlayerSaysMove),
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
            (nameof(LifeStepsComeBackAfterAHealNotAJitter), LifeStepsComeBackAfterAHealNotAJitter),
            (nameof(AFightRestartsWhenTheBossIsFullAfterThePlayersDeath), AFightRestartsWhenTheBossIsFullAfterThePlayersDeath),
            (nameof(TheCatalogReadsItsKinds), TheCatalogReadsItsKinds),
            (nameof(AMechanicLineIsKnownInAnyLanguageAndMarkup), AMechanicLineIsKnownInAnyLanguageAndMarkup),
            (nameof(TheFilesMovementRulesBecomeAilments), TheFilesMovementRulesBecomeAilments),
            (nameof(CounterplayWordsAreShort), CounterplayWordsAreShort),
            (nameof(AScriptGivesHidesPhasesEnragesLinesAndPreloads), AScriptGivesHidesPhasesEnragesLinesAndPreloads),
            (nameof(OnlyLinesOfAFightsTurnAreMechanics), OnlyLinesOfAFightsTurnAreMechanics),
            (nameof(ARandomLinePicksAmongItsNumberedSiblings), ARandomLinePicksAmongItsNumberedSiblings),
            (nameof(TheWayOutOfARingOfWallsIsItsOpening), TheWayOutOfARingOfWallsIsItsOpening),
            (nameof(AFewWallsToOneSideGiveNoCircle), AFewWallsToOneSideGiveNoCircle),
        };

        /// <summary>Kabala's ring as the files give it: 80 places on a circle of 50 cells; the walls at <paramref name="missing"/> never rose.</summary>
        private static List<Vector2> Ring(Vector2 centre, params int[] missing) =>
            Enumerable.Range(0, 80).Where(i => !missing.Contains(i))
                .Select(i => centre + (50f * new Vector2(MathF.Cos(i * 4.5f * MathF.PI / 180f), MathF.Sin(i * 4.5f * MathF.PI / 180f)))).ToList();

        private static void TheWayOutOfARingOfWallsIsItsOpening()
        {
            const int BytesPerRow = 200;
            var grid = Enumerable.Repeat((byte)0x11, BytesPerRow * 400).ToArray(); // open ground, 400 x 400 cells
            var centre = new Vector2(200, 200);
            var open = WallRing.Read(grid, BytesPerRow, centre, Ring(centre, 0, 1, 2));
            Equal(WallWay.Open, open!.Value.Way, "three walls missing: a way out on foot");
            Equal(true, open.Value.At.X > 245 && MathF.Abs(open.Value.At.Y - 204) < 12, "through the opening, just beyond the ring");

            var closed = WallRing.Read(grid, BytesPerRow, new Vector2(210, 200), Ring(centre));
            Equal(WallWay.Closed, closed!.Value.Way, "a whole ring: a wall must be broken");
            Equal(true, Vector2.Distance(closed.Value.At, new Vector2(250, 200)) < 1f, "the wall nearest the player");

            Equal(WallWay.Out, WallRing.Read(grid, BytesPerRow, new Vector2(300, 200), Ring(centre))!.Value.Way, "beyond the ring already");
            Equal(true, WallRing.Read(grid, BytesPerRow, centre, Ring(centre).Take(2).ToList()) == null, "two walls shut nobody in");

            // The opening is on the far side of a rock: the walls leave it open, the ground does not.
            for (var y = 190; y <= 215; y++)
            {
                for (var x = 244; x <= 262; x++)
                {
                    grid[(y * BytesPerRow) + (x / 2)] &= (byte)(x % 2 == 0 ? 0xF0 : 0x0F);
                }
            }

            Equal(WallWay.Closed, WallRing.Read(grid, BytesPerRow, centre, Ring(centre, 0, 1, 2))!.Value.Way, "an opening onto rock is no way out");
        }

        private static void AFewWallsToOneSideGiveNoCircle()
        {
            var centre = new Vector2(200, 200);
            var fitted = WallRing.Fit(Ring(centre, 0, 1, 2));
            Equal(true, fitted is { } ring && Vector2.Distance(ring.Centre, centre) < 0.5f && MathF.Abs(ring.Radius - 50f) < 0.5f, "the ring's circle, opening or not");
            Equal(true, WallRing.Fit(Ring(centre).Take(10).ToList()) == null, "45 degrees of arc say too little of the circle");
            Equal(true, WallRing.Fit(new[] { new Vector2(0, 0), new Vector2(4, 0), new Vector2(8, 0) }) == null, "walls in a line");
        }

        private static Counterplay? Answer(AttackArea area, Vector2 target, Vector2 player, Vector2? monster = null) =>
            BossCounterplay.Of(area, monster ?? Vector2.Zero, target, player, 0f, 0f);

        private static void AMortarAimedAtThePlayerSaysMove()
        {
            // The ritual's Rotten Druid: "MMSFungusBehemothProjectile|mortar||…/swamp_mortar_spam_impact.ao|", aimed at the player.
            var data = new AttackData();
            data.AddTelegraphSkills(new[] { "MMSFungusBehemothProjectile|mortar||Metadata/Effects/Spells/monsters_effects/swamp_mortar_spam_impact.ao|" });
            var mortar = data.Area("MMSFungusBehemothProjectile");
            Equal(true, mortar != null, "every mortar has an area: where it was aimed");
            var druid = new Vector2(0, 0);
            var spot = new Vector2(500, 0);
            Equal(CounterplayKind.Move, BossCounterplay.Of(mortar!.Value, druid, spot, spot + new Vector2(10, 0), 30f, 40f)?.Kind, "standing on the aimed spot: move");
            Equal(null, BossCounterplay.Of(mortar.Value, druid, spot, new Vector2(800, 0), 30f, 40f), "far from where it lands: nothing");
            var sized = AttackAreas.Parse(AttackAreas.Mortar, "length=12;range=15");
            Equal(CounterplayKind.Move, sized is { } known ? BossCounterplay.Of(AttackAreas.InWorld(known), druid, spot, spot + new Vector2(250, 0), 30f, 40f)?.Kind : null,
                "a size the tables give (blast 12, scatter 15 cells) widens it");
            Equal(true, sized is { } scattered && AttackAreas.Catches(AttackAreas.InWorld(scattered), druid, spot, spot + new Vector2(250, 0), 30f, 40f),
                "the dodge gate reads its range as the scatter around the aimed spot, not a reach from the druid");
        }

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
            Equal(BossNews.Back, tracker.Update(Facts(gone: false), 6000 + BossPhaseTracker.BackSettleMs).Single(), "back for good, sooner than a leaving");
            Equal(0, tracker.Update(Facts(gone: true), 9000).Count, "gone again");
            Equal(0, tracker.Update(Facts(gone: true), 9000 + BossPhaseTracker.BackSettleMs).Count, "a leaving still waits the full settle");
        }

        private static void NothingIsSaidBeforeTheFightIsOn()
        {
            var tracker = new BossPhaseTracker();
            Equal(0, tracker.Update(Facts(engaged: false, gone: true, phase: 0), 0).Count, "waiting hidden in its arena");
            Equal(0, tracker.Update(Facts(engaged: false, gone: false, phase: 1), 2000).Count, "emerging, its music starting");
            Equal(0, tracker.Update(Facts(gone: false, phase: 1), 4000).Count, "the first hit is no news");
            Equal(0, tracker.Update(Facts(gone: false, phase: 2), 5000).Count, "its music stepping up with the aggro");
            Equal(BossNews.NextPhase, tracker.Update(Facts(gone: false, phase: 3), 4000 + BossPhaseTracker.EngageGraceMs + 1000).Single(), "a later rise");
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
                $"seek|{Balbala}|hiding",
                $"add|{Wolf}|Metadata/Monsters/Baron/BaronWerewolfSummon",
                "wall|Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBoss|Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBossBoneWall",
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
            Equal(true, catalog.IsSeekState(Balbala, "hiding") && catalog.HidesToBeFound(Balbala), "Balbala hides until she is found");
            Equal(false, catalog.IsSeekState(Wolf, "faded") || catalog.HidesToBeFound(Wolf), "the Wolf's fog is no hide-and-seek");
            Equal(true, catalog.IsAdd(Wolf, "Metadata/Monsters/Baron/BaronWerewolfSummon"), "its werewolves");
            Equal(true, catalog.IsWall("Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBoss", "Metadata/Monsters/SerpentClanMonster/SerpentClanCasterBossBoneWall"), "Kabala's bone walls");
            Equal(false, catalog.IsWall(Wolf, "Metadata/Monsters/Baron/BaronWerewolfSummon"), "an add that fights is no wall");
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
                Equal("El jefe se esconde: sigue el latido, abajo a la izquierda", Announcements.BossHiding(null, 5, true), "a boss to find, by the heartbeat");
                Equal("Balbala se esconde, arriba", Announcements.BossHiding("Balbala", 2, false), "named among several, the heartbeat off");
                Equal("Muros: sigue el latido hasta la salida, derecha", Announcements.BossWalls(true, 0, true), "walled in, with a way out");
                Equal("Muros por todos lados: rompe uno, arriba", Announcements.BossWalls(false, 2, false), "walled in on every side, the heartbeat off");
                Equal("Fuera de los muros", Announcements.BossWallsLeft(), "out of them");
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
                    on_or_create_state_hiding_1 = { AddEffectPack( 'Metadata/Effects/Spells/monsters_effects/Act2_FOUR/Balbala/fade_by_player_dist.epk' ); }
                    on_state_hiding_0 = { RemoveEffectPack( 'Metadata/Effects/Spells/monsters_effects/Act2_FOUR/Balbala/fade_by_player_dist.epk' ); }
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
            Equal("hiding", string.Join(",", script.SeekStates), "drawn by the player's distance: it hides to be found; the fade's smoke is another pack");
            Equal(true, script.Lines.Contains(("on_effect_start_GSBaronBossColdBreathChannel", "CorruptWolf_Skill_IceBreath_Random")), "a line with its skill");
            Equal("Metadata/Monsters/Baron/BaronWerewolfSummon", script.Monsters.Single(), "its add");
            Equal("Metadata/Monsters/Baron/Objects/BaronWolfBloodOrb", script.Objects.Single(), "its object");
            Equal("GSBaronBossColdBreathChannel", BossScripts.SkillOf("on_effect_start_GSBaronBossColdBreathChannel"), "the skill of a handler");
            Equal(false, script.Blocks, "a boss walks: it blocks no ground");
            var wall = BossScripts.Parse("version 3\nextends \"Metadata/Monsters/Monster\"\nextends \"Metadata/Utility/InanimateObject\"\nPositioned\n{\n\tblocking = true\n\tignore_being_pushed = true\n}\n");
            Equal(true, wall.Blocks && wall.Extends.Contains("Metadata/Utility/InanimateObject"), "the Bone Wall: inanimate and in the way");
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

        /// <summary>Beira back at full life after yarkey died under her (Clearfell, 2026-11-08): her fight, phases and all, starts over.</summary>
        private static void AFightRestartsWhenTheBossIsFullAfterThePlayersDeath()
        {
            Equal(true, BossLifeSteps.FightRestarted(diedAt: 5000, hurtAt: 4800, engaged: true, percent: 100), "full after the death");
            Equal(false, BossLifeSteps.FightRestarted(diedAt: 5000, hurtAt: 4800, engaged: true, percent: 99), "still hurt");
            Equal(false, BossLifeSteps.FightRestarted(diedAt: null, hurtAt: 4800, engaged: true, percent: 100), "a heal with no death");
            Equal(false, BossLifeSteps.FightRestarted(diedAt: 5000, hurtAt: 9000, engaged: true, percent: 100), "hurt again after the death, then healed");
            Equal(false, BossLifeSteps.FightRestarted(diedAt: 5000, hurtAt: 0, engaged: false, percent: 100), "never engaged");
        }

        private static void LifeStepsComeBackAfterAHealNotAJitter()
        {
            Equal(30, BossLifeSteps.Next(BossLifeSteps.Full, 25), "a big hit passes several steps at once");
            Equal(75, BossLifeSteps.Next(BossLifeSteps.Full, 70), "the first step");
            Equal(BossLifeSteps.Full, BossLifeSteps.Next(BossLifeSteps.Full, 100), "nothing at full life");
            Equal(30, BossLifeSteps.Next(30, 31), "regeneration across a step does not repeat it");
            Equal(30, BossLifeSteps.Next(30, 39), "nor up to the next step");
            Equal(50, BossLifeSteps.Next(30, 40), "healed back to the step above: it comes again");
            Equal(BossLifeSteps.Full, BossLifeSteps.Next(30, 100), "full again (the fight restarted after the player died): all of them");
            Equal(75, BossLifeSteps.Next(BossLifeSteps.Next(30, 100), 74), "then said again from the top");
            Equal(0, BossLifeSteps.Next(0, 100), "a dead boss read alive for a moment stays dead");
        }
    }
}
