namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The shop's item page (docs/research/shop.md): its title and text for the controls in it, and its buy button.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The page's controls lie at most this far under the shop's page (equip button: 12 ancestors to "EshopMainPage").</summary>
        private const int ShopPageHops = 13;

        /// <summary>The page's texts lie at most this deep under it (description: 5 levels, the item's own card: 6).</summary>
        private const int ShopItemDepth = 6;

        /// <summary>The points label under the shop's page: bar_area > topbar_right > gold_icon > label.</summary>
        private const int ShopBalanceDepth = 4;

        /// <summary>
        ///     Where the shop's chosen card is looked for: the open item page's frame, else the shop's page — the list under an
        ///     open page keeps its own card marked, which was read for the page's card.
        /// </summary>
        public IntPtr ShopHighlightRoot(IntPtr shopPage)
        {
            if (shopPage == IntPtr.Zero || !this.elements.TryReadElement(shopPage, out var page) ||
                this.ShownChild(page, GameLayout.InGameUi.ShopSubpagesName) is not { } subpages)
            {
                return shopPage;
            }

            foreach (var tabAddress in this.elements.ReadChildren(subpages))
            {
                if (this.elements.TryReadElement(tabAddress, out var tab) && tab.IsVisibleFlag &&
                    this.ShownChild(tab, GameLayout.InGameUi.ShopDetailPageName) is { } detail &&
                    this.ShownChild(detail, GameLayout.InGameUi.ShopDetailBodyName) is { } body &&
                    this.ShownChild(body, GameLayout.InGameUi.ShopItemPageName) is { } frame)
                {
                    return frame.Address;
                }
            }

            return shopPage;
        }

        private UiElementReader.ElementInfo? ShownChild(in UiElementReader.ElementInfo parent, string name) => this.ShownNamed(parent, name, 1);

        /// <summary>The item page a control lies in: its title, and what it says of the item under it (text, "¡NUEVO!", a discount).</summary>
        private (string Title, IReadOnlyList<string> Notes)? ShopItemPageOf(in UiElementReader.ElementInfo focused)
        {
            if (this.ShopItemPage(focused) is not (var shown, _))
            {
                return null;
            }

            var title = this.ShownText(shown, GameLayout.InGameUi.ShopItemTitleName, ShopItemDepth);
            var notes = new[]
            {
                this.ShownText(shown, GameLayout.InGameUi.ShopItemTextName, ShopItemDepth),
                this.ShownText(shown, GameLayout.InGameUi.ShopItemNewName, ShopItemDepth),
                string.Join(" ", this.ShownTexts(shown, GameLayout.InGameUi.ShopItemDiscountName, ShopItemDepth)),
            };
            return title.Length > 0 ? (title, notes.Where(note => note.Length > 0).ToList()) : null;
        }

        /// <summary>The page's buy button: what it buys, its price or its confirmation, and the account's points.</summary>
        private (string Label, string Value)? ShopBuyButtonOf(in UiElementReader.ElementInfo focused)
        {
            if (focused.Name != GameLayout.InGameUi.ShopBuyButtonName || this.ShopItemPage(focused) is not (var shown, var shop))
            {
                return null;
            }

            var card = this.ShownNamed(shown, GameLayout.InGameUi.ShopSingleCardName, ShopItemDepth);
            return ShopPurchase.Button(new ShopOffer(
                Item: this.ShownText(shown, GameLayout.InGameUi.ShopItemTitleName, ShopItemDepth),
                Caption: this.ShownText(focused, GameLayout.InGameUi.ShopBuyCaptionName, 1),
                ConfirmTip: this.ShownText(focused, GameLayout.InGameUi.ShopConfirmTipName, 1),
                PointsTip: this.ShownText(focused, GameLayout.InGameUi.ShopPointsTipName, 1),
                OldPrice: this.ShownText(focused, GameLayout.InGameUi.ShopOldPriceName, 2),
                Owned: card is { } single ? this.ShownText(single, GameLayout.InGameUi.ShopOwnedLabel, 1) : string.Empty,
                Balance: shop == IntPtr.Zero || !this.elements.TryReadElement(shop, out var page) ? string.Empty
                    : this.ShownText(page, GameLayout.InGameUi.ShopBalanceName, ShopBalanceDepth)));
        }

        /// <summary>The shown page (one item, a bundle or variants) of the item page frame above the control, and the shop's page when it is above too.</summary>
        private (UiElementReader.ElementInfo Shown, IntPtr Shop)? ShopItemPage(in UiElementReader.ElementInfo focused)
        {
            var chain = this.elements.ReadAncestors(focused, ShopPageHops);
            var frame = chain.FindIndex(ancestor => ancestor.Name == GameLayout.InGameUi.ShopItemPageName);
            if (frame < 1)
            {
                return null;
            }

            var shop = chain.Skip(frame).FirstOrDefault(ancestor => ancestor.Name == GameLayout.InGameUi.ShopPageName).Address;
            return (chain[frame - 1], shop);
        }

        /// <summary>The text shown at or under the first shown element with this control name, empty when none shows.</summary>
        private string ShownText(in UiElementReader.ElementInfo root, string name, int depth) =>
            this.ShownTexts(root, name, depth).FirstOrDefault() ?? string.Empty;

        private List<string> ShownTexts(in UiElementReader.ElementInfo root, string name, int depth)
        {
            var texts = new List<(string Name, string Text)>();
            if (this.ShownNamed(root, name, depth) is { } found)
            {
                this.CollectNamedTexts(found, ShopEntryDepth, texts);
            }

            return texts.Select(text => text.Text).ToList();
        }

        /// <summary>The first element with this control name under the root that shows: every element down to it has its visible flag.</summary>
        private UiElementReader.ElementInfo? ShownNamed(in UiElementReader.ElementInfo root, string name, int depth)
        {
            if (depth == 0)
            {
                return null;
            }

            foreach (var childAddress in this.elements.ReadChildren(root))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                if (child.Name == name)
                {
                    return child;
                }

                if (this.ShownNamed(child, name, depth - 1) is { } below)
                {
                    return below;
                }
            }

            return null;
        }
    }
}
