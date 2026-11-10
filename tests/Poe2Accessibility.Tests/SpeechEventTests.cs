namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>What happened is said however late: only a number that changes by itself may go unsaid behind a kept line.</summary>
    public static class SpeechEventTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(UrgentNewsIsNeverAReading), UrgentNewsIsNeverAReading),
        };

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
