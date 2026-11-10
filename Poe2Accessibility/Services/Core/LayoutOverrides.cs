namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using GameReader.Utils;

    /// <summary>
    ///     <c>config/layout-overrides.txt</c>: where a field sits in one game build that the mod does not follow yet,
    ///     written by hand until a new mod version (docs/patch-day.md). Each line is placed as if the game's code read it
    ///     there, and replaces the code's reading of the same field. A line counts only under the build it names, so the
    ///     next patch cannot inherit it. Pure.
    /// </summary>
    public static class LayoutOverrides
    {
        public const string FileName = "layout-overrides.txt";

        private const string BuildKeyword = "build";
        private const string HexPrefix = "0x";
        private const char Comment = '#';
        private const char TargetSeparator = ':';
        private const int WordsPerField = 3;

        /// <summary>
        ///     The fields for the exe of <paramref name="exeSize"/> bytes, as code-field readings: <c>build 78440792</c>, then
        ///     <c>AreaInstanceOffsets:PlayerInfo 0x5B8 0x5C0</c> or <c>layout:SkillLevelKey 0x58 0x60</c> (the target,
        ///     where the mod has it, where this build has it).
        /// </summary>
        /// <param name="lines">The file.</param>
        /// <param name="exeSize">The running game exe's size, the build's id.</param>
        /// <param name="otherBuilds">Field lines under other builds, which are not used.</param>
        /// <param name="rejected">Lines under this build that do not parse.</param>
        public static IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Parse(IEnumerable<string> lines, long exeSize, out int otherBuilds, out IReadOnlyList<string> rejected)
        {
            var fields = new List<(CodeFieldEntry, int?)>();
            var bad = new List<string>();
            var current = false;
            otherBuilds = 0;
            foreach (var raw in lines)
            {
                var words = raw.Split(Comment)[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }

                if (words[0] == BuildKeyword)
                {
                    current = words.Length == 2 && long.TryParse(words[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size == exeSize;
                    continue;
                }

                if (!current)
                {
                    otherBuilds++;
                    continue;
                }

                if (words.Length != WordsPerField || !IsTarget(words[0]) || !TryHex(words[1], out var mod) || !TryHex(words[2], out var game))
                {
                    bad.Add(raw.Trim());
                    continue;
                }

                fields.Add((new CodeFieldEntry($"override {words[0]}", words[0], mod, Array.Empty<CodeField>()), game));
            }

            rejected = bad;
            return fields;
        }

        /// <summary>The code's readings with each overridden field replaced by the override.</summary>
        public static IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Apply(
            IReadOnlyList<(CodeFieldEntry Entry, int? Found)> read, IReadOnlyList<(CodeFieldEntry Entry, int? Found)> overrides)
        {
            if (overrides.Count == 0)
            {
                return read;
            }

            var replaced = overrides.Select(field => field.Entry.SettleKey).ToHashSet(StringComparer.Ordinal);
            return read.Where(field => !replaced.Contains(field.Entry.SettleKey)).Concat(overrides).ToList();
        }

        /// <summary><c>Struct:Field</c> (a dotted path) or <c>layout:Name</c>; never a report, never a bias.</summary>
        private static bool IsTarget(string target)
        {
            var parts = target.Split(TargetSeparator);
            return parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0 && target != CodeFieldCatalog.ReportTarget;
        }

        private static bool TryHex(string word, out int value)
        {
            value = 0;
            return word.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(word.AsSpan(HexPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
        }
    }
}
