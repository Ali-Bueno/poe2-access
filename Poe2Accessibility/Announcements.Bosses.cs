namespace Poe2Accessibility
{
    using Poe2Accessibility.Services;

    /// <summary>Spoken strings of boss fights' turns and of the way out of a boss's danger area (docs/research/boss-mechanics.md).</summary>
    public static partial class Announcements
    {
        /// <summary>A turn of the fight; the boss is named only when two or more fight at once.</summary>
        public static string BossTurn(BossNews news, string? name, int adds = 0, int phase = 0) => news switch
        {
            BossNews.Gone => name == null ? T("The boss vanished", "El jefe ha desaparecido") : T($"{name} vanished", $"{name} ha desaparecido"),
            BossNews.Back => name == null ? T("The boss is back", "El jefe ha vuelto") : T($"{name} is back", $"{name} ha vuelto"),
            BossNews.Invulnerable => (name == null ? T("Invulnerable", "Invulnerable") : T($"{name} invulnerable", $"{name} invulnerable")) +
                                     (adds > 0 ? ", " + BossAdds(adds) : string.Empty),
            BossNews.Vulnerable => name == null ? T("Vulnerable again", "Vuelve a ser vulnerable") : T($"{name} can be hurt again", $"{name} vuelve a ser vulnerable"),
            BossNews.Enraged => name == null ? T("Enraged", "Enfurecido") : T($"{name} enraged", $"{name} enfurecido"),
            _ => name == null ? T($"Phase {phase}", $"Fase {phase}") : T($"Phase {phase}: {name}", $"Fase {phase}: {name}"),
        };

        /// <summary>"3 adds": the monsters a boss brought, in play.</summary>
        public static string BossAdds(int count) => count == 1 ? T("1 add", "1 esbirro") : T($"{count} adds", $"{count} esbirros");

        /// <summary>The way out of a danger area, short enough to act on: "Out!", "To the centre!", "Left!".</summary>
        public static string Counterplay(CounterplayKind kind, int octant) => kind switch
        {
            CounterplayKind.Out => T("Out!", "¡Sal!"),
            CounterplayKind.Centre => T("To the centre!", "¡Al centro!"),
            CounterplayKind.Behind => T("Behind it!", "¡Detrás!"),
            CounterplayKind.Move => T("Move!", "¡Muévete!"),
            _ => StepWords[((octant % StepWords.Length) + StepWords.Length) % StepWords.Length],
        };

        /// <summary>The way to step, by <see cref="StickDirection"/> octant, as the stick is pushed.</summary>
        private static string[] StepWords => new[]
        {
            T("Right!", "¡Derecha!"), T("Up right!", "¡Arriba a la derecha!"), T("Up!", "¡Arriba!"), T("Up left!", "¡Arriba a la izquierda!"),
            T("Left!", "¡Izquierda!"), T("Down left!", "¡Abajo a la izquierda!"), T("Down!", "¡Abajo!"), T("Down right!", "¡Abajo a la derecha!"),
        };
    }
}
