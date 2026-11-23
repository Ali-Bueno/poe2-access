namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The quick-use currency buttons under a world label (a strongbox's, an essence's) for
    ///     <see cref="UiSemantics"/>. The labels live in their own layer: the targeted one's root is named by
    ///     the object's file ("Metadata/Chests/StrongBoxes/…"), its first child holds the title and the
    ///     modifier lines, and its second is the row of icon-only currency buttons the game offers from the
    ///     player's inventory. Holding the interact button moves the game's focus onto that row, which the
    ///     generic reading named only by the panel's legend, "Select, Back, Inventory" (dump and focus log,
    ///     2026-11-23; docs/research/strongbox-currency.md §4c). What a sighted player has is the label and
    ///     the currencies' icons: the label said as the context, each currency named by its art.
    ///
    ///     The row is one focused element however many currencies it offers: the direction buttons move a
    ///     selection inside it, which the UI manager keeps in its hovered slot (the two buttons' addresses
    ///     alternate there as directions are pressed, dumps and focus log of 2026-11-23) while every button
    ///     stands at the same size. One read therefore names the hovered button, with its place in the row.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Row → label root: the row is the root's child, its buttons' focus wrapper one more hop.</summary>
        private const int QuickCurrencyHops = 4;

        /// <summary>Row → slot frame → button → icon (measured 3 under the single button's wrapper).</summary>
        private const int QuickCurrencyDepth = 4;

        private const int QuickCurrencyNodes = 48;

        /// <summary>
        ///     The last row reported to the log (its element, what it offered, which was hovered): consecutive
        ///     polls of the same row are all the log must not repeat. Forgotten with the other focus memos, so an
        ///     address the game reuses for a rebuilt row logs again.
        /// </summary>
        private (IntPtr Address, int Selected, string[] Currencies) quickCurrencyLogged;

        /// <summary>The UI manager's hovered element as the focus tracker last read it: the row's selected button.</summary>
        public IntPtr HoveredAddress { get; set; }

        private AccessibleUiNode? DescribeQuickCurrency(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame)
            {
                return null;
            }

            var label = default(UiElementReader.ElementInfo);
            var branch = info;
            var found = false;
            foreach (var ancestor in this.elements.ReadAncestors(info, QuickCurrencyHops))
            {
                if (ancestor.Name.StartsWith("Metadata/", StringComparison.Ordinal))
                {
                    label = ancestor;
                    found = true;
                    break;
                }

                branch = ancestor;
            }

            if (!found)
            {
                return null;
            }

            // Every press-sound button in the row, each named by the icon in its own subtree. The icon itself
            // is flagged invisible in every dump (its parent draws it), so the walk cannot stay visible-only.
            var slots = new List<(UiElementReader.ElementInfo Button, string Currency, List<string> Keys)>();
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((branch, 0));
            var budget = QuickCurrencyNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (this.elements.DetectRoleBySound(node.Address) == UiRole.Button)
                {
                    slots.Add((node, this.QuickCurrencyOf(node, out var keys), keys));
                    continue; // a button's own subtree is its icon, already read; no buttons nest inside one
                }

                if (depth == QuickCurrencyDepth)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            if (slots.Count == 0)
            {
                return null; // under a world label but no button in it: not the currency row
            }

            // Tree order is the row's order (the dumps list the buttons left to right); the hovered one is
            // selected, the grown one (a single button stands 61x61 focused, 40x40 not) when none is hovered.
            var selected = slots.FindIndex(offer => offer.Button.Address == this.HoveredAddress);
            if (selected < 0)
            {
                selected = 0;
                var grown = -1f;
                for (var i = 0; i < slots.Count; i++)
                {
                    var size = slots[i].Button.Raw.UnscaledSize;
                    if (size.X * size.Y > grown)
                    {
                        (selected, grown) = (i, size.X * size.Y);
                    }
                }
            }
            var slot = slots[selected];

            // The label whole, as the generic reading already makes it ("Strongbox, Guarded by 10 packs of
            // Monsters", measured by `describe` on the root); it cannot re-enter here, its own ancestors
            // carry no Metadata name.
            var labelText = this.DescribeElement(label.Address, false)?.Text ?? string.Empty;

            var logged = this.quickCurrencyLogged;
            var unchanged = logged.Address == info.Address && logged.Selected == selected && logged.Currencies is { } seen && seen.Length == slots.Count;
            for (var i = 0; unchanged && i < slots.Count; i++)
            {
                unchanged = slots[i].Currency == logged.Currencies[i];
            }

            if (!unchanged)
            {
                this.quickCurrencyLogged = (info.Address, selected, slots.ConvertAll(offer => offer.Currency).ToArray());
                this.ArtLog?.Invoke(
                    $"Quick currency: {label.Name}: {string.Join(" | ", slots.ConvertAll(offer => $"{(offer.Currency.Length > 0 ? offer.Currency : "unread")} [{string.Join(", ", offer.Keys)}] {offer.Button.Raw.UnscaledSize.X:0}x{offer.Button.Raw.UnscaledSize.Y:0}"))}, selected {selected + 1} of {slots.Count}");
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Button,
                Text = slot.Currency.Length > 0 ? slot.Currency : Announcements.QuickCurrencyUnnamed,
                Position = slots.Count > 1 ? Announcements.ListPosition(selected + 1, slots.Count) : string.Empty,
                IsVisible = this.elements.IsVisible(info),
                Context = labelText,
                Tooltip = this.QuickTooltipOf(slot.Button),
            };
        }

        /// <summary>The currency a row button offers, by the first icon art in its subtree; every key read, for the log.</summary>
        private string QuickCurrencyOf(in UiElementReader.ElementInfo button, out List<string> keys)
        {
            keys = new List<string>();
            var currency = string.Empty;
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((button, 0));
            var budget = 8;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (this.elements.ReadAnyTextureKey(node) is { } key)
                {
                    keys.Add(key);
                    if (currency.Length == 0 && CurrencyIcons.QuickUseName(key) is { Length: > 0 } named)
                    {
                        currency = named;
                    }
                }

                if (depth == 2)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return currency;
        }

        /// <summary>The first tooltip lines in a button's subtree (the currency item's, as its backpack cell reads).</summary>
        private List<string> QuickTooltipOf(in UiElementReader.ElementInfo button)
        {
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((button, 0));
            var budget = 8;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (this.TooltipLines(node) is { Count: > 0 } lines)
                {
                    return lines;
                }

                if (depth == 2)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            return new List<string>();
        }
    }
}
