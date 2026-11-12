namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The panel beside the inventory in <see cref="UiSemantics"/>: the shop page, the side panel's title and the label and grid searches behind it.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>How deep and how many elements <see cref="FirstLabel"/> looks under a panel without a framed title.</summary>
        private const int LabelSearchDepth = 4;
        private const int LabelSearchNodes = 60;

        /// <summary>
        ///     The microtransaction shop's page (panels_parent > eshop_message_box > "EshopMainPage", UI dump 2026-10-29) while it
        ///     is shown, else zero: it marks the chosen entry with a "highlight" child and focuses nothing.
        /// </summary>
        public IntPtr ShopPage(IntPtr inventoryPanel)
        {
            if (!this.elements.TryReadElement(inventoryPanel, out var root) || !root.IsVisibleFlag)
            {
                return IntPtr.Zero;
            }

            foreach (var child in this.elements.ReadChildren(root))
            {
                if (!this.elements.TryReadElement(child, out var parent) || parent.Name != GameLayout.InGameUi.PanelsParentName)
                {
                    continue;
                }

                foreach (var group in this.elements.ReadChildren(parent))
                {
                    if (this.elements.TryReadElement(group, out var groupInfo) && groupInfo.IsVisibleFlag &&
                        this.elements.ReadChildren(groupInfo) is [var page] && this.elements.TryReadElement(page, out var pageInfo) &&
                        pageInfo.IsVisibleFlag && pageInfo.Name == GameLayout.InGameUi.ShopPageName)
                    {
                        return page;
                    }
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        ///     The title of the panel shown beside the inventory (Stash, Skills, Merchant…, in the game's language), empty when
        ///     none. The inventory panel root's panels_parent groups the shown panels in no fixed order (the stash came
        ///     before the inventory once open, after it while hidden: dumps 2026-10-28), so the inventory is the titled
        ///     panel holding the main inventory's grid and the other shown one is the side panel.
        /// </summary>
        public string SidePanelTitle(IntPtr inventoryPanel)
        {
            if (!this.elements.TryReadElement(inventoryPanel, out var root) || !root.IsVisibleFlag)
            {
                return string.Empty;
            }

            var shift = this.elements.Controls?.Current(ControlGroup.PanelTitle) ?? 0;
            var shown = new List<(UiElementReader.ElementInfo Info, string Title)>();
            var key = inventoryPanel.ToInt64();
            foreach (var child in this.elements.ReadChildren(root))
            {
                if (!this.elements.TryReadElement(child, out var parent) || parent.Name != GameLayout.InGameUi.PanelsParentName)
                {
                    continue;
                }

                foreach (var group in this.elements.ReadChildren(parent))
                {
                    if (!this.elements.TryReadElement(group, out var groupInfo) || !groupInfo.IsVisibleFlag)
                    {
                        continue;
                    }

                    foreach (var panel in this.elements.ReadChildren(groupInfo))
                    {
                        if (this.elements.TryReadElement(panel, out var info) && info.IsVisibleFlag)
                        {
                            var title = this.TitleAt(panel, shift);
                            shown.Add((info, title));
                            key = MixKey(MixKey(key, panel.ToInt64()), title.GetHashCode());
                        }
                    }
                }
            }

            // Which panel is the side one takes a walk of each (hundreds of elements, four times a second from the salvage
            // watch): the answer is kept while the same panels show the same titles (SettlingMemo).
            var now = Environment.TickCount64;
            if (!this.sidePanel.TryGet(key, now, out var side))
            {
                side = this.SidePanelAmong(shown);
                this.sidePanel.Set(key, now, side);
            }

            return side;
        }

        private static long MixKey(long key, long value) => unchecked((key ^ value) * KeyPrime);

        /// <summary>The first titled panel that is not the inventory's own; without one, the first label of an untitled one.</summary>
        private string SidePanelAmong(List<(UiElementReader.ElementInfo Info, string Title)> shown)
        {
            var labelled = string.Empty;
            foreach (var (info, title) in shown)
            {
                if (title.Length > 0 && !this.HoldsMainInventory(info))
                {
                    return title;
                }

                if (title.Length == 0 && labelled.Length == 0 && !this.HoldsMainInventory(info))
                {
                    labelled = this.FirstLabel(info);
                }
            }

            return labelled;
        }

        /// <summary>
        ///     The first visible label within a few levels of a panel without a framed title: the salvage bench keeps
        ///     "Mesa de reciclaje" in a label three levels down (dump 2026-11-01, `[0,1,17,2,1,25,0,2,1]`, alone visible in
        ///     its group), so its title was never found and its salvages went unsaid (2026-11-02).
        /// </summary>
        private string FirstLabel(in UiElementReader.ElementInfo panel)
        {
            var budget = LabelSearchNodes;
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((panel, 0));
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (depth > 0 && this.elements.TryReadText(node.Address, out var text) && text.Trim() is { Length: >= 3 and <= MaxPanelTitleLength } label &&
                    label.Count(char.IsLetter) >= 3 && !label.Contains('/') && !label.Contains('_'))
                {
                    return label;
                }

                if (depth >= LabelSearchDepth)
                {
                    continue;
                }

                foreach (var child in this.elements.ReadChildren(node))
                {
                    if (queue.Count >= budget)
                    {
                        break; // never looked at: the budget ends before its turn (the passive tree's canvas has 4,500 children)
                    }

                    if (this.elements.TryReadElement(child, out var info) && info.IsVisibleFlag)
                    {
                        queue.Enqueue((info, depth + 1));
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>Whether a grid of the main inventory lies within a few levels of the panel (the inventory panel's own).</summary>
        private bool HoldsMainInventory(in UiElementReader.ElementInfo panel)
        {
            var budget = MainGridSearchNodes;
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((panel, 0));
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                if (this.inventory.InventoryOf(node.Address) == GameReader.RemoteEnums.InventoryName.MainInventory1)
                {
                    return true;
                }

                if (depth < MainGridSearchDepth)
                {
                    foreach (var child in this.elements.ReadChildren(node))
                    {
                        if (queue.Count >= budget)
                        {
                            break; // never looked at: the budget ends before its turn
                        }

                        if (this.elements.TryReadElement(child, out var info))
                        {
                            queue.Enqueue((info, depth + 1));
                        }
                    }
                }
            }

            return false;
        }

        private const int MainGridSearchDepth = 5;
        private const int MainGridSearchNodes = 600;

        /// <summary>The 64-bit FNV prime: mixes the shown panels into the key of <see cref="sidePanel"/>.</summary>
        private const long KeyPrime = 1099511628211;

        private readonly SettlingMemo<string> sidePanel = new();
    }
}
