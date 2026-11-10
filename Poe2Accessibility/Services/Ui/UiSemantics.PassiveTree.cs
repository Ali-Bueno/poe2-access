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
        ///     footing (the node at the view's centre), and so does anything else that moves the view (the zoom, Y's centring):
        ///     the next push from there is not judged.
        /// </summary>
        public void ObserveStick(float x, float y, float rightX, float rightY, bool viewMoved, long now)
        {
            if (viewMoved || MathF.Max(MathF.Abs(rightX), MathF.Abs(rightY)) >= ReleaseDeflection)
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

            this.judgedPath = this.PlayerPath?.Invoke() ?? string.Empty;
            this.TreeFields.JudgeAllocated(graph.State, graph.Links.Keys);
            this.PlacesShowing(graph, record);
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
            if (tooltip.Count > 0 && (tooltip[0] == node.Name || PassiveTreeCatalog.Shared.IsNameOf(node.Id, tooltip[0], ShownName)))
            {
                tooltip.RemoveAt(0); // the title the name already said, in the game's language when the mod speaks another
            }

            tooltip.RemoveAll(line => line.StartsWith('(') && line.EndsWith(')')); // the English name under the title
            if (tooltip.Count > 0 && tooltip[^1] == this.UnallocatedLabel?.Invoke())
            {
                tooltip.RemoveAt(tooltip.Count - 1); // the state word says it
            }

            asides.RemoveAll(line => line == this.UnallocatedLabel?.Invoke()); // the same, should the game draw it red

            // The Build Planner's active build: the nodes the tree marks, with the author's notes when the file is here.
            var marked = this.BuildMarks(graph);
            // Marks that cannot be read say nothing of the build: the plan known from the game's settings stays.
            var plan = this.buildMarksUnreadable ? null : this.buildPlans.Matching(AskingCharacter(), marked.Select(this.PassiveIdOf).ToHashSet());
            if (plan != null)
            {
                CompatibilityReport.Current?.Found(BuildMarksPart); // a file's passives under the marks: the byte is the mark
            }
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
            yield return $"build marks {marked.Count}: {string.Join(' ', marked.Take(12))}; file: {this.buildPlans.Matching(AskingCharacter(), marked)?.Name ?? "none"}";
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
        private PassivePlaces PlacesOf(PassiveGraph graph, bool countNow = false)
        {
            var now = Environment.TickCount64;
            if (this.passivePlaces is { } cached && ReferenceEquals(cached.Graph, graph))
            {
                // The tree shows other nodes once an ascendancy is chosen or opened: places made before would give them none.
                if (!countNow && now < this.placesCountedAt + PlacesSliceMs)
                {
                    return cached;
                }

                this.placesCountedAt = now;
                if ((countNow ? this.ShownNodes(graph) : this.ShownNodesSlice(graph)) is not { } shown || shown == cached.ShownNodes)
                {
                    return cached;
                }
            }

            (this.placesCountedAt, this.shownOrder) = (now, null);
            var records = new List<IntPtr>();
            var positions = new List<Vector2>();
            var gated = new HashSet<IntPtr>();
            foreach (var (record, element) in graph.Elements)
            {
                if (this.elements.TryReadElement(element, out var info) && info.IsVisibleFlag &&
                    this.PassiveRow(record) is { } row && this.ReadPassiveRow(record, row) is { IconOnly: false } node &&
                    this.NodeCentre(graph, element) is { } centre)
                {
                    records.Add(record);
                    positions.Add(centre);
                    if (node.NoLinkedStep)
                    {
                        gated.Add(record);
                    }
                }
            }

            if (gated.Count * MaxGatedShare > records.Count)
            {
                // The key is null on the main tree's rows: set on this many, the bytes read are another column's, and are not believed.
                this.TreeFields.Log?.Invoke($"Passive stick: {gated.Count} of {records.Count} shown nodes read as skipping the linked step; the row's field is not where measured, every node keeps its links");
                gated.Clear();
            }

            var index = records.Select((record, at) => (record, at)).ToDictionary(pair => pair.record, pair => pair.at);
            var places = new PassivePlaces(graph.State, positions.ToArray(), index, Array.Empty<int[]>()) { Graph = graph, ShownNodes = this.ShownNodes(graph), Gated = gated };
            places = places with { Links = records.Select(record => gated.Contains(record) ? Array.Empty<int>() : places.IndexesOf(graph.StickLinks.GetValueOrDefault(record))).ToArray() };
            this.passivePlaces = places;
            // Both hold indexes into the places just replaced.
            this.passiveAtlas = null;
            this.stickOrigin = null;
            return places;
        }

        /// <summary>
        ///     While a node is read the shown nodes are counted a slice at a time, a slice this often: all 4,500 elements'
        ///     flags at once were 25 ms of one frame every two seconds (live, 2026-11-10). A count takes some 3 s this way.
        /// </summary>
        private const int PlacesSliceMs = 100;
        private const int PlacesSliceNodes = 256;

        /// <summary>A hovered node with no place asks for a whole count at once, this often at most.</summary>
        private const int PlacesForcedMs = 500;

        /// <summary>The count under way: the graph's elements in one order, how far it got and how many were shown so far.</summary>
        private IntPtr[]? shownOrder;
        private int shownAt;
        private int shownSoFar;

        /// <summary>The linked step's gate is an ascendancy's key: over a third of the shown nodes carrying it is a misread (docs/research/passive-tree.md).</summary>
        private const int MaxGatedShare = 3;

        private long placesCountedAt;
        private long placesForcedAt;

        /// <summary>How many node elements the tree shows now: what the places were made from.</summary>
        private int ShownNodes(PassiveGraph graph) => graph.Elements.Values.Count(this.IsShownNode);

        private bool IsShownNode(IntPtr element) =>
            this.reader.TryRead<GameOffsets.Objects.UiElement.UiElementBaseOffset>(element, out var raw) && GameOffsets.Objects.UiElement.UiElementBaseFuncs.IsVisibleChecker(raw.Flags);

        /// <summary>
        ///     The next slice of the count of shown node elements; the count once every element was looked at, null until
        ///     then. Nodes shown or hidden while a count is under way give a count that is off, which reads as a change
        ///     and makes the places again, as it should.
        /// </summary>
        private int? ShownNodesSlice(PassiveGraph graph)
        {
            if (this.shownOrder == null)
            {
                (this.shownOrder, this.shownAt, this.shownSoFar) = (graph.Elements.Values.ToArray(), 0, 0);
            }

            var end = Math.Min(this.shownAt + PlacesSliceNodes, this.shownOrder.Length);
            for (; this.shownAt < end; this.shownAt++)
            {
                this.shownSoFar += this.IsShownNode(this.shownOrder[this.shownAt]) ? 1 : 0;
            }

            if (this.shownAt < this.shownOrder.Length)
            {
                return null;
            }

            var shown = this.shownSoFar;
            (this.shownAt, this.shownSoFar) = (0, 0);
            return shown;
        }

        /// <summary>The hovered node is on screen: when the places hold none for it they are stale, and are counted again now.</summary>
        private void PlacesShowing(PassiveGraph graph, IntPtr hovered)
        {
            var now = Environment.TickCount64;
            if (!this.PlacesOf(graph).Index.ContainsKey(hovered) && now - this.placesForcedAt >= PlacesForcedMs)
            {
                this.placesForcedAt = now;
                this.PlacesOf(graph, countNow: true);
            }
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
        ///     read back whole (<see cref="RecordEdgeOrder"/>), else the same order rebuilt from the graph file's edge list. None
        ///     for a node whose row holds an ascendancy's key (<see cref="GameLayout.PassiveTree.RowNoLinkedStepOffset"/>).
        /// </summary>
        private List<IntPtr> StickLinksOf(PassiveGraph graph, IntPtr record) =>
            this.PlacesOf(graph).Gated.Contains(record) ? new List<IntPtr>() // the game skips the linked stage from such a node (0x9B9DE2)
            : this.RecordEdgeOrder(graph, record) ?? graph.StickLinks.GetValueOrDefault(record) ?? new List<IntPtr>();

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
        ///     when the way is longer than <paramref name="maxMoves"/> (<paramref name="far"/>) or a push lands nowhere nearer.
        /// </summary>
        private List<int>? PushesBetween(PassiveGraph graph, PassivePlaces places, IntPtr from, int here, int there, int maxMoves, out bool far)
        {
            far = false;
            if (!this.StickTrusted)
            {
                return null; // the side alone is said then
            }

            var window = graph.Elements.TryGetValue(from, out var element) ? this.StickWindow(graph, element) : null;
            var first = places.IndexesOf(this.StickLinksOf(graph, from));
            return PassiveStick.Route(places.Positions, here, there, maxMoves, index => index == here ? first : places.Links[index], window, out far);
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
                ? new StickOrigin(graph.State, record, here, places.IndexesOf(this.StickLinksOf(graph, record)), this.StickWindow(graph, element)) { Places = places }
                : null;
            if (origin == null || origin.State != graph.State || !ReferenceEquals(origin.Places, places) || origin.Record == record || origin.Window is not { } window ||
                this.stickPush is not { Judged: false } push || Environment.TickCount64 - push.StartedAt > StickStepMs || !places.Index.TryGetValue(record, out var landed))
            {
                return;
            }

            this.stickPush = push with { Judged = true };
            if (places.Gated.Contains(origin.Record))
            {
                return; // the game also shifts such a node by its sub-canvas (0x9A1470), which the rule does not model: not judged
            }

            if (this.focusLeftNode)
            {
                // The first push after a popup over the tree (the attribute's "+5 to Strength, 1 of 3", the weapon set's "Allocate"):
                // three of four landed elsewhere than predicted on 2026-11-10 and lost the rule; not judged, as after the right stick.
                this.focusLeftNode = false;
                return;
            }

            var offsets = places.OffsetsFrom(origin.Index);
            var predicted = PassiveStick.Pick(offsets, push.Direction, origin.Linked, window);
            if (predicted < 0 || !SteadyPick(offsets, push.Direction, origin.Linked, window, predicted))
            {
                // No judgement where the rule itself is unsure: a push it expects to move nothing becomes a pan when held, and a
                // push on the line between two nodes is the game's to sample, a frame apart from the mod's.
                return;
            }

            var verdict = this.StickCheck.Record(predicted == landed);
            if (verdict != StickVerdict.Same)
            {
                var pushed = $"last push {this.PassiveIdOf(origin.Record)} -> {this.PassiveIdOf(record)}, predicted " +
                           (predicted >= 0 ? this.PassiveIdOf(places.Index.First(pair => pair.Value == predicted).Key) : "no move");
                this.TreeFields.Log?.Invoke(verdict == StickVerdict.Lost
                    ? $"Stick rule lost: {this.StickCheck}; {pushed}; links are said by their side until it holds again"
                    : $"Stick rule holds again: {this.StickCheck}; {pushed}");
            }
        }

        /// <summary>True when the rule picks the same node a little to either side of the push: only such a push can be judged.</summary>
        private static bool SteadyPick(IReadOnlyList<Vector2> offsets, Vector2 direction, IReadOnlyList<int> linked, PassiveStick.Window window, int picked)
        {
            foreach (var turn in new[] { -JudgeSlackRadians, JudgeSlackRadians })
            {
                var (sin, cos) = MathF.SinCos(turn);
                var turned = new Vector2((direction.X * cos) - (direction.Y * sin), (direction.X * sin) + (direction.Y * cos));
                if (PassiveStick.Pick(offsets, turned, linked, window) != picked)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>How far the game's own sample of a push may lie from the mod's (ten degrees): a thumb is still turning when a push begins.</summary>
        private const float JudgeSlackRadians = MathF.PI / 18f;

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
            var now = Environment.TickCount64;
            if (this.TreeFields.AllocatedAt(state, GameLayout.PassiveTree.AllocatedOffset) is { } list)
            {
                this.lastAllocated = (state, now, list.ToHashSet());
                return this.lastAllocated.Ids;
            }

            // One failed read is not "nothing allocated": what was just read of this tree stands for a moment.
            return this.lastAllocated.State == state && now - this.lastAllocated.At < AllocatedKeptMs ? this.lastAllocated.Ids : new HashSet<int>();
        }

        private const int AllocatedKeptMs = 2000;
        private (IntPtr State, long At, HashSet<int> Ids) lastAllocated = (IntPtr.Zero, 0, new HashSet<int>());

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

        /// <summary>A row's graph id: the game files' by the row's Id, else the row's own column (a node newer than the catalog).</summary>
        private int? RowGraphId(IntPtr row) =>
            this.RowId(row) is { } id && PassiveTreeCatalog.Shared.ById(id) is { } node ? node.GraphId
            : this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var read) ? read : null;

        /// <summary>
        ///     A node record's graph id and that of the row its class sees in its place (null when it sees its own), for the
        ///     allocated list's check. Called off the frame thread: it takes the player's path as last noted there and no cache.
        /// </summary>
        private (int? Own, int? Shown) GraphIdsOf(IntPtr record)
        {
            if (this.PassiveRow(record) is not { } row)
            {
                return (null, null);
            }

            var own = this.RowGraphId(row);
            var path = this.judgedPath;
            if (this.Overrides is not { Count: > 0 } overrides || path.Length == 0 || this.RowId(row) is not { } id || overrides.Find(path, id) is not { } swap)
            {
                return (own, null);
            }

            var other = row + (nint)overrides.Step(swap);
            return (own, this.RowId(other) == swap.ShownId && this.reader.TryRead<ushort>(other + GameLayout.PassiveTree.RowGraphIdOffset, out var shown) ? shown : null);
        }

        /// <summary>The player's path when the allocated list was last judged, for <see cref="GraphIdsOf"/>'s worker thread.</summary>
        private volatile string judgedPath = string.Empty;

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
        ///     A node as the game shows it. What a node is cannot change while the game runs, so its name, kind, graph id and
        ///     class start come from the game's files by the row's Id (<see cref="PassiveTreeCatalog"/>), the row's columns
        ///     backing them up and checked against them. The row is read as before for a node the files lack (one a patch
        ///     added) and for the row the player's class sees in its place (<see cref="ShownRow"/>), which gives name and kind.
        /// </summary>
        private PassiveNodeRow ReadPassiveRow(IntPtr record, IntPtr row)
        {
            var shownRow = this.ShownRow(row);
            var catalog = PassiveTreeCatalog.Shared;
            var known = this.RowId(row) is { } id ? catalog.ById(id) : null;
            if (known != null && shownRow == row)
            {
                var named = KeywordLink.Replace(catalog.Name(known.Id, Localization.Language) ?? string.Empty, ShownText);
                var gated = this.reader.TryRead<long>(row + GameLayout.PassiveTree.RowNoLinkedStepOffset, out var gate) && gate != 0;
                this.CompareWithRow(known, row);
                return new PassiveNodeRow(record, known.GraphId, named, Announcements.PassiveKind(known.Class), known.IconOnly, known.IsStart, known.Class, gated) { Id = known.Id };
            }

            var bytes = this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes);
            if (bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false, PassiveClass.Other);
            }

            var shown = shownRow == row ? bytes : this.elements.ReadBlock(shownRow, GameLayout.PassiveTree.RowBytes);
            if (shown.Length < GameLayout.PassiveTree.RowBytes)
            {
                shown = bytes;
            }

            var name = KeywordLink.Replace(this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(shown, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty, ShownText);
            var isStart = known?.IsStart ?? BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0;
            var iconOnly = shown[GameLayout.PassiveTree.RowIconOnlyOffset] == 1;
            var kind = PassiveTreeCatalog.ClassOf(
                shown[GameLayout.PassiveTree.RowKeystoneOffset] == 1, shown[GameLayout.PassiveTree.RowNotableOffset] == 1,
                shown[GameLayout.PassiveTree.RowJewelSocketOffset] == 1, iconOnly, isStart);
            var noLinkedStep = BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNoLinkedStepOffset) != 0;
            return new PassiveNodeRow(record, known?.GraphId ?? BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset), name, Announcements.PassiveKind(kind), iconOnly, isStart, kind, noLinkedStep)
            {
                ShownGraphId = BitConverter.ToUInt16(shown, GameLayout.PassiveTree.RowGraphIdOffset),
                Id = this.RowId(shownRow) ?? string.Empty,
            };
        }

        /// <summary>A catalog name as the tooltip's title shows it: keyword links as their text.</summary>
        private static string ShownName(string name) => KeywordLink.Replace(name, ShownText);

        /// <summary>
        ///     The cross-check of the two sources, on the first nodes read: the row's own columns must say what the files say
        ///     of the node (its name in whichever language the game runs in, its flags, graph id and class start).
        /// </summary>
        private void CompareWithRow(PassiveTreeCatalog.Node known, IntPtr row)
        {
            if (!this.TreeFields.WantsNode(known.Id) || this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes) is not { } bytes || bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return; // a row not readable now compares nothing
            }

            var differences = new List<string>();
            var name = this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty;
            if (!PassiveTreeCatalog.Shared.IsNameOf(known.Id, name))
            {
                differences.Add($"the row's name reads '{name}'");
            }

            if ((bytes[GameLayout.PassiveTree.RowKeystoneOffset] == 1) != known.Keystone || (bytes[GameLayout.PassiveTree.RowNotableOffset] == 1) != known.Notable ||
                (bytes[GameLayout.PassiveTree.RowJewelSocketOffset] == 1) != known.JewelSocket || (bytes[GameLayout.PassiveTree.RowIconOnlyOffset] == 1) != known.IconOnly)
            {
                differences.Add("the row's kind flags differ");
            }

            if (BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset) != known.GraphId)
            {
                differences.Add($"the row's graph id reads {BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset)}, the files' {known.GraphId}");
            }

            if ((BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0) != known.IsStart)
            {
                differences.Add("the row's class list differs");
            }

            this.TreeFields.NoteNode(known.Id, differences.Count == 0 ? null : string.Join(", ", differences));
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
            (this.RowGraphId(row) is { } id && allocated.Contains(id)) ||
            (this.ShownRow(row) is var shown && shown != row && this.reader.TryRead<ushort>(shown + GameLayout.PassiveTree.RowGraphIdOffset, out var shownId) && allocated.Contains(shownId));

        /// <summary>
        ///     The tree's links and node elements, read once per canvas (they persist while the tree is closed). Which nodes
        ///     link is the game files' to say (<see cref="PassiveTreeCatalog"/>); the graph's edge list in memory backs them
        ///     up, is checked against them, and gives the order the stick step walks a node's links in.
        /// </summary>
        private PassiveGraph? PassiveGraphOf(IntPtr canvasAddress, in UiElementReader.ElementInfo hoveredNode)
        {
            // Applying passive changes gives the canvas a new state object (the old one's allocated list goes stale:
            // a node allocated and applied read "reachable", 2026-10-24), so the cache holds while both still match.
            var state = this.TreeState(canvasAddress);
            var catalog = PassiveTreeCatalog.Shared;
            if (this.passiveGraph is { } cached && cached.Canvas == canvasAddress && state == cached.State && !this.EdgesReadNow(cached, canvasAddress))
            {
                if (cached.OwnStart == IntPtr.Zero && this.OwnStart(cached.Links.Keys, cached.Nodes) is var start && start != IntPtr.Zero)
                {
                    // The character was not readable when the tree was first read: without its start nothing reads as reachable.
                    this.passiveGraph = cached = cached with { OwnStart = start };
                    this.passiveAtlas = null;
                }

                return cached;
            }

            if (state == IntPtr.Zero || !this.elements.TryReadElement(canvasAddress, out var canvas) ||
                this.TreeFields.GraphFile(state, false) is var graphFile && graphFile == IntPtr.Zero ||
                !this.reader.TryRead<IntPtr>(hoveredNode.Address, out var nodeClass))
            {
                return null;
            }

            var edgeList = this.TreeFields.Edges(graphFile, this.elements.ReadChildren(canvas).Length, catalog.Count > 0);
            if (edgeList == null && catalog.Count == 0)
            {
                return null;
            }

            // Links in both directions: a record lists only its out-edges. For the stick's order, a record's in-edges
            // (file order) then its out-edges, as its own two edge vectors hold them.
            var links = new Dictionary<IntPtr, List<IntPtr>>();
            var inLinks = new Dictionary<IntPtr, List<IntPtr>>();
            var outLinks = new Dictionary<IntPtr, List<IntPtr>>();
            var edgeVector = edgeList ?? default;
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

            var nodes = this.FileNodes(catalog, elementsByRecord.Keys.Concat(links.Keys));
            if (edgeList == null && nodes.Count == 0)
            {
                return null; // neither source gives a link yet (the rows not readable): tried again on the next node
            }

            links = nodes.Count > 0 ? this.LinksFromFiles(nodes, links) : links;
            // Without the edge list the files' order stands in, which is not the game's: each node's own lists give that (StickLinksOf).
            var stickLinks = links.ToDictionary(pair => pair.Key, pair => inLinks.ContainsKey(pair.Key) || outLinks.ContainsKey(pair.Key)
                ? (inLinks.GetValueOrDefault(pair.Key) ?? new List<IntPtr>()).Concat(outLinks.GetValueOrDefault(pair.Key) ?? new List<IntPtr>()).ToList()
                : pair.Value);
            var graph = new PassiveGraph(canvasAddress, state, links, stickLinks, elementsByRecord, this.OwnStart(links.Keys, nodes)) { Nodes = nodes, EdgesFromMemory = edgeList != null };
            this.passiveGraph = graph;
            return graph;
        }

        /// <summary>A graph made from the files alone looks for the edge list again this often: its order serves the stick step.</summary>
        private const int EdgesAgainMs = 10_000;

        private long edgesAgainAt;

        /// <summary>Whether a graph made without the edge list can now be made with it.</summary>
        private bool EdgesReadNow(PassiveGraph graph, IntPtr canvasAddress)
        {
            var now = Environment.TickCount64;
            if (graph.EdgesFromMemory || now < this.edgesAgainAt)
            {
                return false;
            }

            this.edgesAgainAt = now + EdgesAgainMs;
            return this.elements.TryReadElement(canvasAddress, out var canvas) && this.TreeFields.GraphFile(graph.State, false) is var file && file != IntPtr.Zero &&
                   this.TreeFields.Edges(file, this.elements.ReadChildren(canvas).Length, true) != null;
        }

        /// <summary>The files' node of each record whose row's Id they know; empty without the catalog.</summary>
        private Dictionary<IntPtr, PassiveTreeCatalog.Node> FileNodes(PassiveTreeCatalog catalog, IEnumerable<IntPtr> records)
        {
            var nodes = new Dictionary<IntPtr, PassiveTreeCatalog.Node>();
            if (catalog.Count == 0)
            {
                return nodes;
            }

            foreach (var record in records)
            {
                if (!nodes.ContainsKey(record) && this.PassiveRow(record) is { } row && this.RowId(row) is { } id && catalog.ById(id) is { } node)
                {
                    nodes[record] = node;
                }
            }

            return nodes;
        }

        /// <summary>
        ///     Each record's links as the game's files give them, set beside the edge list's (<paramref name="fromMemory"/>,
        ///     empty when it did not read). What the files cannot know stays as memory has it: a node newer than the catalog,
        ///     a class start's root record, which has no row.
        /// </summary>
        private Dictionary<IntPtr, List<IntPtr>> LinksFromFiles(Dictionary<IntPtr, PassiveTreeCatalog.Node> nodes, Dictionary<IntPtr, List<IntPtr>> fromMemory)
        {
            var recordOf = new Dictionary<int, IntPtr>();
            foreach (var (record, node) in nodes)
            {
                recordOf.TryAdd(node.GraphId, record);
            }

            var links = new Dictionary<IntPtr, List<IntPtr>>();
            var (compared, differing, first) = (0, 0, string.Empty);
            foreach (var (record, node) in nodes)
            {
                var linked = node.Links.Where(recordOf.ContainsKey).Select(id => recordOf[id]).Where(other => other != record).ToList();
                if (fromMemory.TryGetValue(record, out var read))
                {
                    compared++;
                    var known = read.Where(nodes.ContainsKey).ToHashSet();
                    if (!known.SetEquals(linked))
                    {
                        differing++;
                        first = first.Length > 0 ? first : $"{node.Id}: {known.Count} links in memory, {linked.Count} in the files";
                    }

                    linked.AddRange(read.Where(other => !nodes.ContainsKey(other)));
                }

                links[record] = linked;
            }

            foreach (var (record, read) in fromMemory)
            {
                links.TryAdd(record, read);
            }

            this.TreeFields.NoteLinks(compared, differing, first);
            return links;
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

        /// <summary>
        ///     The start node of the player's own character (its Id is the player's path): the one the game's files give the
        ///     class, else the one whose row's class list holds it.
        /// </summary>
        private IntPtr OwnStart(IEnumerable<IntPtr> records, IReadOnlyDictionary<IntPtr, PassiveTreeCatalog.Node> nodes)
        {
            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            if (path.Length == 0)
            {
                return IntPtr.Zero;
            }

            if (PassiveTreeCatalog.Shared.StartOf(path) is { } start && nodes.FirstOrDefault(pair => pair.Value.GraphId == start.GraphId).Key is var known && known != IntPtr.Zero)
            {
                return known;
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
            var path = this.PlayerPath?.Invoke() ?? string.Empty;
            var catalog = PassiveTreeCatalog.Shared;
            if (this.RowId(row) is { } rowId && catalog.ById(rowId) is { IsStart: true } start)
            {
                // The classes a node starts are the files' to say; the row's list stands in when they name none of them.
                var own = start.StartClasses.FirstOrDefault(character => string.Equals(character, path, StringComparison.OrdinalIgnoreCase));
                var named = (own != null ? new[] { own } : start.StartClasses).Select(character => catalog.ClassName(character, Localization.Language)).OfType<string>().Distinct().ToList();
                if (named.Count > 0)
                {
                    return Announcements.Either(named);
                }
            }

            if (!this.reader.TryRead<long>(row + GameLayout.PassiveTree.RowCharactersOffset, out var count) || count is <= 0 or > 16 ||
                !this.reader.TryRead<IntPtr>(row + GameLayout.PassiveTree.RowCharactersOffset + sizeof(long), out var keys))
            {
                return null;
            }

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
        private sealed record PassiveGraph(IntPtr Canvas, IntPtr State, Dictionary<IntPtr, List<IntPtr>> Links, Dictionary<IntPtr, List<IntPtr>> StickLinks, Dictionary<IntPtr, IntPtr> Elements, IntPtr OwnStart)
        {
            /// <summary>Gets the game files' node of each record they know (<see cref="PassiveTreeCatalog"/>); empty without the catalog.</summary>
            public IReadOnlyDictionary<IntPtr, PassiveTreeCatalog.Node> Nodes { get; init; } = new Dictionary<IntPtr, PassiveTreeCatalog.Node>();

            /// <summary>Gets a value indicating whether the graph's edge list read, so <see cref="StickLinks"/> hold the game's order.</summary>
            public bool EdgesFromMemory { get; init; } = true;
        }

        /// <summary>A push of the left stick in progress: when it began, its screen direction (y down) and whether a landing was judged on it.</summary>
        /// <summary>A left push; <paramref name="Judged"/> starts true for one from where the right stick left the cursor (not judged).</summary>
        private sealed record StickPush(long StartedAt, Vector2 Direction, bool Judged);

        /// <summary>The node hovered before the current one, as the stick rule saw from it: its index among the drawn nodes, its edge order, the window.</summary>
        private sealed record StickOrigin(IntPtr State, IntPtr Record, int Index, int[] Linked, PassiveStick.Window? Window)
        {
            /// <summary>Gets the places <see cref="Index"/> counts in: made again, the origin judges nothing.</summary>
            public PassivePlaces? Places { get; init; }
        }

        /// <summary>A node's row; <paramref name="NoLinkedStep"/> when the stick step skips its linked stage (<see cref="GameLayout.PassiveTree.RowNoLinkedStepOffset"/>).</summary>
        private sealed record PassiveNodeRow(IntPtr Record, int GraphId, string Name, string Kind, bool IconOnly, bool IsStart, PassiveClass Class, bool NoLinkedStep = false)
        {
            /// <summary>Gets the graph id of the row the class sees in this node's place; -1 when it sees this one.</summary>
            public int ShownGraphId { get; init; } = -1;

            /// <summary>Gets the PassiveSkills Id of the row shown; empty when unread.</summary>
            public string Id { get; init; } = string.Empty;

            /// <summary>Whether the node is in the allocated list, under its own graph id or its class override's.</summary>
            public bool TakenIn(HashSet<int> allocated) => allocated.Contains(this.GraphId) || (this.ShownGraphId >= 0 && allocated.Contains(this.ShownGraphId));
        }

        /// <summary>Drawn nodes' centres in the canvas's units, by record, with each one's links in the stick step's edge order.</summary>
        private sealed record PassivePlaces(IntPtr State, Vector2[] Positions, Dictionary<IntPtr, int> Index, int[][] Links)
        {
            /// <summary>Gets the graph the places were made from.</summary>
            public PassiveGraph? Graph { get; init; }

            /// <summary>Gets how many node elements the tree showed when they were made: another count makes them again.</summary>
            public int ShownNodes { get; init; }

            /// <summary>Gets the drawn nodes the game's stick step skips its linked stage from (<see cref="GameLayout.PassiveTree.RowNoLinkedStepOffset"/>).</summary>
            public HashSet<IntPtr> Gated { get; init; } = new();

            /// <summary>The indexes of those records that are drawn.</summary>
            public int[] IndexesOf(IEnumerable<IntPtr>? records) =>
                records?.Where(this.Index.ContainsKey).Select(record => this.Index[record]).ToArray() ?? Array.Empty<int>();

            /// <summary>Every node's offset from the one at <paramref name="here"/> (itself at zero, never landed on).</summary>
            public Vector2[] OffsetsFrom(int here) => this.Positions.Select((position, index) => index == here ? Vector2.Zero : position - this.Positions[here]).ToArray();
        }
    }
}
