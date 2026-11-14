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

    /// <summary>Map markers, buffs and animations, wall bumps, code-field signatures and layout overrides.</summary>
    public static class CodeFieldAndMarkerTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(MapMarkersFindTheirCheckpoints), MapMarkersFindTheirCheckpoints),
            (nameof(CheckpointsAreNamedByTheirNeighbour), CheckpointsAreNamedByTheirNeighbour),
            (nameof(OnlyHarmfulAnimationsWarn), OnlyHarmfulAnimationsWarn),
            (nameof(BuffIdsNameAilments), BuffIdsNameAilments),
            (nameof(WallBumpThudsOncePerContact), WallBumpThudsOncePerContact),
            (nameof(FriendsGetTheirOwnStream), FriendsGetTheirOwnStream),
            (nameof(CodeFieldsNeedTwoSignaturesThatAgree), CodeFieldsNeedTwoSignaturesThatAgree),
            (nameof(TheStunRecordIsFoundByItsHead), TheStunRecordIsFoundByItsHead),
            (nameof(TheGridMustHoldWhereThePlayerWalks), TheGridMustHoldWhereThePlayerWalks),
            (nameof(CodeFieldCatalogParses), CodeFieldCatalogParses),
            (nameof(CodeFieldsSettleEachFieldApart), CodeFieldsSettleEachFieldApart),
            (nameof(LayoutOverridesCountOnlyForTheirBuild), LayoutOverridesCountOnlyForTheirBuild),
        };

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

            // Past the pairing budget the cheaper match still names them (it named none before).
            const int Many = 16;
            var ring = Enumerable.Range(0, Many).Select(i => V(MathF.Cos(i * 2 * MathF.PI / Many) * 1000, MathF.Sin(i * 2 * MathF.PI / Many) * 1000)).ToArray();
            var shuffled = Enumerable.Range(0, Many).Select(i => (i * 5) % Many).ToArray();
            var many = shuffled.Select(i => ring[i] / 20).ToArray();
            Equal(string.Join(",", shuffled), string.Join(",", MapPlaces.Assign(many, ring)), "sixteen checkpoints past the budget are each named");
            Equal("-1", string.Join(",", MapPlaces.Greedy(new[] { V(-30, -30) }, new[] { V(100, 0) })), "the cheap match leaves a marker with nothing in its direction unnamed");
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
            Equal(true, StatusAilments.EndHoldMs(StatusAilment.Chilled) > StatusAilments.EndHoldMs(StatusAilment.Frozen), "a chill each hit renews waits longer for its end than a freeze, whose end frees the player");
            Equal(StatusAilments.EndHoldMs(StatusAilment.Frozen), StatusAilments.EndHoldMs(StatusAilment.Overheated), "the weapon ready is said as soon");
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
    }
}
