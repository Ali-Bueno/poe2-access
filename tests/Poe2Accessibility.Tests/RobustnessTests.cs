namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     What keeps a reading from being wrong in silence or a warning from being false (docs/research/robustness-audit-2026-11-10.md):
    ///     the rules that ask for a second reading, a second witness or a second fact.
    /// </summary>
    public static partial class RobustnessTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => Readings().Concat(Wide()).Concat(Checks());

        private static IEnumerable<(string Name, Action Test)> Readings() => new (string, Action)[]
        {
            (nameof(AnOverrideNodeDoesNotMoveTheAllocatedList), AnOverrideNodeDoesNotMoveTheAllocatedList),
            (nameof(OnlyAListOfKnownNodesTakesTheAllocatedPlace), OnlyAListOfKnownNodesTakesTheAllocatedPlace),
            (nameof(ABossIsDefeatedOnlyWhenItStaysDead), ABossIsDefeatedOnlyWhenItStaysDead),
            (nameof(ANewGameJudgesTheDodgeFieldsAgain), ANewGameJudgesTheDodgeFieldsAgain),
            (nameof(ATradeWindowIsReadHoweverItsBodyIsOrdered), ATradeWindowIsReadHoweverItsBodyIsOrdered),
            (nameof(TheEnabledBitIsLostOnlyOnTwoScreens), TheEnabledBitIsLostOnlyOnTwoScreens),
            (nameof(ALossNeedsTwoDifferentWitnesses), ALossNeedsTwoDifferentWitnesses),
            (nameof(ACatalogInUseLoadsEmpty), ACatalogInUseLoadsEmpty),
            (nameof(ABatchOfOddReadingsDoesNotLoseAField), ABatchOfOddReadingsDoesNotLoseAField),
            (nameof(APartLostInOneSweepIsLookedAtAgain), APartLostInOneSweepIsLookedAtAgain),
            (nameof(OneDropdownThatDoesNotReadIsNoLostLayout), OneDropdownThatDoesNotReadIsNoLostLayout),
            (nameof(AnUnreadCodeIsSaidOnlyForFieldsWithNoOtherJudge), AnUnreadCodeIsSaidOnlyForFieldsWithNoOtherJudge),
        };

        private static void AnOverrideNodeDoesNotMoveTheAllocatedList()
        {
            var graph = Enumerable.Range(100, 400).ToHashSet();
            Equal(AllocatedVerdict.Kept, AllocatedListCheck.Judge(new[] { 100, 101, 102 }, graph), "every id a node of the graph");
            Equal(AllocatedVerdict.Kept, AllocatedListCheck.Judge(new[] { 100, 101, 9000 }, graph), "a Witch's override node among three");
            Equal(AllocatedVerdict.Kept, AllocatedListCheck.Judge(Enumerable.Range(100, 28).Append(9000).Append(9001).ToList(), graph), "two unknown in thirty");
            Equal(AllocatedVerdict.Unjudged, AllocatedListCheck.Judge(new[] { 9000 }, graph), "one node, an override: no verdict, never a loss");
            Equal(AllocatedVerdict.Unjudged, AllocatedListCheck.Judge(new[] { 100, 9000, 9001 }, graph), "between right and wrong nothing is decided");
            Equal(AllocatedVerdict.Unjudged, AllocatedListCheck.Judge(Array.Empty<int>(), graph), "a new character");
            Equal(AllocatedVerdict.Unjudged, AllocatedListCheck.Judge(null, graph), "no list there");
            Equal(AllocatedVerdict.Broken, AllocatedListCheck.Judge(new[] { 9000, 9001, 9002, 9003 }, graph), "another list: nothing known");
            Equal(AllocatedVerdict.Broken, AllocatedListCheck.Judge(Enumerable.Range(9000, 19).Append(100).ToList(), graph), "one known in twenty is a coincidence");
        }

        private static void OnlyAListOfKnownNodesTakesTheAllocatedPlace()
        {
            var graph = Enumerable.Range(100, 400).ToHashSet();
            Equal(true, AllocatedListCheck.Strong(new[] { 100, 250, 300 }, graph), "three nodes, each once");
            Equal(false, AllocatedListCheck.Strong(new[] { 100 }, graph), "one known id is a coincidence");
            Equal(false, AllocatedListCheck.Strong(new[] { 100, 100, 100 }, graph), "a list of one value repeated is no allocation");
            Equal(false, AllocatedListCheck.Strong(new[] { 100, 101, 9000 }, graph), "a list good enough to keep is not good enough to move to");
            Equal(true, AllocatedListCheck.IdsUsable(300, 400), "most records gave an id");
            Equal(false, AllocatedListCheck.IdsUsable(10, 400), "rows not readable yet: nothing is judged on a few ids");
            Equal(false, AllocatedListCheck.IdsUsable(0, 0), "no records");
        }

        private static void ABossIsDefeatedOnlyWhenItStaysDead()
        {
            Equal(false, BossLifeSteps.Defeated(0, 5000), "alive");
            Equal(false, BossLifeSteps.Defeated(5000, 5000), "the first reading that says dead");
            Equal(false, BossLifeSteps.Defeated(5000, 5000 + BossLifeSteps.DefeatConfirmMs - 1), "not yet");
            Equal(true, BossLifeSteps.Defeated(5000, 5000 + BossLifeSteps.DefeatConfirmMs), "still not alive half a second later");
        }

        private static void ANewGameJudgesTheDodgeFieldsAgain()
        {
            var check = new AttackFieldCheck(4);
            for (var i = 0; i < 4; i++)
            {
                check.Observe(false);
            }

            Equal(FieldTrust.Unjudged, check.Trust, "one batch that says lost is no verdict");
            for (var i = 0; i < 4; i++)
            {
                check.Observe(false);
            }

            Equal(FieldTrust.Lost, check.Trust, "a second batch agrees: lost in this game process");
            check.Reset();
            Equal(FieldTrust.Unjudged, check.Trust, "the next process starts unjudged");
            for (var i = 0; i < 4; i++)
            {
                check.Observe(true);
            }

            Equal(FieldTrust.Intact, check.Trust, "and counts from nothing");
        }

        private static void ABatchOfOddReadingsDoesNotLoseAField()
        {
            var check = new AttackFieldCheck(4);
            foreach (var holds in new[] { false, false, false, false, true, true, true, true })
            {
                check.Observe(holds);
            }

            Equal(FieldTrust.Intact, check.Trust, "a first batch of odd monsters, then four that keep the rule: the field is intact");
        }

        private static void APartLostInOneSweepIsLookedAtAgain()
        {
            var look = new SecondLook();
            Equal(0, look.Confirm(new[] { "life", "skills" }).Count, "lost for the first time: swept again, nothing said");
            Equal("life", string.Join(",", look.Confirm(new[] { "life", "ground items" })), "lost twice in a row: said; the newly lost one waits");
            Equal("ground items", string.Join(",", look.Confirm(new[] { "ground items" })), "its turn");
            Equal(0, look.Confirm(Array.Empty<string>()).Count, "nothing lost");
            Equal(0, look.Confirm(new[] { "life" }).Count, "lost again after an intact look: a first time again");
        }

        private static void OneDropdownThatDoesNotReadIsNoLostLayout()
        {
            const string part = "combo boxes";
            var log = new PluginLog(Path.Join(Path.GetTempPath(), "poe2access-tests"), false);
            var report = new CompatibilityReport(log);
            long now = 1_000_000;
            var controls = new ControlFields(report, () => 0, () => now);
            for (var look = 0; look < 5; look++)
            {
                controls.Shift(ControlGroup.ComboBox, 1, _ => false);
                now += 6000; // past the retry of one control
            }

            Equal(false, report.IsMissing(part), "one dropdown that never reads (an empty list): five misses, no warning");
            Equal(false, report.AnyMissing, "and nothing missing holds back the line of a patch come through");
            controls.Shift(ControlGroup.ComboBox, 2, _ => false);
            Equal(true, report.IsMissing(part), "a second dropdown does not read either: the layout is lost");
            Equal(true, report.AnyMissing, "one part missing: the patch was not come through whole");

            var healthy = new CompatibilityReport(log);
            var fine = new ControlFields(healthy, () => 0, () => now);
            fine.Shift(ControlGroup.ComboBox, 1, _ => false);
            fine.Shift(ControlGroup.ComboBox, 2, shift => shift == 0);
            fine.Shift(ControlGroup.ComboBox, 3, _ => false);
            Equal(false, healthy.IsMissing(part), "once a dropdown proved the layout, others that do not read say nothing");
        }

        private static void AnUnreadCodeIsSaidOnlyForFieldsWithNoOtherJudge()
        {
            Equal(0, CodeFieldCatalog.Heard(new[] { "Positioned.Reaction", "Life.Health.Total", "Terrain.BytesPerRow" }).Count, "fields a live judgment finds or says lost by itself");
            Equal(0, CodeFieldCatalog.Heard(new[] { "Transitionable.CurrentState", "StatusEffect.TimeLeft" }).Count, "fields nothing of the mod reads");
            Equal("Stack.Count,Shrine.IsUsed", string.Join(",", CodeFieldCatalog.Heard(new[] { "Stack.Count", "Area.Entities", "Shrine.IsUsed" })), "fields only the code vouches for are still said");
        }

        private static void ATradeWindowIsReadHoweverItsBodyIsOrdered()
        {
            static (bool, bool) Grid() => (true, false);
            static (bool, bool) Text() => (false, true);
            static (bool, bool) Other() => (false, false);

            // As measured the texts alone tell (the grids are not asked for): grid, grid, title, title.
            var measured = new[] { Other(), Other(), Text(), Text() };
            Equal(((int, int, int)?)(1, 3, 2), TradeOfferNews.Sides(measured, 0), "the other side's grid and title from the first grid");
            Equal(((int, int, int)?)(0, 2, 3), TradeOfferNews.Sides(measured, 1), "and from the second");
            Equal(((int, int, int)?)null, TradeOfferNews.Sides(new[] { Other(), Other(), Text(), Other() }, 0), "no title where measured, no grids known: unread");

            var reordered = new[] { Text(), Grid(), Text(), Grid(), Other() };
            Equal(((int, int, int)?)(3, 2, 0), TradeOfferNews.Sides(reordered, 1), "titles before their grids: paired in order");
            Equal(((int, int, int)?)(1, 0, 2), TradeOfferNews.Sides(reordered, 3), "from the other grid");
            Equal(((int, int, int)?)null, TradeOfferNews.Sides(new[] { Text(), Grid(), Text(), Grid(), Grid() }, 1), "three grids are no trade window");
            Equal(((int, int, int)?)null, TradeOfferNews.Sides(new[] { Text(), Grid(), Other(), Grid(), Other() }, 1), "one title is not two");
            Equal(((int, int, int)?)null, TradeOfferNews.Sides(reordered, 4), "the focus is no grid of the body");
        }

        private static void TheEnabledBitIsLostOnlyOnTwoScreens()
        {
            var check = new EnabledBitCheck();
            Equal(false, check.Observe(1, 0.05), "a login still connecting greys every button: one screen says nothing");
            Equal(false, check.Observe(1, 0.05), "the same screen again is the same witness");
            Equal(true, check.Trusted, "believed meanwhile");
            Equal(true, check.Observe(2, 0.1), "a second screen agrees: lost");
            Equal(false, check.Trusted, "every control then counts as enabled");
            Equal(true, check.Observe(3, 0.9), "a later screen shows it working: given back");
            Equal(true, check.Trusted, "believed again");
            Equal(false, check.Observe(4, 0.0), "once proved, a greyed screen is only a greyed screen");
            Equal(true, check.Trusted, "kept");

            var healthy = new EnabledBitCheck();
            Equal(true, healthy.Observe(1, 0.8), "the first screen proves it");
            Equal(true, healthy.Proved, "nothing more to judge");
        }

        private static void ALossNeedsTwoDifferentWitnesses()
        {
            var witnesses = new LossWitnesses();
            Equal(false, witnesses.Against(7), "one batch that read nothing");
            Equal(false, witnesses.Against(7), "the same batch again");
            Equal(true, witnesses.Against(8), "another one: lost, said once");
            Equal(false, witnesses.Against(9), "not said again");
            Equal(true, witnesses.Lost, "still lost");
            witnesses.Clear();
            Equal(false, witnesses.Against(8), "found in between: counted from nothing");
            Equal(LossWitnesses.KeyOf(new long[] { 3, 5, 9 }), LossWitnesses.KeyOf(new long[] { 9, 3, 5 }), "a batch is the same in any order");
            Equal(false, LossWitnesses.KeyOf(new long[] { 3, 5, 9 }) == LossWitnesses.KeyOf(new long[] { 3, 5, 9, 0 }), "one more member is another batch");
        }

        private static void ACatalogInUseLoadsEmpty()
        {
            var path = Path.Join(Path.GetTempPath(), $"poe2access-tests-locked-{Guid.NewGuid():N}.txt");
            File.WriteAllLines(path, new[] { "KillingPalmPlayer" });
            try
            {
                Equal(1, CullSkills.Load(path).Count, "read when free");
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    Equal(0, CullSkills.Load(path).Count, "a catalog another process holds: empty, not a throw");
                    Equal(0, HazardGround.Load(path).Count, "the hazards the same");
                }

                Equal(0, CullSkills.Load(path + ".none").Count, "a missing file");
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
