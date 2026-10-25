namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameHelper.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Passive tree nodes for <see cref="UiSemantics"/> (D2R skill tree readout, adapted): the hovered node's name,
    ///     kind and state from its dat row and the tree's allocated list, its stats from the tooltip, then the nodes it
    ///     links to with their direction on screen — the free cursor moves by distance, not along links
    ///     (docs/research/passive-tree.md).
    /// </summary>
    public sealed partial class UiSemantics
    {
        private const int MaxPassiveNameChars = 96;

        /// <summary>Upper bounds of the graph read once per tree: edges, groups, nodes.</summary>
        private const int MaxPassiveEdges = 20000;

        private const int MaxPassiveElements = 20000;

        /// <summary>The tree's canvas holds dozens of node groups; an element with fewer children is never searched as one.</summary>
        private const int MinCanvasGroups = 10;

        /// <summary>The longest "Unallocated" line a tooltip ends with; the state word replaces it.</summary>
        private const int MaxStateLineChars = 24;

        private PassiveGraph? passiveGraph;

        /// <summary>The player's metadata path, to find the class's own start node.</summary>
        public Func<string>? PlayerPath { get; set; }

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
            var allocated = this.AllocatedIds(graph.State);
            var node = this.ReadPassiveRow(record, row);
            var state = this.StateOf(graph, node, allocated);
            var tooltip = this.TooltipLines(info);
            if (tooltip.Count > 0 && tooltip[0] == node.Name)
            {
                tooltip.RemoveAt(0);
            }

            tooltip.RemoveAll(line => line.StartsWith('(') && line.EndsWith(')')); // the English name under the title
            if (state != PassiveState.Allocated && tooltip.Count > 0 && tooltip[^1].Length <= MaxStateLineChars && !tooltip[^1].Any(char.IsDigit))
            {
                tooltip.RemoveAt(tooltip.Count - 1); // the game's "Unallocated": the state word says it
            }

            var links = this.DescribeLinks(graph, info.Address, record, allocated);
            if (links.Length > 0)
            {
                tooltip.Add(links);
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Panel,
                Text = Announcements.PassiveNode(node.Name, node.Kind, StateWord(state)),
                IsVisible = this.elements.IsVisible(info),
                IsHovered = hovered,
                Context = this.PanelTitle(info),
                Tooltip = tooltip,
            };
        }

        /// <summary>"Up: Armour and Evasion, allocated; down left: Attribute, reachable", clockwise from up.</summary>
        private string DescribeLinks(PassiveGraph graph, IntPtr element, IntPtr record, HashSet<int> allocated)
        {
            if (!graph.Links.TryGetValue(record, out var linked) ||
                !PluginUiElementReflection.TryGetAbsoluteRect(element, out var position, out var size))
            {
                return string.Empty;
            }

            var here = position + (size / 2);
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

                var words = Announcements.PassiveLink(node.Name, StateWord(this.StateOf(graph, node, allocated)));
                if (!graph.Elements.TryGetValue(other, out var otherElement) ||
                    !PluginUiElementReflection.TryGetAbsoluteRect(otherElement, out var otherPosition, out var otherSize))
                {
                    parts.Add((double.MaxValue - 1, words)); // not drawn: no side to give
                    continue;
                }

                // Screen y grows downwards; StickDirections count counter-clockwise from right. Clockwise from up.
                var offset = otherPosition + (otherSize / 2) - here;
                var angle = Math.Atan2(-offset.Y, offset.X) * 180 / Math.PI;
                var octant = (int)(((Math.Round(angle / 45) % 8) + 8) % 8);
                var clockwiseFromUp = (((112.5 - angle) % 360) + 360) % 360; // from the up sector's left edge, so sectors keep their order
                parts.Add((clockwiseFromUp, $"{Announcements.StickDirections[octant]}, {words}"));
            }

            return parts.Count == 0 ? string.Empty : Announcements.PassiveLinks(parts.OrderBy(part => part.Order).Select(part => part.Text));
        }

        private PassiveState StateOf(PassiveGraph graph, PassiveNodeRow node, HashSet<int> allocated)
        {
            if (allocated.Contains(node.GraphId))
            {
                return PassiveState.Allocated;
            }

            // Reachable: linked to an allocated node, or to the class's own start.
            return graph.Links.TryGetValue(node.Record, out var linked) &&
                   linked.Any(other => other == graph.OwnStart || (this.PassiveRow(other) is { } row && this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var id) && allocated.Contains(id)))
                ? PassiveState.Reachable
                : PassiveState.Locked;
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

        private IntPtr TreeStateAt(IntPtr canvas, int shift) =>
            this.reader.TryRead<IntPtr>(canvas + GameLayout.PassiveTree.CanvasStateOffset + shift, out var state) && state != IntPtr.Zero &&
            this.TreeFields.GraphFile(state, false) != IntPtr.Zero
                ? state
                : IntPtr.Zero;

        /// <summary>A node record's graph id (from its PassiveSkills row); null when it does not lead to one.</summary>
        private int? GraphIdOf(IntPtr record) =>
            this.PassiveRow(record) is { } row && this.reader.TryRead<ushort>(row + GameLayout.PassiveTree.RowGraphIdOffset, out var id) ? id : null;

        /// <summary>A node element's graph record, judged where it leads to a PassiveSkills row.</summary>
        private IntPtr? NodeRecord(IntPtr node)
        {
            var shift = this.elements.Controls?.Shift(ControlGroup.PassiveNode, node, at => this.NodeRecordAt(node, at) is { } record && this.PassiveRow(record) != null) ?? 0;
            return this.NodeRecordAt(node, shift);
        }

        private IntPtr? NodeRecordAt(IntPtr node, int shift) =>
            this.reader.TryRead<IntPtr>(node + GameLayout.PassiveTree.NodeRecordOffset + shift, out var record) && record != IntPtr.Zero ? record : null;

        /// <summary>The PassiveSkills row of a node record, trusted only when its key names the table.</summary>
        private IntPtr? PassiveRow(IntPtr record) => this.DatRow(record + GameLayout.PassiveTree.RecordSkillKeyOffset, GameLayout.PassiveTree.SkillTable);

        private PassiveNodeRow ReadPassiveRow(IntPtr record, IntPtr row)
        {
            var bytes = this.elements.ReadBlock(row, GameLayout.PassiveTree.RowBytes);
            if (bytes.Length < GameLayout.PassiveTree.RowBytes)
            {
                return new PassiveNodeRow(record, -1, string.Empty, string.Empty, false, false);
            }

            var name = this.elements.ReadUtf16BufferAt((IntPtr)BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowNameOffset), MaxPassiveNameChars) ?? string.Empty;
            var isStart = BitConverter.ToInt64(bytes, GameLayout.PassiveTree.RowCharactersOffset) > 0;
            var iconOnly = bytes[GameLayout.PassiveTree.RowIconOnlyOffset] == 1;
            var kind = bytes[GameLayout.PassiveTree.RowKeystoneOffset] == 1 ? Announcements.PassiveKeystone
                : bytes[GameLayout.PassiveTree.RowNotableOffset] == 1 ? Announcements.PassiveNotable
                : bytes[GameLayout.PassiveTree.RowJewelSocketOffset] == 1 ? Announcements.PassiveJewelSocket
                : isStart || iconOnly ? string.Empty
                : Announcements.PassiveSmall;
            return new PassiveNodeRow(record, BitConverter.ToUInt16(bytes, GameLayout.PassiveTree.RowGraphIdOffset), name, kind, iconOnly, isStart);
        }

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
                this.TreeFields.Edges(graphFile) is not { } edgeVector ||
                !this.reader.TryRead<IntPtr>(hoveredNode.Address, out var nodeClass))
            {
                return null;
            }

            // Links in both directions: a record lists only its out-edges.
            var links = new Dictionary<IntPtr, List<IntPtr>>();
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

            var graph = new PassiveGraph(canvasAddress, state, links, elementsByRecord, this.OwnStart(links.Keys));
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

        private sealed record PassiveGraph(IntPtr Canvas, IntPtr State, Dictionary<IntPtr, List<IntPtr>> Links, Dictionary<IntPtr, IntPtr> Elements, IntPtr OwnStart);

        private sealed record PassiveNodeRow(IntPtr Record, int GraphId, string Name, string Kind, bool IconOnly, bool IsStart);
    }
}
