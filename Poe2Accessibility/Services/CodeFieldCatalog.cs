namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using GameHelper.Utils;

    /// <summary>A field the game's code reads, and what follows from where it reads it.</summary>
    /// <param name="Name">The field, for the log.</param>
    /// <param name="Target"><c>Struct:Field[:bias]</c>, <c>layout:Name</c> or <c>report</c>.</param>
    /// <param name="Value">Where it was when the signatures were made.</param>
    /// <param name="Signatures">The byte sequences holding it.</param>
    public sealed record CodeFieldEntry(string Name, string Target, int Value, IReadOnlyList<CodeField> Signatures)
    {
        private const char Separator = ':';

        /// <summary>A move a remap cannot follow, only tell.</summary>
        public bool IsReport => this.Target == CodeFieldCatalog.ReportTarget;

        /// <summary>The plugin offset it sets, or null.</summary>
        public string? Layout => this.Target.StartsWith(CodeFieldCatalog.LayoutPrefix, StringComparison.Ordinal)
            ? this.Target[CodeFieldCatalog.LayoutPrefix.Length..] : null;

        /// <summary>The host struct it moves, or null.</summary>
        public string? Struct => this.IsReport || this.Layout != null ? null : this.Target.Split(Separator)[0];

        /// <summary>The struct's field: a dotted path through nested structs.</summary>
        public string Field => this.Target.Split(Separator)[1];

        /// <summary>The code reads this far past the field: a vector's second float, a key's other half.</summary>
        public int Bias => this.Target.Split(Separator) is { Length: > 2 } parts ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;
    }

    /// <summary>
    ///     <c>catalog/code-fields.txt</c>: the fields no live check can judge, each with the code that reads it. The code
    ///     is a candidate, not the truth: only two sequences that hit once and agree count, a miss keeps the constant,
    ///     and a live judgment beats it (docs/research/exe-layouts.md).
    /// </summary>
    public static class CodeFieldCatalog
    {
        public const string ReportTarget = "report";
        public const string LayoutPrefix = "layout:";

        private const char Column = '\t';
        private const char Comment = '#';
        private const string HexPrefix = "0x";
        private const int FirstSignature = 4;

        /// <summary>Fields have moved by a few bytes at a time; a value this far off is a hit on something else.</summary>
        private const int MaxMove = 0x400;

        /// <summary>The entries of the file; a line that does not parse is left out.</summary>
        public static IReadOnlyList<CodeFieldEntry> Parse(IEnumerable<string> lines)
        {
            var entries = new List<CodeFieldEntry>();
            foreach (var line in lines)
            {
                var columns = line.Split(Column);
                if (line.Length == 0 || line[0] == Comment || columns.Length <= FirstSignature
                    || !columns[2].StartsWith(HexPrefix, StringComparison.Ordinal)
                    || !int.TryParse(columns[2].AsSpan(HexPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                    || !int.TryParse(columns[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
                {
                    continue;
                }

                var name = columns[0];
                var signatures = columns.Skip(FirstSignature).Select(pattern => CodeFieldSearch.Parse(name, pattern, size)).ToList();
                entries.Add(new CodeFieldEntry(name, columns[1], value, signatures));
            }

            return entries;
        }

        /// <summary>Where the code reads each entry; an entry it does not settle has no value.</summary>
        public static IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Read(ReadOnlySpan<byte> code, IReadOnlyList<CodeFieldEntry> entries)
        {
            var read = new List<(CodeFieldEntry, int?)>(entries.Count);
            foreach (var entry in entries)
            {
                var found = CodeFieldSearch.Agreed(code, entry.Signatures);
                read.Add((entry, found is { } value && Math.Abs(value - entry.Value) <= MaxMove ? value - entry.Bias : null));
            }

            return read;
        }
    }
}
