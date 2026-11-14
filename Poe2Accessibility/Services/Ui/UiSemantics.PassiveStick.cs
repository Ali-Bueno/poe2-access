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

    /// <summary>The passive tree's node places and the game's stick rule: where the nodes stand and which push of the stick reaches each linked one.</summary>
    public sealed partial class UiSemantics
    {
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

            if (!graph.AscendancyOnly && gated.Count * MaxGatedShare > records.Count)
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

        /// <summary>The linked step's gate is an ascendancy's key: but on an ascendancy's tree alone, over a third of the shown nodes carrying it is a misread (docs/research/passive-tree.md).</summary>
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
    }
}
