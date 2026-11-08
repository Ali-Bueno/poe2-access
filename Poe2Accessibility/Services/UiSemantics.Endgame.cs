namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The endgame's skill graphs on screen (docs/research/endgame.md): the Atlas map's nodes, the Atlas passive tree and
    ///     the league trees are PassiveSkills rows drawn from .psg files like the main tree, so a hovered node is told by the
    ///     same chain (element, graph record, PassiveSkills row) and the rest comes from catalog/endgame.json: its name when
    ///     the data holds one, what it asks, and its links with the side each lies on. The game's popup is read as it is.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>How far from the main tree's node record offset an endgame node's record is looked for.</summary>
        private const int EndgameRecordReach = 0x200;

        private const int EndgameRecordStep = 8;

        /// <summary>Elements searched for a graph's drawn nodes, below the hovered node's grandparent.</summary>
        private const int MaxEndgameElements = 8000;

        /// <summary>Hovered elements remembered as no endgame node, so a menu's buttons are searched once.</summary>
        private const int MaxNotEndgame = 512;

        private readonly Dictionary<IntPtr, int> endgameRecordByClass = new();
        private readonly HashSet<IntPtr> notEndgame = new();
        private (IntPtr Root, string Graph, Dictionary<string, Vector2> Centres)? endgameCentres;

        /// <summary>A hovered node of an endgame graph; null for any other element.</summary>
        private AccessibleUiNode? DescribeEndgameNode(in UiElementReader.ElementInfo info, bool hovered)
        {
            if (!this.InGame || !hovered || EndgameCatalog.Shared.NodeCount == 0 ||
                this.EndgamePassiveId(info.Address) is not { } id || EndgameCatalog.Shared.Node(id) is not { } node)
            {
                return null;
            }

            var catalog = EndgameCatalog.Shared;
            var language = Localization.Language;
            var entries = this.TooltipEntries(info);
            var popup = entries.Count > 0 ? entries[^1] : new List<string>();
            var name = popup.Count > 0 ? popup[0] : catalog.NodeName(id, language) is { Length: > 0 } known ? known : Announcements.AtlasNode;
            var tooltip = popup.Skip(1).ToList();
            tooltip.AddRange(catalog.NodeDetails(id, language).Where(detail => !popup.Contains(detail)));

            var centres = this.EndgameCentres(info, node.Graph);
            Vector2? Place(string passive) => centres != null && centres.TryGetValue(passive, out var drawn) ? drawn
                : centres == null && catalog.Node(passive) is { } other ? new Vector2(other.X, other.Y) : null;
            var links = AtlasLinks.Describe(Place(id), (node.Links ?? new List<string>()).Where(other => catalog.SameGraph(id, other))
                .Select(other => (Place(other), catalog.NodeName(other, language) is { Length: > 0 } label ? label : Announcements.AtlasNode)));
            if (links.Count > 0)
            {
                tooltip.Add(Announcements.PassiveLinks(links));
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Panel,
                Text = name,
                IsVisible = this.elements.IsVisible(info),
                IsHovered = hovered,
                Context = this.PanelTitle(info),
                Tooltip = tooltip,
                TooltipPending = popup.Count == 0,
            };
        }

        /// <summary>Research (dev command "atlasnode"): what the hovered element is as an endgame node, and where its record was found.</summary>
        public string EndgameNodeReport(IntPtr hovered)
        {
            if (!this.elements.TryReadElement(hovered, out var info) || !this.reader.TryRead<IntPtr>(hovered, out var nodeClass))
            {
                return $"no element @{hovered:X}";
            }

            this.notEndgame.Remove(hovered);
            var id = this.EndgamePassiveId(hovered);
            var offset = this.endgameRecordByClass.TryGetValue(nodeClass, out var at) ? $"+0x{at:X}" : "none";
            var node = id == null ? null : EndgameCatalog.Shared.Node(id);
            var described = this.DescribeEndgameNode(info, true);
            return $"@{hovered:X} \"{info.Name}\" class {nodeClass:X} record {offset}: {id ?? "no endgame node"}" +
                   (node == null ? string.Empty : $" graph {node.Graph} pin {node.Pin} maps {string.Join(",", node.Maps ?? new List<string>())} at {node.X},{node.Y}") +
                   (described == null ? string.Empty : $" says \"{described.Text}\" | {string.Join(" | ", described.Tooltip ?? Array.Empty<string>())}");
        }

        /// <summary>
        ///     The PassiveSkills id of an element that draws an endgame graph's node, else null: its record is a pointer whose
        ///     first field is a key into PassiveSkills, at the main tree's offset or near it, learnt once per element class and
        ///     trusted only when the id is a catalogued endgame node (so no other widget is ever taken for one).
        /// </summary>
        private string? EndgamePassiveId(IntPtr element)
        {
            if (this.notEndgame.Contains(element) || !this.reader.TryRead<IntPtr>(element, out var nodeClass) || nodeClass == IntPtr.Zero)
            {
                return null;
            }

            if (this.endgameRecordByClass.TryGetValue(nodeClass, out var known) && this.EndgameIdAt(element, known) is { } id)
            {
                return id;
            }

            var found = OffsetSearch.First(GameLayout.PassiveTree.NodeRecordOffset, EndgameRecordReach, EndgameRecordStep, at => this.EndgameIdAt(element, at) != null, EndgameRecordStep);
            if (found is not { } offset)
            {
                if (this.notEndgame.Count >= MaxNotEndgame)
                {
                    this.notEndgame.Clear();
                }

                this.notEndgame.Add(element);
                return null;
            }

            this.endgameRecordByClass[nodeClass] = offset;
            return this.EndgameIdAt(element, offset);
        }

        private string? EndgameIdAt(IntPtr element, int offset) =>
            this.reader.TryRead<IntPtr>(element + offset, out var record) && record != IntPtr.Zero &&
            this.PassiveRow(record) is { } row && this.reader.TryRead<IntPtr>(row, out var text) && text != IntPtr.Zero &&
            this.elements.ReadUtf16BufferAt(text, MaxPassiveNameChars) is { Length: > 0 } id && EndgameCatalog.Shared.Node(id) != null
                ? id
                : null;

        /// <summary>
        ///     The drawn centres of the hovered node's graph, by id: the nodes under its grandparent (group, then canvas, as the
        ///     main tree is laid out); null when the hovered one is not among them. Kept while the hovered node is found in them:
        ///     panning and zooming move every node alike, so the sides between them hold.
        /// </summary>
        private Dictionary<string, Vector2>? EndgameCentres(in UiElementReader.ElementInfo hovered, string graph)
        {
            var ancestors = this.elements.ReadAncestors(hovered, 2);
            if (ancestors.Count == 0 || !this.reader.TryRead<IntPtr>(hovered.Address, out var nodeClass) || !this.endgameRecordByClass.TryGetValue(nodeClass, out var offset))
            {
                return null;
            }

            var root = ancestors[^1].Address;
            if (this.endgameCentres is { } cached && cached.Root == root && cached.Graph == graph && this.EndgameIdAt(hovered.Address, offset) is { } here && cached.Centres.ContainsKey(here))
            {
                return cached.Centres;
            }

            var centres = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var pending = new Queue<UiElementReader.ElementInfo>();
            pending.Enqueue(ancestors[^1]);
            for (var seen = 0; pending.Count > 0 && seen < MaxEndgameElements; seen++)
            {
                var info = pending.Dequeue();
                if (this.reader.TryRead<IntPtr>(info.Address, out var itsClass) && itsClass == nodeClass && this.EndgameIdAt(info.Address, offset) is { } id &&
                    EndgameCatalog.Shared.Node(id)?.Graph == graph && PluginUiElementReflection.TryGetAbsoluteRect(info.Address, out var position, out var size))
                {
                    centres[id] = position + (size / 2);
                    continue;
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    if (this.elements.TryReadElement(child, out var childInfo))
                    {
                        pending.Enqueue(childInfo);
                    }
                }
            }

            var hoveredId = this.EndgameIdAt(hovered.Address, offset);
            if (hoveredId == null || !centres.ContainsKey(hoveredId))
            {
                return null;
            }

            this.endgameCentres = (root, graph, centres);
            return centres;
        }
    }
}
