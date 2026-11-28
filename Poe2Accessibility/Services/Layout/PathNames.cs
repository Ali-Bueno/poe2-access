namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>A path of <c>catalog/path-names.txt</c> and the first source file matching it.</summary>
    public readonly record struct PathName(string Path, string Where);

    /// <summary>
    ///     The game's files the plugin matches by their path: every "Metadata/…" its source holds (a boss's piece, a league's
    ///     object, a kind of chest), whole or as the start of one. <c>catalog/path-names.txt</c> lists them (tests' dev command
    ///     <c>path-names</c>) and the data step "game names" looks each up in the game's files after a patch: one that starts
    ///     no file's path was renamed or removed, so what matched it has gone silent and the step says so
    ///     (docs/compatibility.md, "Names in the game's files"). Pure.
    /// </summary>
    public static class PathNames
    {
        public const string FileName = "path-names.txt";

        /// <summary>How the game's own files start: entities carry these paths.</summary>
        public const string Root = "Metadata/";

        private const char Column = '\t';
        private const char Comment = '#';
        private const string LineComment = "//";

        private static readonly Regex Literal = new("\"(" + Root + "(?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);

        /// <summary>The entries of the file; a line that does not parse is left out.</summary>
        public static IReadOnlyList<PathName> Parse(IEnumerable<string> lines) =>
            lines.Where(line => line.Length > 0 && line[0] != Comment).Select(line => line.Split(Column))
                .Where(columns => columns[0].StartsWith(Root, StringComparison.Ordinal))
                .Select(columns => new PathName(columns[0], columns.Length > 1 ? columns[1] : string.Empty)).ToList();

        public static string Line(PathName entry) => $"{entry.Path}{Column}{entry.Where}";

        /// <summary>The paths a source file matches: its string literals that start as the game's files do. Comments are not read.</summary>
        public static IEnumerable<string> Candidates(string source)
        {
            foreach (var raw in source.Split('\n'))
            {
                var comment = raw.IndexOf(LineComment, StringComparison.Ordinal);
                foreach (Match literal in Literal.Matches(comment < 0 ? raw : raw[..comment]))
                {
                    // A text with a hole in it is made at run time: not a path to look up.
                    if (!literal.Groups[1].Value.Contains('{'))
                    {
                        yield return literal.Groups[1].Value;
                    }
                }
            }
        }

        /// <summary>
        ///     The entries that start no path of <paramref name="sortedLowerPaths"/>: the game's files, lower case, in ordinal
        ///     order. A literal is the whole of a file's path but its extension, or the folders above some.
        /// </summary>
        public static IReadOnlyList<PathName> Gone(string[] sortedLowerPaths, IEnumerable<PathName> entries)
        {
            var gone = new List<PathName>();
            foreach (var entry in entries)
            {
                var start = entry.Path.ToLowerInvariant();
                var at = Array.BinarySearch(sortedLowerPaths, start, StringComparer.Ordinal);
                at = at < 0 ? ~at : at;
                if (at >= sortedLowerPaths.Length || !sortedLowerPaths[at].StartsWith(start, StringComparison.Ordinal))
                {
                    gone.Add(entry);
                }
            }

            return gone;
        }
    }
}
