namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;

    /// <summary>Pure: which focus reads without a name, and the log line a developer fixes it from (docs/research/ui-unseen-panels.md).</summary>
    public static class UnnamedFocus
    {
        /// <summary>The log prefix of a focus nothing named, and of a control named by its art; both are greppable in a player's log.</summary>
        public const string FocusPrefix = "Unnamed focus:";

        public const string ArtPrefix = "Control named from its art:";

        /// <summary>Elements remembered as logged before the memory starts over.</summary>
        public const int MaxLogged = 2000;

        /// <summary>Ancestors a log line lists.</summary>
        public const int MaxAncestors = 5;

        /// <summary>True when the focus is announced with no label, value or tooltip title: a bare role word or only a position.</summary>
        public static bool Is(string text, string value, int tooltipLines, bool emptyCell) =>
            text.Length == 0 && value.Length == 0 && tooltipLines == 0 && !emptyCell;

        public static string Details(string role, IReadOnlyList<string> ancestors, IReadOnlyList<string> art, int children, string layer) =>
            $"role={role} ancestors=[{string.Join(" < ", ancestors)}] art=[{string.Join(", ", art)}] children={children} layer=\"{layer}\"";
    }
}
