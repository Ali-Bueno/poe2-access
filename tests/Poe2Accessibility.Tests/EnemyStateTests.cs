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

    /// <summary>Weak points, culling, cooldowns and charges, enemy states, shields, stun, parry and hazard sounds.</summary>
    public static class EnemyStateTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(WeakPointLeadsToTheHead), WeakPointLeadsToTheHead),
            (nameof(WeakPointBeatsFasterThenSteady), WeakPointBeatsFasterThenSteady),
            (nameof(SpiritValuesNameTheirWeaponSet), SpiritValuesNameTheirWeaponSet),
            (nameof(CullingThresholdsByRarity), CullingThresholdsByRarity),
            (nameof(CullingSkillsComeFromTheCatalog), CullingSkillsComeFromTheCatalog),
            (nameof(CrossingAnAreaLeadsToTheWayOn), CrossingAnAreaLeadsToTheWayOn),
            (nameof(ExploredGroundIsKeptAsBytes), ExploredGroundIsKeptAsBytes),
            (nameof(PlacedObjectsSoundByDistance), PlacedObjectsSoundByDistance),
            (nameof(SkillsAreReadyOnceTheirCooldownEnds), SkillsAreReadyOnceTheirCooldownEnds),
            (nameof(ChargesAreKnownByTheirBuff), ChargesAreKnownByTheirBuff),
            (nameof(EnemyStatesFromBuffsAndAnimations), EnemyStatesFromBuffsAndAnimations),
            (nameof(PrimedForStunByRarity), PrimedForStunByRarity),
            (nameof(EnemyStatesAreNewsOnlyAsTheyBegin), EnemyStatesAreNewsOnlyAsTheyBegin),
            (nameof(ShieldRaisedAndLowered), ShieldRaisedAndLowered),
            (nameof(StunBarSpeaksEachStepOnce), StunBarSpeaksEachStepOnce),
            (nameof(ParryCueOnlyForBlowsToParry), ParryCueOnlyForBlowsToParry),
            (nameof(UnblockableAttacksAreNeverToParry), UnblockableAttacksAreNeverToParry),
            (nameof(HarmfulGroundIsKnownByItsBuff), HarmfulGroundIsKnownByItsBuff),
            (nameof(HazardAlarmLoopsWithoutASeam), HazardAlarmLoopsWithoutASeam),
            (nameof(ParryCueIsItsOwnSound), ParryCueIsItsOwnSound),
        };

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

            const long sixSeconds = 6000;
            cooldowns.Update(Usable(false), 30000, sixSeconds);
            Equal(0, cooldowns.Update(Usable(true), 33400, sixSeconds).Count, "a 3.4 s cooldown is under the shortest worth a line");
            cooldowns.Update(Usable(false), 40000, sixSeconds);
            Equal(grenade, cooldowns.Update(Usable(true), 46000, sixSeconds).Single(), "a cooldown of the shortest is announced");
            cooldowns.Update(Usable(false), 50000, 0);
            Equal(grenade, cooldowns.Update(Usable(true), 53400, 0).Single(), "0 announces every skill");
            cooldowns.Reset();
            cooldowns.Update(Usable(false), 60000, sixSeconds);
            Equal(grenade, cooldowns.Update(Usable(true), 60100, sixSeconds).Single(), "spent before the area: its length is unknown, so its end is news");
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
            var full = new FullEdges();
            Equal(true, full.Reached("rage", 30, 30), "full is news");
            Equal(false, full.Reached("rage", 29, 30) || full.Reached("rage", 30, 30), "a point spent and regained is the same fullness");
            Equal(false, full.Reached("rage", 23, 30), "spent under four fifths");
            Equal(true, full.Reached("rage", 30, 30), "full again is news again");
            Equal(false, full.Reached("totems", 0, 0), "no top, never full");
            Equal(false, full.Reached("rage", 30, 40), "the top raised while full: no longer full");
            Equal(true, full.Reached("rage", 40, 40), "full at the higher top is news");
            Equal(false, full.Reached("rage", 40, 30) || full.Reached("rage", 40, 40), "a top that fell and came back is the same fullness");
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
    }
}
