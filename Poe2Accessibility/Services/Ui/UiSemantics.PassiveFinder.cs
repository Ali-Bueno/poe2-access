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
        /// <summary>The pushes counted toward the finder's target; beyond, only its direction is said (30 until 2026-11-10: a place left far from the start).</summary>
        private const int MaxFinderPushes = 60;

        /// <summary>No passive node read for this long, and the tree was left (a popup over it is shorter, and changes no node).</summary>
        private const int TreeLeftMs = 5000;

        private readonly PassiveFinder finder = new();
        private PassiveAtlas? passiveAtlas;
        private (IntPtr From, IntPtr To, int Max, Way? Way)? finderWay;
        private (IntPtr Record, int Taken, GuideSay Say)? guideSaid;
        private string briefGoal = string.Empty;
        private PassiveResume? resume;
        /// <summary>When a node was last read; halved so that the time since then does not overflow before the first one.</summary>
        private long lastPassiveAt = long.MinValue / 2;
        private IntPtr openedOn;
        private int returnTaken;

        /// <summary>The finder is on (setting PassiveFinder): the D-pad answers and locked nodes say what they cost.</summary>
        public bool PassiveFinderOn { get; set; } = true;

        /// <summary>
        ///     While a guide leads (the finder's target or the build's next node), a node passed on the way is its name and the way on,
        ///     the goal said only when it changes, and the node to take leaves its links and button hints for the details (setting PassiveGuideBrief).
        /// </summary>
        public bool PassiveGuideBrief { get; set; } = true;

        /// <summary>The character whose tree is open, for <see cref="PassiveResumeFile"/>; set by the plugin.</summary>
        public Func<string>? CharacterName { get; set; }

        /// <summary>Where each character's cursor last was on the tree (config/passive-resume.txt); null keeps it for this session.</summary>
        public string? PassiveResumeFile
        {
            set => this.resume = new PassiveResume(value);
        }

        /// <summary>
        ///     Whether the stick's cursor is on a node of <paramref name="tree"/> while the game's focus rests on the folded
        ///     Build Planner's button: on a class's preview the game focuses that button as the stick walks the nodes, and
        ///     it hid them (docs/research/trials-and-campaign.md §3). With the planner unfolded the focus is the player's.
        /// </summary>
        public bool NodeUnderPlannerButton(IntPtr tree, IntPtr hovered, IntPtr focused)
        {
            if (!this.elements.TryReadElement(hovered, out var node) || !this.elements.TryReadElement(node.Raw.ParentPtr, out var holder) || holder.Raw.ParentPtr != tree ||
                !this.elements.TryReadElement(focused, out var button) || button.Name != AscendancyChoice.PlannerButtonName || !this.elements.TryReadElement(button.Raw.ParentPtr, out var planner))
            {
                return false;
            }

            foreach (var part in this.elements.ReadChildren(planner))
            {
                if (this.elements.TryReadElement(part, out var info) && info.Name == AscendancyChoice.PlannerOpenName && info.IsVisibleFlag)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     A node of <paramref name="canvas"/> for the finder to start from while the cursor is on none: the last one it
        ///     was on when that is the canvas's, else the first node read there; zero for a canvas without nodes.
        /// </summary>
        public IntPtr FinderAnchor(IntPtr canvas, IntPtr lastNode)
        {
            if (this.elements.TryReadElement(lastNode, out var last) && this.elements.TryReadElement(last.Raw.ParentPtr, out var holder) &&
                holder.Raw.ParentPtr == canvas && this.NodeRecord(lastNode) != null)
            {
                return lastNode;
            }

            if (!this.elements.TryReadElement(canvas, out var tree))
            {
                return IntPtr.Zero;
            }

            foreach (var groupAddress in this.elements.ReadChildren(tree))
            {
                if (!this.elements.TryReadElement(groupAddress, out var group) || !group.IsVisibleFlag)
                {
                    continue;
                }

                foreach (var node in this.elements.ReadChildren(group))
                {
                    if (this.NodeRecord(node) is { } record && this.PassiveRow(record) != null)
                    {
                        return node;
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>A D-pad press on a passive node: moves the finder and returns what to say; null when the tree cannot be read.</summary>
        /// <param name="hovered">The hovered node's element.</param>
        /// <param name="listStep">+1 next list, -1 previous, 0 to stay in it.</param>
        /// <param name="entryStep">+1 next entry, -1 previous.</param>
        public string? PassiveFinderMove(IntPtr hovered, int listStep, int entryStep)
        {
            if (!this.PassiveFinderOn || !this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.NodeRecord(hovered) is not { } record || this.PassiveGraphOf(this.TreeCanvasOf(group.Raw.ParentPtr), info) is not { } graph)
            {
                return null;
            }

            var atlas = this.AtlasOf(graph);
            var allocated = this.AllocatedIn(graph);
            var steps = atlas.StepsFrom(allocated);
            var marked = this.BuildMarks(graph);
            var plan = marked.Count > 0 ? this.PlanOf(graph, marked) : null;
            var order = plan == null ? null : this.BuildStepsOf(plan, marked);
            int BuildStep(IntPtr node) => order != null && order.Steps.TryGetValue(this.PassiveIdOf(node), out var step) ? step : int.MaxValue;
            // The build's list is what its guide leads to: the nodes to take now, as many as there are points for (the owner, 2026-11-14).
            var points = this.PointsLeft(graph);
            int Pool(IntPtr node) => this.PoolOf(plan, node);
            int Waits(IntPtr node) => BuildPlan.PointsOrder(Pool(node), points);
            // What each node with a choice was given, and which of them the build's notes would have otherwise.
            var choices = this.ChoicesIn(graph);
            var choiceCounts = PassiveChoices.Counts(choices.Values);
            bool Differs(IntPtr node) => this.AttributesOf(plan, node, choices) is (BuildNotes.Attribute wanted, BuildNotes.Attribute chosen) && wanted != chosen;
            var refunding = this.Refunding(graph);
            IReadOnlyList<IntPtr> Members(PassiveCategory category) => category switch
            {
                PassiveCategory.Attributes => atlas.Chosen(choices, choiceCounts.Select(count => count.Variant).ToList(), record, Differs),
                PassiveCategory.Refundable => refunding ? atlas.Refundable(allocated, record) : Array.Empty<IntPtr>(),
                PassiveCategory.Build => BuildPlan.TakeNow(atlas.Members(category, steps, record, marked, BuildStep, Waits).Select(node => (node, Pool(node))), points),
                _ => atlas.Members(category, steps, record, marked, BuildStep, Waits),
            };
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

            // The build's lists hold the node under the cursor too, so that they count what their header does.
            var found = this.finder.Target == record
                ? $"{Announcements.Here(atlas.Nodes[target].Name)}, {Where(atlas, target, steps)}"
                : Announcements.PassiveFound(atlas.Nodes[target].Name, Where(atlas, target, steps), this.WayTo(graph, atlas, record, this.finder.Target, MaxFinderPushes)?.Words(false) ?? string.Empty);
            if (this.finder.Category is PassiveCategory.Build or PassiveCategory.BuildTaken && order != null && BuildStep(this.finder.Target) is var step and < int.MaxValue)
            {
                // The build's nodes are walked in its order: each says which step of it it is, and the weapon set whose points take it.
                var set = this.finder.Category == PassiveCategory.Build ? plan?.WeaponSetOf(this.PassiveIdOf(this.finder.Target)) ?? 0 : 0;
                found = $"{Announcements.BuildStep(step + 1, order.Count)}{(set > 0 ? ", " + Announcements.BuildWithSetPoints(set) : string.Empty)}. {found}";
            }

            if (this.finder.Category == PassiveCategory.Attributes && ChoiceName(atlas.Nodes[target], choices) is { Length: > 0 } choice)
            {
                var advice = this.AttributesOf(plan, this.finder.Target, choices) is (BuildNotes.Attribute wanted, BuildNotes.Attribute chosen) && wanted != chosen
                    ? ", " + Announcements.BuildAttributeDiffers(Announcements.AttributeName(wanted), Announcements.AttributeName(chosen))
                    : string.Empty;
                found = Announcements.PassiveChoice(choice + advice, found);
            }

            if (listStep == 0)
            {
                return found;
            }

            var list = Announcements.PassiveCategoryName(this.finder.Category);
            if (order is { NotShown: > 0 } && string.Join(", ", order.NotShownIds) is var absent && absent != this.loggedNotShown)
            {
                this.loggedNotShown = absent; // once per build: what the header's "more of the build" are
                this.TreeFields.Log?.Invoke($"Build nodes not on this tree ({order.Ascendancy} of an ascendancy): {absent}");
            }

            var progress = this.finder.Category is PassiveCategory.Build or PassiveCategory.BuildTaken ? BuildProgress(atlas, steps, marked, order?.NotShown ?? 0, order?.Ascendancy ?? 0) : string.Empty;
            if (this.finder.Category == PassiveCategory.Build && points != null)
            {
                // Why the list is shorter than what is left of the build: it holds what the points pay for.
                progress += ", " + Announcements.BuildToTakeNow(Members(PassiveCategory.Build).Count(node => points.Of(Pool(node)) > 0));
            }
            else if (this.finder.Category == PassiveCategory.Ascendancy)
            {
                // How far along the ascendancy is: its nodes taken of those it shows, the one under the cursor too.
                var own = Enumerable.Range(0, atlas.Nodes.Length).Where(index => BuildPlan.IsAscendancyNode(atlas.Nodes[index].Id) && !atlas.Nodes[index].IsStart).ToList();
                progress = Announcements.BuildProgress(own.Count(index => steps[index] == 0), own.Count, 0);
            }
            else if (this.finder.Category == PassiveCategory.Attributes)
            {
                progress = Announcements.PassiveChoiceCounts(choiceCounts.Select(count =>
                    (ShownName(PassiveTreeCatalog.Shared.VariantOf(count.Variant) is { } variant ? PassiveTreeCatalog.Shared.Name(variant.Id, Localization.Language) ?? variant.Id : string.Empty), count.Nodes)));
            }

            return Announcements.InContext(progress.Length > 0 ? $"{list}, {progress}" : list, found);
        }

        /// <summary>The build's absent node ids last written to the log.</summary>
        private string loggedNotShown = string.Empty;

        /// <summary>How far along the active build the character is: its nodes taken, the points spent outside it, and the file's nodes this tree does not show.</summary>
        private static string BuildProgress(PassiveAtlas atlas, int[] steps, HashSet<IntPtr> marked, int notShown, int ascendancy)
        {
            // A start is no point to spend: an ascendancy's is marked with the build's nodes and never in the allocated list.
            var toTake = marked.Where(node => !(atlas.Places.Index.TryGetValue(node, out var index) && atlas.Nodes[index].IsStart)).ToList();
            var taken = toTake.Count(node => atlas.Places.Index.TryGetValue(node, out var index) && steps[index] == 0);
            var outside = atlas.Places.Index.Count(place => steps[place.Value] == 0 && !marked.Contains(place.Key) && !atlas.Nodes[place.Value].IsStart);
            return Announcements.BuildProgress(taken, toTake.Count, outside, notShown, ascendancy);
        }

        /// <summary>Research (dev command "passivemap"): from each class's start, how many points the nearest keystones and notables cost.</summary>
        public IEnumerable<string> PassiveMapReport(IntPtr hovered)
        {
            if (!this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.PassiveGraphOf(this.TreeCanvasOf(group.Raw.ParentPtr), info) is not { } graph)
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
        ///     What the guide says on the hovered node — the finder's target first, else the build's next node — worked out once per
        ///     node and allocation: kept while the cursor stays, since a choice made on this node is said by the D-pad and a changed
        ///     line would cut that off, while a node taken here moves the guide on.
        /// </summary>
        private GuideSay GuideAt(PassiveGraph graph, IntPtr record, HashSet<int> allocated, HashSet<IntPtr> marked, IntPtr? next)
        {
            if (this.guideSaid is { } shown && shown.Record == record && shown.Taken == allocated.Count)
            {
                return shown.Say;
            }

            var atlas = this.AtlasOf(graph);
            var say = atlas.Places.Index.TryGetValue(this.finder.Target, out var target) ? this.FinderGuide(graph, atlas, record, target, allocated, marked)
                : next is { } goal ? this.BuildGuide(graph, atlas, record, goal, allocated, marked)
                : GuideSay.None;
            this.guideSaid = (record, allocated.Count, say);
            return say;
        }

        /// <summary>
        ///     "Toward Scarred Faith: up, 7 pushes" until the cursor has been on the target, to read it; then "Scarred Faith, 8 points to
        ///     go: allocate Attribute: down left, 2 pushes", "allocate this one" on that node, until the target is taken.
        /// </summary>
        private GuideSay FinderGuide(PassiveGraph graph, PassiveAtlas atlas, IntPtr record, int target, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            var name = atlas.Nodes[target].Name;
            if (this.finder.Returning)
            {
                if (this.finder.Arrive(record, false))
                {
                    return new GuideSay(Announcements.ResumeArrived, true, false, null, true);
                }

                if (allocated.Count != this.returnTaken)
                {
                    this.finder.Drop(); // a node taken elsewhere: work goes on there
                    return GuideSay.None;
                }

                var back = this.WayTo(graph, atlas, record, this.finder.Target, MaxFinderPushes);
                return new GuideSay(this.GoalAndWay($"return {target}", Announcements.ResumeGoal(name), back), false, false, back?.Octant, true);
            }

            if (!this.finder.Taking && record != this.finder.Target)
            {
                var way = this.WayTo(graph, atlas, record, this.finder.Target, MaxFinderPushes);
                return new GuideSay(this.GoalAndWay($"toward {target}", Announcements.PassiveToward(name), way), false, false, way?.Octant, true);
            }

            var trail = TrailTo(atlas, target, allocated, marked);
            if (this.finder.Arrive(record, trail is { Count: > 0 }))
            {
                var arrived = Announcements.BeaconArrived(name);
                return this.finder.Taking && trail != null
                    ? this.TrailSay(graph, atlas, record, arrived, trail) with { Here = true }
                    : new GuideSay(arrived, true, false, null, true);
            }

            if (trail is not { Count: > 0 })
            {
                this.finder.Drop();
                return new GuideSay(trail == null ? Announcements.BeaconOff : $"{Announcements.PassiveGot(name)}. {Announcements.BeaconOff}", false, false, null, true);
            }

            return this.TrailSay(graph, atlas, record, name, trail);
        }

        /// <summary>The points left for the target and the next node to take, with the way to it from the hovered node.</summary>
        private GuideSay TrailSay(PassiveGraph graph, PassiveAtlas atlas, IntPtr from, string lead, List<int> trail)
        {
            var next = atlas.Nodes[trail[0]];
            var at = trail.FindIndex(index => atlas.Nodes[index].Record == from);
            if (next.Record == from)
            {
                // Brief, the node's name and "allocate this one" are enough; on the target itself, the arrival comes first.
                var here = !this.PassiveGuideBrief ? Announcements.PassiveTrail(lead, trail.Count, false, Announcements.AllocateThis)
                    : lead == next.Name ? Announcements.AllocateThis
                    : $"{lead}, {Announcements.AllocateThis}";
                return new GuideSay(here, true, true, null, true);
            }

            var way = this.WayTo(graph, atlas, from, next.Record, MaxFinderPushes);
            var goal = Announcements.PassiveTrail(lead, trail.Count, !this.PassiveGuideBrief && at > 0 && at < trail.Count - 1, Announcements.AllocateGoal(next.Name));
            return new GuideSay(this.GoalAndWay($"trail {trail[^1]} {trail.Count} {trail[0]}", goal, way), false, at >= 0, way?.Octant, true);
        }

        /// <summary>The build's next node: nothing on it (its state says "next in the build"), elsewhere the way to it.</summary>
        private GuideSay BuildGuide(PassiveGraph graph, PassiveAtlas atlas, IntPtr record, IntPtr next, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            if (next == record)
            {
                return new GuideSay(string.Empty, true, true, null, true);
            }

            var way = this.WayTo(graph, atlas, record, next, MaxFinderPushes);
            var name = atlas.Places.Index.TryGetValue(next, out var at) ? atlas.Nodes[at].Name : string.Empty;
            // A next node on the other of the two trees says which: the ascendancy's is drawn inside the class's.
            var nextInAscendancy = BuildPlan.IsAscendancyNode(this.PassiveIdOf(next));
            var tree = nextInAscendancy == BuildPlan.IsAscendancyNode(this.PassiveIdOf(record)) ? string.Empty : Announcements.BuildNextTree(nextInAscendancy);
            // A node the build takes with a weapon set's points says so: held A chooses the points, and the tree's own may be none.
            var set = this.PlanOf(graph, marked)?.WeaponSetOf(this.PassiveIdOf(next)) ?? 0;
            var withPoints = set > 0 ? Announcements.BuildWithSetPoints(set) : string.Empty;
            var line = this.PassiveGuideBrief ? this.GoalAndWay($"build {next}", Announcements.BuildNextGoal(name, tree, withPoints), way) : this.DescribeBuildNext(graph, record, next, allocated);
            return new GuideSay(line, false, marked.Contains(record), way?.Octant, true);
        }

        /// <summary>"goal: way"; brief, the way is short ("up, 3") and the goal said only when it changed since the last one said.</summary>
        private string GoalAndWay(string key, string goal, Way? way)
        {
            var words = way?.Words(this.PassiveGuideBrief) ?? string.Empty;
            var line = words.Length == 0 ? goal : this.PassiveGuideBrief && key == this.briefGoal ? words : $"{goal}: {words}";
            this.briefGoal = key;
            return line;
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

        /// <summary>
        ///     Notes the node under the cursor as the character's place on the tree, kept across sessions, but for the node the tree
        ///     opens on; and on opening, when no build leads, guides back to the place last noted (user 2026-11-10: "el juego al
        ///     cerrarlo y abrir regresa el árbol al inicio").
        /// </summary>
        private void NoteTreePlace(PassiveGraph graph, IntPtr record, HashSet<int> allocated, IntPtr? buildNext)
        {
            var now = Environment.TickCount64;
            var opened = now - this.lastPassiveAt > TreeLeftMs;
            this.lastPassiveAt = now;
            this.treeOpenings += opened ? 1 : 0;
            if (this.resume == null || this.CharacterName?.Invoke() is not { Length: > 0 } character)
            {
                return;
            }

            var id = this.PassiveIdOf(record);
            if (opened)
            {
                this.openedOn = record;
                if (buildNext == null && this.finder.Target == IntPtr.Zero && this.resume.Get(character) is { } last && last != id &&
                    graph.Elements.Keys.FirstOrDefault(other => this.PassiveIdOf(other) == last) is var back && back != IntPtr.Zero)
                {
                    this.finder.Return(back);
                    this.returnTaken = allocated.Count;
                }

                return;
            }

            if (record != this.openedOn)
            {
                this.openedOn = IntPtr.Zero;
                this.resume.Set(character, id);
            }
        }

        /// <summary>The pushes from one node to another, the direction alone when it is too far to count; null for a node with no place.</summary>
        private Way? WayTo(PassiveGraph graph, PassiveAtlas atlas, IntPtr from, IntPtr to, int maxMoves)
        {
            if (this.finderWay is { } cached && cached.From == from && cached.To == to && cached.Max == maxMoves)
            {
                return cached.Way;
            }

            var places = atlas.Places;
            Way? way = null;
            if (places.Index.TryGetValue(from, out var here) && places.Index.TryGetValue(to, out var there))
            {
                var pushes = this.PushesBetween(graph, places, from, here, there, maxMoves, out var far);
                way = pushes is { Count: > 0 } ? new Way(pushes[0], pushes.Count, false)
                    : new Way(PassiveStick.Nearest(places.Positions[there] - places.Positions[here]), null, far);
            }

            this.finderWay = (from, to, maxMoves, way);
            return way;
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

        /// <summary>Every drawn node with its row and links, by the index of <see cref="PassivePlaces"/>, read once per set of places.</summary>
        private PassiveAtlas AtlasOf(PassiveGraph graph)
        {
            // Its indexes are the places': made again (an ascendancy shown since), the atlas is too.
            var places = this.PlacesOf(graph);
            if (this.passiveAtlas is { } cached && ReferenceEquals(cached.Places, places))
            {
                return cached;
            }

            var nodes = new PassiveNodeRow[places.Positions.Length];
            var links = new int[nodes.Length][];
            foreach (var (record, index) in places.Index)
            {
                nodes[index] = this.PassiveRow(record) is { } row ? this.ReadPassiveRow(record, row) : new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false, PassiveClass.Other);
                links[index] = places.IndexesOf(graph.Links.GetValueOrDefault(record));
            }

            var atlas = new PassiveAtlas(graph.State, places, nodes, links, places.IndexesOf(graph.Links.GetValueOrDefault(graph.OwnStart)), places.IndexesOf(new[] { graph.AscendancyStart }));
            this.passiveAtlas = atlas;
            return atlas;
        }

        /// <summary>
        ///     The first push toward a node and how many it takes; no count when it is further than counted (<paramref name="Far"/>)
        ///     or the stick rule is not trusted, then its direction alone.
        /// </summary>
        private readonly record struct Way(int Octant, int? Pushes, bool Far)
        {
            /// <summary>"up, 7 pushes", brief "up, 7", "far, up", or "up".</summary>
            public string Words(bool brief)
            {
                var direction = Announcements.StickDirections[this.Octant];
                return this.Pushes is { } count ? (brief ? $"{direction}, {count}" : Announcements.PassivePushes(direction, count))
                    : this.Far ? Announcements.Far(direction)
                    : direction;
            }
        }

        /// <summary>
        ///     What a guide says on a node: its line; whether the node is the one to take (or the finder's target reached); whether it
        ///     lies on the guide's line (a build node, a node of the finder's trail); the next push's octant; whether a guide leads at all.
        /// </summary>
        private readonly record struct GuideSay(string Line, bool Here, bool OnLine, int? Octant, bool Leading)
        {
            public static GuideSay None => new(string.Empty, false, false, null, false);
        }

        /// <summary>
        ///     The tree for the finder; the class's own start, which has no place on the canvas, is one more node after the drawn
        ///     ones, and the ascendancy's own start, which is drawn, counts as taken (<paramref name="OwnNodes"/>).
        /// </summary>
        private sealed record PassiveAtlas(IntPtr State, PassivePlaces Places, PassiveNodeRow[] Nodes, int[][] Links, int[] StartLinks, int[] OwnNodes)
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
            ///     taken last. The build's two lists hold every node of it not taken, and taken, the one under the cursor too, in the
            ///     build file's order when it is here; the caller keeps of the first those the points left pay for.
            /// </summary>
            /// <param name="waits">Where a node to take stands by the points left (<see cref="BuildPlan.PointsOrder"/>): the build's list begins with those there are points for.</param>
            public IReadOnlyList<IntPtr> Members(PassiveCategory category, int[] steps, IntPtr near, HashSet<IntPtr> marked, Func<IntPtr, int> buildStep, Func<IntPtr, int> waits)
            {
                var here = this.Places.Index.TryGetValue(near, out var at) ? this.Places.Positions[at] : Vector2.Zero;
                return Enumerable.Range(0, this.Nodes.Length)
                    .Where(index => this.Nodes[index].Record != near || category is PassiveCategory.Build or PassiveCategory.BuildTaken)
                    .Where(index => category switch
                    {
                        PassiveCategory.Build => marked.Contains(this.Nodes[index].Record) && steps[index] != 0 && !this.Nodes[index].IsStart,
                        PassiveCategory.BuildTaken => marked.Contains(this.Nodes[index].Record) && steps[index] == 0 && !this.Nodes[index].IsStart,
                        PassiveCategory.OutsideBuild => marked.Count > 0 && steps[index] == 0 && !marked.Contains(this.Nodes[index].Record) && !this.Nodes[index].IsStart,
                        PassiveCategory.Keystones => this.Nodes[index].Class == PassiveClass.Keystone,
                        PassiveCategory.Notables => this.Nodes[index].Class == PassiveClass.Notable,
                        PassiveCategory.JewelSockets => this.Nodes[index].JewelSocket,
                        PassiveCategory.Ascendancy => BuildPlan.IsAscendancyNode(this.Nodes[index].Id) && !this.Nodes[index].IsStart,
                        PassiveCategory.Reachable => steps[index] == 1,
                        _ => false,
                    })
                    .OrderBy(index => category == PassiveCategory.Build ? waits(this.Nodes[index].Record) : 0)
                    .ThenBy(index => category is PassiveCategory.Build or PassiveCategory.BuildTaken ? buildStep(this.Nodes[index].Record) : 0)
                    .ThenBy(index => steps[index] > 0 ? steps[index] : int.MaxValue)
                    .ThenBy(index => this.Nodes[index].Class)
                    .ThenBy(index => Vector2.Distance(this.Places.Positions[index], here))
                    .Select(index => this.Nodes[index].Record)
                    .ToList();
            }

            /// <summary>The allocated nodes that were given a choice, those the build would have otherwise first, then by choice in the order given, nearest the cursor first.</summary>
            public IReadOnlyList<IntPtr> Chosen(IReadOnlyDictionary<int, int> choices, IReadOnlyList<int> order, IntPtr near, Func<IntPtr, bool> differs)
            {
                var here = this.Places.Index.TryGetValue(near, out var at) ? this.Places.Positions[at] : Vector2.Zero;
                var places = order.ToList();
                return Enumerable.Range(0, this.Nodes.Length)
                    .Where(index => ChoiceOf(this.Nodes[index], choices) != null)
                    .OrderBy(index => differs(this.Nodes[index].Record) ? 0 : 1)
                    .ThenBy(index => places.IndexOf(ChoiceOf(this.Nodes[index], choices) ?? 0))
                    .ThenBy(index => Vector2.Distance(this.Places.Positions[index], here))
                    .Select(index => this.Nodes[index].Record)
                    .ToList();
            }

            /// <summary>How many allocated nodes hang from this one (<see cref="PassiveMap.Held"/>): none, and the game refunds it now.</summary>
            public int Held(HashSet<int> allocated, int node) => PassiveMap.Held(this.Links.Append(this.StartLinks).ToArray(), this.Taken(allocated).ToHashSet(), this.Roots, node);

            /// <summary>The allocated nodes that hold no other, but the one under the cursor, nearest it first.</summary>
            public IReadOnlyList<IntPtr> Refundable(HashSet<int> allocated, IntPtr near)
            {
                var here = this.Places.Index.TryGetValue(near, out var at) ? this.Places.Positions[at] : Vector2.Zero;
                var links = this.Links.Append(this.StartLinks).ToArray();
                var taken = this.Taken(allocated).ToHashSet();
                var roots = this.Roots;
                return Enumerable.Range(0, this.Nodes.Length)
                    .Where(index => taken.Contains(index) && this.Nodes[index].Record != near && !this.Nodes[index].IsStart && PassiveMap.Held(links, taken, roots, index) == 0)
                    .OrderBy(index => Vector2.Distance(this.Places.Positions[index], here))
                    .Select(index => this.Nodes[index].Record)
                    .ToList();
            }

            /// <summary>Where the trees begin: the ascendancy's own start and the class's, the node after the drawn ones.</summary>
            private IReadOnlyCollection<int> Roots => this.OwnNodes.Append(this.Nodes.Length).ToList();

            private IEnumerable<int> Taken(HashSet<int> allocated) =>
                Enumerable.Range(0, this.Nodes.Length).Where(index => this.Nodes[index].TakenIn(allocated)).Concat(this.OwnNodes).Append(this.Nodes.Length);
        }
    }
}
