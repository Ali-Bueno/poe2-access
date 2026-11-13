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

        /// <summary>The boss's arena changing, by the kind its state names ("burn", "collapse"; any other: it changes).</summary>
        public static string BossArena(string kind) => kind switch
        {
            "burn" => T("The arena burns", "La arena arde"),
            "dark" => T("The arena darkens", "La arena se oscurece"),
            "flood" => T("The arena floods", "La arena se inunda"),
            "lava" => T("Lava in the arena", "Lava en la arena"),
            "collapse" => T("The arena collapses", "La arena se derrumba"),
            "shrink" => T("The arena shrinks", "La arena se encoge"),
            "sandstorm" => T("Sandstorm", "Tormenta de arena"),
            "maze" => T("The arena turns into a maze", "La arena se vuelve un laberinto"),
            "platform" => T("The platforms change", "Las plataformas cambian"),
            _ => T("The arena changes", "La arena cambia"),
        };

        /// <summary>The element of the boss's damage to come, in the game's words for damage types; any other: it changes.</summary>
        public static string BossElement(string element) => element switch
        {
            "fire" => T("Element: fire", "Elemento: fuego"),
            "cold" => T("Element: cold", "Elemento: hielo"),
            "lightning" => T("Element: lightning", "Elemento: rayo"),
            "chaos" => T("Element: chaos", "Elemento: caos"),
            "physical" => T("Element: physical", "Elemento: físico"),
            _ => T("The element changes", "Cambia de elemento"),
        };

        /// <summary>A counter of the fight: its share of its top when the files give one, else its value.</summary>
        public static string BossProgress(long value, int? max) => max is > 0 and var top
            ? T($"Progress {value * 100 / top} percent", $"Progreso {value * 100 / top} por ciento")
            : T($"Progress {value}", $"Progreso {value}");

        /// <summary>A buff that empowers or protects the boss, in the game's own words; without them, that it is empowered.</summary>
        public static string BossEmpowered(string? name, string text) => (name, text.Length) switch
        {
            (null, 0) => T("The boss is empowered", "El jefe se fortalece"),
            (_, 0) => T($"{name} is empowered", $"{name} se fortalece"),
            (null, _) => T($"The boss: {text}", $"El jefe: {text}"),
            _ => $"{name}: {text}",
        };

        /// <summary>Adds came into the fight whose own skill empowers or protects the boss.</summary>
        public static string BossFed() => T("Its adds empower it", "Sus esbirros lo fortalecen");

        /// <summary>The player inside a boss fight's shelter, or out of it while it is still needed.</summary>
        public static string BossSheltered(bool inside) => inside ? T("Sheltered", "A cubierto") : T("Out of the shelter", "Fuera del refugio");

        /// <summary>The boss starts an attack that seizes the player.</summary>
        public static string BossGrab() => T("Grab!", "¡Agarre!");

        /// <summary>The player is held in place.</summary>
        public static string BossGrabbed() => T("Held!", "¡Atrapado!");

        /// <summary>A counted buff of the fight on the player: "Explosive Retch, 3".</summary>
        public static string BossStacks(string name, int count) => name.Length == 0 ? T($"{count} stacks", $"{count} acumulaciones") : $"{name}, {count}";

        /// <summary>A place of a boss's arena and the way to it: "Shelter at Asala: follow the heartbeat, up", "Lever, can be used, left".</summary>
        public static string BossPlace(BossPlaceKind kind, string name, int octant, bool heartbeat)
        {
            var way = StickDirections[((octant % StickDirections.Length) + StickDirections.Length) % StickDirections.Length];
            var named = name.Length > 0;
            var what = kind switch
            {
                BossPlaceKind.Refuge => named ? T($"Shelter at {name}", $"Refugio junto a {name}") : T("Shelter", "Refugio"),
                BossPlaceKind.Clone => T("The true one", "El verdadero"),
                BossPlaceKind.Cover => named ? T($"Cover, {name}", $"Cobertura, {name}") : T("Cover", "Cobertura"),
                BossPlaceKind.Use => named ? T($"{name}, can be used", $"{name}, se puede usar") : T("Something to use", "Algo que usar"),
                _ => named ? T($"{name}, can be broken", $"{name}, se puede romper") : T("Something to break", "Algo que romper"),
            };
            return heartbeat ? $"{what}: {T("follow the heartbeat", "sigue el latido")}, {way}" : $"{what}, {way}";
        }

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
