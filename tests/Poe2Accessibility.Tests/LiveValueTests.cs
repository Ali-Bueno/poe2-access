namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using GameReader.RemoteObjects.States.InGameStateObjects;
    using GameReader.Utils;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>Values that must follow the game, not a table or an old reading (docs/research/robustness-audit-2026-11-10.md, stale values).</summary>
    public static class LiveValueTests
    {
        private const string Life = "Frasco de vida grandioso";
        private const string Mana = "Frasco de maná mayor";

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ABuffAButtonPutsOnIsNotToldEachTime), ABuffAButtonPutsOnIsNotToldEachTime),
            (nameof(ABuffThatStaysIsTold), ABuffThatStaysIsTold),
            (nameof(AFlaskCountsUsesOnlyOnceTwoDrinksAgreeOnTheCost), AFlaskCountsUsesOnlyOnceTwoDrinksAgreeOnTheCost),
            (nameof(AFlaskIsEmptyOnlyBelowTheCostSeen), AFlaskIsEmptyOnlyBelowTheCostSeen),
            (nameof(AChargeGainedDuringADrinkDoesNotChangeTheCost), AChargeGainedDuringADrinkDoesNotChangeTheCost),
            (nameof(ADrainTeachesNoCost), ADrainTeachesNoCost),
            (nameof(FlaskNewsIsToldBySlot), FlaskNewsIsToldBySlot),
            (nameof(AFlasksCostOutlivesAnAreaButNotACharacter), AFlasksCostOutlivesAnAreaButNotACharacter),
            (nameof(ABossNotThereToReadIsNotSaid), ABossNotThereToReadIsNotSaid),
            (nameof(TheBossBeingFoughtIsTheOneSaid), TheBossBeingFoughtIsTheOneSaid),
            (nameof(ATargetNameSharedByAPackIsNoOnesLife), ATargetNameSharedByAPackIsNoOnesLife),
            (nameof(AnEntityNameBelongsToItsAddressAndId), AnEntityNameBelongsToItsAddressAndId),
            (nameof(AnotherInstanceOfTheSameAreaIsToldOnce), AnotherInstanceOfTheSameAreaIsToldOnce),
            (nameof(AnUntypedEntitysComponentsRestLikeATypedOnes), AnUntypedEntitysComponentsRestLikeATypedOnes),
            (nameof(ABuffHeldAsSeveralEntriesCountsEveryOne), ABuffHeldAsSeveralEntriesCountsEveryOne),
            (nameof(ABlowIsLearntAsItsPlaceInTheClip), ABlowIsLearntAsItsPlaceInTheClip),
            (nameof(AMaximumFromTheTablesIsNeverSaidAsFull), AMaximumFromTheTablesIsNeverSaidAsFull),
            (nameof(ABuffIdThatEndsLikeASuffixIsStillItself), ABuffIdThatEndsLikeASuffixIsStillItself),
            (nameof(AnAreaHashIsAnotherOnceReadTwice), AnAreaHashIsAnotherOnceReadTwice),
        };

        /// <summary>A flask whose drinks were seen to cost <paramref name="cost"/>, left at <paramref name="charges"/>; the clock ends past the last drink.</summary>
        private static (FlaskDrinks Drinks, long Now) Learnt(int slot, string name, int charges, int cost)
        {
            var drinks = new FlaskDrinks();
            drinks.Update(slot, name, charges + (2 * cost), 0);
            drinks.Update(slot, name, charges + cost, 10_000);
            drinks.Update(slot, name, charges, 20_000);
            drinks.Update(slot, name, charges, 20_000 + FlaskDrinks.DrinkAloneMs);
            return (drinks, 30_000);
        }

        private static void AFlaskCountsUsesOnlyOnceTwoDrinksAgreeOnTheCost()
        {
            // yarkey's magic flask: "Consume 8 de 75 cargas"; its base type says 10 (7 uses, where the truth is 9).
            var drinks = new FlaskDrinks();
            Equal(FlaskNews.New, drinks.Update(0, Life, 75, 0), "first read");
            Equal<int?>(null, drinks.Uses(0, Life, 75), "no drink seen: no use count, the charges are said");
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 67, 10_000), "a drink is news before its cost is known");
            Equal(FlaskNews.None, drinks.Update(0, Life, 67, 14_000), "nothing new");
            Equal<int?>(null, drinks.Uses(0, Life, 67), "one drink could have been cut short by a charge gained: not believed alone");
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 59, 20_000), "the second drink");
            Equal<int?>(null, drinks.PerUse(0, Life), "not judged until the drop has been left alone");
            Equal(FlaskNews.None, drinks.Update(0, Life, 59, 20_000 + FlaskDrinks.DrinkAloneMs), "learning the cost is not news");
            Equal<int?>(8, drinks.PerUse(0, Life), "two drinks agree");
            Equal<int?>(7, drinks.Uses(0, Life, 59), "59 charges at 8 a drink");
            Equal<int?>(9, drinks.Uses(0, Life, 75), "a full flask: 9 drinks, not the 7 of the base type");
        }

        private static void AFlaskIsEmptyOnlyBelowTheCostSeen()
        {
            var (drinks, now) = Learnt(0, Life, 17, 8);
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 9, now), "a drink that leaves one more");
            Equal<int?>(1, drinks.Uses(0, Life, 9), "9 charges at 8 a drink: one drink left, where the 10 of the base type said empty");
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 1, now + 10_000), "the last drink: said once, empty");
            Equal<int?>(0, drinks.Uses(0, Life, 1), "empty below the cost");
            Equal(FlaskNews.None, drinks.Update(0, Life, 3, now + 11_000), "charges gained short of a drink are not news");
            Equal(FlaskNews.Refilled, drinks.Update(0, Life, 8, now + 12_000), "a drink gained");

            var unknown = new FlaskDrinks();
            unknown.Update(1, Mana, 9, 0);
            Equal(FlaskNews.None, unknown.Update(1, Mana, 9, 500), "9 charges of an unknown cost: never called empty");
            unknown.Update(1, Mana, 12, 1000);
            Equal(FlaskNews.None, unknown.Update(1, Mana, 15, 1500), "nor refilled: every charge gained would be told");
            Equal(FlaskNews.Drunk, unknown.Update(1, Mana, 0, 10_000), "no charge at all is empty whatever a drink costs");
            Equal(FlaskNews.Refilled, unknown.Update(1, Mana, 75, 20_000), "an empty flask filled at the well: said, though no drink has shown its cost");
            Equal(FlaskNews.None, unknown.Update(1, Mana, 78, 21_000), "once: the charges after it are not told");
        }

        private static void AChargeGainedDuringADrinkDoesNotChangeTheCost()
        {
            var (drinks, now) = Learnt(0, Life, 50, 8);
            drinks.Update(0, Life, 44, now); // a drink and two charges from a kill between two reads
            drinks.Update(0, Life, 44, now + 5000);
            Equal<int?>(8, drinks.PerUse(0, Life), "one odd drop is not believed");
            drinks.Update(0, Life, 36, now + 10_000);
            drinks.Update(0, Life, 36, now + 15_000);
            drinks.Update(0, Life, 30, now + 20_000);
            drinks.Update(0, Life, 30, now + 25_000);
            Equal<int?>(8, drinks.PerUse(0, Life), "nor a second one after a drink at the known cost");
            drinks.Update(0, Life, 24, now + 30_000);
            drinks.Update(0, Life, 24, now + 35_000);
            Equal<int?>(6, drinks.PerUse(0, Life), "two drinks in a row at another cost: the flask or its modifiers changed");
        }

        private static void ADrainTeachesNoCost()
        {
            var drinks = new FlaskDrinks();
            drinks.Update(0, Life, 75, 0);
            Equal(FlaskNews.Drunk, drinks.Update(0, Life, 73, 10_000), "the first drop could be a drink");
            var at = 10_500L;
            for (var charges = 71; charges > 40; charges -= 2, at += 500)
            {
                Equal(FlaskNews.None, drinks.Update(0, Life, charges, at), "a drop right after another is a drain: not told as drinks");
            }

            drinks.Update(0, Life, 41, 60_000);
            Equal<int?>(null, drinks.PerUse(0, Life), "ticks one after another teach no cost");

            var aura = new FlaskDrinks();
            aura.Update(0, Life, 75, 0);
            Equal(FlaskNews.None, aura.Update(0, Life, 70, 10_000, draining: true), "under a flask-drain effect a drop is no drink");
            aura.Update(0, Life, 65, 20_000, draining: true);
            aura.Update(0, Life, 65, 30_000);
            Equal<int?>(null, aura.PerUse(0, Life), "and teaches no cost, however alone it comes");
        }

        private static void FlaskNewsIsToldBySlot()
        {
            var (drinks, now) = Learnt(1, Mana, 60, 10);
            Equal(FlaskNews.Drunk, drinks.Update(1, Mana, 50, now), "a drink, whatever item entity the game made for it");
            Equal(FlaskNews.None, drinks.Update(1, Mana, 50, now + 500), "no change");
            Equal(FlaskNews.None, drinks.Update(1, Mana, 55, now + 1000), "charges short of a use are not news");
            Equal(FlaskNews.Refilled, drinks.Update(1, Mana, 60, now + 1500), "a use gained from kills");
            Equal(FlaskNews.New, drinks.Update(1, Life, 20, now + 2000), "another flask put in the slot");
            Equal<int?>(null, drinks.Uses(1, Life, 20), "which has its own cost to be seen");
            Equal(FlaskNews.New, drinks.Update(2, Mana, 30, now + 2500), "a flask in a slot empty until now");
        }

        private static void ABossNotThereToReadIsNotSaid()
        {
            // The player died to the Miller at 40% and respawned at the checkpoint: the record still says 40, the game says full.
            Equal<int?>(null, BossLifeSteps.Spoken(new (int?, long, long)[] { (null, 9000, 9000) }, diedAt: 9500), "out of sight: nothing is known of it now");
            Equal<int?>(null, BossLifeSteps.Spoken(new (int?, long, long)[] { (100, 9000, 9000) }, diedAt: 9500), "read back at full life after the death: the game reset the fight");
            Equal<int?>(0, BossLifeSteps.Spoken(new (int?, long, long)[] { (38, 9000, 9000) }, diedAt: null), "there and hurt: said, with the life just read");
            Equal<int?>(0, BossLifeSteps.Spoken(new (int?, long, long)[] { (100, 9000, 9000) }, diedAt: null), "healed to full with no death is still the fight");
            Equal<int?>(null, BossLifeSteps.Spoken(Array.Empty<(int?, long, long)>(), diedAt: null), "no boss engaged");
        }

        private static void TheBossBeingFoughtIsTheOneSaid()
        {
            var bosses = new (int?, long, long)[] { (40, 5000, 20_000), (72, 19_800, 20_000), (15, 21_000, 21_000) };
            Equal<int?>(2, BossLifeSteps.Spoken(bosses, diedAt: null), "the one whose life changed last, not the first engaged");
            bosses[2] = (null, 21_000, 21_000);
            Equal<int?>(1, BossLifeSteps.Spoken(bosses, diedAt: null), "one that left is passed over for one that is there");
        }

        private static void ATargetNameSharedByAPackIsNoOnesLife()
        {
            const string Wolf = "Lobo putrefacto";
            var pack = new (IntPtr, string, float)[] { (0x100, Wolf, 900f), (0x200, Wolf, 400f), (0x300, "Beira", 100f), (0x400, Wolf, 2500f) };
            Equal(((IntPtr)0x200, false), TargetMatch.Pick(Wolf, pack), "the nearest wolf stands for the name, but which wolf is aimed at is not known");
            Equal(((IntPtr)0x300, true), TargetMatch.Pick("Beira", pack), "the only one of its name is the target: its life is the target's");
            Equal((IntPtr.Zero, false), TargetMatch.Pick("Oso", pack), "no entity carries the name");
        }

        private static void AnEntityNameBelongsToItsAddressAndId()
        {
            var names = new EntityNameMemo();
            var reads = 0;
            Func<string?> wolf = () => { reads++; return "Lobo putrefacto"; };
            Equal("Lobo putrefacto", names.Of(0x100, 41, wolf), "read");
            Equal("Lobo putrefacto", names.Of(0x100, 41, wolf), "kept");
            Equal(1, reads, "one read for the same entity");
            Equal("Oso", names.Of(0x100, 77, () => "Oso"), "a new monster at the dead one's address is read again, by its id");
            Equal(string.Empty, names.Of(0x200, 5, () => null), "a name that could not be read");
            Equal("Beira", names.Of(0x200, 5, () => "Beira"), "is asked again, not kept empty for the area");
        }

        private static void AnotherInstanceOfTheSameAreaIsToldOnce()
        {
            var gate = new AreaInstanceGate();
            Equal(false, gate.Changed(string.Empty), "no instance read yet");
            Equal(false, gate.Changed("54492AF8"), "the first instance met is the one the host started in");
            gate.AreaEntered("962301CC");
            Equal(false, gate.Changed("962301CC"), "the instance of the area just entered: its change of area told it");
            Equal(false, gate.Changed("962301CC"), "a respawn at a checkpoint reloads the same instance");
            Equal(false, gate.Changed(string.Empty), "nor is a loading screen, while nothing is read");
            Equal(true, gate.Changed("D28E11CE"), "a trial's next room: the same area row, another instance");
            Equal(false, gate.Changed("D28E11CE"), "told once");

            gate.AreaEntered("D28E11CE"); // the row changed before the host read the new instance
            Equal(true, gate.Changed("A21F934"), "what was gathered from the old instance meanwhile goes too");
            gate.AreaEntered(string.Empty);
            Equal(false, gate.Changed("B59327DE"), "an area entered with no instance read yet: the first one is its own");
        }

        private static void AnUntypedEntitysComponentsRestLikeATypedOnes()
        {
            const int Rest = 100;
            Equal(EntityRefresh.Reads.Everything, EntityRefresh.OfResting(untyped: true, typingDue: true, useless: false, 0, Rest), "a new entity is read whole and typed");
            Equal(EntityRefresh.Reads.Everything, EntityRefresh.OfResting(untyped: true, typingDue: true, useless: true, 20, Rest), "as is one whose next typing attempt is due");

            // A barrier or a trap the host cannot type: its state was served up to a second old, between typing attempts.
            Equal(EntityRefresh.Reads.Components, EntityRefresh.OfResting(untyped: true, typingDue: false, useless: true, 100, Rest), "between attempts its components are read at the resting rate");
            Equal(EntityRefresh.Reads.Nothing, EntityRefresh.OfResting(untyped: true, typingDue: false, useless: true, 40, Rest), "not more often than a typed one's");

            Equal(EntityRefresh.Reads.Components, EntityRefresh.OfResting(untyped: false, typingDue: false, useless: false, 100, Rest), "a typed one, as before");
            Equal(EntityRefresh.Reads.Nothing, EntityRefresh.OfResting(untyped: false, typingDue: false, useless: false, 99, Rest), "resting");
            Equal(EntityRefresh.Reads.Nothing, EntityRefresh.OfResting(untyped: false, typingDue: false, useless: true, 5000, Rest), "an opened chest is not read at all");

            Equal(true, EntityRefresh.Rests(player: false, typedMonster: false, monsterComponent: false, monsterPath: false, untyped: true), "a door rests");
            Equal(true, EntityRefresh.Rests(player: false, typedMonster: false, monsterComponent: false, monsterPath: true, untyped: true), "as does a trap under the monsters' path that could not be typed");
            Equal(false, EntityRefresh.Rests(player: false, typedMonster: true, monsterComponent: true, monsterPath: true, untyped: false), "a monster does not");
            Equal(false, EntityRefresh.Rests(player: false, typedMonster: false, monsterComponent: true, monsterPath: true, untyped: true), "nor one known only by its component");
            Equal(false, EntityRefresh.Rests(player: false, typedMonster: false, monsterComponent: false, monsterPath: true, untyped: false), "nor a typed thing under the monsters' path");
            Equal(false, EntityRefresh.Rests(player: true, typedMonster: false, monsterComponent: false, monsterPath: false, untyped: false), "nor a player");
        }

        private static void ABuffHeldAsSeveralEntriesCountsEveryOne()
        {
            // The host keeps one entry per buff name: the first as read, each further one merged into it.
            short held = 0;
            held = BuffStacks.Merged(held, 0);
            Equal<short>(2, held, "two entries without a count are two stacks");
            held = BuffStacks.Merged(held, 0);
            Equal<short>(3, held, "three are three (they were merged to two)");
            Equal(3, PlayerCharges.Held(held), "as the plugin counts them");
            Equal<short>(5, BuffStacks.Merged(3, 2), "entries with their own counts add up");
            Equal<short>(4, BuffStacks.Merged(0, 3), "a first entry without a count is one");
            Equal(1, PlayerCharges.Held(0), "one entry alone is one, unmerged");
            Equal(short.MaxValue, BuffStacks.Merged(short.MaxValue, 5), "never past what the field holds");
        }

        private static void ABlowIsLearntAsItsPlaceInTheClip()
        {
            // A golem's slam lands 1.156 s into its clip. Chilled to 0.7 of its speed the blow is felt 1651 ms after the start.
            var chilled = new AnimationReading(Clip: 7, Position: 1.05f, Speed: 1f, GlobalSpeed: 0.7f, GlobalOff: false, Start: 0f, End: 2f);
            var learnt = AttackClock.NominalMsAt(chilled, readAt: 1500, moment: 1651);
            Equal(1156, learnt, "where the clip stood at the blow, not the 1651 ms that passed");
            Equal(500, AttackClock.MsToNominal(new AnimationReading(7, 0.656f, 1f, 1f, false, 0f, 2f), learnt), "replayed at speed 1 it warns on time");
            Equal(714, AttackClock.MsToNominal(new AnimationReading(7, 0.656f, 1f, 0.7f, false, 0f, 2f), learnt), "and chilled again, as late as the clip plays");

            var hasted = new AnimationReading(7, 1.0f, 1.3f, 1f, false, 0f, 2f);
            Equal(1156, AttackClock.NominalMsAt(hasted, readAt: 769, moment: 889), "hasted: the 889 ms that passed are 1156 of the clip");
            Equal(1100, AttackClock.NominalMsAt(new AnimationReading(7, 1.4f, 1f, 1f, false, 0.3f, 2f), readAt: 5000, moment: 5000), "counted from the clip's own start");
            Equal(1000, AttackClock.NominalMsAt(new AnimationReading(7, 1.2f, 1f, 1f, false, 0f, 2f), readAt: 5000, moment: 4800), "a projectile's release, its flight before the reading");
        }

        private static void AMaximumFromTheTablesIsNeverSaidAsFull()
        {
            const string Heat = "stat:number_of_crossbow_heat_stacks";
            ResourceTracker.Reading At(int count, int max = 0, int reach = 31) => new(Heat, count, max, ResourceTracker.ModeFor(Math.Max(max, reach)), reach);
            var tracker = new ResourceTracker();
            tracker.Update(new[] { At(0) }, 0);
            var now = ResourceTracker.BaselineGraceMs + 100L;
            Equal(0, tracker.Update(new[] { At(12) }, now).Count, "a pool on its way is not news");
            var reached = tracker.Update(new[] { At(31) }, now + 100);
            Equal(1, reached.Count, "reaching what the skill's level row gives is told");
            Equal((ResourceChange.Count, 31, 0), (reached[0].Change, reached[0].Count, reached[0].Max), "as a count: no live stat says 31 is the maximum");
            Equal(0, tracker.Update(new[] { At(36) }, now + 200).Count, "and when something raised the real maximum, going on past it is no news");
            Equal(ResourceChange.Empty, tracker.Update(new[] { At(0) }, now + 300)[0].Change, "running out is still told");

            var live = new ResourceTracker();
            live.Update(new[] { At(0, max: 40, reach: 0) }, 0);
            Equal(ResourceChange.Full, live.Update(new[] { At(40, max: 40, reach: 0) }, now)[0].Change, "a maximum the game holds in a live stat is full when reached");
        }

        private static void ABuffIdThatEndsLikeASuffixIsStillItself()
        {
            Equal("rallying_cry_new_weapon_damage_added", BuffIds.Base("rallying_cry_new_weapon_damage_added"), "a lower-case word is no suffix of the host's, though all its letters are hex digits");
            Equal("power_charge", BuffIds.Base("power_charge_79"), "the host's suffix");
            Equal("chilled", BuffIds.Base("chilled_1A"), "in upper case hex");

            var catalog = ClassMechanicCatalog.Parse("""
                {"buffs":{"shrine_fire":{"type":7,"names":{"en":"Fire Shrine"}},"shrine_fire_2":{"type":7,"names":{"en":"Greater Fire Shrine"}},
                          "shrine_corrupt_2":{"type":7,"names":{"en":"Corrupting Shrine"}},"rallying_cry_new_weapon_damage_added":{"type":1,"names":{"en":"Inspired"}},
                          "jade_stacks":{"type":1,"names":{"en":"Jade"}}}}
                """);
            Equal("Greater Fire Shrine", catalog.BuffName("shrine_fire_2", "en"), "an id of the table is itself, not the one it would be cut to");
            Equal("Corrupting Shrine", catalog.BuffName("shrine_corrupt_2", "en"), "nor lost when the cut one is no id");
            Equal("Inspired", catalog.BuffName("rallying_cry_new_weapon_damage_added", "en"), "the warcry's buff has its name");
            Equal("Jade", catalog.BuffName("jade_stacks_79", "en"), "a key with the host's suffix is the id without it");
            Equal("Greater Fire Shrine", catalog.BuffName("shrine_fire_2_1A", "en"), "also when the id itself ends in a number");
            Equal("jade_stacks", catalog.BuffKey("jade_stacks_79"), "one key per buff");
            Equal(-1, catalog.BuffType("unknown_buff_79"), "a buff the table lacks stays unknown");
        }

        private static void AnAreaHashIsAnotherOnceReadTwice()
        {
            var hash = default(ConfirmedChange);
            hash.Set(0x962301CC);
            Equal(false, hash.Offer(0x962301CC), "the same area");
            Equal(false, hash.Offer(0x0BADF00D), "one odd reading could be a read caught mid-write");
            Equal(false, hash.Offer(0x962301CC), "and it was");
            Equal(false, hash.Offer(0xD28E11CE), "another area in the instance the game already had: seen once");
            Equal(true, hash.Offer(0xD28E11CE), "seen again: what is held is the old area's");
            Equal(false, hash.Offer(0xD28E11CE), "told once");
        }

        private static void AFlasksCostOutlivesAnAreaButNotACharacter()
        {
            var (drinks, now) = Learnt(0, Life, 59, 8);
            drinks.Update(0, Life, 51, now);
            drinks.Clear();
            Equal(FlaskNews.New, drinks.Update(0, Life, 75, now + 1000), "a new area: first read (the well refilled it)");
            Equal<int?>(9, drinks.Uses(0, Life, 75), "the same flask costs the same in the next area");
            drinks.ForgetCosts();
            Equal<int?>(null, drinks.Uses(0, Life, 75), "another character's flask of the same name is learnt again");
        }
        private static void ABuffAButtonPutsOnIsNotToldEachTime()
        {
            // The buckler raised to parry: a buff with no duration, on for under two seconds, again and again.
            const string Parry = "buff:physical_projectile_retaliation";
            var steady = new SteadyBuffs();
            var now = 10_000L;
            for (var raise = 0; raise < 6; raise++)
            {
                for (var held = 0; held <= 1500; held += 250)
                {
                    Equal(false, steady.Tells(Parry, timed: false, now + held), $"raise {raise}: not told while it has not stayed");
                    steady.EndReading(now + held);
                }

                steady.EndReading(now + 1750); // lowered
                now += 5000;
            }

            Equal(true, steady.Quiet.Contains(Parry), "and after three comings and goings it is a button's own effect");

            // Held long from then on: still not told.
            Equal(false, steady.Tells(Parry, timed: false, now), "raised again");
            steady.EndReading(now);
            Equal(false, steady.Tells(Parry, timed: false, now + 10_000), "held ten seconds: quiet all the same");
        }

        private static void ABuffThatStaysIsTold()
        {
            var steady = new SteadyBuffs();
            Equal(false, steady.Tells("buff:aura", timed: false, 1000), "a buff with no duration, just begun");
            steady.EndReading(1000);
            Equal(true, steady.Tells("buff:aura", timed: false, 1000 + SteadyBuffs.HoldMs), "told once it has stayed");
            Equal(true, steady.Tells("buff:long", timed: true, 3000), "a buff with a duration is told from its start");
            Equal(0, steady.EndReading(3000).Count, "nothing went");

            // It goes and comes back twice in two minutes: not enough to call it a button's.
            steady.EndReading(4000);
            steady.Tells("buff:aura", timed: false, 30_000);
            steady.EndReading(30_000);
            steady.EndReading(31_000);
            Equal(false, steady.Quiet.Contains("buff:aura"), "two goings are not three");

            // A third going long after the window started over counts alone.
            steady.Tells("buff:aura", timed: false, 400_000);
            steady.EndReading(400_000);
            Equal(0, steady.EndReading(401_000).Count, "goings outside the window are forgotten");
            Equal(false, steady.Quiet.Contains("buff:aura"), "still told");

            steady.Reset();
            Equal(false, steady.Tells("buff:aura", timed: false, 500_000), "after a reset it waits again");
        }
    }
}
