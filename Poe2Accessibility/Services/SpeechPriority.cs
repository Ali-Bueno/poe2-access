namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>Kinds of gameplay news whose delivery the player picks in the settings menu (D2R speech priority).</summary>
    public enum SpeechKind
    {
        StatusBegins,
        StatusEnds,
        LifeAndMana,
        BossLife,
        EnemyNames,
        Quest,
        Places,
        GroundItems,
        Arrivals,
    }

    /// <summary>
    ///     D2R speech priority: each kind of news is delivered at one of four levels — waits its turn, cuts in when
    ///     urgent, cuts in, or cuts in and holds (later interrupts queue behind it for a moment). Menus and the focus
    ///     always interrupt; they are not news.
    /// </summary>
    public static class SpeechPriority
    {
        public const string Waits = "waits";
        public const string Urgent = "urgent";
        public const string CutsIn = "cuts";
        public const string Holds = "holds";

        /// <summary>How long a queued life or mana warning keeps later interrupts behind it (the length of one such line).</summary>
        private const int WaitingVitalHoldMs = 1500;

        /// <summary>The levels in the settings menu's order, quietest first.</summary>
        public static readonly string[] Levels = { Waits, Urgent, CutsIn, Holds };

        /// <summary>The player's level for a kind (from the settings); unset, the D2R defaults apply.</summary>
        public static Func<SpeechKind, string>? Chosen { get; set; }

        /// <summary>D2R defaults: a state beginning and an arrival hold, a state ending cuts in, quest lines and pick-ups wait.</summary>
        public static string Default(SpeechKind kind) => kind switch
        {
            SpeechKind.StatusBegins or SpeechKind.Arrivals => Holds,
            SpeechKind.StatusEnds => CutsIn,
            SpeechKind.Quest or SpeechKind.GroundItems => Waits,
            _ => Urgent,
        };

        /// <summary>Whether this news interrupts what is being said (a held line holds only through <see cref="Say"/>).</summary>
        public static bool Interrupts(SpeechKind kind, bool urgent = false) => Level(kind) switch
        {
            Holds or CutsIn => true,
            Urgent => urgent,
            _ => false,
        };

        /// <summary>Speaks a piece of news at the level the player chose for its kind; urgent news cutting in also holds.</summary>
        public static void Say(this IAccessibilityOutputService output, SpeechKind kind, string text, bool urgent = false)
        {
            if (Level(kind) == Holds || (urgent && Level(kind) == Urgent))
            {
                output.Urge(text);
                return;
            }

            if (kind == SpeechKind.LifeAndMana && !Interrupts(kind, urgent))
            {
                // A waiting life warning was dropped by a state beginning 80 ms later (2026-10-24): it keeps its turn.
                output.Keep(text, WaitingVitalHoldMs);
                return;
            }

            output.Speak(text, Interrupts(kind, urgent));
        }

        private static string Level(SpeechKind kind) => Chosen?.Invoke(kind) is { } chosen && Array.IndexOf(Levels, chosen) >= 0 ? chosen : Default(kind);
    }
}
