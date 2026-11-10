namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Texts a focus move brought into its layer: the focused item's own details, or rows scrolling in. Pure.</summary>
    public static class FocusDetails
    {
        /// <summary>
        ///     Details headed by the focus's name ("Destrozahuesos" over a skill's tags and description, the old details gone
        ///     as they came), not rows scrolling in (the focused row itself at most); <paramref name="labelName"/> marks a
        ///     label that is no detail (an item's tier numeral).
        /// </summary>
        public static bool HeadedBy(IReadOnlyList<ScreenText> texts, string focusText, string labelName) =>
            texts.Count > 1 && Names(texts[0].Text, focusText) &&
            texts.Skip(1).Any(text => text.Name != labelName && !focusText.Contains(text.Text, StringComparison.Ordinal));

        /// <summary>
        ///     Where a focus that arrived with a tabbed page is: the page's selected tab when the focus is on that page or
        ///     names no part of its own (options), else its own part — the stash opens with the cursor on the inventory, and
        ///     "tab 1" before an inventory item placed it in the stash (2026-11-08).
        /// </summary>
        public static string PagePlace(string focusContext, string tab)
        {
            var title = Announcements.TabTitle(tab);
            return focusContext.Length == 0 || focusContext.Split(". ").Contains(title) ? title : focusContext;
        }

        /// <summary>
        ///     The tabbed page that opened beside the focus's own part, named with its selected tab (a merchant's "Armas",
        ///     "Alijo, pestaña 1"): said before the focus's part, so the page is known without its tab being taken for where
        ///     the focus is. Empty when the focus is on the page itself, which <see cref="PagePlace"/> names.
        /// </summary>
        /// <param name="sidePanel">The title of the panel beside the inventory, empty when it shows none.</param>
        public static string PageBeside(string focusContext, string tab, string sidePanel)
        {
            var title = Announcements.TabTitle(tab);
            if (title.Length == 0 || PagePlace(focusContext, tab) == title)
            {
                return string.Empty;
            }

            return sidePanel.Length > 0 && sidePanel != focusContext && !sidePanel.Contains(title, StringComparison.Ordinal) ? $"{sidePanel}, {title}" : title;
        }

        /// <summary>The focus's name alone, as a header over its details ("Terremoto" for "Terremoto, Nivel 1").</summary>
        public static bool Names(string text, string focusText) =>
            text.Length > 0 && (focusText == text || focusText.StartsWith(text + ", ", StringComparison.Ordinal));

        /// <summary>
        ///     What a focused tooltip's change adds. While it is being built, a line the focus announcement holds is no news; a line
        ///     replaced in place is, held or not ("Forma de oso" back after "Forma de hombre lobo", Y on a talisman, 2026-11-06).
        /// </summary>
        public static List<string> NewTooltipLines(IReadOnlyList<string> said, IReadOnlyList<string> tooltip, string focusSpeech)
        {
            var inPlace = said.Count == tooltip.Count;
            return tooltip.Where(line => !said.Contains(line) && (inPlace || !focusSpeech.Contains(line, StringComparison.Ordinal))).ToList();
        }

        /// <summary>
        ///     A line replaced by another answers a press, and the next press cuts it ("Forma de oso", "Forma de hombre lobo", Y pressed
        ///     again and again, 2026-11-06); a number ticking in a line ("Actualmente tiene 6 cargas") is no reason to cut what is said.
        /// </summary>
        public static bool LineSwapped(IReadOnlyList<string> said, IReadOnlyList<string> tooltip) =>
            said.Count == tooltip.Count && said.Zip(tooltip).Any(pair => !ScreenDiffer.SameBesideNumbers(pair.First, pair.Second));
    }
}
