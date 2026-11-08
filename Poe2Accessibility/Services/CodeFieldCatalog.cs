namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using GameHelper.Utils;
    using GameHelper.Utils.CodeReader;

    /// <summary>A field the game's code reads, and what follows from where it reads it.</summary>
    /// <param name="Name">The field, for the log.</param>
    /// <param name="Target"><c>Struct:Field[:bias]</c>, <c>layout:Name</c> or <c>report</c>.</param>
    /// <param name="Value">Where it was when the signatures were made.</param>
    /// <param name="Signatures">The byte sequences holding it.</param>
    /// <param name="Rule">Or the semantic rule reading it (<c>catalog/semantic-fields.txt</c>).</param>
    public sealed record CodeFieldEntry(string Name, string Target, int Value, IReadOnlyList<CodeField> Signatures, SemanticRule? Rule = null)
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

        /// <summary>The field's own place when the signatures were made: what a reading (bias off) compares with.</summary>
        public int Measured => this.Value - this.Bias;
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
        private const int RuleColumn = 3;

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

        /// <summary>
        ///     The entries of <c>catalog/semantic-fields.txt</c>: name, target, value at the last measured build and a
        ///     <see cref="SemanticRule"/>; a line that does not parse is left out.
        /// </summary>
        public static IReadOnlyList<CodeFieldEntry> ParseSemantic(IEnumerable<string> lines)
        {
            var entries = new List<CodeFieldEntry>();
            foreach (var line in lines)
            {
                var columns = line.Split(Column);
                if (line.Length == 0 || line[0] == Comment || columns.Length != RuleColumn + 1 || ValueOf(columns[2]) is not { } value
                    || SemanticRule.Parse(columns[RuleColumn]) is not { } rule)
                {
                    continue;
                }

                entries.Add(new CodeFieldEntry(columns[0], columns[1], value, Array.Empty<CodeField>(), rule));
            }

            return entries;
        }

        /// <summary>Where the semantic rules read each entry, from the exe file; as <see cref="Read"/>, a far value is not believed.</summary>
        public static IReadOnlyList<(CodeFieldEntry Entry, int? Found)> ReadSemantic(byte[] file, IReadOnlyList<CodeFieldEntry> entries)
        {
            var values = SemanticFieldReader.Read(file, entries.Select(entry => entry.Rule!).ToList());
            return entries.Select((entry, index) => (entry, values[index] is { } value ? Believed(entry, value) : null)).ToList();
        }

        /// <summary>
        ///     One value per target from both sources, the first entry naming it: a target the sources read differently is
        ///     left unsettled, and listed.
        /// </summary>
        public static (IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Settled, IReadOnlyList<string> Disagree) Settle(
            IReadOnlyList<(CodeFieldEntry Entry, int? Found)> code, IReadOnlyList<(CodeFieldEntry Entry, int? Found)> semantic)
        {
            var settled = new List<(CodeFieldEntry, int?)>();
            var disagree = new List<string>();
            foreach (var target in code.Concat(semantic).GroupBy(read => read.Entry.Target))
            {
                var values = target.Where(read => read.Found != null).Select(read => read.Found!.Value).Distinct().ToList();
                if (values.Count > 1)
                {
                    disagree.Add($"{target.First().Entry.Name} ({string.Join(" or ", values.Select(value => $"+0x{value:X}"))})");
                }

                settled.Add((target.First().Entry, values.Count == 1 ? values[0] : null));
            }

            return (settled, disagree);
        }

        /// <summary>Where the code reads each entry; an entry it does not settle has no value.</summary>
        public static IReadOnlyList<(CodeFieldEntry Entry, int? Found)> Read(ReadOnlySpan<byte> code, IReadOnlyList<CodeFieldEntry> entries)
        {
            var read = new List<(CodeFieldEntry, int?)>(entries.Count);
            foreach (var entry in entries)
            {
                read.Add((entry, CodeFieldSearch.Agreed(code, entry.Signatures) is { } value ? Believed(entry, value) : null));
            }

            return read;
        }

        /// <summary>The field's offset from a value the code reads at its bias; null when too far from where it was to be it.</summary>
        private static int? Believed(CodeFieldEntry entry, long value) =>
            Math.Abs(value - entry.Value) <= MaxMove ? (int)(value - entry.Bias) : null;

        private static int? ValueOf(string column) =>
            column.StartsWith(HexPrefix, StringComparison.Ordinal) && int.TryParse(column.AsSpan(HexPrefix.Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                ? value : null;
    }
}
