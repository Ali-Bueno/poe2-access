namespace Poe2Accessibility
{
    using System.Linq;
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the Ascendancy trials beyond the Sekhemas HUD: rounds, room doors, the trials' icons.</summary>
    public static partial class Announcements
    {
        /// <summary>A shrine's line with what it asks in the Trial's currency ("Honra a Tabana, A, cuesta 5 de agua sagrada"); as it is with no cost.</summary>
        public static string WithCost(string text, int cost) =>
            cost > 0 ? T($"{text}, costs {cost} Sacred Water", $"{text}, cuesta {cost} de agua sagrada") : text;

        /// <summary>The Trial of the Sekhemas' room door, as the guide names it.</summary>
        public static string TrialDoor => T("room door", "puerta de la sala");

        /// <summary>"Room door open: 12 steps, up" — the chosen room's door, with the way to it.</summary>
        public static string TrialDoorOpen(string described) => T($"Room door open: {described}", $"Puerta de la sala abierta: {described}");

        /// <summary>"Round 3 of 10" — a Trial of Chaos round beginning.</summary>
        public static string TrialRound(int round, int of) => of > 0 ? T($"Round {round} of {of}", $"Ronda {round} de {of}") : T($"Round {round}", $"Ronda {round}");

        /// <summary>A class on the Altar of Ascendancy: "Herrero de Kitava, 3 de 3, en la build. Ansías el poder…".</summary>
        public static string AscendancyCard(string name, int oneBasedIndex, int count, bool inBuild, string about) =>
            Sentences(new[]
            {
                string.Join(", ", new[] { name, ListPosition(oneBasedIndex, count), inBuild ? InBuild(false) : string.Empty }.Where(part => part.Length > 0)),
                about,
            });

        /// <summary>An icon of the trials by what it stands for: "Affliction: Honed Claws. Monsters deal 30% more Damage".</summary>
        public static string TrialEntry(TrialEntryKind kind, string name, string about)
        {
            var text = name.Length > 0 && about.Length > 0 ? $"{name}. {about}" : name.Length > 0 ? name : about;
            if (text.Length == 0)
            {
                return string.Empty;
            }

            return kind switch
            {
                // The Spanish client's own words: "Te aplica la desventaja Niebla roja al entrar", "te aplica una ventaja" (2026-11-12).
                TrialEntryKind.Affliction => T($"Affliction: {text}", $"Desventaja: {text}"),
                TrialEntryKind.Boon => T($"Boon: {text}", $"Ventaja: {text}"),
                TrialEntryKind.Modifier => T($"Modifier: {text}", $"Modificador: {text}"),
                TrialEntryKind.Ascendancy => T($"Ascendancy: {text}", $"Ascendencia: {text}"),
                _ => text,
            };
        }
    }
}
