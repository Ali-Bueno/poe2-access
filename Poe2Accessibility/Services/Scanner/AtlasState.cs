namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>A node's state on the Atlas map, from its status byte's two low bits (<see cref="GameLayout.AtlasNode"/>).</summary>
    public enum AtlasNodeState
    {
        Locked,

        /// <summary>Can be opened and not completed (bit0 set, bit1 clear); a completed node keeps bit0 (status 3) and reads Completed — Completed wins.</summary>
        Accessible,
        Completed,
    }

    /// <summary>The status byte read as bits, never as an exact value (higher bits occur on special nodes). Pure.</summary>
    public static class AtlasState
    {
        /// <summary>The compatibility report's part for the nodes' status byte and grid cell.</summary>
        public const string NodesPart = "Atlas nodes";

        /// <summary>Fewest catalogued nodes on a canvas for its fields to be judged.</summary>
        private const int MinJudged = 20;

        /// <summary>No Atlas is this many cells wide: a pointer or a float read as a cell is far larger.</summary>
        private const int MaxCell = 10_000;

        private const int LowBits = GameLayout.AtlasNode.StatusAccessibleBit | GameLayout.AtlasNode.StatusCompletedBit;

        public static AtlasNodeState Of(byte status) =>
            (status & GameLayout.AtlasNode.StatusCompletedBit) != 0 ? AtlasNodeState.Completed
            : (status & GameLayout.AtlasNode.StatusAccessibleBit) != 0 ? AtlasNodeState.Accessible
            : AtlasNodeState.Locked;

        /// <summary>
        ///     Whether a canvas's catalogued nodes read as the Atlas's own, so a game update that moved the status byte or the
        ///     grid is heard (docs/research/atlas-navigator.md): cells are small numbers, some node is accessible and a
        ///     completed one is accessible too (1 → 3). Null with too few nodes to tell.
        /// </summary>
        public static bool? Plausible(IEnumerable<(int GridX, int GridY, byte Status)> catalogued)
        {
            var nodes = catalogued.ToList();
            if (nodes.Count < MinJudged)
            {
                return null;
            }

            var placed = nodes.Count(node => Math.Abs((long)node.GridX) <= MaxCell && Math.Abs((long)node.GridY) <= MaxCell);
            var accessible = nodes.Count(node => (node.Status & GameLayout.AtlasNode.StatusAccessibleBit) != 0);
            var completed = nodes.Count(node => (node.Status & GameLayout.AtlasNode.StatusCompletedBit) != 0);
            var both = nodes.Count(node => (node.Status & LowBits) == LowBits);
            return placed * 2 > nodes.Count && accessible > 0 && both * 2 >= completed;
        }

        /// <summary>For the log: how many nodes carry each value of the two status bits, and how many cells are small numbers.</summary>
        public static string Census(IEnumerable<(int GridX, int GridY, byte Status)> catalogued)
        {
            var nodes = catalogued.ToList();
            var bits = Enumerable.Range(0, LowBits + 1).Select(value => $"{value}:{nodes.Count(node => (node.Status & LowBits) == value)}");
            var placed = nodes.Count(node => Math.Abs((long)node.GridX) <= MaxCell && Math.Abs((long)node.GridY) <= MaxCell);
            return $"{nodes.Count} catalogued, status bits {string.Join(" ", bits)}, {placed} cells in range";
        }
    }
}
