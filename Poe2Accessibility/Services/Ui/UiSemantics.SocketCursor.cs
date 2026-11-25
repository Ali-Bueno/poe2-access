namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Numerics;
    using GameReader.RemoteEnums;

    /// <summary>
    ///     The socket of a piece of gear the cursor is on, for <see cref="UiSemantics"/> (docs/research/item-mods.md, "The
    ///     socket under the cursor"): with a rune in the hand the D-pad walks a piece's sockets while the focus stays on it.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The socket of the item the cursor is on ("engarce 2 de 2: vacío"); empty when it is on none, or cannot be told.</summary>
        private string SocketUnderCursor(IntPtr itemElement)
        {
            if (itemElement == IntPtr.Zero)
            {
                return string.Empty;
            }

            // The pointer is on the socket itself only while it is filled: an empty one has no tooltip to own.
            if (this.SocketOf(this.HoveredAddress) is { } hovered && hovered.Item == itemElement)
            {
                return hovered.Line;
            }

            return this.HoldsSocketable() ? this.SocketAimedAt(itemElement) : string.Empty;
        }

        /// <summary>The socket an element is, by its place among its siblings: item → sockets → socket. Null for any other element.</summary>
        private (IntPtr Item, string Line)? SocketOf(IntPtr element)
        {
            if (element == IntPtr.Zero || !this.elements.TryReadElement(element, out var info) ||
                !this.elements.TryReadElement(info.Raw.ParentPtr, out var holder))
            {
                return null;
            }

            var socketed = this.GearSockets(holder.Raw.ParentPtr);
            var views = this.SocketViews(holder, socketed.Count);
            var index = views.IndexOf(element);
            return index < 0 ? null : (holder.Raw.ParentPtr, this.SocketLine(index, socketed));
        }

        /// <summary>
        ///     The socket the game's cursor frame is centred on: a layer of its own, moved over the cell, or the socket, that A
        ///     acts on.
        /// </summary>
        private string SocketAimedAt(IntPtr itemElement)
        {
            var socketed = this.GearSockets(itemElement);
            if (socketed.Count == 0 || !this.elements.TryReadElement(itemElement, out var item) ||
                !this.elements.TryReadElement(this.LayerContainer, out var container))
            {
                return string.Empty;
            }

            foreach (var holderAddress in this.elements.ReadChildren(item))
            {
                if (!this.elements.TryReadElement(holderAddress, out var holder) || this.SocketViews(holder, socketed.Count) is not { Count: > 0 } views ||
                    this.LayoutRect(holderAddress, this.LayerContainer) is not { } holderRect)
                {
                    continue;
                }

                var places = views.Select(view => this.LayoutRect(view, this.LayerContainer)?.Position).ToList();
                if (places.Contains(null))
                {
                    continue;
                }

                // A socket's element has no size of its own: the sockets fill their holder.
                var far = places.Aggregate(holderRect.Position, (most, place) => Vector2.Max(most, place!.Value));
                var size = holderRect.Position + holderRect.Size - far;
                var centres = places.Select(place => place!.Value + (size / 2)).ToList();
                foreach (var layer in this.elements.ReadChildren(container).Reverse())
                {
                    if (this.LayoutRect(layer, this.LayerContainer) is { } cursor &&
                        SocketAim.Socket(cursor.Position + (cursor.Size / 2), cursor.Size, centres, size) is >= 0 and var aimed)
                    {
                        return this.SocketLine(aimed, socketed);
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>What fills each socket of a piece of gear (a gem's are its supports, read elsewhere); empty for any other element.</summary>
        private IReadOnlyList<IntPtr> GearSockets(IntPtr itemElement)
        {
            var entity = this.inventory.ItemEntity(itemElement);
            return entity == IntPtr.Zero || this.inventory.IsGem(entity) ? Array.Empty<IntPtr>() : this.inventory.SocketedItems(entity);
        }

        /// <summary>The holder's socket elements in order: as many children, all of one class, as the item has sockets; none otherwise.</summary>
        private List<IntPtr> SocketViews(in UiElementReader.ElementInfo holder, int count)
        {
            var views = new List<IntPtr>();
            var children = this.elements.ReadChildren(holder);
            if (count == 0 || children.Length != count)
            {
                return views;
            }

            var vtable = IntPtr.Zero;
            foreach (var child in children)
            {
                if (!this.elements.TryReadElement(child, out var info) || (vtable != IntPtr.Zero && info.Raw.Vtable != vtable))
                {
                    return new List<IntPtr>();
                }

                vtable = info.Raw.Vtable;
                views.Add(child);
            }

            return views;
        }

        private string SocketLine(int index, IReadOnlyList<IntPtr> socketed) =>
            Announcements.SocketAt(index, socketed.Count, socketed[index] == IntPtr.Zero ? null : this.inventory.DescribeItemEntity(socketed[index]) ?? Announcements.UnknownItem);

        /// <summary>The asking player holds a rune, a soul core or a talisman: what goes into a gear socket.</summary>
        private bool HoldsSocketable()
        {
            try
            {
                return this.inventory.Locals?.Of(Seats.Input) is { } inventories && inventories.TryGetValue(InventoryName.Cursor1, out var cursor) &&
                       ServerInventory.Items(this.reader, cursor).Any(held => SocketAim.IsSocketable(this.inventory.ItemBaseId(held.Entity)));
            }
            catch (InvalidOperationException)
            {
                return false; // the host rebuilt its inventory list meanwhile
            }
        }
    }
}
