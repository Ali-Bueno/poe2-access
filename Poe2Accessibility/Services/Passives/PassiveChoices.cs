namespace Poe2Accessibility.Services
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     The choices made on the passive tree's nodes that have one ("+5 to any attribute" taken as Strength), as the
    ///     tree's state lists them: 4-byte entries {u16 the node's graph id, u16 the graph id of the PassiveSkills row
    ///     chosen} (docs/research/passive-tree.md#attribute-choices). Pure.
    /// </summary>
    public static class PassiveChoices
    {
        /// <returns>
        ///     Each node's choice by its graph id; null when an entry names a node the tree has not or a row that is no
        ///     choice of any node: the list is then another one, or read while the game was writing it.
        /// </returns>
        /// <param name="nodeIds">Every graph id of the tree.</param>
        /// <param name="variantIds">The graph ids of the rows a node can be made (PassiveSkillVariants).</param>
        public static Dictionary<int, int>? Read(IReadOnlyList<uint> entries, IReadOnlySet<int> nodeIds, IReadOnlySet<int> variantIds)
        {
            var choices = new Dictionary<int, int>();
            foreach (var entry in entries)
            {
                var (node, variant) = ((int)(entry & 0xFFFF), (int)(entry >> 16));
                if (!nodeIds.Contains(node) || !variantIds.Contains(variant))
                {
                    return null;
                }

                choices[node] = variant;
            }

            return choices;
        }

        /// <summary>How many nodes were given each choice, the most taken first, ties by the choice's id: what the list's header counts.</summary>
        public static IReadOnlyList<(int Variant, int Nodes)> Counts(IEnumerable<int> chosen) =>
            chosen.GroupBy(variant => variant).Select(group => (Variant: group.Key, Nodes: group.Count()))
                .OrderByDescending(entry => entry.Nodes).ThenBy(entry => entry.Variant).ToList();
    }
}
