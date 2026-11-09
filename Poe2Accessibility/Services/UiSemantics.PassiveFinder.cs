namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The passive tree's finder for <see cref="UiSemantics"/>: a blind player cannot look across the tree, so the D-pad,
    ///     which the game leaves free there, lists the active build's big nodes, keystones, notables, jewel sockets and what is
    ///     reachable, by the points they cost, and every node then says the way to the one chosen; once the cursor has been on
    ///     it, the way to the next node to take for it (docs/research/passive-tree.md).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The pushes counted toward the finder's target; beyond, only its direction is said.</summary>
        private const int MaxFinderPushes = 30;

        private readonly PassiveFinder finder = new();
        private PassiveAtlas? passiveAtlas;
        private (IntPtr From, IntPtr To, string Words)? finderWay;
        private (IntPtr Record, int Taken, string Line)? finderLine;

        /// <summary>The finder is on (setting PassiveFinder): the D-pad answers and locked nodes say what they cost.</summary>
        public bool PassiveFinderOn { get; set; } = true;

        /// <summary>A D-pad press on a passive node: moves the finder and returns what to say; null when the tree cannot be read.</summary>
        /// <param name="hovered">The hovered node's element.</param>
        /// <param name="listStep">+1 next list, -1 previous, 0 to stay in it.</param>
        /// <param name="entryStep">+1 next entry, -1 previous.</param>
        public string? PassiveFinderMove(IntPtr hovered, int listStep, int entryStep)
        {
            if (!this.PassiveFinderOn || !this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.NodeRecord(hovered) is not { } record || this.PassiveGraphOf(group.Raw.ParentPtr, info) is not { } graph)
            {
                return null;
            }

            var atlas = this.AtlasOf(graph);
            var steps = atlas.StepsFrom(this.AllocatedIds(graph.State));
            var marked = this.BuildMarks(graph);
            var plan = marked.Count > 0 ? this.buildPlans.Matching(marked.Select(this.PassiveIdOf).ToHashSet()) : null;
            var order = plan?.Passives.GroupBy(passive => passive.Id).ToDictionary(group => group.Key, group => group.First().Order);
            int BuildStep(IntPtr node) => order != null && order.TryGetValue(this.PassiveIdOf(node), out var step) ? step : int.MaxValue;
            IReadOnlyList<IntPtr> Members(PassiveCategory category) => atlas.Members(category, steps, record, marked, BuildStep);
            var hadTarget = this.finder.Target != IntPtr.Zero;
            if (listStep != 0)
            {
                this.finder.ChangeCategory(listStep, Members);
            }
            else
            {
                this.finder.ChangeEntry(entryStep, Members(this.finder.Category));
            }

            if (this.finder.Category == PassiveCategory.Position || !atlas.Places.Index.TryGetValue(this.finder.Target, out var target))
            {
                var position = atlas.Places.Index.TryGetValue(record, out var here) ? Announcements.PassivePosition(Where(atlas, here, steps)) : Announcements.PassiveCategoryName(PassiveCategory.Position);
                return hadTarget && this.finder.Target == IntPtr.Zero ? $"{position}. {Announcements.BeaconOff}" : position;
            }

            var found = Announcements.PassiveFound(atlas.Nodes[target].Name, Where(atlas, target, steps), this.WayTo(graph, atlas, record, this.finder.Target));
            return listStep != 0 ? Announcements.InContext(Announcements.PassiveCategoryName(this.finder.Category), found) : found;
        }

        /// <summary>Research (dev command "passivemap"): from each class's start, how many points the nearest keystones and notables cost.</summary>
        public IEnumerable<string> PassiveMapReport(IntPtr hovered)
        {
            if (!this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.PassiveGraphOf(group.Raw.ParentPtr, info) is not { } graph)
            {
                yield break;
            }

            var atlas = this.AtlasOf(graph);
            yield return $"{atlas.Nodes.Length} drawn nodes, rings by node: {string.Join(" ", atlas.Nodes.Select((_, index) => PassiveMap.Zone(atlas.Places.Positions, index).Ring).GroupBy(ring => ring).OrderBy(ring => ring.Key).Select(ring => $"{ring.Key}={ring.Count()}"))}";
            foreach (var start in graph.Links.Keys)
            {
                if (this.PassiveRow(start) is not { } row || !this.ReadPassiveRow(start, row).IsStart)
                {
                    continue;
                }

                var steps = PassiveMap.Steps(atlas.LinksWithStart(graph, start), new[] { atlas.Nodes.Length });
                string Nearest(PassiveClass kind) => string.Join(", ", atlas.Nodes.Select((node, index) => (node, Steps: steps[index]))
                    .Where(entry => entry.node.Class == kind && entry.Steps > 0).OrderBy(entry => entry.Steps).Take(3).Select(entry => $"{entry.node.Name} {entry.Steps}"));
                yield return $"{this.StartClasses(row)}: keystones {Nearest(PassiveClass.Keystone)}; notables {Nearest(PassiveClass.Notable)}; " +
                             $"keystones within 20/30/40 points: {string.Join("/", new[] { 20, 30, 40 }.Select(limit => atlas.Nodes.Where((node, index) => node.Class == PassiveClass.Keystone && steps[index] > 0 && steps[index] <= limit).Count()))}";
            }
        }

        /// <summary>A locked node's state with what it costs from what is taken: "not linked, 7 points away".</summary>
        private string LockedState(PassiveGraph graph, IntPtr record, HashSet<int> allocated)
        {
            if (!this.PassiveFinderOn)
            {
                return Announcements.PassiveLocked;
            }

            var atlas = this.AtlasOf(graph);
            return atlas.Places.Index.TryGetValue(record, out var at) && atlas.StepsFrom(allocated)[at] is > 1 and var points
                ? $"{Announcements.PassiveLocked}, {Announcements.PassivePoints(points)}"
                : Announcements.PassiveLocked;
        }

        /// <summary>
        ///     The finder's line for the hovered node: the way to its target, or that the cursor got there, then the next node to
        ///     take for it; empty with no target. Kept while the cursor stays and nothing is allocated: a choice made on this node is
        ///     said by the D-pad, and a changed line would cut that off; a node taken here moves the guide on to the next one.
        /// </summary>
        private string FinderLine(PassiveGraph graph, IntPtr record, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            if (this.finderLine is { } shown && shown.Record == record && shown.Taken == allocated.Count)
            {
                return shown.Line;
            }

            var atlas = this.AtlasOf(graph);
            var line = atlas.Places.Index.TryGetValue(this.finder.Target, out var target) ? this.GuideLine(graph, atlas, record, target, allocated, marked) : string.Empty;
            this.finderLine = (record, allocated.Count, line);
            return line;
        }

        /// <summary>
        ///     "Toward Scarred Faith: up, 7 pushes" until the cursor has been on the target, to read it; then "Scarred Faith, 8 points to
        ///     go: allocate Attribute: down left, 2 pushes", "allocate this one" on that node, until the target is taken.
        /// </summary>
        private string GuideLine(PassiveGraph graph, PassiveAtlas atlas, IntPtr record, int target, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            var name = atlas.Nodes[target].Name;
            if (!this.finder.Taking && record != this.finder.Target)
            {
                return Announcements.PassiveToward(name, this.WayTo(graph, atlas, record, this.finder.Target));
            }

            var trail = TrailTo(atlas, target, allocated, marked);
            if (this.finder.Arrive(record, trail is { Count: > 0 }))
            {
                var arrived = Announcements.BeaconArrived(name);
                return this.finder.Taking && trail != null ? this.TrailLine(graph, atlas, record, arrived, trail) : arrived;
            }

            if (trail is not { Count: > 0 })
            {
                this.finder.Drop();
                return trail == null ? Announcements.BeaconOff : $"{Announcements.PassiveGot(name)}. {Announcements.BeaconOff}";
            }

            return this.TrailLine(graph, atlas, record, name, trail);
        }

        /// <summary>The points left for the target and the next node to take, with the way to it from the hovered node.</summary>
        private string TrailLine(PassiveGraph graph, PassiveAtlas atlas, IntPtr from, string lead, List<int> trail)
        {
            var next = atlas.Nodes[trail[0]];
            var at = trail.FindIndex(index => atlas.Nodes[index].Record == from);
            var step = next.Record == from ? Announcements.AllocateThis : Announcements.AllocateNode(next.Name, this.WayTo(graph, atlas, from, next.Record));
            return Announcements.PassiveTrail(lead, trail.Count, at > 0 && at < trail.Count - 1, step);
        }

        /// <summary>The nodes to take for the target: the fewest points, along the active build where it can (all of it for a build's node).</summary>
        private static List<int>? TrailTo(PassiveAtlas atlas, int target, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            var targetInBuild = marked.Contains(atlas.Nodes[target].Record);
            int Cost(int index) => index < atlas.Nodes.Length
                ? PassiveMap.TrailCost(marked.Contains(atlas.Nodes[index].Record), atlas.Nodes[index].Class == PassiveClass.Notable, targetInBuild)
                : PassiveMap.TrailCost(false, false, false);
            return atlas.TrailTo(allocated, target, Cost);
        }

        /// <summary>"up, 7 pushes" from one node to another, its direction alone when it is too far to count.</summary>
        private string WayTo(PassiveGraph graph, PassiveAtlas atlas, IntPtr from, IntPtr to)
        {
            if (this.finderWay is { } cached && cached.From == from && cached.To == to)
            {
                return cached.Words;
            }

            var places = atlas.Places;
            if (!places.Index.TryGetValue(from, out var here) || !places.Index.TryGetValue(to, out var there))
            {
                return string.Empty;
            }

            var pushes = this.PushesBetween(graph, places, from, here, there, MaxFinderPushes);
            var words = pushes is { Count: > 0 }
                ? Announcements.PassivePushes(Announcements.StickDirections[pushes[0]], pushes.Count)
                : Announcements.Far(Announcements.StickDirections[PassiveStick.Nearest(places.Positions[there] - places.Positions[here])]);
            this.finderWay = (from, to, words);
            return words;
        }

        /// <summary>"down right, outer ring, 7 points away", or "allocated" in place of the points.</summary>
        private static string Where(PassiveAtlas atlas, int node, int[] steps)
        {
            var (octant, ring) = PassiveMap.Zone(atlas.Places.Positions, node);
            var cost = steps[node] switch
            {
                0 => Announcements.PassiveAllocated,
                PassiveMap.NoPath => Announcements.PassiveLocked,
                var points => Announcements.PassivePoints(points),
            };
            return $"{Announcements.StickDirections[octant]}, {Announcements.PassiveRing(ring)}, {cost}";
        }

        /// <summary>Every drawn node with its row and links, by the index of <see cref="PassivePlaces"/>, read once per tree state.</summary>
        private PassiveAtlas AtlasOf(PassiveGraph graph)
        {
            if (this.passiveAtlas is { } cached && cached.State == graph.State)
            {
                return cached;
            }

            var places = this.PlacesOf(graph);
            var nodes = new PassiveNodeRow[places.Positions.Length];
            var links = new int[nodes.Length][];
            foreach (var (record, index) in places.Index)
            {
                nodes[index] = this.PassiveRow(record) is { } row ? this.ReadPassiveRow(record, row) : new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false, PassiveClass.Other);
                links[index] = places.IndexesOf(graph.Links.GetValueOrDefault(record));
            }

            var atlas = new PassiveAtlas(graph.State, places, nodes, links, places.IndexesOf(graph.Links.GetValueOrDefault(graph.OwnStart)));
            this.passiveAtlas = atlas;
            return atlas;
        }

        /// <summary>The tree for the finder; the class's own start, which has no place on the canvas, is one more node after the drawn ones.</summary>
        private sealed record PassiveAtlas(IntPtr State, PassivePlaces Places, PassiveNodeRow[] Nodes, int[][] Links, int[] StartLinks)
        {
            /// <summary>What each node costs from the allocated ids and the own start.</summary>
            public int[] StepsFrom(HashSet<int> allocated) => PassiveMap.Steps(this.Links.Append(this.StartLinks).ToArray(), this.Taken(allocated));

            /// <summary>The nodes to take, in order, for the target; see <see cref="PassiveMap.Trail"/>.</summary>
            public List<int>? TrailTo(HashSet<int> allocated, int target, Func<int, int> cost) =>
                PassiveMap.Trail(this.Links.Append(this.StartLinks).ToArray(), this.Taken(allocated), target, cost);

            /// <summary>The links with another start in the place of the own one.</summary>
            public int[][] LinksWithStart(PassiveGraph graph, IntPtr start) =>
                this.Links.Append(this.Places.IndexesOf(graph.Links.GetValueOrDefault(start))).ToArray();

            /// <summary>
            ///     A list's nodes but the one under the cursor, those nearest in points first, then nearest the cursor; what is already
            ///     taken last. The build's list holds its keystones, notables and jewel sockets not taken, in the build file's order when it is here.
            /// </summary>
            public IReadOnlyList<IntPtr> Members(PassiveCategory category, int[] steps, IntPtr near, HashSet<IntPtr> marked, Func<IntPtr, int> buildStep)
            {
                var here = this.Places.Index.TryGetValue(near, out var at) ? this.Places.Positions[at] : Vector2.Zero;
                return Enumerable.Range(0, this.Nodes.Length)
                    .Where(index => this.Nodes[index].Record != near)
                    .Where(index => category switch
                    {
                        PassiveCategory.Build => marked.Contains(this.Nodes[index].Record) && steps[index] != 0 &&
                                                 this.Nodes[index].Class is PassiveClass.Keystone or PassiveClass.Notable or PassiveClass.JewelSocket,
                        PassiveCategory.Keystones => this.Nodes[index].Class == PassiveClass.Keystone,
                        PassiveCategory.Notables => this.Nodes[index].Class == PassiveClass.Notable,
                        PassiveCategory.JewelSockets => this.Nodes[index].Class == PassiveClass.JewelSocket,
                        PassiveCategory.Reachable => steps[index] == 1,
                        _ => false,
                    })
                    .OrderBy(index => category == PassiveCategory.Build ? buildStep(this.Nodes[index].Record) : 0)
                    .ThenBy(index => steps[index] > 0 ? steps[index] : int.MaxValue)
                    .ThenBy(index => this.Nodes[index].Class)
                    .ThenBy(index => Vector2.Distance(this.Places.Positions[index], here))
                    .Select(index => this.Nodes[index].Record)
                    .ToList();
            }

            private IEnumerable<int> Taken(HashSet<int> allocated) =>
                Enumerable.Range(0, this.Nodes.Length).Where(index => this.Nodes[index].TakenIn(allocated)).Append(this.Nodes.Length);
        }
    }
}
