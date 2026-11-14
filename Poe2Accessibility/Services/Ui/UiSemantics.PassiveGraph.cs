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

    /// <summary>The passive tree's graph: the links and node elements read once per canvas, from the game files and the edge list, and its records.</summary>
    public sealed partial class UiSemantics
    {
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
            if (this.passiveGraph is { } cached && cached.Canvas == canvasAddress && state == cached.State && !this.CanvasChanged(cached) && !this.EdgesReadNow(cached, canvasAddress))
            {
                if (cached.OwnStart == IntPtr.Zero && this.OwnStartDue())
                {
                    // The character was not readable when the tree was first read: without its start nothing reads as reachable.
                    if (this.OwnStart(cached.Links.Keys, cached.Nodes) is var start && start != IntPtr.Zero)
                    {
                        this.passiveGraph = cached = cached with { OwnStart = start };
                        this.passiveAtlas = null;
                    }

                    this.NoteOwnStart(cached);
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

            // Node elements by record, for the screen positions of linked nodes. An ascendancy's tree is a canvas of its
            // own among the groups (docs/research/passive-tree.md#the-ascendancys-tree): its nodes are taken last, over
            // the hidden ones the class's canvas keeps for the same records.
            var elementsByRecord = new Dictionary<IntPtr, IntPtr>();
            var budget = MaxPassiveElements;
            var nodeShift = this.elements.Controls?.Current(ControlGroup.PassiveNode) ?? 0;
            var canvasShift = this.elements.Controls?.Current(ControlGroup.PassiveCanvas) ?? 0;
            var nodeClasses = new HashSet<IntPtr> { nodeClass };
            var otherClasses = new HashSet<IntPtr>();
            var (inner, innerState) = (IntPtr.Zero, IntPtr.Zero);
            var innerRecords = new List<IntPtr>();
            var children = new DroppedChildren();
            void TakeNodes(in UiElementReader.ElementInfo holder, List<IntPtr>? taken)
            {
                foreach (var element in this.elements.ReadChildren(holder))
                {
                    if (budget-- <= 0)
                    {
                        children.OverBudget = true;
                        break;
                    }

                    children.Seen++;
                    var head = this.reader.ReadArray<IntPtr>(element, 1);
                    if (head.Length != 1)
                    {
                        children.Unreadable++;
                        continue;
                    }

                    if (otherClasses.Contains(head[0]))
                    {
                        children.OtherClass++;
                        continue;
                    }

                    if (this.NodeRecordAt(element, nodeShift) is not { } record)
                    {
                        children.NoRecord++;
                        continue;
                    }

                    // The two canvases' nodes need not be of one class: a class whose element leads to a passive's row is a node's.
                    if (!nodeClasses.Contains(head[0]))
                    {
                        (this.PassiveRow(record) != null ? nodeClasses : otherClasses).Add(head[0]);
                    }

                    if (nodeClasses.Contains(head[0]))
                    {
                        elementsByRecord[record] = element;
                        taken?.Add(record);
                    }
                    else
                    {
                        children.NotPassive++;
                    }
                }
            }

            var groups = this.elements.ReadChildren(canvas);
            foreach (var groupAddress in groups)
            {
                if (!this.elements.TryReadElement(groupAddress, out var group))
                {
                    continue;
                }

                if (inner == IntPtr.Zero && group.IsVisibleFlag && this.TreeStateAt(groupAddress, canvasShift) is var nested && nested != IntPtr.Zero)
                {
                    (inner, innerState) = (groupAddress, nested);
                    continue;
                }

                TakeNodes(group, null);
            }

            if (inner != IntPtr.Zero && this.elements.TryReadElement(inner, out var innerCanvas))
            {
                foreach (var holderAddress in this.elements.ReadChildren(innerCanvas))
                {
                    if (this.elements.TryReadElement(holderAddress, out var holder))
                    {
                        TakeNodes(holder, innerRecords);
                    }
                }
            }

            this.TreeFields.Log?.Invoke(children.Describe(otherClasses.Count));
            var nodes = this.FileNodes(catalog, elementsByRecord.Keys.Concat(links.Keys));
            if (edgeList == null && nodes.Count == 0)
            {
                return null; // neither source gives a link yet (the rows not readable): tried again on the next node
            }

            // An ascendancy's own start: of the tree drawn inside the class's, or of a tree that is an ascendancy's alone (the altar's preview).
            var ascendancyOnly = inner == IntPtr.Zero && elementsByRecord.Count > 0 && elementsByRecord.Keys.All(record => BuildPlan.IsAscendancyNode(this.PassiveIdOf(record)));
            var ascendancyStart = (inner != IntPtr.Zero ? innerRecords : ascendancyOnly ? elementsByRecord.Keys : Enumerable.Empty<IntPtr>())
                .FirstOrDefault(record => BuildPlan.IsAscendancyStart(this.PassiveIdOf(record)));

            links = nodes.Count > 0 ? this.LinksFromFiles(nodes, links) : links;
            // Without the edge list the files' order stands in, which is not the game's: each node's own lists give that (StickLinksOf).
            var stickLinks = links.ToDictionary(pair => pair.Key, pair => inLinks.ContainsKey(pair.Key) || outLinks.ContainsKey(pair.Key)
                ? (inLinks.GetValueOrDefault(pair.Key) ?? new List<IntPtr>()).Concat(outLinks.GetValueOrDefault(pair.Key) ?? new List<IntPtr>()).ToList()
                : pair.Value);
            var graph = new PassiveGraph(canvasAddress, state, links, stickLinks, elementsByRecord, this.OwnStart(links.Keys, nodes))
            {
                Nodes = nodes,
                EdgesFromMemory = edgeList != null,
                Groups = groups.Length,
                Inner = inner,
                InnerState = innerState,
                AscendancyStart = ascendancyStart,
                AscendancyOnly = ascendancyOnly,
            };
            this.passiveGraph = graph;
            this.NoteOwnStart(graph);
            return graph;
        }

        /// <summary>
        ///     The group children a tree's read left out, by why: counted per read of the canvas and logged, so an element of a new
        ///     kind in a rebuilt tree shows in a user's log (<see cref="PassiveGraphOf"/>).
        /// </summary>
        private sealed class DroppedChildren
        {
            public int Seen { get; set; }

            public int Unreadable { get; set; }

            public int OtherClass { get; set; }

            public int NoRecord { get; set; }

            public int NotPassive { get; set; }

            public bool OverBudget { get; set; }

            public string Describe(int otherClasses) =>
                $"Passive tree: {this.Unreadable + this.OtherClass + this.NoRecord + this.NotPassive} of {this.Seen} group children read are no node " +
                $"({this.NoRecord} with no node record, {this.NotPassive} whose record has no PassiveSkills row, {this.OtherClass} of {otherClasses} other element classes, {this.Unreadable} unreadable" +
                $"{(this.OverBudget ? "; the read stopped at its limit" : string.Empty)})";
        }

        /// <summary>The own start is looked for with the tree open and the character readable: a tree that is an ascendancy's alone or a class's preview has none to find.</summary>
        private void NoteOwnStart(PassiveGraph graph)
        {
            if ((this.PlayerPath?.Invoke() ?? string.Empty).Length > 0 && !graph.AscendancyOnly && !this.IsClassPreview(graph))
            {
                this.TreeFields.NoteOwnStart(graph.OwnStart != IntPtr.Zero);
            }
        }

        /// <summary>
        ///     The tree's canvas for the canvas a node hangs from: an ascendancy's tree is drawn inside the class's, and the
        ///     two are one tree to the stick, the build and the finder.
        /// </summary>
        private IntPtr TreeCanvasOf(IntPtr nodeCanvas) =>
            this.elements.TryReadElement(nodeCanvas, out var canvas) &&
            this.TreeStateAt(canvas.Raw.ParentPtr, this.elements.Controls?.Current(ControlGroup.PassiveCanvas) ?? 0) != IntPtr.Zero
                ? canvas.Raw.ParentPtr
                : nodeCanvas;

        /// <summary>A graph is looked at for a change of its canvas this often (an ascendancy taken: its tree is one more group).</summary>
        private const int CanvasAgainMs = 1000;

        private long canvasAgainAt;

        /// <summary>Whether the canvas holds other groups than the graph was read from, or its ascendancy's tree another state.</summary>
        private bool CanvasChanged(PassiveGraph graph)
        {
            var now = Environment.TickCount64;
            if (now < this.canvasAgainAt)
            {
                return false;
            }

            this.canvasAgainAt = now + CanvasAgainMs;
            return (this.elements.TryReadElement(graph.Canvas, out var canvas) && canvas.Raw.ChildrensPtr.TotalElements(IntPtr.Size) != graph.Groups) ||
                   (graph.Inner != IntPtr.Zero && this.TreeStateAt(graph.Inner, this.elements.Controls?.Current(ControlGroup.PassiveCanvas) ?? 0) != graph.InnerState);
        }

        /// <summary>
        ///     A start that was not found for a readable character is looked for again this often: the search reads every
        ///     node's row, and a class no node starts would cost that on each reading of the hovered node.
        /// </summary>
        private const int OwnStartAgainMs = 1000;

        private long ownStartAgainAt;

        /// <summary>Whether the class's start is worth looking for now: the character reads, and the last search was a moment ago.</summary>
        private bool OwnStartDue()
        {
            var now = Environment.TickCount64;
            if ((this.PlayerPath?.Invoke() ?? string.Empty).Length == 0 || now < this.ownStartAgainAt)
            {
                return false; // no character yet (nothing to find, nothing spent), or just searched
            }

            this.ownStartAgainAt = now + OwnStartAgainMs;
            return true;
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

            var (seen, drawn, unknown) = (new HashSet<IntPtr>(), 0, 0);
            foreach (var record in records)
            {
                if (!seen.Add(record) || this.PassiveRow(record) is not { } row || this.RowId(row) is not { } id)
                {
                    continue;
                }

                drawn++;
                if (catalog.ById(id) is { } node)
                {
                    nodes[record] = node;
                }
                else
                {
                    unknown++;
                }
            }

            this.TreeFields.NoteCoverage(drawn, unknown);
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

            // A catalog of another tree disagrees on most nodes: then the game's own list stands for all of them.
            var memoryDecides = PassiveTreeVerdicts.LinksDisagree(compared, differing);
            this.TreeFields.NoteLinks(compared, differing, first, memoryDecides);
            return memoryDecides ? fromMemory : links;
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

            /// <summary>Gets how many groups the canvas held when the graph was read.</summary>
            public int Groups { get; init; }

            /// <summary>Gets the ascendancy's tree drawn inside this one (a canvas among the groups); zero when none.</summary>
            public IntPtr Inner { get; init; }

            /// <summary>Gets the tree state of <see cref="Inner"/>.</summary>
            public IntPtr InnerState { get; init; }

            /// <summary>Gets the drawn start of the character's ascendancy, its own by ascending and in no allocated list; zero when none.</summary>
            public IntPtr AscendancyStart { get; init; }

            /// <summary>Gets a value indicating whether the tree is an ascendancy's alone (the Altar of Ascendancy's preview).</summary>
            public bool AscendancyOnly { get; init; }

            /// <summary>Whether a record is a start the character owns: its class's, or its ascendancy's.</summary>
            public bool IsOwnStart(IntPtr record) => record != IntPtr.Zero && (record == this.OwnStart || record == this.AscendancyStart);
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

            /// <summary>Gets a value indicating whether the node holds a jewel, whatever else its kind says (a notable can).</summary>
            public bool JewelSocket { get; init; }

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
