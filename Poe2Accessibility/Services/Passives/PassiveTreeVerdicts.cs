namespace Poe2Accessibility.Services
{
    /// <summary>
    ///     When the passive tree's catalog is older than the game's tree (docs/research/passive-tree.md#ready-for-a-rebuilt-tree-2026-11-14):
    ///     the shares of disagreement past which the catalog is not trusted. Pure.
    /// </summary>
    public static class PassiveTreeVerdicts
    {
        /// <summary>The parts said lost when the catalog or the graph is not the game's tree (<see cref="CompatibilityReport.Missing"/>).</summary>
        public const string StaleCatalogPart = "passive tree catalog";

        public const string GraphFilePart = "passive tree graph file";
        public const string OwnStartPart = "passive tree start";

        /// <summary>Nodes compared (links) or drawn (known by the catalog) before a verdict is given: a screenful of the tree.</summary>
        public const int MinNodesJudged = 64;

        /// <summary>The share of compared nodes whose links differ past which the whole tree's links are read from memory (0.5.5f: none differ).</summary>
        public const double LinkDisagreeShare = 0.1;

        /// <summary>The share of drawn nodes the catalog does not know past which the catalog is said to be older than the game (0.5.5f: none).</summary>
        public const double UnknownNodeShare = 0.1;

        /// <summary>Readings in a row that find no start for a readable character, or no graph file under a tree, before the part is said lost.</summary>
        public const int MissesToSay = 3;

        /// <summary>Whether the files' links differ from the graph in memory on so many nodes that memory's stand for the whole tree.</summary>
        public static bool LinksDisagree(int compared, int differing) =>
            compared >= MinNodesJudged && differing > compared * LinkDisagreeShare;

        /// <summary>Whether so many drawn nodes are unknown to the catalog that it is the game's older tree.</summary>
        public static bool CatalogStale(int drawn, int unknown) =>
            drawn >= MinNodesJudged && unknown > drawn * UnknownNodeShare;
    }
}
