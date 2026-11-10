namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Models;

    /// <summary>Pure: whether a focused control that changed in place has news, and which part of it.</summary>
    public static class FocusNews
    {
        private const string PartSeparator = ", ";

        /// <summary>A length of time ("11 minutos 46 segundos") or a clock's digits ("12:28:45"): what counts down by itself.</summary>
        private static readonly Regex Duration = new(@"^(\d+\s+\p{L}+\s*)+$|\d:\d\d", RegexOptions.Compiled);

        /// <summary>
        ///     The parts of a focused cell, slot or list row that changed when only its numbers did ("Orbe de aumento, 3" to
        ///     "Orbe de aumento, 2" gives "2", a gem's "Nivel 7" its "Nivel 8"); null for any other control or change, and
        ///     for a clock ticking in the row.
        /// </summary>
        public static string? ChangedNumbers(UiRole role, string before, string after)
        {
            if (role is not (UiRole.GridCell or UiRole.ItemSlot or UiRole.ListItem) || before == after || !ScreenDiffer.SameBesideNumbers(before, after))
            {
                return null;
            }

            var was = before.Split(PartSeparator);
            var now = after.Split(PartSeparator);
            if (was.Length != now.Length)
            {
                return after;
            }

            var changed = now.Where((part, index) => part != was[index]).ToList();
            return changed.All(part => Duration.IsMatch(part)) ? null : string.Join(PartSeparator, changed);
        }

        /// <summary>A counter ticking by itself in the focused text (the Instance Manager's time left): said once, never as news.</summary>
        public static bool IsTick(UiRole role, string before, string after) =>
            ScreenDiffer.SameBesideNumbers(before, after) && ChangedNumbers(role, before, after) == null;
    }
}
