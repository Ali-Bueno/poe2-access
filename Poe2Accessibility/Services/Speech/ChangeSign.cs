namespace Poe2Accessibility.Services
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>The game's "before > after" change, spoken with a word in place of the sign.</summary>
    public static class ChangeSign
    {
        private const string Separated = " > ";

        /// <summary>A sign with spaces around it, a value ending on its left and one starting on its right.</summary>
        private static readonly Regex Sign = new(@"(?<=[\p{L}\p{N}%)\]]) > (?=[\p{L}\p{N}+\-(\[])", RegexOptions.Compiled);

        /// <summary>The text with each "before > after" said as "before to after"; a sign inside markup or between path words stays.</summary>
        public static string Spoken(string text)
        {
            if (!text.Contains(Separated, StringComparison.Ordinal))
            {
                return text;
            }

            return Sign.Replace(text, match => InMarkup(text, match.Index) || BesidePath(text, match.Index) ? match.Value : $" {Announcements.ChangeWord} ");
        }

        /// <summary>True when a tag opened before the index is still open there.</summary>
        private static bool InMarkup(string text, int index) => text.LastIndexOf('<', index) > text.LastIndexOf('>', index);

        /// <summary>True when the word on either side of the sign holds a path separator.</summary>
        private static bool BesidePath(string text, int index)
        {
            var start = text.LastIndexOf(' ', index - 1) + 1;
            var rightStart = index + Separated.Length;
            var after = text.IndexOf(' ', rightStart);
            var end = after < 0 ? text.Length : after;
            return text.AsSpan(start, index - start).IndexOfAny('/', '\\') >= 0 || text.AsSpan(rightStart, end - rightStart).IndexOfAny('/', '\\') >= 0;
        }
    }
}
