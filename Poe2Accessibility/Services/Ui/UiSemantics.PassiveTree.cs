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
                var buildLines = plan != null && marked.Contains(record) ? this.BuildLines(plan, record, marked).ToList() : new List<string>();
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
    }
}
