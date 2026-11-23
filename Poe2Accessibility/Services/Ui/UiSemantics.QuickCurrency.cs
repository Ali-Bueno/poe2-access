namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     The quick-use currency buttons under a world label (a strongbox's, an essence's) for
    ///     <see cref="UiSemantics"/>. The labels live in their own layer: the targeted one's root is named by
    ///     the object's file ("Metadata/Chests/StrongBoxes/…"), its first child holds the title and the
    ///     modifier lines, and each further child is one icon-only currency button the game offers from the
    ///     player's inventory. Holding the interact button moves the game's focus onto a button, which the
    ///     generic reading named only by the panel's legend, "Select, Back, Inventory" (dump and focus log,
    ///     2026-11-23; docs/research/strongbox-currency.md §4b). What a sighted player has is the label and
    ///     the currency's icon: the label said as the context, the currency named by its art.
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Button → label root: the button's wrapper is the root's child (measured 1; a row holder would make 2).</summary>
        private const int QuickCurrencyHops = 4;

        /// <summary>Wrapper → frame → button → icon (measured 3).</summary>
        private const int QuickCurrencyDepth = 3;

        private const int QuickCurrencyNodes = 16;

        /// <summary>Buttons already reported to the log, so each is named there once.</summary>
        private readonly HashSet<IntPtr> quickCurrencySeen = new();

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

            var currency = string.Empty;
            var keys = new List<string>();
            var tooltip = new List<string>();
            var press = false;
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((info, 0));
            var budget = QuickCurrencyNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                press |= this.elements.DetectRoleBySound(node.Address) == UiRole.Button;
                if (this.elements.ReadAnyTextureKey(node) is { } key)
                {
                    keys.Add(key);
                    if (currency.Length == 0 && CurrencyIcons.QuickUseName(key) is { Length: > 0 } named)
                    {
                        currency = named;
                    }
                }

                if (tooltip.Count == 0 && this.TooltipLines(node) is { Count: > 0 } lines)
                {
                    tooltip = lines;
                }

                if (depth == QuickCurrencyDepth)
                {
                    continue;
                }

                // The icon itself is flagged invisible in every dump (its parent draws it): texture hunting
                // cannot stay with the visible children.
                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child))
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            if (!press)
            {
                return null; // under a world label but no button in it: not the currency row
            }

            // The label whole, as the generic reading already makes it ("Strongbox, Guarded by 10 packs of
            // Monsters", measured by `describe` on the root); it cannot re-enter here, its own ancestors
            // carry no Metadata name.
            var labelText = this.DescribeElement(label.Address, false)?.Text ?? string.Empty;

            var children = this.elements.ReadChildren(label);
            var slots = new List<IntPtr>();
            for (var i = 1; i < children.Length; i++)
            {
                if (this.elements.TryReadElement(children[i], out var slot) && slot.IsVisibleFlag)
                {
                    slots.Add(children[i]);
                }
            }

            var position = string.Empty;
            var at = slots.IndexOf(branch.Address);
            if (at >= 0 && slots.Count > 1)
            {
                position = Announcements.ListPosition(at + 1, slots.Count);
            }

            if (this.quickCurrencySeen.Add(info.Address))
            {
                this.ArtLog?.Invoke(
                    $"Quick currency: {label.Name}: {(currency.Length > 0 ? currency : "unread")}, keys [{string.Join(", ", keys)}], slots {slots.Count}");
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Button,
                Text = currency.Length > 0 ? currency : Announcements.QuickCurrencyUnnamed,
                Position = position,
                IsVisible = this.elements.IsVisible(info),
                Context = labelText,
                Tooltip = tooltip,
            };
        }
    }
}
