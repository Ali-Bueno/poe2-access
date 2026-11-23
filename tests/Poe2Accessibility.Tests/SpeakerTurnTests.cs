namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The name before a dialogue line: once per turn, never repeated after it.</summary>
    public static class SpeakerTurnTests
    {
        private static readonly DateTime Start = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(ANameGoesBeforeTheLineWhenTheTurnChanges), ANameGoesBeforeTheLineWhenTheTurnChanges),
            (nameof(ANameJustSaidIsNotSaidAgainByTheScreen), ANameJustSaidIsNotSaidAgainByTheScreen),
            (nameof(AnEndedDialogueNamesItsSpeakerAgain), AnEndedDialogueNamesItsSpeakerAgain),
        };

        private static void ANameGoesBeforeTheLineWhenTheTurnChanges()
        {
            var turn = new SpeakerTurn();
            Equal("Farrow", turn.Say("Farrow", Start), "first line");
            Equal(string.Empty, turn.Say("Farrow", Start), "same speaker");
            Equal(string.Empty, turn.Say(string.Empty, Start), "no name read");
            Equal("Artista marcial", turn.Say("Artista marcial", Start), "the answer");
            Equal("Farrow", turn.Say("Farrow", Start), "back");
        }

        private static void ANameJustSaidIsNotSaidAgainByTheScreen()
        {
            var turn = new SpeakerTurn();
            turn.Next("Dannig");
            Equal(false, turn.JustSaid("Dannig", Start), "still waiting to be said");
            turn.Say("Farrow", Start);
            Equal(true, turn.JustSaid("Farrow", Start.AddMilliseconds(300)), "the label, a snapshot later");
            Equal(false, turn.JustSaid("Dannig", Start.AddMilliseconds(300)), "another name");
            Equal(false, turn.JustSaid("Farrow", Start.AddSeconds(5)), "long ago");
        }

        private static void AnEndedDialogueNamesItsSpeakerAgain()
        {
            var turn = new SpeakerTurn();
            turn.Say("Farrow", Start);
            turn.Forget();
            Equal(false, turn.JustSaid("Farrow", Start), "forgotten");
            Equal("Farrow", turn.Say("Farrow", Start), "new dialogue");
        }
    }
}
