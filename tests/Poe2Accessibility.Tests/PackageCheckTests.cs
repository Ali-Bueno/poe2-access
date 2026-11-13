namespace Poe2Accessibility.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Poe2Access;
    using static Check;

    /// <summary>The package check ("Poe2Access.exe check"): a missing or empty file and an unreadable catalog or phrase table are faults.</summary>
    public static class PackageCheckTests
    {
        public static IEnumerable<(string Name, Action Test)> All() => new (string, Action)[]
        {
            (nameof(AMissingOrEmptyShippedFileIsAFault), AMissingOrEmptyShippedFileIsAFault),
            (nameof(ACatalogThatDoesNotParseIsAFault), ACatalogThatDoesNotParseIsAFault),
            (nameof(APhraseTableIsAnObjectOfTexts), APhraseTableIsAnObjectOfTexts),
            (nameof(AFolderOfCatalogsIsJudgedFileByFile), AFolderOfCatalogsIsJudgedFileByFile),
        };

        private static string NewFolder()
        {
            var folder = Path.Join(Path.GetTempPath(), "poe2access-tests", "check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static void AMissingOrEmptyShippedFileIsAFault()
        {
            var root = NewFolder();
            Directory.CreateDirectory(Path.Join(root, "app"));
            File.WriteAllText(Path.Join(root, "app", "good.dll"), "x");
            File.WriteAllText(Path.Join(root, "app", "empty.dll"), string.Empty);
            var faults = PackageCheck.FileFaults(root, [Path.Join("app", "good.dll"), Path.Join("app", "empty.dll"), Path.Join("app", "gone.dll")]).ToList();
            Expect(2, faults.Count, "the empty and the missing file");
            Expect(true, faults.Any(f => f.Contains("empty.dll") && f.StartsWith("Empty")), "the empty one is named");
            Expect(true, faults.Any(f => f.Contains("gone.dll") && f.StartsWith("Missing")), "the missing one is named");
            Expect(0, PackageCheck.FileFaults(root, []).Count(), "no list, nothing to miss");
        }

        private static void ACatalogThatDoesNotParseIsAFault()
        {
            Expect(null, PackageCheck.CatalogFault("a.json", "{\"a\":{\"b\":1}}"), "valid JSON with a value");
            Expect(true, PackageCheck.CatalogFault("a.json", "{\"a\":").Contains("JSON"), "cut short");
            Expect(true, PackageCheck.CatalogFault("a.json", "{}") is not null, "no entries");
            Expect(null, PackageCheck.CatalogFault("a.txt", "# comment\nrow\tvalue\n"), "a data line");
            Expect(true, PackageCheck.CatalogFault("a.txt", "# comment only\n\n") is not null, "a text catalog with no data line");
        }

        private static void APhraseTableIsAnObjectOfTexts()
        {
            Expect(null, PackageCheck.PhraseFault("{\"Hello\":\"Hallo\"}"), "a table");
            Expect(true, PackageCheck.PhraseFault("[\"Hello\"]") is not null, "an array is no table");
            Expect(true, PackageCheck.PhraseFault("{\"Hello\":3}") is not null, "a number is no text");
            Expect(true, PackageCheck.PhraseFault("{\"Hello\"").Contains("JSON"), "cut short");
        }

        private static void AFolderOfCatalogsIsJudgedFileByFile()
        {
            var plugin = NewFolder();
            Expect(true, PackageCheck.CatalogFaults(plugin).Single().StartsWith("No catalog folder"), "no catalog folder");
            var catalogs = Path.Join(plugin, "catalog");
            Directory.CreateDirectory(Path.Join(catalogs, "lang"));
            File.WriteAllText(Path.Join(catalogs, "ok.json"), "{\"a\":\"b\"}");
            File.WriteAllText(Path.Join(catalogs, "README.md"), "not a catalog");
            File.WriteAllText(Path.Join(catalogs, "broken.txt"), string.Empty);
            File.WriteAllText(Path.Join(catalogs, "lang", "de.json"), "{\"a\":\"b\"}");
            File.WriteAllText(Path.Join(catalogs, "lang", "fr.json"), "{\"a\":");
            var faults = PackageCheck.CatalogFaults(plugin).ToList();
            Expect(2, faults.Count, "the broken catalog and the broken table");
            Expect(true, faults[0].Contains("broken.txt"), "the catalog is named");
            Expect(true, faults[1].Contains("fr.json"), "the table is named");
        }
    }
}
