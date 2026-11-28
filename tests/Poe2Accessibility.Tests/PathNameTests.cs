namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Accessibility.Services;
    using static Check;

    /// <summary>The paths of the game's files the plugin matches (<see cref="PathNames"/>), and the list of them the mod ships.</summary>
    public static class PathNameTests
    {
        /// <summary>Dev command: make catalog/path-names.txt from the plugin's source.</summary>
        public const string MakeCommand = "path-names";

        private static readonly string CatalogPath = Path.Combine("Poe2Accessibility", "catalog", PathNames.FileName);

        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(APathThatStartsNoFileIsGone), APathThatStartsNoFileIsGone),
            (nameof(TheSourcesPathsAreItsLiteralsOfTheGamesFiles), TheSourcesPathsAreItsLiteralsOfTheGamesFiles),
            (nameof(TheShippedListIsTheSourcesPaths), TheShippedListIsTheSourcesPaths),
        };

        /// <summary>Dev command: writes the list from this repository's source; 0 when written.</summary>
        public static int Make()
        {
            var root = ExeNameTests.Root();
            var lines = Lines(root);
            File.WriteAllLines(Path.Combine(root, CatalogPath), lines);
            Console.WriteLine($"{lines.Count(line => !line.StartsWith('#'))} paths of the source, written to {CatalogPath}");
            return 0;
        }

        private static List<string> Lines(string root) => new[]
        {
            "# The paths of the game's files the plugin matches, each with the first source file holding it: the data step \"game names\"",
            "# looks each up after a patch and fails, naming it, when no file's path starts so (docs/compatibility.md, \"Names in the game's files\").",
            "# Made from the source: dotnet run --project tests/Poe2Accessibility.Tests -c Release -- path-names",
        }.Concat(ExeNameTests.Listed(root, (_, text) => PathNames.Candidates(text)).Select(pair => PathNames.Line(new PathName(pair.Key, pair.Value)))).ToList();

        private static void APathThatStartsNoFileIsGone()
        {
            var files = new[] { "metadata/chests/abyss/abysschest.ot", "metadata/monsters/doryani/doryaniboss.ot", "metadata/pet/cat/cat.ao" };
            Array.Sort(files, StringComparer.Ordinal);
            var entries = new[]
            {
                new PathName("Metadata/Chests/Abyss/AbyssChest", "LeaguePieces.cs"), new PathName("Metadata/Pet/", "EntityCatalog.Predicates.cs"),
                new PathName("Metadata/Monsters/Doryani/DoryaniBossArm", "BossThrows.cs"), new PathName("Metadata/Zebra", "X.cs"),
            };
            Expect("Metadata/Monsters/Doryani/DoryaniBossArm, Metadata/Zebra", string.Join(", ", PathNames.Gone(files, entries).Select(entry => entry.Path)),
                "a file's path without its extension and the folders above one are there, whatever their case; a renamed piece and a path past the last file are not");
        }

        private static void TheSourcesPathsAreItsLiteralsOfTheGamesFiles()
        {
            const string Source = """
                /// <summary>Not read: "Metadata/Doc/Comment".</summary>
                public const string Crack = "Metadata/MiscellaneousObjects/Abyss/AbyssCrack";
                if (path.StartsWith("Metadata/Pet/", StringComparison.Ordinal) || name == "Abyss") // "Metadata/Trailing"
                var made = $"Metadata/Monsters/{family}/";
                """;
            Expect("Metadata/MiscellaneousObjects/Abyss/AbyssCrack, Metadata/Pet/", string.Join(", ", PathNames.Candidates(Source)), "outside comments, and not a text made at run time");
        }

        private static void TheShippedListIsTheSourcesPaths()
        {
            var root = ExeNameTests.Root();
            Expect(string.Join("\n", Lines(root)), string.Join("\n", File.ReadAllLines(Path.Combine(root, CatalogPath))),
                "regenerate catalog/path-names.txt when a path the source matches is added or removed: dotnet run --project tests/Poe2Accessibility.Tests -c Release -- path-names");
        }
    }
}
