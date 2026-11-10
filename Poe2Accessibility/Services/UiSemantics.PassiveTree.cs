namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameOffsets.Natives;
    using GameReader;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Passive tree nodes for <see cref="UiSemantics"/> (D2R skill tree readout, adapted): the hovered node's name,
    ///     kind and state from its dat row and the tree's allocated list, its stats from the tooltip, then the nodes it
    ///     links to with the push of the stick that reaches each, as the game's own stick rule lands it
    ///     (<see cref="PassiveStick"/>, docs/research/passive-tree.md).
    /// </summary>
    public sealed partial class UiSemantics
    {
        private const int MaxPassiveNameChars = 96;

        /// <summary>Upper bounds of the graph read once per tree: edges, groups, nodes.</summary>
        private const int MaxPassiveEdges = 20000;

        private const int MaxPassiveElements = 20000;

        /// <summary>A node links to a handful of nodes; a record edge vector longer than this is a misread.</summary>
        private const int MaxRecordEdges = 64;

        /// <summary>The tree's canvas holds dozens of node groups; an element with fewer children is never searched as one.</summary>
        private const int MinCanvasGroups = 10;

        /// <summary>A push steps the cursor at once; a landing seen later than this is a hold's pan and snap, not the step rule.</summary>
        private const int StickStepMs = 300;

        /// <summary>The deflection a push must reach to count, and the one it must drop under to end.</summary>
        private const float PushDeflection = 0.5f;

        private const float ReleaseDeflection = 0.3f;

        /// <summary>The compatibility part said once when the self-check loses the rule.</summary>
        private const string StickRulePart = "passive tree stick rule";

        private PassiveGraph? passiveGraph;
        private PassivePlaces? passivePlaces;
        private StickPush? stickPush;
        private StickOrigin? stickOrigin;

        /// <summary>
        ///     The right stick moved the cursor since the last left push began: two false "rule lost" in one evening of the
        ///     user's, each after right-stick moves (2026-11-09: "7 hit, 3 missed", then links by their side only and the finder's
        ///     ways gone, "la guía con el d pad se volvió loca").
        /// </summary>
        private bool cursorMovedFreely;

        /// <summary>The player's metadata path, to find the class's own start node and the nodes its class sees in others' place.</summary>
        public Func<string>? PlayerPath { get; set; }

        /// <summary>The nodes a class sees in another node's place (catalog passive-overrides.txt); null or empty: none swapped.</summary>
        public PassiveOverrides? Overrides { get; set; }

        private readonly Dictionary<IntPtr, IntPtr> shownRows = new();
        private string shownRowsFor = string.Empty;

        /// <summary>The line an unallocated node's tooltip ends with, in the client's language.</summary>
        public Func<string>? UnallocatedLabel { get; set; }

        /// <summary>Gets the stick rule's live self-check; once it is lost, links are given by their side only (<see cref="StickCheck"/>).</summary>
        public StickCheck StickCheck { get; } = new();

        private bool StickTrusted => !this.StickCheck.Lost;

        private bool focusLeftNode;

        /// <summary>
        ///     Per frame, both sticks as the game received them (y up): a left push begins past <see cref="PushDeflection"/>
        ///     and ends under <see cref="ReleaseDeflection"/>. The right stick moves the tree's cursor off the step rule's
        ///     footing (the node at the view's centre): the next push from there is not judged.
        /// </summary>
        public void ObserveStick(float x, float y, float rightX, float rightY, long now)
        {
            if (MathF.Max(MathF.Abs(rightX), MathF.Abs(rightY)) >= ReleaseDeflection)
            {
                this.cursorMovedFreely = true;
            }

            var deflection = MathF.Max(MathF.Abs(x), MathF.Abs(y));
            if (this.stickPush == null && deflection >= PushDeflection)
            {
                // Screen y grows down, as the game flips it.
                this.stickPush = new StickPush(now, Vector2.Normalize(new Vector2(x, -y)), this.cursorMovedFreely);
                this.cursorMovedFreely = false;
            }
            else if (this.stickPush != null && deflection < ReleaseDeflection)
            {
                this.stickPush = null;
            }
        }

        private enum PassiveState
        {
            Locked,
            Reachable,
            Allocated,
        }

        /// <summary>A hovered passive node; null for any other element.</summary>
        private AccessibleUiNode? DescribePassiveNode(in UiElementReader.ElementInfo info, bool hovered)
        {
            // A node is a child of a group of the canvas that holds the tree state; its record is judged only there.
            if (!this.InGame || !hovered ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) || this.TreeState(group.Raw.ParentPtr) == IntPtr.Zero ||
                this.NodeRecord(info.Address) is not { } record || this.PassiveRow(record) is not { } row ||
                this.PassiveGraphOf(group.Raw.ParentPtr, info) is not { } graph)
            {
                return null;
            }

            this.TreeFields.JudgeAllocated(graph.State, graph.Links.Keys);
            this.JudgeStickLanding(graph, info.Address, record);
            var allocated = this.AllocatedIds(graph.State);
            var node = this.ReadPassiveRow(record, row);
            var state = this.StateOf(graph, node, allocated);
            // The node's own tooltip comes last; before it, the author's note of a Build Planner build.
            var asides = new List<string>(); // button hints and red notices ("No cumples los requisitos"), apart
            var entries = this.TooltipEntries(info, asides);
            var tooltip = entries.Count > 0 ? entries[^1] : new List<string>();
            var notes = entries.SkipLast(1).SelectMany(lines => lines).ToList();
            var tooltipPending = tooltip.Count == 0;
            if (tooltip.Count > 0 && tooltip[0] == node.Name)
            {
                tooltip.RemoveAt(0);
            }

            tooltip.RemoveAll(line => line.StartsWith('(') && line.EndsWith(')')); // the English name under the title
            if (tooltip.Count > 0 && tooltip[^1] == this.UnallocatedLabel?.Invoke())
            {
                tooltip.RemoveAt(tooltip.Count - 1); // the state word says it
            }

            asides.RemoveAll(line => line == this.UnallocatedLabel?.Invoke()); // the same, should the game draw it red

            // The Build Planner's active build: the nodes the tree marks, with the author's notes when the file is here.
            var marked = this.BuildMarks(graph);
            var plan = marked.Count > 0 ? this.buildPlans.Matching(marked.Select(this.PassiveIdOf).ToHashSet()) : null;
            var next = marked.Count > 0 ? this.NextBuildNode(graph, marked, plan, allocated, record) : null;
            var buildWord = marked.Contains(record) ? Announcements.InBuild(next == record) : string.Empty;
            var stateWords = state == PassiveState.Locked ? this.LockedState(graph, record, allocated) : StateWord(state);
            var text = Announcements.PassiveNode(node.Name, node.Kind, string.Join(", ", new[] { stateWords, buildWord }.Where(part => part.Length > 0)));

            // One guide at a time: the finder's target takes the place of the build's next node.
            this.NoteTreePlace(graph, record, allocated, next);
            var guide = hovered ? this.GuideAt(graph, record, allocated, marked, next) : GuideSay.None;

            // Before the game builds the tooltip the name and state are said alone; stats, then links, follow with it.
            var whole = new List<string>();
            var stats = new List<string>(tooltip); // what the node does, without the build's step and note
            if (!tooltipPending)
            {
                var buildLines = plan != null && marked.Contains(record) ? this.BuildLines(plan, record).ToList() : new List<string>();
                tooltip.AddRange(buildLines);
                tooltip.AddRange(notes.Where(note => !buildLines.Any(line => SaysAlready(line, note))));
                whole.AddRange(tooltip);
                whole.AddRange(asides);
                var links = this.DescribeLinks(graph, info.Address, record, allocated, marked);
                if (links.Length > 0)
                {
                    whole.Add(links);
                }

                if (guide.Line.Length > 0)
                {
                    whole.Add(guide.Line);
                }
            }

            IReadOnlyList<string> said = whole;
            if (this.PassiveGuideBrief && guide.Leading)
            {
                // A guide leads: its words come first, then what the node does (user 2026-11-10: "que la guía tenga más prioridad",
                // "los tooltips del árbol, no de la build"), so the next push can cut in at once; links, button hints and, but on the
                // node to take, the build's step and note are left for the details (L3 held). The one to take says so with its name.
                if (!guide.Here && guide.Line.Length > 0)
                {
                    var word = state == PassiveState.Allocated ? StateWord(state)
                        : guide.OnLine ? (this.finder.Target != IntPtr.Zero ? Announcements.OnTheWay : Announcements.InBuild(false))
                        : string.Empty;
                    text = word.Length > 0 ? $"{node.Name}, {word}" : node.Name;
                    said = stats.Prepend(guide.Line).ToList();
                    tooltipPending = false;
                }
                else if (record == this.finder.Target)
                {
                    said = guide.Line.Length > 0 && !tooltipPending ? tooltip.Append(guide.Line).ToList() : tooltip; // read the target, then go on
                }
                else
                {
                    text = guide.Line.Length > 0 ? $"{text}, {guide.Line}" : text;
                    said = tooltip;
                }
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Panel,
                Text = text,
                IsVisible = this.elements.IsVisible(info),
                IsHovered = hovered,
                IsPassiveNode = true,
                Context = this.PanelTitle(info),
                Tooltip = said,
                Details = ReferenceEquals(said, whole) ? null : whole,
                TooltipPending = tooltipPending,
            };
        }

        /// <summary>
        ///     "Up: Armour and Evasion, allocated; down left: Attribute, reachable", clockwise from up; each link named by the
        ///     push of the stick that lands on it (<see cref="PassiveStick"/>), not its bare angle: a link 60 degrees
        ///     up-right is reached with up when nothing lies nearer that way (user 2026-10-31).
        /// </summary>
        private string DescribeLinks(PassiveGraph graph, IntPtr element, IntPtr record, HashSet<int> allocated, HashSet<IntPtr> marked)
        {
            if (!graph.Links.TryGetValue(record, out var linked) || this.NodeCentre(graph, element) is not { } here)
            {
                return string.Empty;
            }

            var places = this.PlacesOf(graph);
            var offsets = places.Index.TryGetValue(record, out var hereIndex) ? places.OffsetsFrom(hereIndex) : null;
            var stickLinks = places.IndexesOf(this.StickLinksOf(graph, record));
            var window = this.StickWindow(graph, element);
            var parts = new List<(double Order, string Text)>();
            foreach (var other in linked)
            {
                if (this.PassiveRow(other) is not { } row)
                {
                    continue;
                }

                var node = this.ReadPassiveRow(other, row);
                if (node.IconOnly)
                {
                    continue; // links through a mastery icon are not drawn
                }

                if (node.IsStart)
                {
                    // The class portrait at the centre; the node's own name is a PoE 1 class in capitals ("SIX").
                    parts.Add((double.MaxValue, Announcements.PassiveStartLink(this.StartClasses(row) ?? node.Name, other == graph.OwnStart)));
                    continue;
                }

                var state = StateWord(this.StateOf(graph, node, allocated));
                var words = Announcements.PassiveLink(node.Name, marked.Contains(other) ? $"{state}, {Announcements.InBuild(false)}" : state);
                if (!graph.Elements.TryGetValue(other, out var otherElement) || this.NodeCentre(graph, otherElement) is not { } there)
                {
                    parts.Add((double.MaxValue - 1, words)); // not drawn: no side to give
                    continue;
                }

                // Canvas y grows downwards, as on screen; StickDirections count counter-clockwise from right. Clockwise from up.
                var offset = there - here;
                var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
                var geometric = Announcements.StickDirections[PassiveStick.Nearest(offset)];
                var direction = offsets == null || !this.StickTrusted || !places.Index.TryGetValue(other, out var otherIndex)
                    ? geometric // a node the cursor never lands on (hidden), or a rule the self-check lost: its side only
                    : PassiveStick.OctantTo(offsets, otherIndex, stickLinks, window) is { } octant ? Announcements.StickDirections[octant]
                    : PassiveStick.BetweenTo(offsets, otherIndex, stickLinks, window) is { } half
                        ? Between(half)
                    : Announcements.NoDirectPush(geometric);
                var clockwiseFromUp = (((112.5 - angle) % 360) + 360) % 360; // from the up sector's left edge, so sectors keep their order
                parts.Add((clockwiseFromUp, $"{direction}, {words}"));
            }

            return parts.Count == 0 ? string.Empty : Announcements.PassiveLinks(parts.OrderBy(part => part.Order).Select(part => part.Text));
        }

        /// <summary>
        ///     Research (dev command "passivenear"): the hovered node and the drawn nodes nearest it on screen, each with its
        ///     angle (degrees, counter-clockwise from right), distance in pixels, whether it is linked, name and id.
        /// </summary>
        public IEnumerable<string> PassiveNeighbours(IntPtr hovered, int count)
        {
            if (!this.elements.TryReadElement(hovered, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var group) ||
                this.NodeRecord(hovered) is not { } record || this.PassiveGraphOf(group.Raw.ParentPtr, info) is not { } graph ||
                !PluginUiElementReflection.TryGetAbsoluteRect(hovered, out var position, out var size))
            {
                yield break;
            }

            var here = position + (size / 2);
            var linked = graph.Links.GetValueOrDefault(record) ?? new List<IntPtr>();
            var shown = graph.Elements.Count(pair => this.elements.TryReadElement(pair.Value, out var element) && element.IsVisibleFlag);
            yield return $"hovered {this.PassiveIdOf(record)} at {here.X:F0},{here.Y:F0} size {size.X:F0}; {shown} of {graph.Elements.Count} node elements shown";
            // What the stick rule says each push lands on (octants counter-clockwise from right), to check against the game,
            // with its inputs: the window, the record's own edge order against the graph file's, the row's linked-stage gate.
            var places = this.PlacesOf(graph);
            if (places.Index.TryGetValue(record, out var hereIndex))
            {
                var records = places.Index.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();
                var offsets = places.OffsetsFrom(hereIndex);
                var stickLinks = places.IndexesOf(this.StickLinksOf(graph, record));
                var window = this.StickWindow(graph, hovered);
                yield return "picks " + string.Join(", ", Enumerable.Range(0, 8).Select(octant =>
                    $"{Announcements.StickDirections[octant]} {(PassiveStick.Pick(offsets, octant, stickLinks, window) is var pick and >= 0 ? this.PassiveIdOf(records[pick]) : "-")}"));
                yield return $"window {(window is { } seen ? $"{seen.Size.X:F0}x{seen.Size.Y:F0} cursor {seen.Cursor.X:F1},{seen.Cursor.Y:F1} px per unit {seen.PixelsPerUnit:F4}" : "none (fitted model)")}";
                yield return $"self-check: {this.StickCheck}{(PassiveStick.Retuned ? "; constants retuned from the exe" : string.Empty)}";
                var fromRecord = this.RecordEdgeOrder(graph, record);
                yield return $"edge order record: {(fromRecord == null ? "unreadable" : string.Join(" ", fromRecord.Select(this.PassiveIdOf)))}; " +
                             $"file: {string.Join(" ", (graph.StickLinks.GetValueOrDefault(record) ?? new List<IntPtr>()).Select(this.PassiveIdOf))}; " +
                             $"no linked step {(this.PassiveRow(record) is { } hoveredRow ? this.ReadPassiveRow(record, hoveredRow).NoLinkedStep : "?")}";
            }

            var marked = this.BuildMarks(graph).Select(this.PassiveIdOf).ToHashSet();
            yield return $"build marks {marked.Count}: {string.Join(' ', marked.Take(12))}; file: {this.buildPlans.Matching(marked)?.Name ?? "none"}";
            var near = new List<(double Distance, string Line)>();
            foreach (var (other, element) in graph.Elements)
            {
                if (other == record || this.PassiveRow(other) is not { } row || this.ReadPassiveRow(other, row) is not { IconOnly: false } node ||
                    !this.elements.TryReadElement(element, out var otherInfo) || !otherInfo.IsVisibleFlag ||
                    !PluginUiElementReflection.TryGetAbsoluteRect(element, out var otherPosition, out var otherSize))
                {
                    continue;
                }

                var offset = otherPosition + (otherSize / 2) - here;
                var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
                near.Add((offset.Length(), $"{angle,7:F1} deg {offset.Length(),6:F0} px {(linked.Contains(other) ? "linked" : "      ")} {node.Name} ({this.PassiveIdOf(other)}) @{element:X}"));
            }

            foreach (var (_, line) in near.OrderBy(entry => entry.Distance).Take(count))
            {
                yield return line;
            }
        }

        private string PassiveIdOf(IntPtr record) =>
            this.PassiveRow(record) is { } row && this.reader.TryRead<IntPtr>(row, out var id) ? this.elements.ReadUtf16BufferAt(id, MaxPassiveNameChars) ?? "?" : "?";

        /// <summary>
        ///     The drawn nodes' centres on the tree's own canvas (icon-only ones never take the cursor), read once per graph:
        ///     laid-out places under the canvas (<see cref="LayoutRect"/>), the tree's coordinates whatever the window, the
        ///     zoom, the pan or the opening animation. One frame's screen pixels were cached before, and a frame taken while
        ///     the tree opened gave other picks at 5:4 and 21:9 than the game made, which were the same at both (2026-11-08).
        /// </summary>
        private PassivePlaces PlacesOf(PassiveGraph graph)
        {
            if (this.passivePlaces is { } cached && cached.State == graph.State)
            {
                return cached;
            }

            var records = new List<IntPtr>();
            var positions = new List<Vector2>();
            foreach (var (record, element) in graph.Elements)
            {
                if (this.elements.TryReadElement(element, out var info) && info.IsVisibleFlag &&
                    this.PassiveRow(record) is { } row && !this.ReadPassiveRow(record, row).IconOnly &&
                    this.NodeCentre(graph, element) is { } centre)
                {
                    records.Add(record);
                    positions.Add(centre);
                }
            }

            var index = records.Select((record, at) => (record, at)).ToDictionary(pair => pair.record, pair => pair.at);
            var places = new PassivePlaces(graph.State, positions.ToArray(), index, Array.Empty<int[]>());
            places = places with { Links = records.Select(record => places.IndexesOf(graph.StickLinks.GetValueOrDefault(record))).ToArray() };
            this.passivePlaces = places;
            return places;
        }

        /// <summary>A node's centre in the canvas's units (y down, as on screen), null when its chain does not reach the canvas.</summary>
        private Vector2? NodeCentre(PassiveGraph graph, IntPtr element) =>
            this.LayoutRect(element, graph.Canvas) is { } rect ? rect.Position + (rect.Size / 2) : null;

        /// <summary>
        ///     What the game's stick step sees through: the hovered node's centre and screen size against its canvas size
        ///     give the scale, the client area its bounds. Null when either is unreadable; the fitted model then stands in.
        /// </summary>
        private PassiveStick.Window? StickWindow(PassiveGraph graph, IntPtr hoveredElement)
        {
            var area = Core.Process.WindowArea;
            if (area.Width <= 0 || area.Height <= 0 ||
                !PluginUiElementReflection.TryGetAbsoluteRect(hoveredElement, out var position, out var size) || size.X <= 0 ||
                this.LayoutRect(hoveredElement, graph.Canvas) is not { } laid || laid.Size.X <= 0)
            {
                return null;
            }

            // The game pans until the hovered node sits at its frame's centre; the readout comes while the view still pans.
            var cursor = this.SettledCursor(graph) ?? position + (size / 2);
            return new PassiveStick.Window(cursor, new Vector2(area.Width, area.Height), size.X / laid.Size.X);
        }

        /// <summary>The centre of the canvas's frame, where the hovered node rests once the view stops panning; null when unreadable.</summary>
        private Vector2? SettledCursor(PassiveGraph graph) =>
            this.elements.TryReadElement(graph.Canvas, out var canvas) &&
            PluginUiElementReflection.TryGetAbsoluteRect(canvas.Raw.ParentPtr, out var framePosition, out var frameSize) && frameSize.X > 0 && frameSize.Y > 0
                ? PassiveStick.SettledCursor(framePosition, frameSize)
                : null;

        /// <summary>
        ///     The nodes linked to a record in the order the stick step walks them: the record's own edge vectors when they
        ///     read back whole (<see cref="RecordEdgeOrder"/>), else the same order rebuilt from the graph file's edge list.
        /// </summary>
        private List<IntPtr> StickLinksOf(PassiveGraph graph, IntPtr record) =>
            this.RecordEdgeOrder(graph, record) ?? graph.StickLinks.GetValueOrDefault(record) ?? new List<IntPtr>();

        /// <summary>
        ///     The other ends of a record's in-edges then its out-edges, as the game's stick step walks them (0x9B9BA0); null
        ///     unless every edge names the record at its end and a node the file links it to (self-loops skipped).
        /// </summary>
        private List<IntPtr>? RecordEdgeOrder(PassiveGraph graph, IntPtr record)
        {
            var known = graph.Links.GetValueOrDefault(record) ?? new List<IntPtr>();
            var order = new List<IntPtr>();
            foreach (var (offset, thisEnd) in new[] { (GameLayout.PassiveTree.RecordInEdgesOffset, 1), (GameLayout.PassiveTree.RecordOutEdgesOffset, 0) })
            {
                if (!this.reader.TryRead<StdVector>(record + offset, out var vector) ||
                    (vector.Last.ToInt64() - vector.First.ToInt64()) / IntPtr.Size is var count && count is < 0 or > MaxRecordEdges)
                {
                    return null;
                }

                foreach (var edge in this.reader.ReadArray<IntPtr>(vector.First, (int)count))
                {
                    var ends = this.reader.ReadArray<IntPtr>(edge, 2);
                    if (ends.Length < 2 || ends[thisEnd] != record)
                    {
                        return null;
                    }

                    var other = ends[1 - thisEnd];
                    if (other == record)
                    {
                        continue;
                    }

                    if (!known.Contains(other))
                    {
                        return null;
                    }

                    order.Add(other);
                }
            }

            return order;
        }

        /// <summary>
        ///     The pushes from a drawn node to another as the game lands them, the first with the node's own edge order; null
        ///     when the way is longer than <paramref name="maxMoves"/> or a push lands nowhere nearer.
        /// </summary>
        private List<int>? PushesBetween(PassiveGraph graph, PassivePlaces places, IntPtr from, int here, int there, int maxMoves)
        {
            if (!this.StickTrusted)
            {
                return null; // the side alone is said then
            }

            var window = graph.Elements.TryGetValue(from, out var element) ? this.StickWindow(graph, element) : null;
            var first = places.IndexesOf(this.StickLinksOf(graph, from));
            return PassiveStick.Route(places.Positions, here, there, maxMoves, index => index == here ? first : places.Links[index], window);
        }

        /// <summary>The focus went to something else than a passive node (a popup over the tree): the next landing is not judged.</summary>
        public void PassiveFocusLeft() => this.focusLeftNode = true;

        /// <summary>
        ///     The stick rule's self-check: when the hovered node changes right after a push, the node the rule predicts from the
        ///     previous one (its edge order and the window seen from it, before the game moved the view) is compared with the one
        ///     the game landed on; <see cref="StickCheck.MaxMisses"/> misses in <see cref="StickCheck.Window"/> pushes lose the rule, said once,
        ///     and pushes are still judged while it is lost, so a passing disturbance gives it back.
        /// </summary>
        private void JudgeStickLanding(PassiveGraph graph, IntPtr element, IntPtr record)
        {
            var places = this.PlacesOf(graph);
            var origin = this.stickOrigin;
            this.stickOrigin = places.Index.TryGetValue(record, out var here)
                ? new StickOrigin(graph.State, record, here, places.IndexesOf(this.StickLinksOf(graph, record)), this.StickWindow(graph, element))
                : null;
            if (origin == null || origin.State != graph.State || origin.Record == record || origin.Window is not { } window ||
                this.stickPush is not { Judged: false } push || Environment.TickCount64 - push.StartedAt > StickStepMs || !places.Index.TryGetValue(record, out var landed))
            {
                return;
            }

            this.stickPush = push with { Judged = true };
            if (this.focusLeftNode)
            {
                // The first push after a popup over the tree (the attribute's "+5 to Strength, 1 of 3", the weapon set's "Allocate"):
                // three of four landed elsewhere than predicted on 2026-11-10 and lost the rule; not judged, as after the right stick.
                this.focusLeftNode = false;
                return;
            }

            var predicted = PassiveStick.Pick(places.OffsetsFrom(origin.Index), push.Direction, origin.Linked, window);
            var verdict = this.StickCheck.Record(predicted == landed);
            if (verdict != StickVerdict.Same)
            {
                var pushed = $"last push {this.PassiveIdOf(origin.Record)} -> {this.PassiveIdOf(record)}, predicted " +
                           (predicted >= 0 ? this.PassiveIdOf(places.Index.First(pair => pair.Value == predicted).Key) : "no move");
                this.TreeFields.Log?.Invoke(verdict == StickVerdict.Lost
                    ? $"Stick rule lost: {this.StickCheck}; {pushed}; links are said by their side until it holds again"
                    : $"Stick rule holds again: {this.StickCheck}; {pushed}");
            }

            if (verdict == StickVerdict.Lost)
            {
                CompatibilityReport.Current?.Missing(StickRulePart);
            }
        }

        private PassiveState StateOf(PassiveGraph graph, PassiveNodeRow node, HashSet<int> allocated)
        {
            if (node.TakenIn(allocated))
            {
                return PassiveState.Allocated;
            }

            // Reachable: linked to an allocated node, or to the class's own start.
            return graph.Links.TryGetValue(node.Record, out var linked) &&
                   linked.Any(other => other == graph.OwnStart || (this.PassiveRow(other) is { } row && this.RowTaken(row, allocated)))
                ? PassiveState.Reachable
                : PassiveState.Locked;
        }

        /// <summary>The push halfway between two octants, the straight direction first ("between left and up left"; Spanish "y izquierda" would want "e").</summary>
        private static string Between(int half)
        {
            var (first, second) = half % 2 == 0 ? (half, (half + 1) % 8) : ((half + 1) % 8, half);
            return Announcements.BetweenDirections(Announcements.StickDirections[first], Announcements.StickDirections[second]);
        }

        private static string StateWord(PassiveState state) => state switch
        {
            PassiveState.Allocated => Announcements.PassiveAllocated,
            PassiveState.Reachable => Announcements.PassiveReachable,
            _ => Announcements.PassiveLocked,
        };

        /// <summary>The graph ids in the tree's allocated list (re-read on every describe: allocating changes it).</summary>
        private HashSet<int> AllocatedIds(IntPtr state)
        {
            return this.TreeFields.AllocatedAt(state, GameLayout.PassiveTree.AllocatedOffset)?.ToHashSet() ?? new HashSet<int>();
        }

        /// <summary>
        ///     The canvas's tree state, judged where it holds the passive skill graph; zero on any other element. When no
        ///     canvas field leads to it, the state's own graph file field is looked for (one change at a time), on an element
        ///     shaped like the canvas only.
        /// </summary>
        private IntPtr TreeState(IntPtr canvas)
        {
            var shift = this.elements.Controls?.Shift(ControlGroup.PassiveCanvas, canvas, at => this.TreeStateAt(canvas, at) != IntPtr.Zero) ?? 0;
            if (this.TreeStateAt(canvas, shift) is var state && state != IntPtr.Zero)
            {
                return state;
            }

            return this.elements.TryReadElement(canvas, out var info) && this.elements.ReadChildren(info).Length >= MinCanvasGroups &&
                   this.reader.TryRead<IntPtr>(canvas + GameLayout.PassiveTree.CanvasStateOffset + shift, out var candidate) && candidate != IntPtr.Zero &&
                   this.TreeFields.GraphFile(candidate, true) != IntPtr.Zero
                ? candidate
                : IntPtr.Zero;
        }

        /// <summary>A node record's graph id (from its own PassiveSkills row); null when it does not lead to one.</summary>
        private int? GraphIdOf(IntPtr record) =>
            this.PassiveRow(record) is { } row && this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var id) ? id : null;

        private IntPtr TreeStateAt(IntPtr canvas, int shift) =>
            this.reader.TryRead<IntPtr>(canvas + GameLayout.PassiveTree.CanvasStateOffset + shift, out var state) && state != IntPtr.Zero &&
            this.TreeFields.GraphFile(state, false) != IntPtr.Zero
                ? state
                : IntPtr.Zero;

        /// <summary>A node element's graph record, judged where it leads to a PassiveSkills row.</summary>
        private IntPtr? NodeRecord(IntPtr node)
        {
            var shift = this.elements.Controls?.Shift(ControlGroup.PassiveNode, node, at => this.NodeRecordAt(node, at) is { } record && this.PassiveRow(record) != null) ?? 0;
            return this.NodeRecordAt(node, shift);
        }

        private IntPtr? NodeRecordAt(IntPtr node, int shift) =>
            this.reader.TryRead<IntPtr>(node + GameLayout.PassiveTree.NodeRecordOffset + shift, out var record) && record != IntPtr.Zero ? record : null;

        /// <summary>Where a node record keeps its PassiveSkills key: at its start in 0.5.5f, found again by the table its file names.</summary>
        private static readonly DatKeySite PassiveSkillKey = new("passive node key", GameLayout.PassiveTree.RecordSkillKeyOffset, GameLayout.PassiveTree.SkillTable);

        /// <summary>The PassiveSkills row of a node record, trusted only when its key names the table.</summary>
        private IntPtr? PassiveRow(IntPtr record) => PassiveSkillKey.Row(this.reader, this.elements, record);

        /// <summary>
        ///     A node as the game shows it: name and kind from the row the player's class sees in its place (<see cref="ShownRow"/>),
        ///     its place in the graph (graph id, start, stick gate) from the record's own row.
        /// </summary>
        private PassiveNodeRow ReadPassiveRow(IntPtr record, IntPtr row)
        {
            var bytes = this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes);
            if (bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false, PassiveClass.Other);
            }

            var shownRow = this.ShownRow(row);
            var shown = shownRow == row ? bytes : this.elements.ReadBlock(shownRow, GameLayout.PassiveTree.RowBytes);
            if (shown.Length < GameLayout.PassiveTree.RowBytes)
            {
                shown = bytes;
            }

            var name = KeywordLink.Replace(this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(shown, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty, ShownText);
            var isStart = BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0;
            var iconOnly = shown[GameLayout.PassiveTree.RowIconOnlyOffset] == 1;
            var kind = shown[GameLayout.PassiveTree.RowKeystoneOffset] == 1 ? PassiveClass.Keystone
                : shown[GameLayout.PassiveTree.RowNotableOffset] == 1 ? PassiveClass.Notable
                : shown[GameLayout.PassiveTree.RowJewelSocketOffset] == 1 ? PassiveClass.JewelSocket
                : isStart || iconOnly ? PassiveClass.Other
                : PassiveClass.Small;
            var noLinkedStep = BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNoLinkedStepOffset) != 0;
            return new PassiveNodeRow(record, BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset), name, Announcements.PassiveKind(kind), iconOnly, isStart, kind, noLinkedStep)
            {
                ShownGraphId = BitConverter.ToUInt16(shown, GameLayout.PassiveTree.RowGraphIdOffset),
            };
        }

        /// <summary>
        ///     The PassiveSkills row the game shows for a node's own row: the player's class's override of it when it has one
        ///     (<see cref="Overrides"/>), found by stepping through the table and trusted only when the Id there is the expected
        ///     one; else the row itself. Remembered per row for the character it was found for.
        /// </summary>
        private IntPtr ShownRow(IntPtr row)
        {
            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            if (this.Overrides is not { Count: > 0 } overrides || path.Length == 0)
            {
                return row;
            }

            if (!string.Equals(path, this.shownRowsFor, StringComparison.OrdinalIgnoreCase))
            {
                this.shownRows.Clear();
                this.shownRowsFor = path;
            }

            if (this.shownRows.TryGetValue(row, out var known))
            {
                return known;
            }

            var shown = row;
            if (this.RowId(row) is { } id && overrides.Find(path, id) is { } swap)
            {
                var other = row + (nint)overrides.Step(swap);
                if (this.RowId(other) == swap.ShownId)
                {
                    shown = other;
                }
                else
                {
                    this.TreeFields.Log?.Invoke($"Passive override of {id} for {path}: no {swap.ShownId} {overrides.Step(swap)} bytes on; its own row is read");
                }
            }

            this.shownRows[row] = shown;
            return shown;
        }

        private string? RowId(IntPtr row) =>
            this.reader.TryRead<IntPtr>(row, out var id) && id != IntPtr.Zero ? this.elements.ReadUtf16BufferAt(id, MaxPassiveNameChars) : null;

        /// <summary>Whether a node's row, or the one its class sees in its place, is in the allocated list (which of the two the game keeps there is not measured).</summary>
        private bool RowTaken(IntPtr row, HashSet<int> allocated) =>
            (this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var id) && allocated.Contains(id)) ||
            (this.ShownRow(row) is var shown && shown != row && this.reader.TryRead<ushort>(shown + GameLayout.PassiveTree.RowGraphIdOffset, out var shownId) && allocated.Contains(shownId));

        /// <summary>The tree's links and node elements, read once per canvas (they persist while the tree is closed).</summary>
        private PassiveGraph? PassiveGraphOf(IntPtr canvasAddress, in UiElementReader.ElementInfo hoveredNode)
        {
            // Applying passive changes gives the canvas a new state object (the old one's allocated list goes stale:
            // a node allocated and applied read "reachable", 2026-10-24), so the cache holds while both still match.
            var state = this.TreeState(canvasAddress);
            if (this.passiveGraph is { } cached && cached.Canvas == canvasAddress && state == cached.State)
            {
                return cached;
            }

            if (state == IntPtr.Zero || !this.elements.TryReadElement(canvasAddress, out var canvas) ||
                this.TreeFields.GraphFile(state, false) is var graphFile && graphFile == IntPtr.Zero ||
                this.TreeFields.Edges(graphFile, this.elements.ReadChildren(canvas).Length) is not { } edgeVector ||
                !this.reader.TryRead<IntPtr>(hoveredNode.Address, out var nodeClass))
            {
                return null;
            }

            // Links in both directions: a record lists only its out-edges. For the stick's order, a record's in-edges
            // (file order) then its out-edges, as its own two edge vectors hold them.
            var links = new Dictionary<IntPtr, List<IntPtr>>();
            var inLinks = new Dictionary<IntPtr, List<IntPtr>>();
            var outLinks = new Dictionary<IntPtr, List<IntPtr>>();
            var edgeCount = (int)Math.Clamp((edgeVector.Last.ToInt64() - edgeVector.First.ToInt64()) / IntPtr.Size, 0, MaxPassiveEdges);
            foreach (var edge in this.reader.ReadArray<IntPtr>(edgeVector.First, edgeCount))
            {
                var ends = this.reader.ReadArray<IntPtr>(edge, 2);
                if (ends.Length < 2 || ends[0] == IntPtr.Zero || ends[1] == IntPtr.Zero || ends[0] == ends[1])
                {
                    continue;
                }

                Link(links, ends[0], ends[1]);
                Link(links, ends[1], ends[0]);
                Link(outLinks, ends[0], ends[1]);
                Link(inLinks, ends[1], ends[0]);
            }

            var stickLinks = links.Keys.ToDictionary(record => record, record => (inLinks.GetValueOrDefault(record) ?? new List<IntPtr>()).Concat(outLinks.GetValueOrDefault(record) ?? new List<IntPtr>()).ToList());

            // Node elements by record, for the screen positions of linked nodes.
            var elementsByRecord = new Dictionary<IntPtr, IntPtr>();
            var budget = MaxPassiveElements;
            foreach (var groupAddress in this.elements.ReadChildren(canvas))
            {
                if (!this.elements.TryReadElement(groupAddress, out var group))
                {
                    continue;
                }

                foreach (var element in this.elements.ReadChildren(group))
                {
                    if (budget-- <= 0)
                    {
                        break;
                    }

                    var head = this.reader.ReadArray<IntPtr>(element, 1);
                    if (head.Length == 1 && head[0] == nodeClass &&
                        this.NodeRecordAt(element, this.elements.Controls?.Current(ControlGroup.PassiveNode) ?? 0) is { } record)
                    {
                        elementsByRecord[record] = element;
                    }
                }
            }

            var graph = new PassiveGraph(canvasAddress, state, links, stickLinks, elementsByRecord, this.OwnStart(links.Keys));
            this.passiveGraph = graph;
            return graph;
        }

        private static void Link(Dictionary<IntPtr, List<IntPtr>> links, IntPtr from, IntPtr to)
        {
            if (!links.TryGetValue(from, out var list))
            {
                links[from] = list = new List<IntPtr>();
            }

            if (!list.Contains(to))
            {
                list.Add(to);
            }
        }

        /// <summary>The start node whose class list holds the player's own character (its Id is the player's path).</summary>
        private IntPtr OwnStart(IEnumerable<IntPtr> records)
        {
            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            if (path.Length == 0)
            {
                return IntPtr.Zero;
            }

            foreach (var record in records)
            {
                if (this.PassiveRow(record) is not { } row ||
                    !this.reader.TryRead<long>(row + GameLayout.PassiveTree.RowCharactersOffset, out var count) || count is <= 0 or > 16 ||
                    !this.reader.TryRead<IntPtr>(row + GameLayout.PassiveTree.RowCharactersOffset + sizeof(long), out var keys))
                {
                    continue;
                }

                for (var i = 0; i < count; i++)
                {
                    if (this.reader.TryRead<IntPtr>(keys + (i * GameLayout.PassiveTree.KeySize), out var character) &&
                        this.reader.TryRead<IntPtr>(character + GameLayout.PassiveTree.CharacterIdOffset, out var id) &&
                        string.Equals(this.elements.ReadUtf16BufferAt(id, MaxPassiveNameChars), path, StringComparison.OrdinalIgnoreCase))
                    {
                        return record;
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The localized classes of a start node's row ("Sombra o Monje"), only the player's when it is among them;
        ///     null when the list is unreadable.
        /// </summary>
        private string? StartClasses(IntPtr row)
        {
            if (!this.reader.TryRead<long>(row + GameLayout.PassiveTree.RowCharactersOffset, out var count) || count is <= 0 or > 16 ||
                !this.reader.TryRead<IntPtr>(row + GameLayout.PassiveTree.RowCharactersOffset + sizeof(long), out var keys))
            {
                return null;
            }

            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            var names = new List<string>();
            for (var i = 0; i < count; i++)
            {
                if (!this.reader.TryRead<IntPtr>(keys + (i * GameLayout.PassiveTree.KeySize), out var character) ||
                    !this.reader.TryRead<IntPtr>(character + GameLayout.PassiveTree.CharacterNameOffset, out var namePointer) ||
                    this.elements.ReadUtf16BufferAt(namePointer, MaxPassiveNameChars) is not { Length: > 0 } name)
                {
                    continue;
                }

                if (this.reader.TryRead<IntPtr>(character + GameLayout.PassiveTree.CharacterIdOffset, out var id) &&
                    string.Equals(this.elements.ReadUtf16BufferAt(id, MaxPassiveNameChars), path, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }

                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names.Count == 0 ? null : Announcements.Either(names);
        }

        /// <summary>The tree's graph: links both ways, the same in the stick step's edge order (<see cref="StickLinksOf"/>), the drawn element of each record.</summary>
        private sealed record PassiveGraph(IntPtr Canvas, IntPtr State, Dictionary<IntPtr, List<IntPtr>> Links, Dictionary<IntPtr, List<IntPtr>> StickLinks, Dictionary<IntPtr, IntPtr> Elements, IntPtr OwnStart);

        /// <summary>A push of the left stick in progress: when it began, its screen direction (y down) and whether a landing was judged on it.</summary>
        /// <summary>A left push; <paramref name="Judged"/> starts true for one from where the right stick left the cursor (not judged).</summary>
        private sealed record StickPush(long StartedAt, Vector2 Direction, bool Judged);

        /// <summary>The node hovered before the current one, as the stick rule saw from it: its index among the drawn nodes, its edge order, the window.</summary>
        private sealed record StickOrigin(IntPtr State, IntPtr Record, int Index, int[] Linked, PassiveStick.Window? Window);

        /// <summary>A node's row; <paramref name="NoLinkedStep"/> when the stick step skips its linked stage (<see cref="GameLayout.PassiveTree.RowNoLinkedStepOffset"/>).</summary>
        private sealed record PassiveNodeRow(IntPtr Record, int GraphId, string Name, string Kind, bool IconOnly, bool IsStart, PassiveClass Class, bool NoLinkedStep = false)
        {
            /// <summary>Gets the graph id of the row the class sees in this node's place; -1 when it sees this one.</summary>
            public int ShownGraphId { get; init; } = -1;

            /// <summary>Whether the node is in the allocated list, under its own graph id or its class override's.</summary>
            public bool TakenIn(HashSet<int> allocated) => allocated.Contains(this.GraphId) || (this.ShownGraphId >= 0 && allocated.Contains(this.ShownGraphId));
        }

        /// <summary>Drawn nodes' centres in the canvas's units, by record, with each one's links in the stick step's edge order.</summary>
        private sealed record PassivePlaces(IntPtr State, Vector2[] Positions, Dictionary<IntPtr, int> Index, int[][] Links)
        {
            /// <summary>The indexes of those records that are drawn.</summary>
            public int[] IndexesOf(IEnumerable<IntPtr>? records) =>
                records?.Where(this.Index.ContainsKey).Select(record => this.Index[record]).ToArray() ?? Array.Empty<int>();

            /// <summary>Every node's offset from the one at <paramref name="here"/> (itself at zero, never landed on).</summary>
            public Vector2[] OffsetsFrom(int here) => this.Positions.Select((position, index) => index == here ? Vector2.Zero : position - this.Positions[here]).ToArray();
        }
    }
}
