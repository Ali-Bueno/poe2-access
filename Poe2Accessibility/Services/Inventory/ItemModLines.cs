namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>One modifier of an item as memory holds it: its Mods Id and its values, in the entry's order.</summary>
    public sealed record ItemMod(string Id, IReadOnlyList<int> Values);

    /// <summary>
    ///     An item's modifier lists as the host reads them. The enchant list and the others were empty on every item set
    ///     against its tooltip, so nothing made from them is certain (docs/research/item-mods.md).
    /// </summary>
    public sealed record ItemModLists(IReadOnlyList<ItemMod> Implicit, IReadOnlyList<ItemMod> Explicit, IReadOnlyList<ItemMod> Enchant, IReadOnlyList<ItemMod> Other);

    /// <summary>A modifier line made from an item's modifiers in memory.</summary>
    /// <param name="Text">The line as the tooltip writes it; empty when none could be written.</param>
    /// <param name="Certain">Whether every part of it is known: an uncertain line is never said and its absence never held against the item.</param>
    /// <param name="From">What it was made from, for the log: the stats, or the modifier and why it gave no line.</param>
    public sealed record ItemModLine(string Text, bool Certain, string From);

    /// <summary>
    ///     An item's modifier lines without its tooltip (docs/research/item-mods.md): each list's values summed by stat,
    ///     as the tooltip sums them, then each stat's description block written for the sums in the file's order. Pure;
    ///     the game files' facts come through the two catalogs.
    /// </summary>
    public static class ItemModLines
    {
        public static IReadOnlyList<ItemModLine> Render(ItemModLists mods, string language, ModStatCatalog stats, ModTextCatalog texts)
        {
            var lines = new List<ItemModLine>();
            Section(lines, mods.Implicit, true, language, stats, texts);
            Section(lines, mods.Explicit, true, language, stats, texts);
            Section(lines, mods.Enchant, false, language, stats, texts);
            Section(lines, mods.Other, false, language, stats, texts);
            return lines;
        }

        /// <summary>The lines of one list: the tooltip shows each list apart, so a stat is summed within its list.</summary>
        /// <param name="seen">Whether the list was seen to hold what the tooltip shows.</param>
        private static void Section(List<ItemModLine> lines, IReadOnlyList<ItemMod> list, bool seen, string language, ModStatCatalog stats, ModTextCatalog texts)
        {
            var sums = new Dictionary<string, double>(StringComparer.Ordinal);
            var doubted = new HashSet<string>(StringComparer.Ordinal);
            var unknown = false;
            foreach (var mod in list)
            {
                var known = stats.StatsOf(mod.Id);
                if (known != null && known.Count == mod.Values.Count)
                {
                    for (var at = 0; at < known.Count; at++)
                    {
                        sums[known[at].Stat] = sums.GetValueOrDefault(known[at].Stat) + mod.Values[at];
                        if (mod.Values[at] < known[at].Least || mod.Values[at] > known[at].Most)
                        {
                            doubted.Add(known[at].Stat); // not a number this stat of the modifier rolls: the value may be another stat's
                        }
                    }

                    continue;
                }

                // Its values cannot be given to its stats: every line one of them is in is in doubt, and all of them when the stats are not known either.
                var theirs = known?.Select(stat => stat.Stat).ToList() ?? stats.UnprovenStatsOf(mod.Id);
                unknown |= theirs == null;
                doubted.UnionWith(theirs ?? Array.Empty<string>());
                lines.Add(new ItemModLine(string.Empty, false, mod.Id + ": " +
                    (known != null ? $"{mod.Values.Count} values for {known.Count} stats" : theirs != null ? "its values are not told apart" : "not in the game files")));
            }

            var blocks = new SortedSet<int>();
            foreach (var (stat, sum) in sums)
            {
                var found = stats.IsHidden(stat) ? null : texts.BlocksOf(stat);
                if (found is { Count: 1 })
                {
                    blocks.Add(found[0]);
                }
                else if (found != null && sum != 0)
                {
                    lines.Add(new ItemModLine(string.Empty, false, stat + (found.Count == 0 ? ": no description" : ": described twice")));
                }
            }

            foreach (var block in blocks)
            {
                var ids = texts.StatsOf(block);
                var values = ids.Select(id => sums.GetValueOrDefault(id)).ToArray();
                if (values.All(value => value == 0))
                {
                    continue; // a stat of nothing has no line
                }

                var (text, certain) = texts.Show(block, values, language);
                lines.Add(new ItemModLine(text, certain && seen && !unknown && text.Length > 0 && !ids.Any(doubted.Contains), string.Join(' ', ids)));
            }
        }
    }
}
