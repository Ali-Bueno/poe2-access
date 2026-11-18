namespace Poe2Accessibility.Services
{
    using System;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>The passive tree's jewel sockets for <see cref="UiSemantics"/> (docs/research/passive-tree.md#socketed-jewels).</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>A socketed jewel's item element hangs this many levels under its node (0.5.5f: node, frame, item); the levels up to it are tried.</summary>
        private const int SocketItemDepth = 2;

        /// <summary>
        ///     What a jewel socket holds: the jewel with its base ("Solaz de perdición, Esmeralda, raro"), empty when none lies at the
        ///     socket's PassiveJewelSlots row in PassiveJewels1; null for another node or when the catalog or the inventory is unread.
        /// </summary>
        private string? SocketJewel(PassiveNodeRow node)
        {
            if (!node.JewelSocket || PassiveTreeCatalog.Shared.ByGraphId(node.GraphId)?.JewelSlot is not { } slot)
            {
                return null;
            }

            try
            {
                if (this.inventory.Locals?.Of(Seats.Input) is not { } inventories || !inventories.TryGetValue(InventoryName.PassiveJewels1, out var jewels))
                {
                    return null;
                }

                foreach (var (entity, x, _) in ServerInventory.Items(this.reader, jewels))
                {
                    if (x == slot)
                    {
                        return this.inventory.DescribeWithBase(entity) ?? Announcements.UnknownItem;
                    }
                }

                return string.Empty;
            }
            catch (InvalidOperationException)
            {
                return null; // the host rebuilt its inventory list meanwhile
            }
        }

        /// <summary>The passive node a socketed jewel's item element lies in, described as the node; null for any other item.</summary>
        private AccessibleUiNode? SocketOfItem(IntPtr item, bool hovered)
        {
            if (Environment.TickCount64 - this.lastPassiveAt > TreeLeftMs)
            {
                return null; // out of the tree (bag, stash, vendor): no item there is a socket's, and the tree's probes are not run on it
            }

            var at = item;
            for (var level = 0; level < SocketItemDepth; level++)
            {
                if (!this.elements.TryReadElement(at, out var info) || !this.elements.TryReadElement(info.Raw.ParentPtr, out var parent))
                {
                    return null;
                }

                if (this.DescribePassiveNode(parent, hovered) is { } node)
                {
                    return node;
                }

                at = parent.Address;
            }

            return null;
        }
    }
}
