namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    /// <summary>What one table gained and lost between two extractions of the game's files.</summary>
    public sealed record TableNews(string Table, IReadOnlyList<string> Added, IReadOnlyList<string> Removed);

    /// <summary>
    ///     What a game update added, from two lists of the game's own keys (catalog/game-keys.txt): new buffs, monsters, areas and
    ///     screens are what the mod does not know yet, and they would otherwise pass in silence (docs/compatibility.md).
    /// </summary>
    public static class GameNews
    {
        private const char Separator = '\t';
        private const char Comment = '#';

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

        /// <summary>The report's lines; <paramref name="note"/> says what the mod makes of a new key, or null.</summary>
        public static IEnumerable<string> Report(IReadOnlyList<TableNews> news, Func<string, string, string?> note)
        {
            foreach (var table in news)
            {
                yield return $"{table.Table}: {table.Added.Count} new, {table.Removed.Count} gone";
                foreach (var key in table.Added)
                {
                    yield return note(table.Table, key) is { } said ? $"  + {key} ({said})" : $"  + {key}";
                }

                foreach (var key in table.Removed)
                {
                    yield return $"  - {key}";
                }
            }
        }

        /// <summary>
        ///     Compares the catalog with the list last seen and leaves the report beside it. Nothing is news the first time: there
        ///     is nothing to compare with.
        /// </summary>
        public static IReadOnlyList<TableNews> Check(string catalog, string knownFile, string reportFile, Func<string, string, string?> note)
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

            Directory.CreateDirectory(Path.GetDirectoryName(knownFile)!);
            File.WriteAllLines(knownFile, fresh);
            return news;
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
