namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The stat key rule (a stat's key is its row plus one), checked on the character's own level (<see cref="StatKeyCheck"/>).</summary>
    public static class StatKeyTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheLevelKeyReadingTheLevelHolds), TheLevelKeyReadingTheLevelHolds),
            (nameof(TwoReadingsOfSomethingElseLoseTheKeys), TwoReadingsOfSomethingElseLoseTheKeys),
            (nameof(WhatCannotBeJudgedSaysNothing), WhatCannotBeJudgedSaysNothing),
        };

        private static void TheLevelKeyReadingTheLevelHolds()
        {
            var check = new StatKeyCheck();
            Equal(true, check.Observe(22, 22, 250), "level 22 under the level key");
            Equal(StatKeyVerdict.Holds, check.Verdict, "the rule holds");
            Equal(false, check.Observe(22, 5, 250), "judged once per game process");
            Equal(StatKeyVerdict.Holds, check.Verdict, "a later reading changes nothing");
        }

        private static void TwoReadingsOfSomethingElseLoseTheKeys()
        {
            var check = new StatKeyCheck();
            Equal(false, check.Observe(22, 21, 250), "one reading off (the level just rose) says nothing");
            Equal(StatKeyVerdict.Pending, check.Verdict, "looked at again");
            Equal(true, check.Observe(22, 22, 250), "the next reads the level");
            Equal(StatKeyVerdict.Holds, check.Verdict, "so the rule holds");

            check.Reset();
            Equal(false, check.Observe(22, 0, 250), "no such key among the stats");
            Equal(true, check.Observe(22, 137, 250), "and another stat's value there the second time");
            Equal(StatKeyVerdict.Lost, check.Verdict, "the keys are not the game's");
            Equal(false, check.Observe(22, 22, 250), "said once");
            check.Reset();
            Equal(StatKeyVerdict.Pending, check.Verdict, "a new game process judges again");
        }

        private static void WhatCannotBeJudgedSaysNothing()
        {
            var check = new StatKeyCheck();
            Equal(false, check.Observe(0, 0, 250), "no level read");
            Equal(false, check.Observe(101, 101, 250), "a number that is no level");
            Equal(false, check.Observe(22, 0, 3), "a stat list not read yet");
            Equal(false, check.Observe(22, 0, 0), "or empty");
            Equal(StatKeyVerdict.Pending, check.Verdict, "none of them counts as a miss");
            Equal(false, check.Observe(22, 5, 250), "the first real miss");
            Equal(StatKeyVerdict.Pending, check.Verdict, "is still only one");
        }
    }
}
