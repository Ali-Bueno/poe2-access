namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>A name of <c>catalog/exe-names.txt</c>: whether the exe held it when the list was made, and the first source file matching it.</summary>
    public readonly record struct ExeName(string Name, bool Held, string Where);

    /// <summary>
    ///     The game's names the plugin matches by their text that the exe holds as whole strings: a control's name in the UI
    ///     tree ("tribute_button"), a table's path. <c>catalog/exe-names.txt</c> lists every candidate of the source and
    ///     whether the exe held it when the list was made (tests' dev command <c>exe-names</c>); one it held that the running
    ///     game's exe lacks was renamed or removed by a patch, so what matched it has gone silent and it is said
    ///     (docs/compatibility.md, "Names in the exe"). Pure.
    /// </summary>
    public static class ExeNames
    {
        public const string Part = "exe names";
        public const string FileName = "exe-names.txt";

        /// <summary>The source file whose every string constant is a candidate, whatever its shape: it holds what the game calls things.</summary>
        public const string LayoutFile = "GameLayout.cs";

        private const char Column = '\t';
        private const char Comment = '#';
        private const string HeldMark = "+";
        private const string NotHeldMark = "-";
        private const string LineComment = "//";
        private const byte FirstText = 0x20;
        private const byte LastText = 0x7E;

        // words_joined_by_underscores: how the game names its controls, and little else in the source.
        private static readonly Regex Snake = new("^[A-Za-z][A-Za-z0-9]*(_[A-Za-z0-9]+)+$", RegexOptions.Compiled);
        private static readonly Regex Literal = new("\"((?:[^\"\\\\]|\\\\.)*)\"", RegexOptions.Compiled);
        private static readonly Regex Constant = new("const string \\w+ = \"((?:[^\"\\\\]|\\\\.)*)\";", RegexOptions.Compiled);

        /// <summary>The entries of the file; a line that does not parse is left out.</summary>
        public static IReadOnlyList<ExeName> Parse(IEnumerable<string> lines)
        {
            var entries = new List<ExeName>();
            foreach (var line in lines)
            {
                var columns = line.Split(Column);
                if (line.Length > 0 && line[0] != Comment && columns.Length >= 2 && (columns[0] is HeldMark or NotHeldMark) && columns[1].Length > 0)
                {
                    entries.Add(new ExeName(columns[1], columns[0] == HeldMark, columns.Length > 2 ? columns[2] : string.Empty));
                }
            }

            return entries;
        }

        public static string Line(ExeName entry) => $"{(entry.Held ? HeldMark : NotHeldMark)}{Column}{entry.Name}{Column}{entry.Where}";

        /// <summary>
        ///     The names a source file may match against the game's: its string literals shaped like a control's name and,
        ///     for <see cref="LayoutFile"/>, every string constant. Comments are not read.
        /// </summary>
        public static IEnumerable<string> Candidates(string source, bool everyConstant)
        {
            foreach (var raw in source.Split('\n'))
            {
                var comment = raw.IndexOf(LineComment, StringComparison.Ordinal);
                var line = comment < 0 ? raw : raw[..comment];
                foreach (Match literal in Literal.Matches(line))
                {
                    if (Snake.IsMatch(literal.Groups[1].Value))
                    {
                        yield return literal.Groups[1].Value;
                    }
                }

                if (everyConstant && Constant.Match(line) is { Success: true } constant && constant.Groups[1].Value.Length > 0)
                {
                    yield return constant.Groups[1].Value;
                }
            }
        }

        /// <summary>
        ///     Those of <paramref name="wanted"/> the image holds as a whole string: UTF-16, printable ASCII from its start to
        ///     its NUL. One pass over the image, whatever the number of names.
        /// </summary>
        public static HashSet<string> Whole(ReadOnlySpan<byte> image, IReadOnlyCollection<string> wanted)
        {
            var names = wanted as IReadOnlySet<string> ?? wanted.ToHashSet(StringComparer.Ordinal);
            var lengths = names.Select(name => name.Length).ToHashSet();
            var found = new HashSet<string>(StringComparer.Ordinal);
            var at = 0;
            while (at + 1 < image.Length)
            {
                if (!IsText(image[at]) || image[at + 1] != 0)
                {
                    at++;
                    continue;
                }

                var start = at;
                while (at + 1 < image.Length && IsText(image[at]) && image[at + 1] == 0)
                {
                    at += sizeof(char);
                }

                if (lengths.Contains((at - start) / sizeof(char)) && at + 1 < image.Length && image[at] == 0 && image[at + 1] == 0)
                {
                    var text = Encoding.Unicode.GetString(image[start..at]);
                    if (names.Contains(text))
                    {
                        found.Add(text);
                    }
                }
            }

            return found;
        }

        /// <summary>
        ///     The names the exe held when the list was made that this image no longer does. A name that is not a whole
        ///     string here is looked for again as the end of one, so what merely came to follow other data is not said gone.
        /// </summary>
        public static IReadOnlyList<ExeName> Gone(ReadOnlySpan<byte> image, IEnumerable<ExeName> entries)
        {
            var held = entries.Where(entry => entry.Held).ToList();
            var whole = Whole(image, held.Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal));
            var gone = new List<ExeName>();
            foreach (var entry in held)
            {
                if (!whole.Contains(entry.Name) && image.IndexOf(Encoding.Unicode.GetBytes(entry.Name + '\0')) < 0)
                {
                    gone.Add(entry);
                }
            }

            return gone;
        }

        /// <summary>"3 of 124 names gone from the game's exe: tribute_button (UiSemantics.RitualFavours.cs), …".</summary>
        public static string Tally(int held, IReadOnlyList<ExeName> gone) => gone.Count == 0
            ? $"Exe names: {held} of {held} still in the game's exe"
            : $"Exe names: {gone.Count} of {held} gone from the game's exe, what matched them is silent: {string.Join(", ", gone.Select(entry => $"{entry.Name} ({entry.Where})"))}";

        private static bool IsText(byte value) => value is >= FirstText and <= LastText;
    }
}
