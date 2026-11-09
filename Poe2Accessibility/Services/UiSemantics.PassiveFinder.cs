namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;

    /// <summary>
    ///     The passive tree's finder for <see cref="UiSemantics"/>: a blind player cannot look across the tree, so the D-pad,
    ///     which the game leaves free there, lists keystones, notables, jewel sockets and what is reachable, by the points
    ///     they cost, and every node then says the way to the one chosen (docs/research/passive-tree.md).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The pushes counted toward the finder's target; beyond, only its direction is said.</summary>
        private const int MaxFinderPushes = 30;

        private readonly PassiveFinder finder = new();
        private PassiveAtlas? passiveAtlas;
        private (IntPtr From, IntPtr To, string Words)? finderWay;
        private (IntPtr Record, string Line)? finderLine;

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
            IReadOnlyList<IntPtr> Members(PassiveCategory category) => atlas.Members(category, steps, record);
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
        ///     The finder's line for the hovered node: the way to its target, or that the cursor got there; empty with no target.
        ///     Kept while the cursor stays: a choice made on this node is said by the D-pad, and a changed line would cut that off.
        /// </summary>
        private string FinderLine(PassiveGraph graph, IntPtr record, PassiveNodeRow node)
        {
            if (this.finderLine is { } shown && shown.Record == record)
            {
                return shown.Line;
            }

            var atlas = this.AtlasOf(graph);
            var line = this.finder.Arrive(record) ? Announcements.BeaconArrived(node.Name)
                : atlas.Places.Index.TryGetValue(this.finder.Target, out var target) ? Announcements.PassiveToward(atlas.Nodes[target].Name, this.WayTo(graph, atlas, record, this.finder.Target))
                : string.Empty;
            this.finderLine = (record, line);
            return line;
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
            public int[] StepsFrom(HashSet<int> allocated)
            {
                var taken = Enumerable.Range(0, this.Nodes.Length).Where(index => this.Nodes[index].TakenIn(allocated)).Append(this.Nodes.Length);
                return PassiveMap.Steps(this.Links.Append(this.StartLinks).ToArray(), taken);
            }

            /// <summary>The links with another start in the place of the own one.</summary>
            public int[][] LinksWithStart(PassiveGraph graph, IntPtr start) =>
                this.Links.Append(this.Places.IndexesOf(graph.Links.GetValueOrDefault(start))).ToArray();

            /// <summary>A list's nodes but the one under the cursor, those nearest in points first, then nearest the cursor; what is already taken last.</summary>
            public IReadOnlyList<IntPtr> Members(PassiveCategory category, int[] steps, IntPtr near)
            {
                var here = this.Places.Index.TryGetValue(near, out var at) ? this.Places.Positions[at] : Vector2.Zero;
                return Enumerable.Range(0, this.Nodes.Length)
                    .Where(index => this.Nodes[index].Record != near)
                    .Where(index => category switch
                    {
                        PassiveCategory.Keystones => this.Nodes[index].Class == PassiveClass.Keystone,
                        PassiveCategory.Notables => this.Nodes[index].Class == PassiveClass.Notable,
                        PassiveCategory.JewelSockets => this.Nodes[index].Class == PassiveClass.JewelSocket,
                        PassiveCategory.Reachable => steps[index] == 1,
                        _ => false,
                    })
                    .OrderBy(index => steps[index] > 0 ? steps[index] : int.MaxValue)
                    .ThenBy(index => this.Nodes[index].Class)
                    .ThenBy(index => Vector2.Distance(this.Places.Positions[index], here))
                    .Select(index => this.Nodes[index].Record)
                    .ToList();
            }
        }
    }
}
