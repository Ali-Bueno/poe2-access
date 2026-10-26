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

        /// <summary>The focus's name alone, as a header over its details ("Terremoto" for "Terremoto, Nivel 1").</summary>
        public static bool Names(string text, string focusText) =>
            text.Length > 0 && (focusText == text || focusText.StartsWith(text + ", ", StringComparison.Ordinal));
    }
}
