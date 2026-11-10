namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Access;
    using static Check;

    /// <summary>The launcher's calm speech: the wait for the game window and the reminder of a step that shows no progress.</summary>
    public static class LauncherWaitTests
    {
        private static readonly DateTime Start = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly), TheWaitGoesOnWhileSomethingRunsAndRemindsCalmly),
            (nameof(TheWaitGivesUpOnlyWhenNothingRanForThePatience), TheWaitGivesUpOnlyWhenNothingRanForThePatience),
            (nameof(AStepWithoutProgressIsRemindedFirstAfterThirtySecondsThenEverySixty), AStepWithoutProgressIsRemindedFirstAfterThirtySecondsThenEverySixty),
            (nameof(AMovingCountRestartsTheSilence), AMovingCountRestartsTheSilence),
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

        private static void AStepWithoutProgressIsRemindedFirstAfterThirtySecondsThenEverySixty()
        {
            var silence = new SilenceReminder(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
            silence.Start(0, Start);
            Expect(null, silence.Due(0, Start.AddSeconds(29)), "not yet");
            Expect(1, silence.Due(0, Start.AddSeconds(30)), "30 s: one minute at least");
            Expect(null, silence.Due(0, Start.AddSeconds(60)), "inside the pace");
            Expect(2, silence.Due(0, Start.AddSeconds(90)), "a minute and a half is said as 2");
            Expect(5, silence.Due(0, Start.AddSeconds(270)), "N minutes since the count moved");
        }

        private static void AMovingCountRestartsTheSilence()
        {
            var silence = new SilenceReminder(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
            silence.Start(0, Start);
            Expect(null, silence.Due(2, Start.AddSeconds(100)), "the count moved: no reminder");
            Expect(null, silence.Due(2, Start.AddSeconds(129)), "the silence counts from the move");
            Expect(1, silence.Due(2, Start.AddSeconds(130)), "30 s after the move");
        }
    }
}
