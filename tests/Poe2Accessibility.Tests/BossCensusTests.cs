namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using static Check;

    /// <summary>The census's class rules in the boss-mechanics extractor (docs/research/boss-mechanics.md §15): scripts, rooms and the lines each rule makes.</summary>
    public static class BossCensusTests
    {
        private const string Boss = "Metadata/Monsters/Test/TestBoss";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AScriptGivesItsStatesHandlersAndComponents), AScriptGivesItsStatesHandlersAndComponents),
            (nameof(SharedBasesAreLeftOutOfAFightsOwnStates), SharedBasesAreLeftOutOfAFightsOwnStates),
            (nameof(ARoomGivesItsObjectsAndPoints), ARoomGivesItsObjectsAndPoints),
            (nameof(TheArenaIsTheRoomHoldingTheFightsObjects), TheArenaIsTheRoomHoldingTheFightsObjects),
            (nameof(ASkillFamilyIsItsFirstTwoWords), ASkillFamilyIsItsFirstTwoWords),
            (nameof(AHelperWithAnAreaSkillIsADaemon), AHelperWithAnAreaSkillIsADaemon),
            (nameof(AnAreaBuffShieldsOrMarksAZone), AnAreaBuffShieldsOrMarksAZone),
            (nameof(AnAddReachingBackToTheBossIsConsumed), AnAddReachingBackToTheBossIsConsumed),
            (nameof(ABuffsOwnWordsSayEmpoweredShelteredStackedOrHeld), ABuffsOwnWordsSayEmpoweredShelteredStackedOrHeld),
            (nameof(ACoverIsABlockageSpawnedBeforeALongBlast), ACoverIsABlockageSpawnedBeforeALongBlast),
            (nameof(ObjectsToUseOrBreakSkipWaysOutAndLoot), ObjectsToUseOrBreakSkipWaysOutAndLoot),
            (nameof(ASummonedBoneWallIsAWall), ASummonedBoneWallIsAWall),
            (nameof(UnavoidableAndGrabSkillsAreNamed), UnavoidableAndGrabSkillsAreNamed),
            (nameof(ArenaStatesNameTheirChangeNotTheFightsEnd), ArenaStatesNameTheirChangeNotTheFightsEnd),
            (nameof(AProgressStateKeepsTheMaximumItIsComparedWith), AProgressStateKeepsTheMaximumItIsComparedWith),
            (nameof(AnElementStateNamesEachValuesElement), AnElementStateNamesEachValuesElement),
            (nameof(TheTrueCloneIsTheStateACloneKindDeclares), TheTrueCloneIsTheStateACloneKindDeclares),
            (nameof(ARefugeIsThePersonTheShelterIsNamedFor), ARefugeIsThePersonTheShelterIsNamedFor),
            (nameof(TheBuffsWithWordsToSayAreThoseOfTheTextKinds), TheBuffsWithWordsToSayAreThoseOfTheTextKinds),
        };

        private static readonly string[] None = Array.Empty<string>();

        private static BossScriptChain Script(string text, string path = "Metadata/Test/Script") => new(new[] { BossScriptFacts.Parse(path, text) });

        private static readonly BossScriptChain NoScript = new(Array.Empty<BossScriptFacts>());

        private static BossSkill Skill(string id, bool area = false, int length = 0, IReadOnlyList<BossGeal>? geals = null, IReadOnlyList<string>? objects = null, bool unavoidable = false) =>
            new(id, "geometry_attack", area, length, geals ?? Array.Empty<BossGeal>(), objects ?? None, None, None, None, None, unavoidable);

        private static BossAdd Add(string id, string name, IReadOnlyList<BossSkill>? skills = null, BossScriptChain? script = null, bool daemon = false, bool inArena = true) =>
            new(id, name, false, (skills ?? Array.Empty<BossSkill>()).Select(skill => skill.Id).ToList(), skills ?? Array.Empty<BossSkill>(), script ?? NoScript, daemon, inArena);

        private static BossFight Fight(
            BossScriptChain? script = null, IReadOnlyList<BossSkill>? skills = null, IReadOnlyList<BossAdd>? adds = null, IReadOnlyList<BossObject>? objects = null,
            IReadOnlyList<BossGeal>? orphans = null, IReadOnlyList<BossBuff>? buffs = null) =>
            new(Boss, script ?? NoScript, skills ?? Array.Empty<BossSkill>(), adds ?? Array.Empty<BossAdd>(), objects ?? Array.Empty<BossObject>(),
                orphans ?? Array.Empty<BossGeal>(), buffs ?? Array.Empty<BossBuff>());

        private static List<string> Lines(BossFight fight, string kind) => BossCensusRules.Lines(fight).Where(line => line.StartsWith(kind + "|", StringComparison.Ordinal)).ToList();

        private const string ObjectScript = """
            version 3
            extends "Metadata/Utility/InanimateObject"
            // a comment with on_fake_handler = { }
            TriggerableBlockage
            {
            	start_blocked = true
            }
            StateMachine
            {
            	define_shared_state = "chosen_one; count = 3;"
            }
            client
            {
            	StateMachine
            	{
            		on_or_create_state_chosen_one_1 = { PlayAnimation( glow ); }
            		on_gain_shield_buff = { AddBuff( shield_buff, 1 ); }
            	}
            }
            Preload
            {
            	preload_object = "Metadata/Test/Rock"
            }
            """;

        private static void AScriptGivesItsStatesHandlersAndComponents()
        {
            var facts = BossScriptFacts.Parse("Metadata/Test/Clone", ObjectScript);
            Equal("Metadata/Utility/InanimateObject", string.Join(",", facts.Extends), "its parent");
            Equal("chosen_one,count=3", string.Join(",", facts.States.Select(state => state.Default == null ? state.Name : $"{state.Name}={state.Default}")), "states and a default");
            Equal("on_or_create_state_chosen_one_1,on_gain_shield_buff", string.Join(",", facts.Handlers.Select(handler => handler.Name)), "handlers, none from the comment");
            Equal(true, facts.Handlers[0].Body.Contains("PlayAnimation", StringComparison.Ordinal), "a handler's body");
            Equal(true, facts.Sections.Contains("TriggerableBlockage") && facts.Sections.Contains(BossScriptFacts.ClientSection), "its components");
            Equal(true, facts.Has("start_blocked", "true"), "a field");
            Equal("Metadata/Test/Rock", facts.Preloads.Single().Value, "its preload");
            Equal(("chosen_one", 1), BossScriptFacts.StateOf("on_or_create_state_chosen_one_1")!.Value, "a state handler's state and value");
            Equal(("arena_sector", 0), BossScriptFacts.StateOf("on_exit_state_arena_sector_0")!.Value, "an exit handler");
            Equal(false, BossScriptFacts.StateOf("on_gain_shield_buff").HasValue, "no state handler");
        }

        private static void SharedBasesAreLeftOutOfAFightsOwnStates()
        {
            var scripts = new Dictionary<string, BossScriptFacts>
            {
                ["Metadata/Test/Boss"] = BossScriptFacts.Parse("Metadata/Test/Boss", "extends \"Metadata/Monsters/BossBase\"\nStateMachine\n{\n\tdefine_shared_state = \"own_state;\"\n}\n"),
                ["Metadata/Monsters/BossBase"] = BossScriptFacts.Parse("Metadata/Monsters/BossBase", "Positioned\n{\n\tblocking = true\n}\nStateMachine\n{\n\tdefine_shared_state = \"arena_sector_id;\"\n}\n"),
            };
            var shared = BossScriptChain.SharedBases(Enumerable.Repeat<IReadOnlyList<string>>(new[] { "Metadata/Monsters/BossBase" }, BossScriptChain.SharedBase)
                .Append(new[] { "Metadata/Test/Rare" }));
            Equal("metadata/monsters/bossbase", string.Join(",", shared).ToLowerInvariant(), "a base extended by many, not a rare parent");
            var chain = BossScriptChain.Of("Metadata/Test/Boss", path => scripts.GetValueOrDefault(path), shared.Contains);
            Equal("own_state", string.Join(",", chain.States.Select(state => state.Name)), "the base's states are no fight's own");
            Equal(true, chain.Has("blocking", "true"), "its components still count");
        }

        private static void ARoomGivesItsObjectsAndPoints()
        {
            var room = BossArenaRooms.Parse("metadata/terrain/test/rooms/unique/boss_cart.arm", string.Join("\n",
                "version 36",
                "\"Metadata/Terrain/Desert/FarudinCarts/cart_abyss.gt\"",
                "104 266 0 \"mapboss\"",
                "103 195 3.12463 \"telepad1\"",
                "110 119 0 \"Metadata/Monsters/NPC/MarakethMatriarchDreadnought_\"",
                "160 537 0 -3.14159 0 0 1 0 0 0 1 288.044 0.75 \"Metadata/Monsters/DreadnoughtLizard/DreadnoughtLizard.ao\" \"Metadata/Terrain/Gallows/Act2/2_12_1/Objects/Pull_Team\" 0",
                "\"arena\" 1 2 3 4 \"\""));
            Equal("Metadata/Monsters/NPC/MarakethMatriarchDreadnought_,Metadata/Terrain/Gallows/Act2/2_12_1/Objects/Pull_Team", string.Join(",", room.Objects), "placed objects, by their entity paths");
            Equal("mapboss,telepad1", string.Join(",", room.Points), "named points");
        }

        private static void TheArenaIsTheRoomHoldingTheFightsObjects()
        {
            var arena = new BossArenaRooms.Room("metadata/terrain/act/area/rooms/unique/fight.arm", new[] { "Metadata/Monsters/Test/Objects/Lever", "Metadata/Monsters/Test/TestBoss" }, None);
            var named = new BossArenaRooms.Room("metadata/terrain/act/area/rooms/unique/boss_01.arm", new[] { "Metadata/Terrain/Doodads/Rock" }, None);
            var plain = new BossArenaRooms.Room("metadata/terrain/act/area/rooms/fill_01.arm", new[] { "Metadata/Terrain/Act/Objects/Crate" }, None);
            var elsewhere = new BossArenaRooms.Room("metadata/terrain/other/rooms/arena.arm", new[] { "Metadata/Test/Objects/Altar", "Metadata/Terrain/Doodads/Pillar_Doodad" }, None);
            var outsideNamed = new BossArenaRooms.Room("metadata/terrain/other/rooms/boss_02.arm", new[] { "Metadata/Terrain/Other/Objects/Crate" }, None);
            var rooms = BossArenaRooms.Rooms.Of(new[] { arena, named, plain, elsewhere, outsideNamed });
            var found = BossArenaRooms.Of("Metadata/Monsters/Test", new HashSet<string> { "metadata/test/objects/altar" }, new[] { "metadata/terrain/act/area/" }, rooms);
            Equal("fight.arm,boss_01.arm,arena.arm", string.Join(",", found.Select(room => room.Path[(room.Path.LastIndexOf('/') + 1)..])),
                "the boss's folder first, then (by path) the area's room named for a boss and a room elsewhere placing the fight's object; not a plain room");
            var objects = BossArenaRooms.ObjectsOf(found, "Metadata/Monsters/Test", path => path.EndsWith("TestBoss", StringComparison.Ordinal)).ToList();
            Equal("Metadata/Monsters/Test/Objects/Lever,Metadata/Test/Objects/Altar", string.Join(",", objects), "objects, no monster, no scenery, no prop of another family");
        }

        private static void ASkillFamilyIsItsFirstTwoWords()
        {
            Equal("PerennialKing", BossFamilies.Of("GSPerennialKingSwordSlam"), "the kind's capitals dropped");
            Equal("PerennialKing", BossFamilies.Of("EGPerennialKingAsalaStun"), "Asala's skill of Jamanra's family");
            Equal<string?>(null, BossFamilies.Of("Melee"), "one word is no family");
        }

        private static void AHelperWithAnAreaSkillIsADaemon()
        {
            var storm = Skill("GSStormContact", area: true);
            var fight = Fight(adds: new[]
            {
                Add("Metadata/Monsters/InvisibleFire/MDStorm", "Invisible", new[] { storm, Skill("MPSStormBolt") }),
                Add("Metadata/Monsters/Test/Wave", "Volatile Core", new[] { storm }, daemon: true),
                Add("Metadata/Monsters/Test/Brute", "Brute", new[] { storm }),
                Add("Metadata/Monsters/InvisibleFire/MDNothing", "Invisible", new[] { Skill("MPSBolt") }),
            });
            Equal($"daemon|{Boss}|Metadata/Monsters/InvisibleFire/MDStorm|GSStormContact,MPSStormBolt;daemon|{Boss}|Metadata/Monsters/Test/Wave|GSStormContact",
                string.Join(";", Lines(fight, "daemon")), "invisible or move-daemon helpers with an area skill, their skills listed; not a named add, not one without an area");
        }

        private static void AnAreaBuffShieldsOrMarksAZone()
        {
            var shield = new BossGeal("1124", None, string.Empty, "triggerer.AddBuff( asala_sand_shield, 0.15 );", None);
            var horn = new BossGeal("1062", None, string.Empty, "triggerer.AddBuff( karui_horn_buff, 4, 10 );", None);
            var poison = new BossGeal("7", None, string.Empty, "triggerer.AddBuff( poison_safe_zone, 1 );", None);
            var onObject = new BossGeal("8", new[] { "Metadata/Test/Objects/Censer" }, string.Empty, "triggerer.AddBuff( asala_sand_shield, 1 );", None);
            var fight = Fight(skills: new[] { Skill("GSHorn", geals: new[] { horn, onObject }) }, orphans: new[] { shield, poison },
                buffs: new[] { new BossBuff("poison_safe_zone", "You take damage over time", false, Harmful: true) });
            Equal($"shelter|{Boss}|asala_sand_shield", string.Join(";", Lines(fight, "shelter")), "a shielding buff on whoever stands inside");
            Equal($"zone|{Boss}|karui_horn_buff;zone|{Boss}|poison_safe_zone", string.Join(";", Lines(fight, "zone")), "any other, a harmful one named safe too; none on a listed object");
        }

        private static void AnAddReachingBackToTheBossIsConsumed()
        {
            var barrier = new BossGeal("1088", None, string.Empty, "triggerer.MakeBeam( Beam, snake_boss, Flip, 0, 4 );", new[] { "Metadata/Monsters/Test/Minion" });
            var stun = new BossGeal("1025", None, string.Empty, "triggerer.SetStateTo( corrupting, 0 ); boss.Stun();", new[] { Boss });
            var kill = new BossGeal("271", None, string.Empty, "triggerer.KillSelf();", new[] { "Metadata/Monsters/Test/Minion" });
            var fight = Fight(skills: new[] { Skill("GSBarrier", geals: new[] { barrier, stun, kill }) });
            Equal($"consume|{Boss}|Metadata/Monsters/Test/Minion", string.Join(";", Lines(fight, "consume")), "an area on adds that reaches back to the boss; not one on the boss itself, not a plain kill");
        }

        private static void ABuffsOwnWordsSayEmpoweredShelteredStackedOrHeld()
        {
            var fight = Fight(buffs: new[]
            {
                new BossBuff("guard_boss_utility_buff", "Twilight Priests are empowering Torvian with immunity to stun and ailments. Twilight Order's Faith", false, false),
                new BossBuff("sudoku_display", "Protected by Ancestral Warriors", false, false),
                new BossBuff("sand_storm", "You are taking Physical damage over time while standing in the Sandstorm.", false, true),
                new BossBuff("sanctuary", "You are within Viridi's Sanctum, gaining significant Life, Mana and Energy Shield Regeneration", false, false),
                new BossBuff("vapors", "You take increased damage for each stack of Portent Vapors", true, true),
                new BossBuff("uncounted", "for each stack", false, false),
                new BossBuff("cocoon", "You are entombed in webs", false, true),
                new BossBuff("punishment", "Stop moving or you will be imprisoned.", false, false),
            });
            Equal($"empowered|{Boss}|guard_boss_utility_buff;empowered|{Boss}|sudoku_display", string.Join(";", Lines(fight, "empowered")), "others empower or protect it; standing in a storm is no empowering");
            Equal($"shelter|{Boss}|sanctuary", string.Join(";", Lines(fight, "shelter")), "within a place that protects");
            Equal($"stack|vapors|{Boss}", string.Join(";", Lines(fight, "stack")), "a counted buff in stacks, the buff first");
            Equal($"grabbed|{Boss}|cocoon", string.Join(";", Lines(fight, "grabbed")), "the player held now, not one warned of a hold");
        }

        private static void ACoverIsABlockageSpawnedBeforeALongBlast()
        {
            var sword = new BossObject("Metadata/Monsters/Test/Sword", Script(ObjectScript));
            var rock = new BossObject("Metadata/Monsters/Test/Rock", Script("Positioned\n{\n\tblocking = true\n}\n"));
            var spawn = Skill("SOSword", objects: new[] { sword.Path, rock.Path });
            Equal(string.Empty, string.Join(";", Lines(Fight(skills: new[] { spawn, Skill("GSBlast", area: true, length: 59) }, objects: new[] { sword, rock }), "cover")), "no area long enough to hide from");
            Equal($"cover|{Boss}|Metadata/Monsters/Test/Sword", string.Join(";", Lines(Fight(skills: new[] { spawn, Skill("GSBlast", area: true, length: BossCensusRules.CoverSkillLength) }, objects: new[] { sword, rock }), "cover")),
                "the blockage that starts blocked; a merely blocking rock is no cover");
        }

        private static void ObjectsToUseOrBreakSkipWaysOutAndLoot()
        {
            var objects = new[]
            {
                new BossObject("Metadata/Terrain/Act/Objects/Lever", Script("InteractionAction\n{\n}\n")),
                new BossObject("Metadata/Monsters/Test/Objects/BossLootProxy", Script("InteractionAction\n{\n}\n")),
                new BossObject("Metadata/Monsters/Test/Objects/ArenaTransition", Script("InteractionAction\n{\n}\n")),
                new BossObject("Metadata/Monsters/Test/Objects/Censer", Script("Life\n{\n}\n")),
                new BossObject("Metadata/Monsters/Test/Objects/ServerOnly", Script("Life\n{\n}\nBase\n{\n\tserver_only = true\n}\n")),
                new BossObject("Metadata/Monsters/Test/Objects/Unknown", NoScript),
            };
            var adds = new[] { Add("Metadata/Monsters/Test/Totem", "Aura Totem", script: Script("Life\n{\n}\n")), Add("Metadata/Monsters/Test/Helper", "Invisible", script: Script("Life\n{\n}\n")) };
            var fight = Fight(objects: objects, adds: adds);
            Equal($"use|{Boss}|Metadata/Terrain/Act/Objects/Lever", string.Join(";", Lines(fight, "use")), "an object to use, not the loot or the way out");
            Equal($"target|{Boss}|Metadata/Monsters/Test/Objects/Censer;target|{Boss}|Metadata/Monsters/Test/Totem", string.Join(";", Lines(fight, "target")),
                "an object with life the client sees, and a skill-less add with life; not a helper");
        }

        private static void ASummonedBoneWallIsAWall()
        {
            var wall = Script("extends \"Metadata/Utility/InanimateObject\"\nPositioned\n{\n\tblocking = true\n}\nLife\n{\n}\n");
            var fight = Fight(adds: new[] { Add("Metadata/Monsters/WifeMonster/WifeMonsterBoneWall", "Bone Wall", script: wall), Add("Metadata/Monsters/Test/Effigy", "Effigy", new[] { Skill("GSPulse") }, wall) });
            Equal($"wall|{Boss}|Metadata/Monsters/WifeMonster/WifeMonsterBoneWall", string.Join(";", Lines(fight, "wall")), "inanimate, blocking and skill-less, however brought; an effigy that pulses is none");
            Equal(string.Empty, string.Join(";", Lines(fight, "target")), "a wall is said as a wall, not as a thing to break");
        }

        private static void UnavoidableAndGrabSkillsAreNamed()
        {
            var fight = Fight(skills: new[] { Skill("GAKingSwordSlam", unavoidable: true), Skill("HellscapeFleshBossTentacleGrab"), Skill("GSPrisonerSlam") });
            Equal($"unavoidable|{Boss}|GAKingSwordSlam", string.Join(";", Lines(fight, "unavoidable")), "cannot be dodged, evaded or blocked");
            Equal($"grab|{Boss}|HellscapeFleshBossTentacleGrab", string.Join(";", Lines(fight, "grab")), "a hold by its name; the Prisoner is no prison");
        }

        private static void ArenaStatesNameTheirChangeNotTheFightsEnd()
        {
            var script = Script("""
                StateMachine
                {
                	define_shared_state = "room_burning_progress; darkness_vacuum_range; fog_active;"
                	on_or_create_state_first_arena_complete_1 = { TriggerBracket( arena_two ); }
                	on_or_create_state_dead_1 = { TriggerBracket( exit ); }
                	on_or_create_state_retreat_caravan_1 = { TriggerBracket( caravan ); }
                }
                Life
                {
                	on_defeat = { TriggerBracket( arena_sector_destruction ); }
                }
                """);
            Equal($"arena|{Boss}|first_arena_complete|change;arena|{Boss}|retreat_caravan|retreat;arena|{Boss}|room_burning_progress|burn",
                string.Join(";", Lines(Fight(script: script), "arena")), "brackets and named changes; not death, a range or fog");
            Equal("dark", BossStateRules.KindOf("darkness"), "a word's start names the kind");
            Equal("change", BossStateRules.KindOf("arena_sector"), "an arena state that says nothing more");
        }

        private static void AProgressStateKeepsTheMaximumItIsComparedWith()
        {
            var script = Script("StateMachine\n{\n\tdefine_shared_state = \"collect_count; timetrialprogress; encounter;\"\n\ton_state_encounter_1 = { IfState( collect_count, 5, () { Win(); } ); If( GetState( collect_count ) >= 3, () {} ); }\n}\n");
            Equal($"progress|{Boss}|collect_count|5;progress|{Boss}|timetrialprogress", string.Join(";", Lines(Fight(script: script), "progress")), "the largest number compared with, else none");
        }

        private static void AnElementStateNamesEachValuesElement()
        {
            var named = Script("StateMachine\n{\n\tdefine_shared_state = \"element;\"\n\ton_or_create_state_element_1 = { SetLightState( fire, 1.0 ); }\n\ton_or_create_state_element_2 = { AddEffectPack( 'epks/infused_ice.epk' ); }\n" +
                               "\ton_or_create_state_element_3 = { SetLightState( fire ); SetLightState( cold ); }\n\ton_or_create_state_element_4 = { SetLightState( default ); }\n}\n");
            Equal($"element|{Boss}|element=1|fire;element|{Boss}|element=2|cold", string.Join(";", Lines(Fight(script: named), "element")), "one element a value; two or none say nothing");
            Equal($"element|{Boss}|elemental|-", string.Join(";", Lines(Fight(script: Script("StateMachine\n{\n\tdefine_shared_state = \"elemental = 1;\"\n}\n")), "element")), "a state no handler names");
        }

        private static void TheTrueCloneIsTheStateACloneKindDeclares()
        {
            var clone = new BossObject("Metadata/Monsters/YamaBoss/objects/YamaFutureClone", Script(ObjectScript));
            var notClone = new BossObject("Metadata/Monsters/YamaBoss/objects/YamaPlayerProxy", Script(ObjectScript));
            Equal($"clone|{Boss}|Metadata/Monsters/YamaBoss/objects/YamaFutureClone|chosen_one", string.Join(";", Lines(Fight(objects: new[] { clone, notClone }), "clone")), "a clone's marking state");
        }

        private static void ARefugeIsThePersonTheShelterIsNamedFor()
        {
            var asala = Script("StateMachine\n{\n\tdefine_shared_state = \"sandstorm_defence; spawned;\"\n}\nclient\n{\n\tStateMachine\n\t{\n\t\ton_state_sandstorm_defence_1 = { PlayTextAudio( AsalaWildGatesTen, 'x' ); }\n\t\ton_state_spawned_1 = { PlayTextAudio( Hello, 'x' ); }\n\t}\n}\n");
            var shelter = new BossGeal("1124", None, string.Empty, "triggerer.AddBuff( asala_sand_shield, 0.15 );", None);
            var add = Add("Metadata/Monsters/NPC/MarakethMatriarchDreadnought_", "Sekhema Asala", script: asala, inArena: false);
            var marker = new BossObject("Metadata/Monsters/PerennialKingStage2/Objects/MatriarchSandstormDefence", NoScript);
            Equal($"refuge|{Boss}|Metadata/Monsters/NPC/MarakethMatriarchDreadnought_|sandstorm_defence",
                string.Join(";", Lines(Fight(adds: new[] { add }, objects: new[] { marker }, orphans: new[] { shelter }), "refuge")), "her defence state, named by the fight's own object");
            Equal(string.Empty, string.Join(";", Lines(Fight(adds: new[] { add }, orphans: new[] { shelter }), "refuge")), "not in this fight: an NPC of another act sharing the skills");
            Equal($"refuge|{Boss}|Metadata/Monsters/NPC/MarakethMatriarchDreadnought_|sandstorm_defence",
                string.Join(";", Lines(Fight(adds: new[] { add with { InArena = true } }, orphans: new[] { shelter }), "refuge")), "placed in the arena");
        }

        private static void TheBuffsWithWordsToSayAreThoseOfTheTextKinds()
        {
            var lines = new[] { $"empowered|{Boss}|a", $"stack|b|{Boss}", $"zone|{Boss}|c", $"grabbed|{Boss}|d", $"shelter|{Boss}|a", $"daemon|{Boss}|x|y", "move|e|stop" };
            Equal("a,b,c,d", string.Join(",", BossCensusRules.TextBuffs(lines)), "each once, the stack's buff from its second field");
            Equal("Movement speed Slowed. x y", BossScripts.Clean(GameMarkup.Plain("Movement speed [Slow|Slowed]. x|y\r\n")), "a link as shown, then no separator or break");
        }
    }
}
