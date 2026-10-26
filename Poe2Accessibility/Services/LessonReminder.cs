namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>When a tutorial lesson already said is said again. Pure.</summary>
    public static class LessonReminder
    {
        /// <summary>
        ///     A lesson shown again during play is said again after this long: the low-mana lesson came back 28 s after it
        ///     was said and held the character until the D-pad right flask, in silence (2026-10-24).
        /// </summary>
        public const int RepeatMs = 5000;

        /// <summary>
        ///     A lesson still pending comes back whenever a panel closes or the focus moves: said again once this long has
        ///     passed, so it is not lost behind other lines (the passive point, 2026-10-26) nor repeated on every move.
        /// </summary>
        public const int RemindMs = 60000;

        /// <summary>Whether a lesson said <paramref name="since"/> ago is said again; <paramref name="boxBack"/>: in a menu or with a panel just closed.</summary>
        public static bool IsDue(TimeSpan since, bool boxBack) => since.TotalMilliseconds >= (boxBack ? RemindMs : RepeatMs);
    }
}
