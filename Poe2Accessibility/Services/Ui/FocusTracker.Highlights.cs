namespace Poe2Accessibility.Services
{
    using System;
    using System.Linq;
    using GameReader.RemoteEnums;
    using Poe2Accessibility.Models;

    /// <summary>Highlight markers and option rows of <see cref="FocusTracker"/>: finding the highlighted control, the disabled option row and the markers that show the cursor.</summary>
    public sealed partial class FocusTracker
    {
        /// <summary>The holder of the options sections <paramref name="element"/> sits in (row → list → page → section → holder), or zero.</summary>
        private IntPtr OptionSectionsOf(IntPtr element)
        {
            if (!this.elements.TryReadElement(element, out var info))
            {
                return IntPtr.Zero;
            }

            var chain = this.elements.ReadAncestors(info, OptionRowHops + 4);
            chain.Insert(0, info);
            for (var hop = 0; hop <= OptionRowHops && hop + 4 < chain.Count; hop++)
            {
                var rows = this.elements.ReadChildren(chain[hop + 1]);
                var row = Array.IndexOf(rows, chain[hop].Address);
                if (rows.Length >= MinOptionRows && row >= 0)
                {
                    // The first list on the way up is the options list or none.
                    return this.elements.IsSectionRow(chain[hop + 3].Address, row) ? chain[hop + 4].Address : IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The control of the options row the active section highlights when the game focuses nothing, if that control is
        ///     disabled ("Resolución de la ventana" while the mode is windowed full screen); zero otherwise.
        /// </summary>
        private IntPtr DisabledOptionRow()
        {
            if (this.optionSections == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (!this.elements.TryReadElement(this.optionSections, out var holder) || !this.elements.IsVisible(holder))
            {
                this.optionSections = IntPtr.Zero; // the options page closed or another tab shows
                return IntPtr.Zero;
            }

            foreach (var section in this.elements.ReadChildren(holder))
            {
                if (this.elements.ReadSectionRow(section) is { } row && this.elements.TryReadElement(section, out var sectionInfo))
                {
                    var rows = this.SectionRows(sectionInfo);
                    return row < rows.Length ? this.DisabledControlIn(rows[row]) : IntPtr.Zero;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>A section's rows: its grandchild with the most children (heading and list sit in a page).</summary>
        private IntPtr[] SectionRows(in UiElementReader.ElementInfo section)
        {
            var rows = Array.Empty<IntPtr>();
            foreach (var page in this.elements.ReadChildren(section))
            {
                if (!this.elements.TryReadElement(page, out var pageInfo))
                {
                    continue;
                }

                foreach (var list in this.elements.ReadChildren(pageInfo))
                {
                    if (this.elements.TryReadElement(list, out var listInfo) && this.elements.ReadChildren(listInfo) is { } children && children.Length > rows.Length)
                    {
                        rows = children;
                    }
                }
            }

            return rows;
        }

        /// <summary>The row's control (its first interactive descendant) when the game has it disabled, else zero.</summary>
        private IntPtr DisabledControlIn(IntPtr row)
        {
            var queue = new System.Collections.Generic.Queue<(IntPtr Address, int Depth)>();
            queue.Enqueue((row, 0));
            while (queue.Count > 0)
            {
                var (address, depth) = queue.Dequeue();
                if (!this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                {
                    return FlagBits.EnabledTrusted && !info.HasFlagBit(GameLayout.Element.EnabledFlagBit) ? address : IntPtr.Zero;
                }

                if (depth < OptionRowHops)
                {
                    foreach (var child in this.elements.ReadChildren(info))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The parent of a visible highlight marker under <paramref name="root"/>, or zero. The markers are searched again
        ///     when none shows (a screen still building, a shop page whose entries are made as it scrolls).
        /// </summary>
        private IntPtr HighlightedControl(IntPtr root)
        {
            if (root == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            if (this.markersRoot != root)
            {
                this.markersRoot = root;
                this.markers = this.FindMarkers(root);
                this.sinceMarkerScan.Restart();
                this.inGameRescanMs = InGameMarkerRescanMs;
            }

            // A pre-game screen is searched again only while it has no marker at all; the shop whenever none shows, since
            // it rebuilds its entries.
            var found = this.VisibleMarkerParent();
            var stale = root == this.CurrentRoot ? this.markers.Count == 0 : found == IntPtr.Zero;
            if (stale && this.sinceMarkerScan.ElapsedMilliseconds >= this.RescanMs(root))
            {
                this.markers = this.FindMarkers(root);
                this.sinceMarkerScan.Restart();
                found = this.VisibleMarkerParent();
                this.inGameRescanMs = found == IntPtr.Zero ? Math.Min(this.inGameRescanMs * 2, InGameMarkerRescanMaxMs) : InGameMarkerRescanMs;
            }

            return found;
        }

        /// <summary>Pre-game screens rarely change; an in-game page rebuilds its entries as it scrolls and switches tabs.</summary>
        private long RescanMs(IntPtr root) => root == this.CurrentRoot ? MarkerRescanMs : this.inGameRescanMs;

        private IntPtr VisibleMarkerParent()
        {
            for (var index = 0; index < this.markers.Count; index++)
            {
                var (marker, frame) = this.markers[index];
                if (!this.elements.TryReadElement(marker, out var info))
                {
                    this.markers.RemoveAt(index--); // gone with its screen
                    continue;
                }

                if (info.IsVisibleFlag && info.Raw.ParentPtr != IntPtr.Zero &&
                    this.elements.TryReadElement(info.Raw.ParentPtr, out var parent) && this.elements.IsVisible(parent))
                {
                    if (this.IsMarker(marker, info, frame))
                    {
                        return info.Raw.ParentPtr;
                    }

                    // An address is a marker only while the element there still is one: a rebuilt screen gives it to another.
                    this.markers.RemoveAt(index--);
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>Whether the element is still what it was found as: a highlight child by its name, or a shop card's frame (unnamed, its card's last child).</summary>
        private bool IsMarker(IntPtr address, in UiElementReader.ElementInfo info, bool frame) =>
            !frame ? Array.IndexOf(HighlightMarkers, info.Name) >= 0
            : info.Name.Length == 0 && this.elements.TryReadElement(info.Raw.ParentPtr, out var card) && this.elements.ReadChildren(card) is { Length: > 1 } children && children[^1] == address;

        /// <summary>
        ///     The frame of a card whose caption is <paramref name="caption"/> ("name_label"): the card's last child, unnamed
        ///     (UI dump 2026-10-29); zero for another card.
        /// </summary>
        private IntPtr CardFrame(in UiElementReader.ElementInfo caption)
        {
            if (caption.Raw.ParentPtr == IntPtr.Zero || !this.elements.TryReadElement(caption.Raw.ParentPtr, out var card))
            {
                return IntPtr.Zero;
            }

            var children = this.elements.ReadChildren(card);
            return children.Length > 1 && this.elements.TryReadElement(children[^1], out var last) && last.Name.Length == 0 ? children[^1] : IntPtr.Zero;
        }

        private System.Collections.Generic.List<(IntPtr Address, bool Frame)> FindMarkers(IntPtr root)
        {
            var found = new System.Collections.Generic.List<(IntPtr Address, bool Frame)>();
            var queue = new System.Collections.Generic.Queue<IntPtr>();
            var seen = new System.Collections.Generic.HashSet<IntPtr>();
            queue.Enqueue(root);
            while (queue.Count > 0 && seen.Count < MaxMarkerScan)
            {
                var address = queue.Dequeue();
                if (!seen.Add(address) || !this.elements.TryReadElement(address, out var info))
                {
                    continue;
                }

                if (Array.IndexOf(HighlightMarkers, info.Name) >= 0)
                {
                    found.Add((address, false));
                }

                if (root != this.CurrentRoot && info.Name == GameLayout.InGameUi.ShopEntryNameLabel && this.CardFrame(info) is var frame &&
                    frame != IntPtr.Zero && !found.Contains((frame, true)))
                {
                    found.Add((frame, true)); // the shop's offer cards: an unnamed frame, last, shown on the chosen one
                }

                foreach (var child in this.elements.ReadChildren(info))
                {
                    queue.Enqueue(child);
                }
            }

            return found;
        }
    }
}
