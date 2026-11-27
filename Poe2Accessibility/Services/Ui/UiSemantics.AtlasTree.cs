namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

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
            if (graph.Length == 0 || !catalog.HasTreeFinder(graph))
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
            if (this.EndgamePassiveId(hovered) == null && this.AtlasTreeAnchor(hovered) is var anchor && anchor != IntPtr.Zero)
            {
                hovered = anchor; // a padlock: the finder counts from the node before it
            }

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
                : this.AtlasTreeWayTo(tree, centres, id, this.atlasTreeFinder.Target)?.Words(false) is { Length: > 0 } way
                    ? Announcements.PassiveFound(name, this.AtlasTreeWhere(tree, target, distances, here: false), way)
                    : $"{name}, {this.AtlasTreeWhere(tree, target, distances, here: false)}"; // no drawn way yet: no "From here:" left hanging
            return listStep == 0 ? found : Announcements.InContext(Announcements.AtlasTreeCategoryName(this.atlasTreeFinder.Category), found);
        }

        /// <summary>
        ///     The Atlas tree's root the cursor rests on once the game's hover went empty: the game's cursor lands on
        ///     a root (the tree's centre, each cluster's start) but hovers nothing there, so the node, its links and
        ///     the D-pad finder were lost on it (live 2026-11-27: "Focus -> none" one push down from "Probabilidad de
        ///     esencias", the view centred on the Generic start). A root already standing in stays while drawn;
        ///     else it is the one root beside <paramref name="left"/>, the node the focus was on (no node of the
        ///     tree touches two roots, catalog of 2026-11-25). Zero when none, or once the tree is hidden.
        /// </summary>
        public IntPtr AtlasTreeRootBeside(IntPtr left)
        {
            if (this.EndgamePassiveId(left) is not { } id || this.AtlasTreeOf(id, out _) is not { } tree || !tree.Index.TryGetValue(id, out var at))
            {
                return IntPtr.Zero;
            }

            var catalog = EndgameCatalog.Shared;
            if (catalog.Marks(id).Root)
            {
                return this.Drawn(left) ? left : IntPtr.Zero;
            }

            var roots = tree.Links[at].Select(other => tree.Ids[other]).Where(other => catalog.Marks(other).Root).ToList();
            var element = roots.Count == 1 ? this.EndgameElementOf(roots[0]) : IntPtr.Zero;
            return element != IntPtr.Zero && this.Drawn(element) ? element : IntPtr.Zero;
        }

        /// <summary>The guide's line said on the padlock under the cursor, kept while it stays.</summary>
        private (IntPtr Stop, string Line)? atlasTreeStopSaid;

        /// <summary>The last Atlas tree node said as hovered: what a padlock between nodes counts from.</summary>
        private IntPtr atlasTreeLast;

        /// <summary>
        ///     The node a stop of the Atlas tree's own counts from: the stick rests on its padlocks between nodes
        ///     ("Completa la Cámara de enigmas oriental para desbloquearlo, botón, 2 de 11", live 2026-11-27), where
        ///     the D-pad finder had no node and the guide no line; the owner chose to count from the last node
        ///     (2026-11-27). A control under the tree's canvas or its frame while that node is still drawn; zero
        ///     for any other.
        /// </summary>
        private IntPtr AtlasTreeAnchor(IntPtr element)
        {
            if (this.atlasTreeLast == IntPtr.Zero || element == this.atlasTreeLast || this.endgameSnapshot is not { } snapshot ||
                !EndgameCatalog.Shared.HasTreeFinder(snapshot.Graph) || !this.Drawn(this.atlasTreeLast) || !this.elements.TryReadElement(element, out var info))
            {
                return IntPtr.Zero;
            }

            var frame = this.elements.TryReadElement(snapshot.Root, out var canvas) ? canvas.Raw.ParentPtr : IntPtr.Zero;
            return this.elements.ReadAncestors(info, 8).Any(ancestor => ancestor.Address == snapshot.Root || (frame != IntPtr.Zero && ancestor.Address == frame))
                ? this.atlasTreeLast
                : IntPtr.Zero;
        }

        /// <summary>A control of the Atlas tree that is no node (a padlock) as a stop of it: the D-pad finder's, ending with the guide's line from the node before.</summary>
        private AccessibleUiNode WithAtlasTreeStop(AccessibleUiNode node)
        {
            if (node.IsAtlasTreeNode || this.AtlasTreeAnchor(node.Address) is var anchor && anchor == IntPtr.Zero)
            {
                return node;
            }

            // Kept per padlock as a node's line is: worked out again, the guide's brief form replaced it and was said too.
            if (this.atlasTreeStopSaid is not { } said || said.Stop != node.Address)
            {
                var line = this.EndgamePassiveId(anchor) is { } id ? this.AtlasTreeGuide(id, this.endgameSnapshot?.Centres, kept: false) : string.Empty;
                this.atlasTreeStopSaid = said = (node.Address, line);
            }

            var lines = node.Tooltip.ToList();
            if (said.Line.Length > 0)
            {
                lines.Add(said.Line);
            }

            return node.AsAtlasTreeStop(lines, Announcements.AtlasSkillTree);
        }

        /// <summary>The Atlas tree's roots found at the view's centre so far, checked again before any walk.</summary>
        private readonly List<IntPtr> atlasTreeRootsSeen = new();

        /// <summary>
        ///     The Atlas tree's root the cursor rests on with no node before it (the tree opened on a root, the
        ///     mod started there): the view pans its cursor's node to the window's centre, a root as any node
        ///     (live 2026-11-27: the Generic and Breach starts drawn at 640,360 of 1280×720, Breach at the tree's
        ///     edge), so the root drawn there is it. A root seen before is checked first; while those are hidden
        ///     the tree is closed and nothing is walked, else the world screen's shown elements are.
        /// </summary>
        public IntPtr AtlasTreeRootAtCentre(IntPtr worldScreen, Vector2 centre)
        {
            var drawnSeen = false;
            foreach (var seen in this.atlasTreeRootsSeen.ToList())
            {
                if (!this.IsAtlasTreeRoot(seen))
                {
                    this.atlasTreeRootsSeen.Remove(seen);
                }
                else if (this.Drawn(seen))
                {
                    drawnSeen = true;
                    if (Centred(seen, centre))
                    {
                        return seen;
                    }
                }
            }

            if ((this.atlasTreeRootsSeen.Count > 0 && !drawnSeen) || !this.elements.TryReadElement(worldScreen, out var top))
            {
                return IntPtr.Zero;
            }

            // The tree is no child of the world screen: walk from the top of its chain.
            for (var depth = 0; depth < 32 && top.Raw.ParentPtr != IntPtr.Zero && this.elements.TryReadElement(top.Raw.ParentPtr, out var parent); depth++)
            {
                top = parent;
            }

            var pending = new Queue<UiElementReader.ElementInfo>(new[] { top });
            var walked = 0;
            for (; pending.Count > 0 && walked < MaxEndgameElements * 4; walked++)
            {
                var info = pending.Dequeue();
                if (!info.IsVisibleFlag)
                {
                    continue;
                }

                if (Centred(info.Address, centre) && this.IsAtlasTreeRoot(info.Address))
                {
                    this.atlasTreeRootsSeen.Add(info.Address);
                    this.TreeFields.Log?.Invoke($"Atlas tree root at the view's centre: {this.EndgamePassiveId(info.Address)}, {walked} elements walked");
                    return info.Address;
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    if (this.elements.TryReadElement(child, out var childInfo))
                    {
                        pending.Enqueue(childInfo);
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>Whether an element is a root of the Atlas tree (one the finder walks), by its record.</summary>
        private bool IsAtlasTreeRoot(IntPtr element) =>
            this.EndgamePassiveId(element) is { } id && EndgameCatalog.Shared.Marks(id).Root && EndgameCatalog.Shared.Node(id) is { } node &&
            EndgameCatalog.Shared.HasTreeFinder(node.Graph);

        /// <summary>Whether an element is drawn on a point, within its own size.</summary>
        private static bool Centred(IntPtr element, Vector2 centre) =>
            PluginUiElementReflection.TryGetAbsoluteRect(element, out var position, out var size) && size.X > 0 &&
            Vector2.Distance(position + (size / 2), centre) <= MathF.Max(size.X, size.Y);

        /// <summary>Whether an element is shown with a rect: a closed tree's nodes are not.</summary>
        private bool Drawn(IntPtr element) =>
            this.elements.TryReadElement(element, out var info) && this.elements.IsVisible(info) && PluginUiElementReflection.TryGetAbsoluteRect(element, out _, out _);

        /// <summary>
        ///     What the guide says on a hovered Atlas tree node ("Toward Stir the Swarm: up, 3"), the arrival on
        ///     the target itself; empty while no target is chosen. Worked out once per node and kept while the
        ///     cursor stays, as the passive guide's is (<see cref="GuideAt"/>): a choice made on this node with
        ///     the D-pad is said by the finder itself, and a guide line changing under it here would be spoken
        ///     over that answer, then again in its brief form (live 2026-11-25: three speeches per press).
        /// </summary>
        private string AtlasTreeGuide(string id, Dictionary<string, Vector2>? centres, bool kept = true)
        {
            if (kept && this.atlasTreeGuideSaid is { } said && said.Node == id)
            {
                return said.Line;
            }

            var line = string.Empty;
            if (this.AtlasTreeOf(id, out _) is { } tree && tree.Index.TryGetValue(id, out _) && this.atlasTreeFinder.Target.Length > 0 &&
                (kept || this.atlasTreeFinder.Target != id)) // a padlock's line, counted from the node before, never arrives on it
            {
                var target = this.atlasTreeFinder.Target;
                var name = tree.Index.TryGetValue(target, out var at) ? this.AtlasTreeName(tree, at) : target;
                line = this.atlasTreeFinder.Arrive(id)
                    ? Announcements.BeaconArrived(name)
                    : this.GoalAndWay($"atlas tree {target}", Announcements.PassiveToward(name), this.AtlasTreeWayTo(tree, centres, id, target));
            }

            if (kept)
            {
                this.atlasTreeGuideSaid = (id, line);
            }

            return line;
        }

        /// <summary>
        ///     A tree as said: the main tree in the mod's words, a league's by the game's own name for its content
        ///     ("Abismo", "Fisura", "Delirio"), else the files' word spaced ("Azmeri Spirit").
        /// </summary>
        private static string AtlasTreeSaid(string cluster) =>
            cluster == AtlasTreeMap.MainTree ? Announcements.AtlasMainTree
            : EndgameCatalog.Shared.Text("content/" + cluster, Localization.Language) is { Length: > 0 } league ? league
            : AtlasTreeMap.SaidCluster(cluster);

        /// <summary>A node's spoken name: the catalog's for a passive, its cluster's start word for a start.</summary>
        private string AtlasTreeName(AtlasTreeMap tree, int at) =>
            EndgameCatalog.Shared.NodeName(tree.Ids[at], Localization.Language) is { Length: > 0 } name ? name
            : tree.Start[at] ? Announcements.PassiveStartLink(AtlasTreeSaid(tree.Clusters[at]), false)
            : Announcements.AtlasNode;

        /// <summary>"Abyss, down right, outer ring, 3 links away": a node's cluster, its side and ring of the tree, and its links from the cursor. A start's name already says its cluster.</summary>
        private string AtlasTreeWhere(AtlasTreeMap tree, int at, int[] distances, bool here)
        {
            var (octant, ring) = PassiveMap.Zone(tree.Places, at);
            var place = tree.Start[at] && !here
                ? $"{Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}"
                : tree.Clusters[at] == AtlasTreeMap.MainTree
                    ? $"{Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}"
                    : $"{AtlasTreeSaid(tree.Clusters[at])}, {Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}";
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
