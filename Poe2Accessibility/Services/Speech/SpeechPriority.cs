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
        Charges,
        Combo,
        Cooldowns,
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

        /// <summary>The levels in the settings menu's order, quietest first.</summary>
        public static readonly string[] Levels = { Waits, Urgent, CutsIn, Holds };

        /// <summary>The player's level for a kind (from the settings); unset, the D2R defaults apply.</summary>
        public static Func<SpeechKind, string>? Chosen { get; set; }

        /// <summary>D2R defaults: a state beginning and an arrival hold, a state ending cuts in; quest lines, pick-ups, charges, combo and skills ready wait.</summary>
        public static string Default(SpeechKind kind) => kind switch
        {
            SpeechKind.StatusBegins or SpeechKind.Arrivals => Holds,
            SpeechKind.StatusEnds => CutsIn,
            SpeechKind.Quest or SpeechKind.GroundItems or SpeechKind.Charges or SpeechKind.Combo or SpeechKind.Cooldowns => Waits,
            _ => Urgent,
        };

        /// <summary>Whether this news interrupts what is being said (a held line holds only through <see cref="Say"/>).</summary>
        public static bool Interrupts(SpeechKind kind, bool urgent = false) => Level(kind) switch
        {
            Holds or CutsIn => true,
            Urgent => urgent,
            _ => false,
        };

        /// <summary>
        ///     News that is a number true only now (a percentage, a count held), not something that happened. Urgent news of
        ///     these kinds is never one: a boss's turn or defeat, a partner down, life at its last mark are said however late.
        ///     Neither is a line that says something <paramref name="happened"/> under a reading kind (a flask drunk, a buff
        ///     begun, rage full): the kind only says which setting the player turns, not what the line is. The level
        ///     does not matter: a held line is never aged, and the others are judged by the kind alone.
        /// </summary>
        public static bool IsReading(SpeechKind kind, bool urgent = false, bool happened = false) =>
            !urgent && !happened && kind is SpeechKind.LifeAndMana or SpeechKind.BossLife or SpeechKind.Charges or SpeechKind.Combo;

        /// <summary>
        ///     Speaks a piece of news at the level the player chose for its kind; urgent news cutting in also holds. News
        ///     held only by its level (a state that began, an arrival) yields to the player's own move.
        ///     <paramref name="happened"/>: the line reports an event under a kind that otherwise carries readings.
        /// </summary>
        public static void Say(this IAccessibilityOutputService output, SpeechKind kind, string text, bool urgent = false, bool happened = false)
        {
            if (Level(kind) == Holds || (urgent && Level(kind) == Urgent))
            {
                output.Urge(text, yieldsToMoves: !urgent);
                return;
            }

            var reading = IsReading(kind, urgent, happened);
            if (kind == SpeechKind.LifeAndMana && !Interrupts(kind, urgent))
            {
                // A waiting life warning was dropped by a state beginning 80 ms later (2026-10-24).
                if (reading)
                {
                    output.Mention(text);
                }
                else
                {
                    output.MentionNews(text);
                }

                return;
            }

            if (reading)
            {
                output.SpeakReading(text, Interrupts(kind, urgent));
                return;
            }

            output.Speak(text, Interrupts(kind, urgent));
        }

        private static string Level(SpeechKind kind) => Chosen?.Invoke(kind) is { } chosen && Array.IndexOf(Levels, chosen) >= 0 ? chosen : Default(kind);
    }
}
