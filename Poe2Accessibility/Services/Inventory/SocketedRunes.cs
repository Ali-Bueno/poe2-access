namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Which lines of a piece of gear's tooltip its socketed runes and soul cores give (docs/research/item-mods.md,
    ///     "Sockets and runes"): the game shows them among the modifier lines, but they are in none of the item's Mods
    ///     lists. What a rune gives depends on the kind of item, by category rows whose classes the files leave empty for
    ///     the broad ones (Martial Weapon, Armour), so every category is written and the tooltip tells which one it is: a
    ///     line counts when the tooltip shows it more often than the item's own modifiers make it. Pure.
    /// </summary>
    public static class SocketedRunes
    {
        /// <summary>The tooltip's lines the runes give, in the tooltip's order.</summary>
        /// <param name="runes">The base path of each rune or soul core socketed, once per socket it fills.</param>
        /// <param name="tooltip">The item's tooltip lines.</param>
        /// <param name="modLines">The lines the item's own modifiers make (certain ones), so a modifier's line equal to a rune's is not taken for it.</param>
        public static IReadOnlyList<int> LinesGiven(IReadOnlyList<string> runes, IReadOnlyList<string> tooltip, IEnumerable<string> modLines, string language, ModStatCatalog stats, ModTextCatalog texts)
        {
            var given = new List<int>();
            if (runes.Count == 0 || tooltip.Count == 0)
            {
                return given;
            }

            var candidates = Candidates(runes, language, stats, texts);
            var own = modLines.GroupBy(line => line, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var shown = tooltip.GroupBy(line => line, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            var left = shown.ToDictionary(pair => pair.Key, pair => pair.Value - own.GetValueOrDefault(pair.Key), StringComparer.Ordinal);
            for (var at = 0; at < tooltip.Count; at++)
            {
                // The runes' lines come before the item's modifiers, so the first showings are theirs.
                if (candidates.Contains(tooltip[at]) && left.GetValueOrDefault(tooltip[at]) > 0)
                {
                    left[tooltip[at]]--;
                    given.Add(at);
                }
            }

            return given;
        }

        /// <summary>
        ///     Every line the runes could give: in each category, all of them summed (two alike runes show one line of the
        ///     sum), and each rune's own rows alone (a category that holds for every item adds to the item's own).
        /// </summary>
        private static HashSet<string> Candidates(IReadOnlyList<string> runes, string language, ModStatCatalog stats, ModTextCatalog texts)
        {
            var lines = new HashSet<string>(StringComparer.Ordinal);
            var rows = runes.Select(stats.RuneStatsOf).ToList();
            void Add(IEnumerable<(string Stat, int Value)> given)
            {
                var sums = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var (stat, value) in given)
                {
                    sums[stat] = sums.GetValueOrDefault(stat) + value;
                }

                lines.UnionWith(ItemModLines.OfStats(sums, language, stats, texts).Where(line => line.Certain).Select(line => line.Text));
            }

            foreach (var category in rows.SelectMany(row => row.Keys).Distinct(StringComparer.Ordinal))
            {
                Add(rows.SelectMany(row => row.TryGetValue(category, out var given) ? given : Array.Empty<(string, int)>()));
            }

            foreach (var given in rows.SelectMany(row => row.Values))
            {
                Add(given);
            }

            return lines;
        }
    }
}
