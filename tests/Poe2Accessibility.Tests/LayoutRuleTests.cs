namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>
    ///     When a part is said lost, lifted out of the classes that read the game: the world watch's counters, the layout
    ///     probe's second look, the in-game fields' absent rule, the flag bits' rounds, the states' wait and the skill names' check.
    /// </summary>
    public static class LayoutRuleTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AMissCountSaysOnceOrFromItsCountOn), AMissCountSaysOnceOrFromItsCountOn),
            (nameof(APoiseRecordIsMissingOnlyOnceASecondAreaMissesIt), APoiseRecordIsMissingOnlyOnceASecondAreaMissesIt),
            (nameof(PartsMissedInPlayAreLookedForAgainWhileStillMissed), PartsMissedInPlayAreLookedForAgainWhileStillMissed),
            (nameof(AFieldThatMayNotExistYetNeedsASecondArea), AFieldThatMayNotExistYetNeedsASecondArea),
            (nameof(AFlagBitIsLostOrMovedOnlyOnASecondRound), AFlagBitIsLostOrMovedOnlyOnASecondRound),
            (nameof(TheStatesAreOverdueAfterTheirWait), TheStatesAreOverdueAfterTheirWait),
            (nameof(SkillNamesAreComparedOnTheSkillsTheFilesName), SkillNamesAreComparedOnTheSkillsTheFilesName),
        };

        private static void AMissCountSaysOnceOrFromItsCountOn()
        {
            var once = new MissCount(2);
            Expect("False True False", $"{once.Once()} {once.Once()} {once.Once()}", "said at the second miss, once (the walkable cells' two windows)");
            var reached = new MissCount(2);
            Expect("False True True", $"{reached.Reached()} {reached.Reached()} {reached.Reached()}", "from the second look on (no world)");
            reached.Clear();
            Expect(false, reached.Reached(), "a world seen again starts over");
        }

        private static void APoiseRecordIsMissingOnlyOnceASecondAreaMissesIt()
        {
            var misses = new AreaMisses(3, 2);
            var (first, second) = (LossWitnesses.AreaKey(0x1000, "A"), LossWitnesses.AreaKey(0x1000, "B"));
            Expect("False False False", $"{misses.Miss(first)} {misses.Miss(first)} {misses.Miss(first)}", "one area's monsters can all lack the record");
            Expect("False False True", $"{misses.Miss(second)} {misses.Miss(second)} {misses.Miss(second)}", "a second area, the same instance reloaded, agrees");
            Expect(false, misses.Miss(second), "said once");
        }

        private static void PartsMissedInPlayAreLookedForAgainWhileStillMissed()
        {
            Expect(true, PartLoss.LookAgainDue(true, 1000, 1000, true, false, false, true), "a UI part missed in play");
            Expect(false, PartLoss.LookAgainDue(true, 999, 1000, true, false, false, true), "not before the next look is due");
            Expect(false, PartLoss.LookAgainDue(false, 1000, 1000, true, false, true, false), "no manager to look from");
            Expect(false, PartLoss.LookAgainDue(true, 1000, 1000, true, true, false, true), "every part found since");
            Expect(true, PartLoss.LookAgainDue(true, 1000, 1000, false, true, true, false), "a state's layer roots still owed");
            Expect(false, PartLoss.LookAgainDue(true, 1000, 1000, false, false, false, false), "nothing missed in play");
        }

        private static void AFieldThatMayNotExistYetNeedsASecondArea()
        {
            var asked = 0;
            bool Second()
            {
                asked++;
                return true;
            }

            Expect(false, PartLoss.AfterMisses(false, 2, 3, true, Second), "too few misses");
            Expect(true, PartLoss.AfterMisses(false, 3, 3, true, Second), "the tree shows the element: lost at once");
            Expect(0, asked, "the areas are not asked then");
            Expect(true, PartLoss.AfterMisses(false, 3, 3, false, Second), "no element shown: a second area must miss it");
            Expect(false, PartLoss.AfterMisses(false, 3, 3, false, () => false), "the first area alone");
            Expect(false, PartLoss.AfterMisses(true, 9, 3, true, Second), "the bind screen's display is never said");
        }

        private static readonly uint[] Labels = { 0, 0, 0, 0 };

        private static void AFlagBitIsLostOrMovedOnlyOnASecondRound()
        {
            const int measured = 4;
            uint[] With(int bit) => Enumerable.Repeat(1u << bit, 12).ToArray();
            var misses = new LossWitnesses();
            var moved = new MovedBit();
            BitVerdict Judge(uint[] carriers, long round) => BitSearch.Judge(carriers, Labels, measured, 0.5, 0.9, 0.1, 3, misses, round, moved);

            Expect(new BitVerdict(BitJudgement.Found, measured, false), Judge(With(measured), 1), "kept where measured");
            Expect(new BitVerdict(BitJudgement.Pending, 6, false), Judge(With(6), 1), "another bit, one round: not yet");
            Expect(new BitVerdict(BitJudgement.Moved, 6, false), Judge(With(6), 2), "a second round names it too");

            var noBit = new[] { 0u, 0u, 0u, 0u };
            Expect(new BitVerdict(BitJudgement.Pending, measured, false), Judge(noBit, 3), "no bit fits one round: no verdict");
            Expect(new BitVerdict(BitJudgement.Pending, measured, false), Judge(noBit, 3), "the same round again counts once");
            Expect(new BitVerdict(BitJudgement.Lost, measured, true), Judge(noBit, 4), "a second round agrees: lost, said now");
            Expect(new BitVerdict(BitJudgement.Lost, measured, false), Judge(noBit, 5), "said once");
        }

        private static void TheStatesAreOverdueAfterTheirWait()
        {
            Expect(false, StatesWait.Overdue(StatesWait.UnreadMs, false), "two minutes reach the login screen");
            Expect(true, StatesWait.Overdue(StatesWait.UnreadMs + 1, false), "after them, the states are lost");
            Expect(false, StatesWait.Overdue(StatesWait.UnreadMs + 1, true), "a client patching itself before its login has longer");
            Expect(true, StatesWait.Overdue(StatesWait.AwaitedMs + 1, true), "said all the same after ten minutes");
        }

        private static void SkillNamesAreComparedOnTheSkillsTheFilesName()
        {
            var files = new Dictionary<string, string> { ["Fireball"] = "Fireball", ["Spark"] = "Spark" };
            var (compared, differing) = SkillNameCheck.Compare(new[] { ("Fireball", "Bola de fuego"), ("Spark", "Spark"), ("Unknown", "?") },
                id => files.ContainsKey(id), (id, name) => files[id] == name);
            Expect((2, "Fireball"), (compared, string.Join(",", differing.Select(skill => skill.Id))), "a skill the files lack is not compared");

            var check = new SkillNameCheck();
            Expect(true, check.NewProcess(1), "the first reading");
            Expect(false, check.NewProcess(1), "the same game process");
            check.Observe(4, 4);
            check.Observe(4, 4);
            Expect(SkillNameSource.Files, check.Source, "two readings disagree");
            Expect((true, SkillNameSource.Unjudged), (check.NewProcess(2), check.Source), "a new game process is judged again");
        }
    }
}
