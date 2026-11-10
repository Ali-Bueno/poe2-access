namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    ///     Values drawn one per weapon set: a row of boxes beside a row of weapon-set icons, box k for the k-th icon (the
    ///     spirit panel's "0 de espíritu" under set I; docs/research/skill-bar.md). Pure.
    /// </summary>
    public static class WeaponSetRows
    {
        /// <summary>Icon to box: the image sits in its cell, the cell in its row, the row in the panel (and the same for a box's text).</summary>
        private const int CellLevels = 1;
        private const int RowLevels = 2;
        private const int PanelLevels = 3;

        /// <summary>The inventory's weapon-swap art (spirit panel, equipment slots) and the cosmetics' numerals.</summary>
        private static readonly Regex SetIcon = new(@"/(?:InventoryPanelWeaponSwap|WeaponSetIcon)(\d)(?:Active)?\.dds", RegexOptions.Compiled);

        /// <summary>The weapon set an icon's art stands for ("…/InventoryPanelWeaponSwap2Active.dds|…" → 2), or null.</summary>
        public static int? FromTextureKey(string key) =>
            SetIcon.Match(key) is { Success: true } match ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;

        /// <summary>
        ///     The weapon set of each text whose box is the k-th child of a row next to a row of set icons in increasing order
        ///     (equipment slots repeat their sets and pair nothing). Nodes are given by parent (-1 at the top) and child index.
        /// </summary>
        public static Dictionary<int, int> Pair(IReadOnlyList<int> parents, IReadOnlyList<int> indices, IEnumerable<(int Node, int Set)> icons, IEnumerable<int> texts)
        {
            int Up(int node, int levels)
            {
                for (; levels > 0 && node >= 0; levels--)
                {
                    node = parents[node];
                }

                return node;
            }

            var rows = icons.Where(icon => Up(icon.Node, PanelLevels) >= 0)
                .GroupBy(icon => Up(icon.Node, RowLevels))
                .ToDictionary(row => row.Key, row => row.OrderBy(icon => indices[Up(icon.Node, CellLevels)]).Select(icon => icon.Set).ToList());
            foreach (var row in rows.Where(row => row.Value.Zip(row.Value.Skip(1)).Any(pair => pair.First >= pair.Second)).ToList())
            {
                rows.Remove(row.Key);
            }

            var pairs = new Dictionary<int, int>();
            foreach (var text in texts)
            {
                var (cell, row, panel) = (Up(text, CellLevels), Up(text, RowLevels), Up(text, PanelLevels));
                if (panel < 0 || rows.ContainsKey(row))
                {
                    continue;
                }

                foreach (var (iconRow, sets) in rows)
                {
                    if (parents[iconRow] == panel && Math.Abs(indices[iconRow] - indices[row]) == 1 && indices[cell] < sets.Count)
                    {
                        pairs[text] = sets[indices[cell]];
                        break;
                    }
                }
            }

            return pairs;
        }
    }
}
