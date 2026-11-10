namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Access;
    using static Check;

    /// <summary>The launcher's calm speech: the wait for the game window.</summary>
    public static class LauncherWaitTests
    {
        private static readonly DateTime Start = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly), TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly),
            (nameof(TheWaitGivesUpOnlyWhenNothingRanForThePatience), TheWaitGivesUpOnlyWhenNothingRanForThePatience),
        };

        private static GameWait NewWait()
        {
            var wait = new GameWait(TimeSpan.FromSeconds(240), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
            wait.Start(Start);
            return wait;
        }

        private static void TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly()
        {
            var wait = NewWait();
            Expect(WaitStep.Wait, wait.Look(true, Start.AddSeconds(10)), "too soon to say anything");
            Expect(WaitStep.Remind, wait.Look(true, Start.AddSeconds(30)), "the first reminder at 30 s");
            Expect(WaitStep.Wait, wait.Look(true, Start.AddSeconds(60)), "not again inside a minute");
            Expect(WaitStep.Remind, wait.Look(true, Start.AddSeconds(90)), "then each minute");
            Expect(WaitStep.Remind, wait.Look(true, Start.AddSeconds(3600)), "an hour in, Steam still patching: no end, and a reminder");
        }

        private static void TheWaitGivesUpOnlyWhenNothingRanForThePatience()
        {
            var wait = NewWait();
            Expect(WaitStep.Remind, wait.Look(true, Start.AddSeconds(200)), "Steam runs: no end");
            wait.Look(true, Start.AddSeconds(1000));
            Expect(WaitStep.Remind, wait.Look(false, Start.AddSeconds(1239)), "nothing runs, but the patience counts from the last sight: still waiting");
            Expect(WaitStep.GiveUp, wait.Look(false, Start.AddSeconds(1240)), "nothing ran for 240 s");
            var fresh = NewWait();
            Expect(WaitStep.GiveUp, fresh.Look(false, Start.AddSeconds(240)), "nothing ever ran: the old 240 s");
        }


    }
}
