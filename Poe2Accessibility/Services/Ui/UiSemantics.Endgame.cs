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

        /// <summary>Elements of a node and its popup looked at for content icons.</summary>
        private const int MaxContentIconElements = 400;

        /// <summary>Hovered elements remembered as no endgame node, so a menu's buttons are searched once.</summary>
        private const int MaxNotEndgame = 512;

        /// <summary>The compatibility part said once when a hovered node's id is missing from catalog/endgame.json.</summary>
        private const string AtlasCatalogPart = "Atlas catalog";

        private readonly Dictionary<IntPtr, int> endgameRecordByClass = new();
        private readonly HashSet<IntPtr> notEndgame = new();
        private readonly HashSet<string> uncatalogued = new(StringComparer.Ordinal);
        private (IntPtr Root, string Graph, Dictionary<string, Vector2> Centres, Dictionary<string, IntPtr> Elements, int Offset)? endgameSnapshot;

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
            var name = popup.Count > 0 ? popup[0] : this.EndgameNodeLabel(id, info.Address, language) is { Length: > 0 } known ? known : Announcements.AtlasNode;
            var tooltip = popup.Skip(1).ToList();
            if (this.EndgameContentIcons(info, language) is { Count: > 0 } contents)
            {
                tooltip.Insert(0, Announcements.AtlasContents(contents));
            }

            tooltip.AddRange(catalog.NodeDetails(id, language).Where(detail => !popup.Contains(detail)));

            var centres = this.EndgameCentres(info, node.Graph);
            var linked = (node.Links ?? new List<string>()).Where(other => catalog.SameGraph(id, other)).ToList();

            // An undrawn (fog) neighbour's place is estimated from a drawn one's local scale, so its side reads as
            // the stick finds it; the graph's own places alone put Epitaph at Marrow's left, where no hop lands.
            Vector2? GraphPlace(string passive) => catalog.Node(passive) is { } at ? new Vector2(at.X, at.Y) : null;
            var scale = centres != null && centres.TryGetValue(id, out var hereCentre) && GraphPlace(id) is { } graphHere
                ? linked.Where(other => centres.ContainsKey(other) && GraphPlace(other) is { } there && Vector2.Distance(graphHere, there) > 1)
                    .Select(other => Vector2.Distance(hereCentre, centres[other]) / Vector2.Distance(graphHere, GraphPlace(other)!.Value))
                    .Cast<float?>().FirstOrDefault()
                : null;
            Vector2? Place(string passive) =>
                centres != null && centres.TryGetValue(passive, out var drawn) ? drawn
                : scale is { } factor && centres != null && GraphPlace(passive) is { } graphThere && GraphPlace(id) is { } graphOrigin
                    ? centres[id] + ((graphThere - graphOrigin) * factor)
                    : centres == null ? GraphPlace(passive) : null;
            var links = AtlasLinks.Describe(Place(id), linked
                .Select(other => (Place(other), Label(other))));

            string Label(string other)
            {
                var element = this.EndgameElementOf(other);
                var label = this.EndgameNodeLabel(other, element, language) is { Length: > 0 } named ? named : Announcements.AtlasNode;

                // A fog neighbour is named from its rolled area, but no hop lands there until it is revealed
                // (Bastille at Mineshaft's left, live 2026-11-25): say so instead of promising a direction. Drawn
                // reads live off the element's own rect — the scan's centres go stale as fog lifts mid-walk, and
                // the visible flag also drops on drawn neighbours.
                return element != IntPtr.Zero && !PluginUiElementReflection.TryGetAbsoluteRect(element, out _, out _)
                    ? Announcements.AtlasUnrevealed(label)
                    : label;
            }
            if (links.Count > 0)
            {
                tooltip.Add(Announcements.PassiveLinks(links));
            }

            if (catalog.TowerReach(id) is > 0 and var reach)
            {
                tooltip.Add(Announcements.AtlasTowerReaches(reach));
            }

            // A tower is far off, beyond the drawn nodes: its side comes from the graph's own places.
            if (node.Towers is { Count: > 0 } towers)
            {
                var here = new Vector2(node.X, node.Y);
                var sides = towers.Select(tower => catalog.Node(tower) is { } towerNode ? AtlasLinks.Side(here, new Vector2(towerNode.X, towerNode.Y)) : string.Empty)
                    .Where(side => side.Length > 0).ToList();
                if (sides.Count > 0)
                {
                    tooltip.Add(Announcements.AtlasTowerReach(sides));
                }
            }

            // The Atlas skill tree's finder (UiSemantics.AtlasTree.cs): the way to its chosen node, as the
            // passive tree's guide says it at the end of every node.
            var atlasTreeNode = node.Graph == AtlasTreeMap.GraphName;
            if (atlasTreeNode && this.AtlasTreeGuide(id, centres) is { Length: > 0 } guide)
            {
                tooltip.Add(guide);
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Panel,
                Text = name,
                IsVisible = this.elements.IsVisible(info),
                IsHovered = hovered,
                IsAtlasTreeNode = atlasTreeNode,
                Context = this.PanelTitle(info),
                Tooltip = tooltip,
                TooltipPending = popup.Count == 0,
            };
        }

        /// <summary>
        ///     The contents an Atlas node shows as icons, on the node or in its popup (a breach, a map boss: pictures with no
        ///     words), named by the art each draws; in drawing order, each once.
        /// </summary>
        private List<string> EndgameContentIcons(in UiElementReader.ElementInfo node, string language)
        {
            var names = new List<string>();
            var pending = new Queue<UiElementReader.ElementInfo>();
            pending.Enqueue(node);
            for (var seen = 0; pending.Count > 0 && seen < MaxContentIconElements; seen++)
            {
                var info = pending.Dequeue();
                if (!info.IsVisibleFlag)
                {
                    continue;
                }

                if (this.elements.ReadTextureKey(info) is { } key && EndgameCatalog.Shared.IconName(key, language) is { Length: > 0 } name && !names.Contains(name))
                {
                    names.Add(name);
                }
                else if (this.elements.TryReadText(info.Address, out var text) && EndgameCatalog.Shared.IsContentName(text.Trim(), language) && !names.Contains(text.Trim()))
                {
                    // Canvas-drawn icons give no texture key; the icon element itself carries the content's name as text.
                    names.Add(text.Trim());
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    if (this.elements.TryReadElement(child, out var childInfo))
                    {
                        pending.Enqueue(childInfo);
                    }
                }
            }

            return names;
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
                   (described == null ? string.Empty : $" says \"{described.Text}\" | {string.Join(" | ", described.Tooltip ?? Array.Empty<string>())}") +
                   this.AtlasNodeDetails(hovered);
        }

        /// <summary>Research (dev command "atlasnode"): the node the Atlas cursor rests on, as <see cref="EndgameNodeReport"/> tells it.</summary>
        public string AtlasSelectedReport(IntPtr worldScreen) =>
            this.SelectedAtlasNode(worldScreen) is { } selected ? this.EndgameNodeReport(selected) : $"no selected atlas node (world screen @{worldScreen:X})";

        /// <summary>
        ///     The landed node's spoken line for <see cref="AtlasNarrator"/>: its name, its state from the status byte,
        ///     what it holds and its links — the popup's flavour and blocked words are the screen reading's, which
        ///     this line queues behind. Null while its popup is unbuilt (the name lives there for a random map) unless
        ///     <paramref name="allowPending"/>; empty when the element is no endgame node at all.
        /// </summary>
        public string? AtlasNodeLine(IntPtr node, bool allowPending)
        {
            if (!this.elements.TryReadElement(node, out var info))
            {
                return string.Empty;
            }

            // Some landed nodes hold no EndgameMapAtlas key (a Rockpools and an Oasis went silent, live 2026-11-25):
            // no catalog identity and no links, but their rolled area, state and popup words still read.
            var described = this.DescribeEndgameNode(info, true);
            var language = Localization.Language;
            var name = described?.Text ?? this.AtlasAreaName(node, language);
            if (name.Length == 0)
            {
                return string.Empty;
            }

            // The map's popup is no part of the node's tooltip; it stands beside map_control and carries the rolled
            // biome and contents in its own words. Until it is built there is nothing full to say.
            var popup = this.AtlasPopupTexts(info, name, language);
            if (!allowPending && popup.Count == 0)
            {
                return null;
            }

            var catalog = EndgameCatalog.Shared;
            var biome = popup.Select(text => catalog.BiomeNameIn(text, language)).FirstOrDefault(found => found.Length > 0) ?? string.Empty;
            var held = popup.SelectMany(text => catalog.ContentNamesIn(text, language)).Distinct(StringComparer.Ordinal).ToList();
            var state = this.reader.TryRead<byte>(node + GameLayout.AtlasNode.StatusOffset, out var status)
                ? Announcements.AtlasState(AtlasState.Of(status))
                : string.Empty;
            var contents = held.Count > 0 ? Announcements.AtlasContents(held) : described == null ? string.Empty : FirstWith(described.Tooltip, Announcements.AtlasContents(Array.Empty<string>()));
            var links = described == null ? string.Empty : FirstWith(described.Tooltip, Announcements.PassiveLinks(Array.Empty<string>()));
            return Announcements.AtlasLanding(name, state, biome, contents, links);

            static string FirstWith(IReadOnlyList<string>? lines, string prefix) =>
                lines?.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal)) ?? string.Empty;
        }

        /// <summary>
        ///     The landed node's popup words: only its own subtree of the world screen — the one holding the node's
        ///     title, else the first beside map_control holding a biome line. The whole screen also carries the quest
        ///     panel, whose "The Grand Expedition" quest read as a content on every landing (live 2026-11-25).
        /// </summary>
        private List<string> AtlasPopupTexts(in UiElementReader.ElementInfo info, string title, string language)
        {
            var ancestors = this.elements.ReadAncestors(info, 3);
            if (ancestors.Count < 3 || !this.elements.TryReadElement(ancestors[^1].Address, out var screen))
            {
                return new List<string>();
            }

            List<string>? byBiome = null;
            var byBiomeAmbiguous = false;
            var seen = 0;
            foreach (var child in this.elements.ReadChildren(screen))
            {
                if (!this.elements.TryReadElement(child, out var subtree) || !subtree.IsVisibleFlag || subtree.Name == GameLayout.WorldMap.ControlName || subtree.Name == "quest_selector")
                {
                    continue;
                }

                var texts = new List<string>();
                var pending = new Stack<UiElementReader.ElementInfo>();
                pending.Push(subtree);
                while (pending.Count > 0 && seen < MaxPopupNodes)
                {
                    seen++;
                    var current = pending.Pop();
                    if (this.elements.TryReadText(current.Address, out var text) && Plain(text) is { Length: > 0 } plain && !texts.Contains(plain))
                    {
                        texts.Add(plain);
                    }

                    foreach (var inner in this.elements.ReadChildren(current))
                    {
                        if (this.elements.TryReadElement(inner, out var innerInfo) && innerInfo.IsVisibleFlag)
                        {
                            pending.Push(innerInfo);
                        }
                    }
                }

                if (title.Length > 0 && texts.Contains(title))
                {
                    return texts;
                }

                if (texts.Any(text => EndgameCatalog.Shared.BiomeNameIn(text, language).Length > 0))
                {
                    // No subtree held the title: a biome line alone does not say whose popup this is, so
                    // two such panels say nothing at all rather than lending another node's words.
                    if (byBiome == null)
                    {
                        byBiome = texts;
                    }
                    else
                    {
                        byBiomeAmbiguous = true;
                    }
                }
            }

            return byBiomeAmbiguous ? new List<string>() : byBiome ?? new List<string>();
        }

        /// <summary>
        ///     The Atlas node the cursor rests on: map_control keeps it (<see cref="GameLayout.AtlasNode.SelectedNodeOffset"/>),
        ///     trusted only when the element's own atlas key reads (map_control is shared with the act maps, whose field
        ///     there is something else). Null when the Atlas is not the shown tab.
        /// </summary>
        public IntPtr? SelectedAtlasNode(IntPtr worldScreen)
        {
            if (!this.elements.TryReadElement(worldScreen, out var screen))
            {
                return null;
            }

            foreach (var child in this.elements.ReadChildren(screen))
            {
                if (this.elements.TryReadElement(child, out var control) && control.Name == GameLayout.WorldMap.ControlName &&
                    this.reader.TryRead<IntPtr>(child + GameLayout.AtlasNode.SelectedNodeOffset, out var node) && node != IntPtr.Zero &&
                    (DatKey.Row(this.reader, this.elements, node + GameLayout.AtlasNode.AtlasKeyOffset, GameLayout.AtlasNode.AtlasTable) != null ||
                     DatKey.Row(this.reader, this.elements, node + GameLayout.AtlasNode.EndgameMapsKeyOffset, GameLayout.AtlasNode.EndgameMapsTable) != null))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        ///     Research: an Atlas node's own fields for the state hunt (docs/research/atlas-navigator.md §6): its identity
        ///     by its dat keys (no record search), its grid place, the unidentified fields after it, and every texture key
        ///     it and its children draw — state art is the remaining on-element candidate for completed/accessible.
        ///     Empty for an element without an atlas key.
        /// </summary>
        private string AtlasNodeDetails(IntPtr node)
        {
            if (DatKey.Row(this.reader, this.elements, node + GameLayout.AtlasNode.AtlasKeyOffset, GameLayout.AtlasNode.AtlasTable) is not { } atlasRow)
            {
                return string.Empty;
            }

            var passive = DatKey.Row(this.reader, this.elements, atlasRow + GameLayout.PassiveTree.RecordSkillKeyOffset, GameLayout.PassiveTree.SkillTable);
            var area = DatKey.Row(this.reader, this.elements, node + GameLayout.AtlasNode.AreaKeyOffset, GameLayout.WorldMap.AreaTable);
            var bytes = this.reader.ReadBytes(node + GameLayout.AtlasNode.GridOffset, 0x20);
            var fields = bytes.Length == 0x20
                ? $" grid {BitConverter.ToInt32(bytes, 0)},{BitConverter.ToInt32(bytes, 4)} status {bytes[GameLayout.AtlasNode.StatusOffset - GameLayout.AtlasNode.GridOffset]} +328 {BitConverter.ToUInt64(bytes, 8):X16} +330 {BitConverter.ToUInt32(bytes, 0x10)} +338 {Convert.ToHexString(bytes, 0x18, 8)}"
                : string.Empty;
            var textures = new List<string>();
            if (this.elements.TryReadElement(node, out var info))
            {
                var pending = new Queue<UiElementReader.ElementInfo>();
                pending.Enqueue(info);
                for (var seen = 0; pending.Count > 0 && seen < MaxContentIconElements; seen++)
                {
                    var current = pending.Dequeue();
                    if (this.elements.ReadTextureKey(current) is { } key && !textures.Contains(key))
                    {
                        textures.Add(key);
                    }

                    foreach (var child in this.elements.ReadChildren(current))
                    {
                        if (this.elements.TryReadElement(child, out var childInfo))
                        {
                            pending.Enqueue(childInfo);
                        }
                    }
                }
            }

            return $" atlas {AtlasRowId(passive)} area {AtlasRowId(area)}{fields} textures [{string.Join(", ", textures)}]";
        }

        /// <summary>A dat row's id (the text at its first field), or "?" when the row did not read.</summary>
        private string AtlasRowId(IntPtr? row) =>
            row is { } at && this.reader.TryRead<IntPtr>(at, out var text) && text != IntPtr.Zero &&
            this.elements.ReadUtf16BufferAt(text, MaxPassiveNameChars) is { Length: > 0 } id ? id : "?";

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

            // A class already judged: a node whose id the catalog lacks is the game ahead of the catalog, said once, read from its popup.
            if (this.endgameRecordByClass.TryGetValue(nodeClass, out var known) && this.EndgameRowId(element, known) is { } id)
            {
                if (EndgameCatalog.Shared.Node(id) != null)
                {
                    return id;
                }

                if (this.uncatalogued.Add(id))
                {
                    this.TreeFields.Log?.Invoke($"Atlas node {id} is not in catalog/endgame.json ({this.uncatalogued.Count} such); the game's popup is read as it is");
                    CompatibilityReport.Current?.Missing(AtlasCatalogPart);
                }

                return null;
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

        /// <summary>The PassiveSkills id the element's record leads to at the offset, catalogued or not; null when the chain breaks.</summary>
        private string? EndgameRowId(IntPtr element, int offset) =>
            this.reader.TryRead<IntPtr>(element + offset, out var record) && record != IntPtr.Zero &&
            this.PassiveRow(record) is { } row && this.reader.TryRead<IntPtr>(row, out var text) && text != IntPtr.Zero &&
            this.elements.ReadUtf16BufferAt(text, MaxPassiveNameChars) is { Length: > 0 } id
                ? id
                : null;

        /// <summary>The id when the catalog knows the node, which is what lets an offset be learnt for a class.</summary>
        private string? EndgameIdAt(IntPtr element, int offset) =>
            this.EndgameRowId(element, offset) is { } id && EndgameCatalog.Shared.Node(id) != null ? id : null;

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
            if (this.endgameSnapshot is { } cached && cached.Root == root && cached.Graph == graph && cached.Offset == offset &&
                this.EndgameIdAt(hovered.Address, offset) is { } here && cached.Centres.ContainsKey(here) &&
                cached.Elements.TryGetValue(here, out var held) && held == hovered.Address)
            {
                return cached.Centres;
            }

            var centres = new Dictionary<string, Vector2>(StringComparer.Ordinal);
            var elements = new Dictionary<string, IntPtr>(StringComparer.Ordinal);
            var pending = new Queue<UiElementReader.ElementInfo>();
            pending.Enqueue(ancestors[^1]);
            for (var seen = 0; pending.Count > 0 && seen < MaxEndgameElements; seen++)
            {
                var info = pending.Dequeue();
                if (this.reader.TryRead<IntPtr>(info.Address, out var itsClass) && itsClass == nodeClass && this.EndgameIdAt(info.Address, offset) is { } id &&
                    EndgameCatalog.Shared.Node(id)?.Graph == graph)
                {
                    // A fog node has no rect to centre on; its element still names the map it rolled.
                    elements[id] = info.Address;
                    if (PluginUiElementReflection.TryGetAbsoluteRect(info.Address, out var position, out var size))
                    {
                        centres[id] = position + (size / 2);
                    }

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
            if (hoveredId == null || !elements.ContainsKey(hoveredId))
            {
                this.endgameSnapshot = null;
                return null;
            }

            // Centres and elements are one snapshot, replaced together; a fog hover (in elements, not in
            // centres) still replaces it, so a failed scan never leaves the preceding addresses live.
            this.endgameSnapshot = (root, graph, centres, elements, offset);
            return centres;
        }

        /// <summary>The element drawing a node of the snapshot's graph, re-read as that node before it is trusted; zero when the snapshot holds none.</summary>
        private IntPtr EndgameElementOf(string passiveId) =>
            this.endgameSnapshot is { } snapshot && snapshot.Elements.TryGetValue(passiveId, out var element) &&
            this.EndgameIdAt(element, snapshot.Offset) == passiveId ? element : IntPtr.Zero;

        /// <summary>
        ///     A node's name before its popup: an Atlas passive's name or the one map it can be (the catalog's), else
        ///     the map the element actually rolled — its area key's name, which is how its popup titles it — else the
        ///     catalog's biomes. On the Atlas map the popup is no part of the node's tooltip, so a random map node is
        ///     named by its rolled area or not at all (live 2026-11-25: landed nodes read "Atlas node").
        /// </summary>
        private string EndgameNodeLabel(string passiveId, IntPtr element, string language)
        {
            var catalog = EndgameCatalog.Shared;
            if (catalog.Text("passive/" + passiveId, language) is { Length: > 0 } passive)
            {
                return passive;
            }

            if (catalog.Node(passiveId)?.Maps is { Count: 1 } && catalog.NodeName(passiveId, language) is { Length: > 0 } single)
            {
                return single;
            }

            if (this.AtlasAreaName(element, language) is { Length: > 0 } rolled)
            {
                return rolled;
            }

            return catalog.NodeName(passiveId, language);
        }

        /// <summary>The name of the map a node element rolled, by its area key; empty when the key or its name does not read.</summary>
        private string AtlasAreaName(IntPtr element, string language) =>
            element != IntPtr.Zero && DatKey.Row(this.reader, this.elements, element + GameLayout.AtlasNode.AreaKeyOffset, GameLayout.WorldMap.AreaTable) is { } row &&
            this.AtlasRowId(row) is { Length: > 0 } areaId && areaId != "?"
                ? EndgameCatalog.Shared.Text("area/" + areaId, language)
                : string.Empty;
    }
}
