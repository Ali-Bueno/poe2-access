namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>What one table gained and lost between two extractions of the game's files.</summary>
    public sealed record TableNews(string Table, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

    /// <summary>What the mod makes of one new key: a note for the file, and whether one of its rules or catalogs covers it.</summary>
    public sealed record NewsNote(string Text, bool Covered);

    /// <summary>
    ///     What a game update added, from two lists of the game's own keys (catalog/game-keys.txt): new buffs, monsters, areas and
    ///     screens are what the mod does not know yet, and they would otherwise pass in silence (docs/compatibility.md).
    /// </summary>
    public static class GameNews
    {
        private const char Separator = '\t';
        private const char Comment = '#';
        private const string PartSuffix = ".part";

        /// <summary>The tables that changed, in the order of the fresh list.</summary>
        public static IReadOnlyList<TableNews> Compare(IEnumerable<string> known, IEnumerable<string> fresh)
        {
            var before = Group(known);
            var after = Group(fresh);
            var news = new List<TableNews>();
            foreach (var (table, keys) in after)
            {
                // A table the old list did not have is one the extractor began to read, not news from the game.
                if (before.TryGetValue(table, out var old))
                {
                    var added = keys.Where(key => !old.Contains(key)).ToList();
                    var removed = old.Where(key => !keys.Contains(key)).ToList();
                    if (added.Count + removed.Count > 0)
                    {
                        news.Add(new TableNews(table, added, removed));
                    }
                }
            }

            return news;
        }

        private const string Unclassified = "unclassified";

        /// <summary>
        ///     The report's lines: per table its counts and how many new keys no rule or catalog covers, then each key with what
        ///     the mod makes of it (<paramref name="note"/>, null for a key it cannot classify).
        /// </summary>
        public static IEnumerable<string> Report(IReadOnlyList<TableNews> news, Func<string, string, NewsNote?> note)
        {
            foreach (var table in news)
            {
                var notes = table.Added.Select(key => (Key: key, Note: note(table.Table, key))).ToList();
                var unclassified = notes.Count(item => item.Note is not { Covered: true });
                yield return $"{table.Table}: {table.Added.Count} new, {table.Removed.Count} gone" +
                             (table.Added.Count == 0 ? string.Empty : unclassified == 0 ? "; all covered" : $"; {unclassified} {Unclassified}");
                foreach (var (key, found) in notes)
                {
                    yield return $"  + {key} ({found?.Text ?? Unclassified})";
                }

                foreach (var key in table.Removed)
                {
                    yield return $"  - {key}";
                }
            }
        }

        /// <summary>Per table with news: how many keys came and how many the mod cannot classify, for the spoken line.</summary>
        public static IReadOnlyList<(string Table, int Added, int Unclassified)> Summary(IReadOnlyList<TableNews> news, Func<string, string, NewsNote?> note) =>
            news.Select(table => (table.Table, table.Added.Count, table.Added.Count(key => note(table.Table, key) is not { Covered: true }))).ToList();

        /// <summary>
        ///     Compares the catalog with the list last seen and leaves the report beside it. Nothing is news the first time: there
        ///     is nothing to compare with.
        /// </summary>
        public static IReadOnlyList<TableNews> Check(string catalog, string knownFile, string reportFile, Func<string, string, NewsNote?> note)
        {
            if (!File.Exists(catalog))
            {
                return Array.Empty<TableNews>();
            }

            var fresh = File.ReadAllLines(catalog);
            var news = File.Exists(knownFile) ? Compare(File.ReadLines(knownFile), fresh) : Array.Empty<TableNews>();
            if (news.Count > 0)
            {
                File.WriteAllLines(reportFile, Report(news, note));
            }

            return news;
        }

        /// <summary>The catalog becomes the list last seen, once its news was said: quitting before play must not lose them.</summary>
        public static void Remember(string catalog, string knownFile)
        {
            if (!File.Exists(catalog))
            {
                return;
            }

            var fresh = File.ReadAllLines(catalog);
            var part = knownFile + PartSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(knownFile)!);
            File.WriteAllLines(part, Merged(File.Exists(knownFile) ? File.ReadAllLines(knownFile) : Array.Empty<string>(), fresh));
            File.Move(part, knownFile, true);
        }

        /// <summary>The fresh list plus the tables it lacks: one table missing from an extraction must not lose its baseline.</summary>
        public static IEnumerable<string> Merged(IEnumerable<string> known, IReadOnlyCollection<string> fresh)
        {
            var tables = Group(fresh);
            return fresh.Concat(known.Where(line => line.IndexOf(Separator) is > 0 and var at && !tables.ContainsKey(line[..at])));
        }

        /// <summary>The keys of one table in a list.</summary>
        public static IReadOnlyCollection<string> Keys(IEnumerable<string> lines, string table) =>
            Group(lines).TryGetValue(table, out var keys) ? keys : Array.Empty<string>();

        /// <summary>
        ///     The kinds no key of the game reads as: the mod's rules name them by id, so a renamed buff would silence its cue with no
        ///     error anywhere.
        /// </summary>
        public static IReadOnlyList<T> Unmatched<T>(IEnumerable<string> keys, IEnumerable<T> kinds, Func<string, T?> classify)
            where T : struct
        {
            var seen = keys.Select(classify).OfType<T>().ToHashSet();
            return kinds.Where(kind => !seen.Contains(kind)).ToList();
        }

        private static Dictionary<string, HashSet<string>> Group(IEnumerable<string> lines)
        {
            var tables = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var line in lines.Where(line => line.Length > 0 && line[0] != Comment))
            {
                var at = line.IndexOf(Separator);
                if (at > 0)
                {
                    var table = line[..at];
                    if (!tables.TryGetValue(table, out var keys))
                    {
                        tables[table] = keys = new HashSet<string>(StringComparer.Ordinal);
                    }

                    keys.Add(line[(at + 1)..]);
                }
            }

            return tables;
        }
    }
}
