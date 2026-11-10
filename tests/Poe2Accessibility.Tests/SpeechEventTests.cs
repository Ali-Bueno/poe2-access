namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>What happened is said however late: only a number that changes by itself may go unsaid behind a kept line.</summary>
    public static class SpeechEventTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(UrgentNewsIsNeverAReading), UrgentNewsIsNeverAReading),
            (nameof(AReadingThatCutsInDoesNotTakeAnEventWithIt), AReadingThatCutsInDoesNotTakeAnEventWithIt),
            (nameof(AWarningSaidWithTheUrgentLineWaitsNoMore), AWarningSaidWithTheUrgentLineWaitsNoMore),
            (nameof(OnlyANumberIsAReadingAtEveryKindAndLevel), OnlyANumberIsAReadingAtEveryKindAndLevel),
        };

        /// <summary>
        ///     "Life 50 percent" waiting behind a lesson is joined to "Stunned" 80 ms later, which goes out behind the lesson
        ///     at once: still waiting too, it was said a second time when the lesson ended.
        /// </summary>
        private static void AWarningSaidWithTheUrgentLineWaitsNoMore()
        {
            var start = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            var floor = new SpeechFloor();
            floor.Keep("Press X to equip the weapon.", true, 1500, start);
            floor.Admit("Picked up: Scroll of Wisdom, 3", false, start.AddMilliseconds(400));
            floor.Admit("Life 50 percent", false, start.AddMilliseconds(500), reading: true);
            floor.Withdraw("Life 50 percent");
            Equal("Picked up: Scroll of Wisdom, 3", string.Join(" | ", floor.Release(start.AddMilliseconds(1600))), "only what was not said yet follows the lesson");
            Equal(0, floor.Dropped.Count, "and the warning does not count as left unsaid");
        }

        /// <summary>
        ///     With the boss's life set to cut in, "Boss 60 percent" behind a lesson replaced the pick-up that waited and
        ///     then went unsaid for its age: neither was heard.
        /// </summary>
        private static void AReadingThatCutsInDoesNotTakeAnEventWithIt()
        {
            var start = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            var floor = new SpeechFloor();
            floor.Keep("Press X to equip the weapon.", true, 6000, start);
            floor.Admit("Picked up: Scroll of Wisdom, 3", false, start.AddMilliseconds(500));
            floor.Admit("Boss 60 percent", true, start.AddMilliseconds(600), reading: true);
            Equal("Picked up: Scroll of Wisdom, 3", string.Join(" | ", floor.Release(start.AddMilliseconds(6100))), "the pick-up is said after the lesson");
            Equal("Boss 60 percent", string.Join(" | ", floor.Dropped), "the old percentage is not");

            floor.Keep("Press X to equip the weapon.", true, 6000, start.AddSeconds(10));
            floor.Admit("Picked up: Scroll of Wisdom, 3", false, start.AddSeconds(10.5));
            floor.Admit("Stash, tab 2", true, start.AddSeconds(10.6));
            Equal("Stash, tab 2", string.Join(" | ", floor.Release(start.AddSeconds(16.1))), "an interrupt that is no reading still replaces what waited");
        }

        /// <summary>
        ///     "Geonor defeated", "Yarkbruja is down" and "Life 25 percent" are spoken under the kinds of the boss's and the
        ///     player's life; at a level the player picked that does not urge them, they were readings and could go unsaid.
        /// </summary>
        private static void UrgentNewsIsNeverAReading()
        {
            Equal(true, SpeechPriority.IsReading(SpeechKind.BossLife) && SpeechPriority.IsReading(SpeechKind.LifeAndMana), "a percentage is a reading");
            Equal(false, SpeechPriority.IsReading(SpeechKind.BossLife, urgent: true) || SpeechPriority.IsReading(SpeechKind.LifeAndMana, urgent: true), "urgent news is not");
            var output = new Recorder();
            try
            {
                SpeechPriority.Chosen = _ => SpeechPriority.Waits;
                output.Say(SpeechKind.BossLife, "boss 60 percent");
                output.Say(SpeechKind.BossLife, "boss defeated", urgent: true);
                output.Say(SpeechKind.LifeAndMana, "life 50 percent");
                output.Say(SpeechKind.LifeAndMana, "partner down", urgent: true);
                SpeechPriority.Chosen = _ => SpeechPriority.CutsIn;
                output.Say(SpeechKind.BossLife, "boss 40 percent");
                output.Say(SpeechKind.BossLife, "the boss turns", urgent: true);
                output.Say(SpeechKind.LifeAndMana, "partner back", urgent: true);
            }
            finally
            {
                SpeechPriority.Chosen = null;
            }

            Equal(
                "reading boss 60 percent False|speak boss defeated False|mention life 50 percent|news partner down|reading boss 40 percent True|speak the boss turns True|speak partner back True",
                string.Join("|", output.Calls),
                "a number waits as a reading, what happened as an event, at the level chosen");
        }

        /// <summary>
        ///     Every kind at every level the settings allow, with and without the urgent and the happened marks: a line
        ///     goes out as a reading (and may be left unsaid after 2 s behind a kept line) only when it is a number of a
        ///     reading kind and neither urgent nor an event, and never at the level that holds.
        /// </summary>
        private static void OnlyANumberIsAReadingAtEveryKindAndLevel()
        {
            var readingKinds = new[] { SpeechKind.LifeAndMana, SpeechKind.BossLife, SpeechKind.Charges, SpeechKind.Combo };
            try
            {
                foreach (var kind in Enum.GetValues<SpeechKind>())
                {
                    foreach (var level in SpeechPriority.Levels)
                    {
                        foreach (var urgent in new[] { false, true })
                        {
                            foreach (var happened in new[] { false, true })
                            {
                                SpeechPriority.Chosen = _ => level;
                                var output = new Recorder();
                                output.Say(kind, "x", urgent, happened);
                                var call = output.Calls.Single();
                                var where = $"{kind} at {level}, urgent {urgent}, happened {happened}";
                                var isReading = readingKinds.Contains(kind) && !urgent && !happened;
                                Equal(isReading, SpeechPriority.IsReading(kind, urgent, happened), "the rule for " + where);
                                var aged = call.StartsWith("reading ") || call.StartsWith("mention ");
                                Equal(isReading && level != SpeechPriority.Holds, aged, "only a number may age: " + where + " said " + call);
                                Equal(level == SpeechPriority.Holds || (urgent && level == SpeechPriority.Urgent), call.StartsWith("urge "), "holding: " + where);
                            }
                        }
                    }
                }
            }
            finally
            {
                SpeechPriority.Chosen = null;
            }
        }

        private sealed class Recorder : IAccessibilityOutputService
        {
            public List<string> Calls { get; } = new();

            public string BackendName => "test";

            public void Speak(string text, bool interrupt = false) => this.Calls.Add($"speak {text} {interrupt}");

            public void SpeakReading(string text, bool interrupt = false) => this.Calls.Add($"reading {text} {interrupt}");

            public void Urge(string text, int holdMs = 1500) => this.Calls.Add($"urge {text}");

            public void Mention(string text) => this.Calls.Add($"mention {text}");

            public void MentionNews(string text) => this.Calls.Add($"news {text}");

            public void Repeat()
            {
            }

            public void Stop()
            {
            }

            public void Dispose()
            {
            }
        }
    }
}
