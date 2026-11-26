namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The Atlas skill tree's finder for <see cref="UiSemantics"/> (docs/research/atlas-navigator.md §5, the
    ///     Atlas tree of Phase 5, named by the owner 2026-11-25): its nodes are the hovered element, the stick
    ///     hops them and the D-pad does nothing there, so the passive tree finder's controls and words serve as
    ///     they are — D-pad up/down walk the lists (Position, Notables, Clusters), left/right their entries, and
    ///     every node then says the way to the one chosen. The passive finder's own machinery cannot run here
    ///     (its tree state is judged by the main tree's graph file; its points, build marks and allocated list
    ///     are the main tree's), so the graph is the files' (<see cref="AtlasTreeMap"/>) and the pushes are
    ///     <see cref="PassiveStick"/>'s over the drawn centres. What it does not say yet: what a node costs in
    ///     Atlas points — the tree's allocated list is unread (no offset validated for this canvas).
    /// </summary>
    public sealed partial class UiSemantics
    {
        private readonly AtlasTreeFinder atlasTreeFinder = new();
        private (string Graph, AtlasTreeMap Map)? atlasTree;
        private (string From, string To, Dictionary<string, Vector2>? Centres, Way? Way)? atlasTreeWay;

        /// <summary>The guide line said on the node under the cursor, kept while it stays (as the passive guide's).</summary>
        private (string Node, string Line)? atlasTreeGuideSaid;

        /// <summary>The skill tree a node is of, from the files, built once per graph; null for a node of none.</summary>
        private AtlasTreeMap? AtlasTreeOf(string id, out string graph)
        {
            var catalog = EndgameCatalog.Shared;
            graph = catalog.Node(id)?.Graph ?? string.Empty;
            if (graph.Length == 0 || !catalog.IsSkillTree(graph))
            {
                return null;
            }

            if (this.atlasTree is not { } built || built.Graph != graph)
            {
                this.atlasTree = catalog.GraphNodes(graph) is { Count: > 0 } nodes ? (graph, AtlasTreeMap.Build(nodes, catalog.Marks)) : null;
            }

            return this.atlasTree?.Map;
        }

        /// <summary>A D-pad press on an Atlas tree node: moves the finder and returns what to say; null when the node is none of the tree's.</summary>
        /// <param name="hovered">The hovered node's element.</param>
        /// <param name="listStep">+1 next list, -1 previous, 0 to stay in it.</param>
        /// <param name="entryStep">+1 next entry, -1 previous.</param>
        public string? AtlasTreeFinderMove(IntPtr hovered, int listStep, int entryStep)
        {
            if (!this.PassiveFinderOn || this.EndgamePassiveId(hovered) is not { } id || this.AtlasTreeOf(id, out var graph) is not { } tree ||
                !tree.Index.TryGetValue(id, out var from))
            {
                return null;
            }

            var distances = tree.DistancesFrom(from);
            IReadOnlyList<string> Members(AtlasTreeCategory category) => tree.Members(category, from, distances);
            var hadTarget = this.atlasTreeFinder.Target.Length > 0;
            if (listStep != 0)
            {
                this.atlasTreeFinder.ChangeCategory(listStep, Members);
            }
            else
            {
                this.atlasTreeFinder.ChangeEntry(entryStep, Members(this.atlasTreeFinder.Category));
            }

            if (this.atlasTreeFinder.Category == AtlasTreeCategory.Position || !tree.Index.TryGetValue(this.atlasTreeFinder.Target, out var target))
            {
                var position = Announcements.PassivePosition(this.AtlasTreeWhere(tree, from, distances, here: true));
                return hadTarget && this.atlasTreeFinder.Target.Length == 0 ? $"{position}. {Announcements.BeaconOff}" : position;
            }

            var centres = this.elements.TryReadElement(hovered, out var info) ? this.EndgameCentres(info, graph) : null;
            var name = this.AtlasTreeName(tree, target);
            var found = this.atlasTreeFinder.Target == id
                ? $"{Announcements.Here(name)}, {this.AtlasTreeWhere(tree, target, distances, here: false)}"
                : Announcements.PassiveFound(name, this.AtlasTreeWhere(tree, target, distances, here: false), this.AtlasTreeWayTo(tree, centres, id, this.atlasTreeFinder.Target)?.Words(false) ?? string.Empty);
            return listStep == 0 ? found : Announcements.InContext(Announcements.AtlasTreeCategoryName(this.atlasTreeFinder.Category), found);
        }

        /// <summary>
        ///     What the guide says on a hovered Atlas tree node ("Toward Stir the Swarm: up, 3"), the arrival on
        ///     the target itself; empty while no target is chosen. Worked out once per node and kept while the
        ///     cursor stays, as the passive guide's is (<see cref="GuideAt"/>): a choice made on this node with
        ///     the D-pad is said by the finder itself, and a guide line changing under it here would be spoken
        ///     over that answer, then again in its brief form (live 2026-11-25: three speeches per press).
        /// </summary>
        private string AtlasTreeGuide(string id, Dictionary<string, Vector2>? centres)
        {
            if (this.atlasTreeGuideSaid is { } said && said.Node == id)
            {
                return said.Line;
            }

            var line = string.Empty;
            if (this.AtlasTreeOf(id, out _) is { } tree && tree.Index.TryGetValue(id, out _) && this.atlasTreeFinder.Target.Length > 0)
            {
                var target = this.atlasTreeFinder.Target;
                var name = tree.Index.TryGetValue(target, out var at) ? this.AtlasTreeName(tree, at) : target;
                line = this.atlasTreeFinder.Arrive(id)
                    ? Announcements.BeaconArrived(name)
                    : this.GoalAndWay($"atlas tree {target}", Announcements.PassiveToward(name), this.AtlasTreeWayTo(tree, centres, id, target));
            }

            this.atlasTreeGuideSaid = (id, line);
            return line;
        }

        /// <summary>A node's spoken name: the catalog's for a passive, its cluster's start word for a start.</summary>
        private string AtlasTreeName(AtlasTreeMap tree, int at) =>
            EndgameCatalog.Shared.NodeName(tree.Ids[at], Localization.Language) is { Length: > 0 } name ? name
            : tree.Start[at] ? Announcements.PassiveStartLink(AtlasTreeMap.SaidCluster(tree.Clusters[at]), false)
            : Announcements.AtlasNode;

        /// <summary>"Abyss, down right, outer ring, 3 links away": a node's cluster, its side and ring of the tree, and its links from the cursor. A start's name already says its cluster.</summary>
        private string AtlasTreeWhere(AtlasTreeMap tree, int at, int[] distances, bool here)
        {
            var (octant, ring) = PassiveMap.Zone(tree.Places, at);
            var place = tree.Start[at] && !here
                ? $"{Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}"
                : $"{AtlasTreeMap.SaidCluster(tree.Clusters[at])}, {Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}";
            return !here && distances[at] > 0 ? $"{place}, {Announcements.AtlasLinksAway(distances[at])}" : place;
        }

        /// <summary>
        ///     The pushes from one node to another, the direction alone when too far to count; kept while the two
        ///     stand. Where the drawn centres give the stick's rule no way, the files' places are tried before
        ///     the direction alone is said (either set can lose a node the other holds).
        /// </summary>
        private Way? AtlasTreeWayTo(AtlasTreeMap tree, Dictionary<string, Vector2>? centres, string fromId, string toId)
        {
            if (this.atlasTreeWay is { } cached && cached.From == fromId && cached.To == toId && ReferenceEquals(cached.Centres, centres))
            {
                return cached.Way;
            }

            Way? way = null;
            if (tree.Index.TryGetValue(fromId, out var from) && tree.Index.TryGetValue(toId, out var to))
            {
                var positions = this.AtlasTreePositions(tree, centres, from);
                var route = tree.Way(positions, from, to, MaxFinderPushes, out _);
                if ((route == null || route is { Pushes: null, Far: false }) && positions != tree.Places &&
                    tree.Way(tree.Places, from, to, MaxFinderPushes, out _) is { Pushes: not null } retried)
                {
                    route = retried;
                }

                if (route is { } found)
                {
                    way = new Way(found.Octant, found.Pushes, found.Far);
                }
            }

            this.atlasTreeWay = (fromId, toId, centres, way);
            return way;
        }

        /// <summary>
        ///     Where each node is for the stick's rule: the drawn centres, with a node the scan missed placed by
        ///     the cursor's local scale (as a fog neighbour's place is estimated for the Atlas map's links); the
        ///     files' places when no centres read.
        /// </summary>
        private Vector2[] AtlasTreePositions(AtlasTreeMap tree, Dictionary<string, Vector2>? centres, int from)
        {
            if (centres == null || !centres.TryGetValue(tree.Ids[from], out var here))
            {
                return tree.Places;
            }

            var scale = tree.Links[from]
                .Where(other => centres.ContainsKey(tree.Ids[other]) && Vector2.Distance(tree.Places[from], tree.Places[other]) > 1)
                .Select(other => (float?)(Vector2.Distance(here, centres[tree.Ids[other]]) / Vector2.Distance(tree.Places[from], tree.Places[other])))
                .FirstOrDefault();
            if (scale is not { } factor)
            {
                return tree.Places;
            }

            var offset = here - (tree.Places[from] * factor);
            return tree.Ids.Select((id, at) => centres.TryGetValue(id, out var drawn) ? drawn : (tree.Places[at] * factor) + offset).ToArray();
        }
    }
}
