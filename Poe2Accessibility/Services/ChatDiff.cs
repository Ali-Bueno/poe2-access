namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    ///     New chat lines between two readings of the message list. The list is a sliding window (old lines drop
    ///     off the top, new ones are appended) and the game rebuilds its line elements, so only the text sequence
    ///     is trusted: the lines after the longest overlap of the old tail with the new head are new. Anything that
    ///     does not line up (history expanded, list rebuilt) is a fresh baseline, never a burst of old lines.
    /// </summary>
    public static class ChatDiff
    {
        /// <summary>A chat line as spoken: the speaker's colon followed by a space ("Renly:Esto es lo que tengo.", 2026-10-24).</summary>
        public static string Spoken(string line)
        {
            var colon = line.IndexOf(':');
            return colon > 0 && colon + 1 < line.Length && !char.IsWhiteSpace(line[colon + 1]) && !char.IsDigit(line[colon + 1])
                ? $"{line[..(colon + 1)]} {line[(colon + 1)..]}"
                : line;
        }

        public static IReadOnlyList<string> Appended(IReadOnlyList<string> before, IReadOnlyList<string> after)
        {
            if (before.Count == 0)
            {
                return after;
            }

            for (var dropped = 0; dropped < before.Count; dropped++)
            {
                var overlap = before.Count - dropped;
                if (overlap > after.Count || !SameRun(before, dropped, after, overlap))
                {
                    continue;
                }

                var added = new List<string>();
                for (var i = overlap; i < after.Count; i++)
                {
                    added.Add(after[i]);
                }

                return added;
            }

            return Array.Empty<string>();
        }

        private static bool SameRun(IReadOnlyList<string> before, int start, IReadOnlyList<string> after, int length)
        {
            for (var i = 0; i < length; i++)
            {
                if (!string.Equals(before[start + i], after[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
