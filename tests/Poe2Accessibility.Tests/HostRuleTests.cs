namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameOffsets;
    using GameReader.Utils;
    using Poe2Accessibility.Services;
    using static Check;
    using Step = Poe2Access.GameDataSteps.Step;

    /// <summary>Rules lifted out of classes that read the game or the machine: the guide's search lines, a boss's life, naming the target, the data step's plan, the update offer and the game process's watch.</summary>
    public static class HostRuleTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(OneSearchWithoutAWayIsNoRouteOnlyWhenTheNextAgrees), OneSearchWithoutAWayIsNoRouteOnlyWhenTheNextAgrees),
            (nameof(ALongWayAndAClosedWayAreEachSaidOncePerLock), ALongWayAndAClosedWayAreEachSaidOncePerLock),
            (nameof(NoWayAtThePartialWaysEndIsAsCloseAsItGoes), NoWayAtThePartialWaysEndIsAsCloseAsItGoes),
            (nameof(ABossReadingDeadOnlyStartsTheWait), ABossReadingDeadOnlyStartsTheWait),
            (nameof(ABossFallsRisesHidesAndRestarts), ABossFallsRisesHidesAndRestarts),
            (nameof(ATargetIsNamedOnceSettledNotAgainWhileAimedAt), ATargetIsNamedOnceSettledNotAgainWhileAimedAt),
            (nameof(AStepRerunsWhenItsInputDoesAndIsCopiedBackWhenMadeHere), AStepRerunsWhenItsInputDoesAndIsCopiedBackWhenMadeHere),
            (nameof(TheFailedListNamesTheIndexOnceAndKeepsTheGivenUp), TheFailedListNamesTheIndexOnceAndKeepsTheGivenUp),
            (nameof(AnUpdateIsOfferedOnlyWhenNewerAndOnlyAskedChecksTell), AnUpdateIsOfferedOnlyWhenNewerAndOnlyAskedChecksTell),
            (nameof(TheClientInFrontIsPickedAndAGoneOneClosed), TheClientInFrontIsPickedAndAGoneOneClosed),
            (nameof(TheStatesStaticIsProvenWatchedOrSearched), TheStatesStaticIsProvenWatchedOrSearched),
        };

        private static void OneSearchWithoutAWayIsNoRouteOnlyWhenTheNextAgrees()
        {
            var news = new RouteSearchNews();
            news.Aim(false, false);
            Expect(SearchWay.Wait, news.Collect(false, false, false, false, true, true).Way, "one failed search keeps the way in use");
            Expect(new SearchVerdict(SearchWay.Drop, SearchLine.NoRoute, false), news.Collect(false, false, false, false, true, true), "the second says no route");
            Expect(new SearchVerdict(SearchWay.Keep, SearchLine.None, false), news.Collect(false, false, false, false, true, false), "said once");
            Expect(SearchLine.None, news.Collect(true, false, false, false, true, false).Line, "a whole way found");
            Expect(SearchWay.Wait, news.Collect(false, false, false, false, true, true).Way, "the count starts over once a way is found");
        }

        private static void ALongWayAndAClosedWayAreEachSaidOncePerLock()
        {
            var news = new RouteSearchNews();
            news.Aim(false, false);
            Expect(new SearchVerdict(SearchWay.Take, SearchLine.LongWay, true), news.Collect(true, true, false, true, true, false), "only the budget ran out");
            Expect(SearchLine.None, news.Collect(true, true, false, true, true, true).Line, "said once");
            Expect(SearchLine.PartialWay, news.Collect(true, true, false, false, true, true).Line, "a closed way found later is still said");
            Expect(SearchLine.None, news.Collect(true, true, false, false, true, true).Line, "and once");
            news.Aim(true, false);
            Expect(SearchLine.None, news.Collect(true, true, true, false, true, false).Line, "the lock said the way stops short");
            news.Aim(false, false);
            Expect(SearchLine.BarrierWay, news.Collect(true, true, true, true, true, false).Line, "a known barrier whatever the budget");
            news.Aim(false, false);
            Expect(SearchLine.None, news.Collect(true, true, false, false, false, false).Line, "no target, nothing said");
        }

        private static void NoWayAtThePartialWaysEndIsAsCloseAsItGoes()
        {
            var news = new RouteSearchNews();
            news.Aim(false, false);
            news.Collect(true, true, false, false, true, false);
            news.Collect(false, false, false, false, true, true);
            Expect(new SearchVerdict(SearchWay.Drop, SearchLine.AtClosest, false), news.Collect(false, false, false, false, true, true), "where the partial way ended");
            Expect(SearchLine.None, news.Closest(), "said once");
            news.Collect(true, false, false, false, true, false);
            Expect(SearchLine.AtClosest, news.Closest(), "a whole way found lets it be said again");
        }

        private static readonly BossLife Miller = BossLife.Fresh("Molinero");

        private static void ABossReadingDeadOnlyStartsTheWait()
        {
            var dead = BossLifeSteps.Read(Miller with { Step = 40, Engaged = true }, false, false, 0, null, 1000);
            Expect(1000L, dead.Boss.DeadSince, "the wait starts");
            Expect(null, dead.Said, "nothing said");
            Expect(1000L, BossLifeSteps.Read(dead.Boss, false, false, 0, null, 1200).Boss.DeadSince, "a second dead reading keeps its start");
            Expect(0L, BossLifeSteps.Read(dead.Boss, true, false, 35, null, 1300).Boss.DeadSince, "a living reading ends it");
        }

        private static void ABossFallsRisesHidesAndRestarts()
        {
            var hit = BossLifeSteps.Read(Miller, true, false, 45, null, 500);
            Expect(45, hit.Said, "the real percent past two steps");
            Expect((50, true, 500L), (hit.Boss.Step, hit.Boss.Engaged, hit.Boss.HurtAt), "the step, the fight on");
            Expect(null, BossLifeSteps.Read(hit.Boss, true, false, 44, null, 600).Said, "no step passed");

            var risen = BossLifeSteps.Read(hit.Boss with { Step = 0 }, true, false, 60, null, 700);
            Expect((true, 60), (risen.Risen, risen.Said ?? -1), "said defeated, read alive: followed again");

            var hidden = BossLifeSteps.Read(hit.Boss, true, true, 30, null, 800);
            Expect((false, (int?)null), (hidden.Boss.Engaged, hidden.Said), "set aside: out of the fight, nothing said");

            var restarted = BossLifeSteps.Read(hit.Boss, true, false, 100, 900, 1000);
            Expect((true, BossLifeSteps.Full, false), (restarted.Restarted, restarted.Boss.Step, restarted.Boss.Engaged), "back to full after the player died");
        }

        private static void ATargetIsNamedOnceSettledNotAgainWhileAimedAt()
        {
            var naming = new TargetNaming();
            var wolf = (IntPtr)0x100;
            var other = (IntPtr)0x200;
            Expect(true, naming.Moved(wolf, 0), "a new target");
            Expect(false, naming.Moved(wolf, 100), "the same");
            Expect(false, naming.MayName(wolf, true, true, 100), "not settled");
            Expect(true, naming.MayName(wolf, true, true, TargetNaming.SettleMs), "settled");
            Expect(false, naming.MayName(wolf, true, false, TargetNaming.SettleMs), "not while the stick is let go");
            Expect((bool?)false, naming.Say(wolf, "Lobo", false, TargetNaming.SettleMs), "the first name cuts nothing");
            Expect(false, naming.MayName(wolf, true, true, 2000), "still on it");
            Expect(false, naming.MayName(wolf, true, true, 2000 + TargetNaming.RepeatMs - 1), "staying on it keeps it from being named again");
            Expect(true, naming.MayName(wolf, true, true, 2000 + (2 * TargetNaming.RepeatMs)), "named again after a while away from it");
            naming.Moved(other, 5000);
            Expect(true, naming.MayName(other, true, true, 5000 + TargetNaming.SettleMs), "another");
            Expect((bool?)null, naming.Say(other, "Lobo", true, 5200), "a pack's next nearest of the same name");
            Expect((bool?)true, naming.Say(wolf, "Araña", false, 5300), "a sweep cuts the last name");
            Expect((bool?)false, naming.Say(other, "Lobo", false, 5300 + TargetNaming.SweepMs), "a name later queues");
        }

        private const string Install = "index=1:2\nexe=3:4\n";

        private static void AStepRerunsWhenItsInputDoesAndIsCopiedBackWhenMadeHere()
        {
            var steps = new[] { new Step("items", 1, "items.json", "items"), new Step("hits", 2, "hits.txt", "hits", true), new Step("sounds", 1, null, null, Input: "items") };
            var stamp = Poe2Access.GameDataStamp.Parse(null).With(Install, "items", 1).With(Install, "hits", 2).With(Install, "sounds", 1);
            Expect("|items.json,hits.txt", Plan(steps, stamp, _ => true, true), "all current: the made files put back");
            Expect("items,sounds|hits.txt", Plan(steps, stamp, file => file != "items.json", true), "a required file gone reruns it, and the step reading it");
            Expect("|items.json,hits.txt", Plan(steps, stamp, file => file != "hits.txt", true), "an optional file gone is not a reason");
            Expect("hits|items.json", Plan(steps, stamp.With(Install, "hits", 1), _ => true, true), "a raised version reruns the step");
            Expect("sounds|items.json,hits.txt", Plan(steps, stamp, _ => true, false), "the sounds' manifest gone");
            var givenUp = stamp.WithFailure(Install, "hits", 2).WithFailure(Install, "hits", 2).WithFailure(Install, "hits", 2);
            Expect("|items.json", Plan(steps, givenUp, _ => true, true), "a step given up on is neither rerun nor copied back");
        }

        private static string Plan(Step[] steps, Poe2Access.GameDataStamp stamp, Func<string, bool> made, bool soundsMade)
        {
            var (stale, copy) = Poe2Access.GameDataSteps.Plan(steps, stamp, Install, made, soundsMade);
            return $"{string.Join(",", stale)}|{string.Join(",", copy)}";
        }

        private static void TheFailedListNamesTheIndexOnceAndKeepsTheGivenUp()
        {
            const string index = "bundle index";
            var failed = Poe2Access.GameDataSteps.Failed(new[] { ("hits", "a column moved"), ("places", index), ("gems", index) }, new[] { "endgame", "hits" }, index);
            Expect("hits, bundle index, endgame", string.Join(", ", failed), "each once, the index for all it failed, the given up after");
        }

        private static void AnUpdateIsOfferedOnlyWhenNewerAndOnlyAskedChecksTell()
        {
            var read = 0;
            bool On()
            {
                read++;
                return true;
            }

            Expect(Poe2Access.UpdateStep.NoVersion, Poe2Access.UpdateOffer.Before(null, false, On), "a development build");
            Expect(Poe2Access.UpdateStep.NoVersion, Poe2Access.UpdateOffer.Before("dev", true, On), "a tag that is no version");
            Expect(0, read, "the setting is not read for a build that cannot update");
            Expect(Poe2Access.UpdateStep.Off, Poe2Access.UpdateOffer.Before("v2026.11.10-7", false, () => false), "automatic, the setting off");
            Expect(Poe2Access.UpdateStep.Check, Poe2Access.UpdateOffer.Before("v2026.11.10-7", true, () => false), "asked for, whatever the setting");
            var installed = Poe2Access.ReleaseTag.Parse("v2026.11.10-7")!;
            Expect(Poe2Access.UpdateStep.UpToDate, Poe2Access.UpdateOffer.After(installed, installed), "the same release");
            Expect(Poe2Access.UpdateStep.Ask, Poe2Access.UpdateOffer.After(installed, Poe2Access.ReleaseTag.Parse("v2026.11.11")!), "a newer one");
            Expect((true, false, false), (Poe2Access.UpdateOffer.Tells(Poe2Access.UpdateStep.UpToDate, true), Poe2Access.UpdateOffer.Tells(Poe2Access.UpdateStep.UpToDate, false),
                Poe2Access.UpdateOffer.Tells(Poe2Access.UpdateStep.Off, true)), "only a check the player asked for says why nothing happens");
        }

        private static void TheClientInFrontIsPickedAndAGoneOneClosed()
        {
            var windows = new[] { (IntPtr)10, (IntPtr)20, (IntPtr)30 };
            Expect(1, GameProcessDetails.Pick(windows.Length, i => windows[i], 20), "the one in front");
            Expect(0, GameProcessDetails.Pick(windows.Length, i => windows[i], 99), "else the first");
            Expect(false, GameProcessDetails.Gone(true, () => false, () => 10, false), "running, with a window");
            Expect(true, GameProcessDetails.Gone(true, () => false, () => 0, false), "no window");
            Expect(true, GameProcessDetails.Gone(true, () => true, () => throw new InvalidOperationException("an exited process's window"), false), "exited: its window is never read");
            Expect(true, GameProcessDetails.Gone(false, () => throw new InvalidOperationException(), () => 0, false), "no process");
            Expect(true, GameProcessDetails.Gone(true, () => false, () => 10, true), "a close asked for");
        }

        private static void TheStatesStaticIsProvenWatchedOrSearched()
        {
            Expect((20L, true), StateSearchRules.Given(new long[] { 10, 20 }, place => place == 20), "the place that holds the object");
            Expect((10L, false), StateSearchRules.Given(new long[] { 10 }, _ => false), "the only place, unproven");
            Expect((0L, false), StateSearchRules.Given(new long[] { 10, 20 }, _ => false), "several, none proven: none");

            Expect(StateLook.TakeGiven, StateSearchRules.Look(true, false, true, false, 3, 3, 0), "the pattern's place holds it, another in use");
            Expect(StateLook.Rejudge, StateSearchRules.Look(true, true, true, false, 3, 3, 0), "the place in use since the attach holds it now");
            Expect(StateLook.Watch, StateSearchRules.Look(false, false, true, false, 3, 3, 0), "found by search: the pattern's place still watched");
            Expect(StateLook.Stop, StateSearchRules.Look(false, false, false, false, 3, 3, 0), "found by search, no pattern place to watch");
            Expect(StateLook.Watch, StateSearchRules.Look(false, true, true, true, 3, 3, 0), "a place that may yet hold it is waited on");
            Expect(StateLook.Search, StateSearchRules.Look(false, true, true, false, 3, 3, 0), "a place holding something else is searched past");

            Expect(0L, StateSearchRules.Settled(new[] { (5L, 100L), (6L, 200L) }, 9), "two objects settle nothing");
            Expect(0L, StateSearchRules.Settled(new[] { (9L, 100L) }, 9), "the pattern's own place: the next look proves it");
            Expect(5L, StateSearchRules.Settled(new[] { (5L, 100L), (6L, 100L) }, 9), "one object: its first static");
            Expect(0L, StateSearchRules.Settled(Array.Empty<(long, long)>(), 9), "nothing found");
        }
    }
}
