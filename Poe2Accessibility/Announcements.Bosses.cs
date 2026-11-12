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

        /// <summary>A boss hiding somewhere in its arena until the player finds it, and the way there: "The boss is hiding: follow the heartbeat, down left".</summary>
        public static string BossHiding(string? name, int octant, bool heartbeat)
        {
            var hiding = name == null ? T("The boss is hiding", "El jefe se esconde") : T($"{name} is hiding", $"{name} se esconde");
            var way = StickDirections[((octant % StickDirections.Length) + StickDirections.Length) % StickDirections.Length];
            return heartbeat ? $"{hiding}: {T("follow the heartbeat", "sigue el latido")}, {way}" : $"{hiding}, {way}";
        }

        /// <summary>
        ///     The walls a boss raised around the player, and the way out of them: "Walls: follow the heartbeat to the way out,
        ///     up", or with no way out on foot "Walls all around: follow the heartbeat and break one, left".
        /// </summary>
        public static string BossWalls(bool open, int octant, bool heartbeat)
        {
            var way = StickDirections[((octant % StickDirections.Length) + StickDirections.Length) % StickDirections.Length];
            var walls = open ? T("Walls", "Muros") : T("Walls all around", "Muros por todos lados");
            var act = (open, heartbeat) switch
            {
                (true, true) => T("follow the heartbeat to the way out", "sigue el latido hasta la salida"),
                (true, false) => T("the way out", "la salida"),
                (false, true) => T("follow the heartbeat and break one", "sigue el latido y rompe uno"),
                _ => T("break one", "rompe uno"),
            };
            return $"{walls}: {act}, {way}";
        }

        /// <summary>The player walked out of a boss's walls.</summary>
        public static string BossWallsLeft() => T("Out of the walls", "Fuera de los muros");

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
