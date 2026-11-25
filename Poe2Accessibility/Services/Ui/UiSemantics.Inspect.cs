namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Item Inspect Mode for <see cref="UiSemantics"/> (docs/research/ui-layers.md, "Item Inspect Mode"): the items its
    ///     layer shows, read from the elements the game names the same in every language, with what the tooltips show only
    ///     as colour or pictures (rarity, sockets) taken from the items in memory. No new memory field: the texts are the
    ///     label fields already read and the entities those of the grid and the worn gear.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>How deep a tooltip is read; the deepest row seen (a tier badge's text) is six levels down.</summary>
        private const int MaxInspectDepth = 10;

        /// <summary>At most this many elements a tooltip: a corrupt pointer cannot make the walk run on.</summary>
        private const int MaxInspectNodes = 600;

        /// <summary>The item the focus was last on in a grid or an equipment slot, and whether it is worn: the item R3 inspects.</summary>
        private (IntPtr Entity, bool Worn) inspectSource;

        /// <summary>The inspect layer last found, and its tooltips' root elements in tree order (the inspected item's first).</summary>
        private (IntPtr Layer, IReadOnlyList<IntPtr> Tooltips) inspectLayer = (IntPtr.Zero, Array.Empty<IntPtr>());

        /// <summary>Gets the game's own name for the mode, the text beside the tooltips ("Inspect Mode"); empty when none was read.</summary>
        public string InspectModeLabel { get; private set; } = string.Empty;

        /// <summary>
        ///     The items item Inspect Mode shows, the inspected one first and the worn ones after it; null when no such layer
        ///     is on the screen, it was not opened on the item last focused, or its tooltips cannot be read apart.
        /// </summary>
        public IReadOnlyList<InspectItem>? InspectItems(ScreenSnapshot screen)
        {
            var (source, sourceWorn) = this.inspectSource;
            if (source == IntPtr.Zero || this.FindInspectLayer(screen) is not { } found)
            {
                this.inspectLayer = (IntPtr.Zero, Array.Empty<IntPtr>());
                return null;
            }

            var (layer, label, tooltips) = found;
            var single = tooltips.Count == 1;
            var reads = tooltips.Select((tooltip, index) => InspectLayout.Read(tooltip.Node, worn: index > 0 || (single && sourceWorn))).ToList();
            var baseName = this.inventory.ItemBaseName(source);
            if (reads.Any(read => read == null) || (baseName.Length > 0 && !reads[0]!.Value.Title.Contains(baseName, StringComparison.Ordinal)))
            {
                // Not the item last focused: another inspect mode (the passive tree's), or the layer of an item gone.
                this.inspectLayer = (IntPtr.Zero, Array.Empty<IntPtr>());
                return null;
            }

            this.inspectLayer = (layer, tooltips.Select(tooltip => tooltip.Address).ToList());
            this.InspectModeLabel = label;
            var worn = this.WornForInspect();
            var slots = InspectLayout.MatchSlots(reads.Skip(1).Select(read => read!.Value.Title).ToList(), worn.Select(piece => (piece.Key, piece.BaseName)).ToList());
            var items = new List<InspectItem> { this.InspectItem(reads[0]!.Value, source, sourceWorn && single, null) };
            for (var index = 1; index < reads.Count; index++)
            {
                var piece = slots[index - 1] is { } key ? worn.First(candidate => candidate.Key == key) : default;
                items.Add(this.InspectItem(reads[index]!.Value, piece.Entity, true, piece.Spoken));
            }

            return items;
        }

        /// <summary>The index in <see cref="InspectItems"/> of the tooltip a focused element sits in; null when it is in none.</summary>
        public int? InspectItemOf(AccessibleUiNode node)
        {
            var tooltips = this.inspectLayer.Tooltips;
            if (tooltips.Count == 0 || !this.elements.TryReadElement(node.Address, out var info))
            {
                return null;
            }

            foreach (var ancestor in this.elements.ReadAncestors(info, MaxInspectDepth).Prepend(info))
            {
                var index = tooltips.ToList().IndexOf(ancestor.Address);
                if (index >= 0)
                {
                    return index;
                }
            }

            return null;
        }

        /// <summary>
        ///     The inspect layer on the screen: a top layer whose second child holds one element whose children are item
        ///     tooltips (each with a `layout`). The one found last is looked at first.
        /// </summary>
        private (IntPtr Layer, string Label, List<(IntPtr Address, InspectNode Node)> Tooltips)? FindInspectLayer(ScreenSnapshot screen)
        {
            var layers = screen.Layers.OrderByDescending(layer => layer.Address == this.inspectLayer.Layer).ThenByDescending(layer => layer.Index);
            foreach (var layer in layers)
            {
                if (!this.elements.TryReadElement(layer.Address, out var layerInfo) || !layerInfo.IsVisibleFlag)
                {
                    continue;
                }

                var parts = this.elements.ReadChildren(layerInfo);
                if (parts.Length < 2 || !this.elements.TryReadElement(parts[1], out var holder) ||
                    this.elements.ReadChildren(holder) is not { Length: > 0 } held || !this.elements.TryReadElement(held[0], out var container))
                {
                    continue;
                }

                var tooltips = new List<(IntPtr Address, InspectNode Node)>();
                foreach (var address in this.elements.ReadChildren(container))
                {
                    var budget = MaxInspectNodes;
                    if (this.InspectNodeAt(address, 0, ref budget) is { } node)
                    {
                        tooltips.Add((address, node));
                    }
                }

                if (tooltips.Count > 0 && tooltips.All(tooltip => tooltip.Node.Children.Any(child => child.Name == InspectLayout.LayoutName)))
                {
                    var label = this.elements.TryReadText(parts[0], out var text) ? text.Trim() : string.Empty;
                    return (layer.Address, label, tooltips);
                }
            }

            return null;
        }

        /// <summary>A shown element and its shown children, as <see cref="InspectLayout"/> reads them; null when hidden or unreadable.</summary>
        private InspectNode? InspectNodeAt(IntPtr address, int depth, ref int budget)
        {
            if (budget-- <= 0 || depth > MaxInspectDepth || !this.elements.TryReadElement(address, out var info) || !info.IsVisibleFlag)
            {
                return null;
            }

            var (text, tagged) = this.elements.TryReadText(info, out var plain, out var marked) ? (plain, marked) : (string.Empty, string.Empty);
            var children = new List<InspectNode>();
            foreach (var child in this.elements.ReadChildren(info))
            {
                if (this.InspectNodeAt(child, depth + 1, ref budget) is { } node)
                {
                    children.Add(node);
                }
            }

            return new InspectNode(info.Name, text, tagged, children);
        }

        /// <summary>The worn items Inspect Mode can set beside an item: a key for each slot, its entity, its base name, its spoken name.</summary>
        private List<(string Key, IntPtr Entity, string BaseName, string? Spoken)> WornForInspect() =>
            this.WornGear().Worn
                .OrderBy(piece => piece.Key.Slot, StringComparer.Ordinal).ThenBy(piece => piece.Key.X)
                .Select(piece => (
                    $"{piece.Key.Slot}:{piece.Key.X}",
                    piece.Value,
                    this.inventory.ItemBaseName(piece.Value),
                    Enum.TryParse<InventoryName>(piece.Key.Slot, out var slot) ? Announcements.SlotName(slot) : null))
                .ToList();

        /// <summary>
        ///     One item of the table: its tooltip's lines, its rarity after its title (as the item line says it), and its
        ///     sockets where the mod's socket sentence goes, both left out when its entity cannot be read.
        /// </summary>
        private InspectItem InspectItem((string Title, IReadOnlyList<InspectLine> Lines) read, IntPtr entity, bool worn, string? slot)
        {
            var lines = read.Lines.ToList();
            if (this.inventory.ItemRarity(entity) is { } rarity && rarity != Rarity.Normal)
            {
                lines[0] = lines[0] with { Spoken = $"{lines[0].Spoken}, {Announcements.RarityName(rarity)}" };
            }

            if (entity != IntPtr.Zero)
            {
                // The socket sentence takes the runes' lines and goes where the first of them was (UiSemantics.Sockets).
                var said = this.WithSockets(entity, lines.Skip(1).Select(line => line.Match).ToList());
                var left = lines.Skip(1).ToList();
                var withSockets = new List<InspectLine> { lines[0] };
                foreach (var text in said)
                {
                    var index = left.FindIndex(line => line.Match == text);
                    withSockets.Add(index >= 0 ? left[index] : new InspectLine(text, InspectRows.SocketsMatch));
                    if (index >= 0)
                    {
                        left.RemoveAt(index);
                    }
                }

                lines = withSockets;
            }

            return new InspectItem(read.Title, lines, worn, slot);
        }
    }
}
