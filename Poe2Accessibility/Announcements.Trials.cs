namespace Poe2Accessibility
{
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of the Ascendancy trials beyond the Sekhemas HUD: rounds, room doors, the trials' icons.</summary>
    public static partial class Announcements
    {
        /// <summary>The Trial of the Sekhemas' room door, as the guide names it.</summary>
        public static string TrialDoor => T("room door", "puerta de la sala");

        /// <summary>"Room door open: 12 steps, up" — the chosen room's door, with the way to it.</summary>
        public static string TrialDoorOpen(string described) => T($"Room door open: {described}", $"Puerta de la sala abierta: {described}");

        /// <summary>"Round 3 of 10" — a Trial of Chaos round beginning.</summary>
        public static string TrialRound(int round, int of) => of > 0 ? T($"Round {round} of {of}", $"Ronda {round} de {of}") : T($"Round {round}", $"Ronda {round}");

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
