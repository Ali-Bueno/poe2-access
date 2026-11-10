namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The side panel's title kept while the same panels are shown: read again as they fill in, then once in a while.</summary>
    public static class SettlingMemoTests
    {
        private const long Tree = 0x1000;
        private const long Stash = 0x2000;

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(NothingIsKeptBeforeTheFirstRead), NothingIsKeptBeforeTheFirstRead),
            (nameof(AnAnswerIsReadAgainAsThePanelFillsInThenKept), AnAnswerIsReadAgainAsThePanelFillsInThenKept),
            (nameof(AnotherSetOfPanelsIsReadAtOnce), AnotherSetOfPanelsIsReadAtOnce),
            (nameof(AKeptAnswerIsReadAgainOnceItIsOld), AKeptAnswerIsReadAgainOnceItIsOld),
            (nameof(AReadThatComesLateCountsForEveryLookItPassed), AReadThatComesLateCountsForEveryLookItPassed),
        };

        private static void NothingIsKeptBeforeTheFirstRead()
        {
            var memo = new SettlingMemo<string>();
            Expect(false, memo.TryGet(Tree, 0, out _), "nothing read yet");
        }

        private static void AnAnswerIsReadAgainAsThePanelFillsInThenKept()
        {
            var memo = new SettlingMemo<string>();
            long now = 10_000;
            memo.Set(Tree, now, "half built");
            var reads = 1;
            string said = "half built";

            // Polled four times a second for five seconds, as the salvage watch does.
            for (var poll = 1; poll <= 19; poll++)
            {
                now += 250;
                if (!memo.TryGet(Tree, now, out said))
                {
                    reads++;
                    said = "Salvage Bench";
                    memo.Set(Tree, now, said);
                }
            }

            Equal(1 + SettlingMemo<string>.LooksMs.Length, reads, "the first read and one per look");
            Expect("Salvage Bench", said, "what the last look read is what is kept");
        }

        private static void AnotherSetOfPanelsIsReadAtOnce()
        {
            var memo = new SettlingMemo<string>();
            memo.Set(Tree, 0, "tree");
            memo.Set(Tree, 3000, "tree");
            Expect(true, memo.TryGet(Tree, 3100, out var kept) && kept == "tree", "kept while the panels are the same");
            Expect(false, memo.TryGet(Stash, 3100, out _), "other panels: read");
            memo.Set(Stash, 3100, "Stash");
            Expect(true, memo.TryGet(Stash, 3200, out kept) && kept == "Stash", "kept until the first look is due");
            Expect(false, memo.TryGet(Stash, 3100 + SettlingMemo<string>.LooksMs[0], out _), "the new panels get their looks too");
            Expect(false, memo.TryGet(Tree, 3200, out _), "back to the first panels: read, not remembered");
        }

        private static void AKeptAnswerIsReadAgainOnceItIsOld()
        {
            var memo = new SettlingMemo<string>();
            long now = 0;
            memo.Set(Tree, now, "tree");
            foreach (var look in SettlingMemo<string>.LooksMs)
            {
                memo.Set(Tree, now = look, "tree");
            }

            Expect(true, memo.TryGet(Tree, now + SettlingMemo<string>.RefreshMs - 1, out _), "kept until it is old");
            Expect(false, memo.TryGet(Tree, now + SettlingMemo<string>.RefreshMs, out _), "old: read again");
            memo.Set(Tree, now + SettlingMemo<string>.RefreshMs, "tree");
            Expect(true, memo.TryGet(Tree, now + SettlingMemo<string>.RefreshMs + 1, out _), "and kept again");
        }

        private static void AReadThatComesLateCountsForEveryLookItPassed()
        {
            var memo = new SettlingMemo<string>();
            memo.Set(Tree, 0, "tree");
            Expect(false, memo.TryGet(Tree, 1200, out _), "three looks are due after 1.2 s without a poll");
            memo.Set(Tree, 1200, "tree");
            Expect(true, memo.TryGet(Tree, 1300, out _), "one read answers them all");
            Expect(false, memo.TryGet(Tree, 2000, out _), "the last look is still owed");
        }
    }
}
