namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using System.Text;

    /// <summary>
    ///     The lists of the Atlas skill tree's finder (on the shoulder buttons: the game hops its nodes with the
    ///     stick and the D-pad both — the owner, 2026-11-27). The first says where the cursor is and holds no node.
    /// </summary>
    public enum AtlasTreeCategory
    {
        Position,
        Notables,

        /// <summary>The start of each cluster (the generic tree's and each league's), to travel between clusters.</summary>
        Clusters,
    }

    /// <summary>
    ///     The Atlas skill tree's graph for its finder, from <see cref="EndgameCatalog"/>'s nodes of the tree: links,
    ///     places, each node's kind from its PassiveSkills marks and the graph's roots (from its id, AtlasAbyssNotable1,
    ///     where a catalog has no marks) and its cluster from its id (AtlasGenericSmall78_). The passive tree's finder cannot serve here — its graph state is judged by the
    ///     main tree's graph file name, and its allocated list, points and build marks are the main tree's — so
    ///     this borrows its pure logic instead: link distances (<see cref="PassiveMap.Steps"/>) and the stick's
    ///     pushes (<see cref="PassiveStick.Route"/>, the same tree widget's rule). Mastery icons hold no links in
    ///     the files and the cursor never lands on one: left out. Pure.
    /// </summary>
    public sealed class AtlasTreeMap
    {
        /// <summary>The 0.5 Atlas passive tree's graph name, for a catalog made before the graph paths (<see cref="EndgameCatalog.HasTreeFinder"/>).</summary>
        public const string LegacyGraphName = "atlasskillgraph";

        /// <summary>
        ///     The game's folders whose skill trees the tree finder walks — only the Atlas passive tree's, by the owner's
        ///     choice (2026-11-25); a league tree (metadata/leagueskillgraphs/) joins only when added here.
        /// </summary>
        public static readonly string[] FinderFolders = { "metadata/atlasskillgraphs/" };

        /// <summary>The catalog's marks (the endgame extractor's NodeFlags): keystone, notable, icon only (a mastery).</summary>
        private const char KeystoneFlag = 'k';

        private const char NotableFlag = 'n';

        private const char MasteryFlag = 'i';

        private AtlasTreeMap(string[] ids, int[][] links, Vector2[] places, bool[] notable, bool[] start, string[] clusters)
        {
            this.Ids = ids;
            this.Links = links;
            this.Places = places;
            this.Notable = notable;
            this.Start = start;
            this.Clusters = clusters;
            this.Index = ids.Select((id, at) => (id, at)).ToDictionary(pair => pair.id, pair => pair.at, StringComparer.Ordinal);
        }

        public string[] Ids { get; }

        public int[][] Links { get; }

        /// <summary>The graph files' own places, y down as on screen.</summary>
        public Vector2[] Places { get; }

        public bool[] Notable { get; }

        public bool[] Start { get; }

        /// <summary>Each node's cluster as the files spell it ("Abyss", "AzmeriSpirit", "Generic").</summary>
        public string[] Clusters { get; }

        public Dictionary<string, int> Index { get; }

        /// <summary>The tree from catalog nodes (id, the group's centre, linked ids); links made both ways, masteries left out.</summary>
        /// <param name="marks">Each node's PassiveSkills marks and root flag; a kind no node is marked with is read from the ids.</param>
        public static AtlasTreeMap Build(IEnumerable<(string Id, int X, int Y, IReadOnlyList<string>? Links)> nodes, Func<string, (string? Flags, bool Root)>? marks = null)
        {
            var all = nodes.ToList();
            var marked = new Dictionary<string, (string? Flags, bool Root)>(StringComparer.Ordinal);
            foreach (var node in all)
            {
                marked.TryAdd(node.Id, marks?.Invoke(node.Id) ?? (null, false));
            }

            bool Marked(string id, char flag) => marked[id].Flags?.Contains(flag, StringComparison.Ordinal) == true;
            var masteryMarks = all.Any(node => Marked(node.Id, MasteryFlag));
            var notableMarks = all.Any(node => Marked(node.Id, NotableFlag) || Marked(node.Id, KeystoneFlag));
            var rootMarks = all.Any(node => marked[node.Id].Root);
            bool IsMastery(string id) => masteryMarks ? Marked(id, MasteryFlag) : id.Contains("Mastery", StringComparison.Ordinal);
            bool IsNotable(string id) => notableMarks ? Marked(id, NotableFlag) || Marked(id, KeystoneFlag) : id.Contains("Notable", StringComparison.Ordinal);
            bool IsStart(string id) => rootMarks ? marked[id].Root : id.TrimEnd('_').EndsWith("Start", StringComparison.Ordinal);

            var kept = all.Where(node => !IsMastery(node.Id)).ToList();
            var index = kept.Select((node, at) => (node.Id, at)).ToDictionary(pair => pair.Id, pair => pair.at, StringComparer.Ordinal);
            var links = kept.Select(_ => new List<int>()).ToArray();
            for (var at = 0; at < kept.Count; at++)
            {
                foreach (var other in kept[at].Links ?? Array.Empty<string>())
                {
                    if (index.TryGetValue(other, out var there) && there != at && !links[at].Contains(there))
                    {
                        links[at].Add(there);
                        links[there].Add(at);
                    }
                }
            }

            return new AtlasTreeMap(
                kept.Select(node => node.Id).ToArray(),
                links.Select(list => list.ToArray()).ToArray(),
                kept.Select(node => new Vector2(node.X, node.Y)).ToArray(),
                kept.Select(node => IsNotable(node.Id)).ToArray(),
                kept.Select(node => IsStart(node.Id)).ToArray(),
                kept.Select(node => ClusterOf(node.Id)).ToArray());
        }

        /// <summary>
        ///     A node's cluster from its id: what stands between "Atlas" and its kind or number
        ///     (AtlasBiomeCity1 → BiomeCity, AtlasAzmeriSpiritEssenceChance → AzmeriSpirit).
        /// </summary>
        public static string ClusterOf(string passiveId)
        {
            var name = passiveId.StartsWith("Atlas", StringComparison.Ordinal) ? passiveId["Atlas".Length..] : passiveId;
            foreach (var kind in new[] { "EssenceNotable", "Notable", "Small", "Keystone", "Start", "EssenceChance" })
            {
                var at = name.IndexOf(kind, StringComparison.Ordinal);
                if (at > 0)
                {
                    return name[..at];
                }
            }

            var letters = name.TakeWhile(ch => !char.IsDigit(ch) && ch != '_').Count();
            return letters > 0 ? name[..letters] : name;
        }

        /// <summary>A cluster as said ("AzmeriSpirit" → "Azmeri Spirit", "BiomeCity" → "Biome City").</summary>
        public static string SaidCluster(string cluster)
        {
            var said = new StringBuilder();
            foreach (var ch in cluster)
            {
                if (char.IsUpper(ch) && said.Length > 0)
                {
                    said.Append(' ');
                }

                said.Append(ch);
            }

            return said.ToString();
        }

        /// <summary>Link distances from a node; nodes of other clusters (no links join them) read <see cref="PassiveMap.NoPath"/>.</summary>
        public int[] DistancesFrom(int from) => PassiveMap.Steps(this.Links, new[] { from });

        /// <summary>
        ///     A list's nodes but the one under the cursor, nearest first: in links where links lead there, then as
        ///     drawn. Clusters are told by their starts, nearest as drawn (little links across to most of them).
        /// </summary>
        public IReadOnlyList<string> Members(AtlasTreeCategory category, int from, int[] distances)
        {
            bool Holds(int at) => category switch
            {
                AtlasTreeCategory.Notables => this.Notable[at],
                AtlasTreeCategory.Clusters => this.Start[at],
                _ => false,
            };
            return Enumerable.Range(0, this.Ids.Length)
                .Where(at => at != from && Holds(at))
                .OrderBy(at => category == AtlasTreeCategory.Clusters ? 0 : distances[at] < 0 ? int.MaxValue : distances[at])
                .ThenBy(at => Vector2.Distance(this.Places[at], this.Places[from]))
                .Select(at => this.Ids[at])
                .ToList();
        }

        /// <summary>
        ///     The first push toward a node and how many it takes, as the passive finder's way: the stick walked by
        ///     the game's own rule over <paramref name="positions"/> (the drawn centres where they read, else the
        ///     files' places). Where no greedy push leads nearer (dense clusters defeat the window-less rule,
        ///     live 2026-11-25: "From here: right" with no count), the side of the next link on the shortest way
        ///     stands in — a push that at least starts along the links. <paramref name="far"/>: only when
        ///     <paramref name="maxMoves"/> ran out is the target far.
        /// </summary>
        public (int Octant, int? Pushes, bool Far)? Way(Vector2[] positions, int from, int to, int maxMoves, out bool far)
        {
            far = false;
            if (from == to)
            {
                return null;
            }

            var pushes = PassiveStick.Route(positions, from, to, maxMoves, at => this.Links[at], null, out far);
            if (pushes is { Count: > 0 })
            {
                return (pushes[0], pushes.Count, false);
            }

            // Nodes of a group share its centre: a zero offset has no side, and Nearest(zero) would invent "right".
            var offset = positions[to] - positions[from];
            var octant = this.NextHopOctant(positions, from, to) ?? (offset != Vector2.Zero ? PassiveStick.Nearest(offset) : (int?)null);
            return octant is { } side ? (side, null, far) : null;
        }

        /// <summary>
        ///     The side of the linked neighbour that begins the shortest way in links to a node; null when no
        ///     links lead there (another cluster) or it is here already.
        /// </summary>
        public int? NextHopOctant(Vector2[] positions, int from, int to)
        {
            if (from == to)
            {
                return null;
            }

            var distances = this.DistancesFrom(to);
            var best = -1;
            foreach (var next in this.Links[from])
            {
                if (distances[next] >= 0 && (best < 0 || distances[next] < distances[best]))
                {
                    best = next;
                }
            }

            var hop = best < 0 ? Vector2.Zero : positions[best] - positions[from];
            return best < 0 || hop == Vector2.Zero ? null : PassiveStick.Nearest(hop);
        }
    }

    /// <summary>
    ///     The Atlas tree finder's choice, as the passive tree's (<see cref="PassiveFinder"/>): up and down walk
    ///     the lists, left and right a list's entries, both wrapping; the entry chosen is the target with nothing
    ///     to confirm, and going back to Position lets it go. Pure.
    /// </summary>
    public sealed class AtlasTreeFinder
    {
        private static readonly AtlasTreeCategory[] Categories = Enum.GetValues<AtlasTreeCategory>();

        public AtlasTreeCategory Category { get; private set; } = AtlasTreeCategory.Position;

        /// <summary>The node guided to, by PassiveSkills id; empty when none.</summary>
        public string Target { get; private set; } = string.Empty;

        /// <param name="step">+1 next, -1 previous.</param>
        /// <param name="members">A list's nodes, nearest first; an empty list is passed over.</param>
        public void ChangeCategory(int step, Func<AtlasTreeCategory, IReadOnlyList<string>> members)
        {
            for (var tried = 0; tried < Categories.Length; tried++)
            {
                this.Category = Categories[SettingsCursor.Wrap((int)this.Category + step, Categories.Length)];
                var list = this.Category == AtlasTreeCategory.Position ? Array.Empty<string>() : members(this.Category);
                if (this.Category == AtlasTreeCategory.Position || list.Count > 0)
                {
                    this.Target = list.Count > 0 ? list[0] : string.Empty;
                    return;
                }
            }
        }

        /// <param name="step">+1 next, -1 previous.</param>
        /// <param name="members">The list's nodes now, nearest first: it re-sorts as the cursor moves, the target stays.</param>
        public void ChangeEntry(int step, IReadOnlyList<string> members)
        {
            if (members.Count > 0)
            {
                var at = members.ToList().IndexOf(this.Target);
                this.Target = members[at < 0 ? 0 : SettingsCursor.Wrap(at + step, members.Count)];
            }
        }

        /// <returns>True when the cursor landed on the node it was guided to, the first time; the guide then ends.</returns>
        public bool Arrive(string hovered)
        {
            if (hovered.Length == 0 || hovered != this.Target)
            {
                return false;
            }

            this.Drop();
            return true;
        }

        /// <summary>Ends the guide.</summary>
        public void Drop() => this.Target = string.Empty;
    }
}
