namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The names the plugin matches that the game's exe holds (<see cref="ExeNames"/>), and the list of them the mod ships.</summary>
    public static class ExeNameTests
    {
        /// <summary>Dev command: make catalog/exe-names.txt from the plugin's source and an exe on disk.</summary>
        public const string MakeCommand = "exe-names";

        private const string Solution = "Poe2Access.slnx";
        private const string Plugin = "Poe2Accessibility";
        private const string SourcePattern = "*.cs";
        private static readonly string[] BuildFolders = { "obj", "bin" };
        private static readonly string CatalogPath = Path.Combine(Plugin, "catalog", ExeNames.FileName);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AWholeStringIsHeldAndAPartOfOneIsNot), AWholeStringIsHeldAndAPartOfOneIsNot),
            (nameof(ANameGoneIsSaidWithWhereItIsMatched), ANameGoneIsSaidWithWhereItIsMatched),
            (nameof(ANameThatCameToFollowOtherDataIsNotGone), ANameThatCameToFollowOtherDataIsNotGone),
            (nameof(TheSourcesCandidatesAreItsControlNames), TheSourcesCandidatesAreItsControlNames),
            (nameof(TheShippedListIsTheSourcesNames), TheShippedListIsTheSourcesNames),
        };

        /// <summary>Dev command: writes the list from this repository's source and <paramref name="exe"/>; 0 when written.</summary>
        public static int Make(string exe)
        {
            var root = Root();
            var image = File.ReadAllBytes(exe);
            var candidates = SourceNames(root);
            var whole = ExeNames.Whole(image, candidates.Keys.ToHashSet(StringComparer.Ordinal));
            var lines = candidates.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => ExeNames.Line(new ExeName(pair.Key, whole.Contains(pair.Key), pair.Value)));
            File.WriteAllLines(Path.Combine(root, CatalogPath), new[]
            {
                "# The names the plugin matches by their text, and whether the game's exe holds each as a whole string (+) or not (-):",
                "# a + name a later exe lacks was renamed by a patch and is said (docs/compatibility.md, \"Names in the exe\").",
                "# Made from the source: dotnet run --project tests/Poe2Accessibility.Tests -c Release -- exe-names <the game's exe>",
            }.Concat(lines));
            Console.WriteLine($"{candidates.Count} names of the source, {whole.Count} whole strings of the exe, written to {CatalogPath}");
            return 0;
        }

        /// <summary>For the exe gate: the listed names this exe no longer holds; 0 when none.</summary>
        public static int CheckExe(string exe, string catalogs)
        {
            var entries = ExeNames.Parse(File.ReadLines(Path.Combine(catalogs, ExeNames.FileName)));
            var gone = ExeNames.Gone(File.ReadAllBytes(exe), entries);
            foreach (var entry in gone)
            {
                Console.WriteLine($"GONE   {entry.Name,-40} {entry.Where}");
            }

            Console.WriteLine(ExeNames.Tally(entries.Count(entry => entry.Held), gone));
            return gone.Count == 0 ? 0 : 1;
        }

        /// <summary>Each candidate of the plugin's source with the first file holding it, by the file's name.</summary>
        private static SortedDictionary<string, string> SourceNames(string root) =>
            Listed(root, (name, text) => ExeNames.Candidates(text, name == ExeNames.LayoutFile));

        /// <summary>What <paramref name="candidates"/> finds in each of the plugin's source files (name, text), with the first file holding each.</summary>
        internal static SortedDictionary<string, string> Listed(string root, Func<string, string, IEnumerable<string>> candidates)
        {
            var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
            var folder = Path.Combine(root, Plugin);
            foreach (var file in Directory.EnumerateFiles(folder, SourcePattern, SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
            {
                var parts = Path.GetRelativePath(folder, file).Split(Path.DirectorySeparatorChar);
                if (BuildFolders.Contains(parts[0]))
                {
                    continue;
                }

                foreach (var name in candidates(Path.GetFileName(file), File.ReadAllText(file)))
                {
                    names.TryAdd(name, Path.GetFileName(file));
                }
            }

            return names;
        }

        private static byte[] Image(params string[] strings) =>
            strings.SelectMany(text => Encoding.Unicode.GetBytes(text + '\0')).Prepend((byte)0).Prepend((byte)0).ToArray();

        private static void AWholeStringIsHeldAndAPartOfOneIsNot()
        {
            var image = Image("name_label_panel", "tribute_button", "Data/Balance/MapPins.dat");
            var whole = ExeNames.Whole(image, new[] { "name_label", "label_panel", "tribute_button", "Data/Balance/MapPins.dat", "map_control" });
            Expect("Data/Balance/MapPins.dat, tribute_button", string.Join(", ", whole.OrderBy(name => name, StringComparer.Ordinal)), "the start and the end of a longer name are not the name");
            Expect(0, ExeNames.Whole(Encoding.ASCII.GetBytes("\0tribute_button\0"), new[] { "tribute_button" }).Count, "an ASCII string is not the wide one the UI is named with");
        }

        private static void ANameGoneIsSaidWithWhereItIsMatched()
        {
            var entries = new[] { new ExeName("tribute_button", true, "GameLayout.cs"), new ExeName("layby_pay_button", true, "GameLayout.cs"), new ExeName("mod_own_word", false, "Settings.cs") };
            var gone = ExeNames.Gone(Image("tribute_button", "defer_button"), entries);
            Expect("layby_pay_button", string.Join(", ", gone.Select(entry => entry.Name)), "the renamed one alone; a name the exe never held is not looked for");
            Expect("Exe names: 1 of 2 gone from the game's exe, what matched them is silent: layby_pay_button (GameLayout.cs)", ExeNames.Tally(2, gone), "the log's line");
            Expect("Exe names: 2 of 2 still in the game's exe", ExeNames.Tally(2, Array.Empty<ExeName>()), "nothing gone");
        }

        private static void ANameThatCameToFollowOtherDataIsNotGone()
        {
            // Two bytes of other data that read as a letter before the string: it is no longer whole to the scan, and still there.
            var image = new byte[] { 0x41, 0x00 }.Concat(Encoding.Unicode.GetBytes("tribute_button\0")).ToArray();
            Expect(0, ExeNames.Whole(image, new[] { "tribute_button" }).Count, "not a whole string");
            Expect(0, ExeNames.Gone(image, new[] { new ExeName("tribute_button", true, "GameLayout.cs") }).Count, "nor gone");
        }

        private static void TheSourcesCandidatesAreItsControlNames()
        {
            const string Source = """
                /// <summary>Not read: "doc_comment_name".</summary>
                public const string PanelsParentName = "panels_parent";
                public const string PinTable = "Data/Balance/MapPins.dat";
                if (info.Name == "tribute_button" || info.Name == "Button") // not "trailing_comment"
                var text = $"spoken {count} words";
                """;
            Expect("panels_parent, tribute_button", string.Join(", ", ExeNames.Candidates(Source, false).Distinct()), "words joined by underscores, outside comments");
            Expect("panels_parent, Data/Balance/MapPins.dat, tribute_button", string.Join(", ", ExeNames.Candidates(Source, true).Distinct()), "and every constant of the layout");
        }

        private static void TheShippedListIsTheSourcesNames()
        {
            var root = Root();
            var shipped = ExeNames.Parse(File.ReadLines(Path.Combine(root, CatalogPath))).Select(entry => entry.Name).ToHashSet(StringComparer.Ordinal);
            var source = SourceNames(root).Keys.ToHashSet(StringComparer.Ordinal);
            var unlisted = string.Join(", ", source.Except(shipped).OrderBy(name => name, StringComparer.Ordinal));
            var stale = string.Join(", ", shipped.Except(source).OrderBy(name => name, StringComparer.Ordinal));
            const string Regenerate = "regenerate catalog/exe-names.txt: dotnet run --project tests/Poe2Accessibility.Tests -c Release -- exe-names <the game's exe>";
            Expect(string.Empty, unlisted, $"names the source matches that the list lacks; {Regenerate}");
            Expect(string.Empty, stale, $"names of the list the source no longer has, which a patch could say gone for nothing; {Regenerate}");
        }

        /// <summary>The repository: the folder above the tests' output that holds the solution.</summary>
        internal static string Root()
        {
            var root = AppContext.BaseDirectory;
            while (root is not null && !File.Exists(Path.Combine(root, Solution)))
            {
                root = Path.GetDirectoryName(root);
            }

            return root ?? throw new InvalidOperationException($"{Solution} is not above {AppContext.BaseDirectory}");
        }
    }
}
