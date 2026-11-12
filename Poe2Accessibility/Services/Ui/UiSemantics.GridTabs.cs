namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Grid tabs of <see cref="UiSemantics"/>: the tab buttons found once per grid and the selected tab's title.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Grid → page → pages container → tab container (vendor stock and stash: 3 hops, 2026-10-23).</summary>
        private const int GridTabHops = 4;

        /// <summary>A grid found without tabs is searched again this often: its panel may still have been building.</summary>
        private const int GridTabRetryMs = 500;

        private (IntPtr Grid, List<IntPtr> Tabs, long At) gridTabs = (IntPtr.Zero, new List<IntPtr>(), 0);

        /// <summary>
        ///     Selected tab of the tab container a grid is a page of ("tab 3" of a vendor's stock, a stash tab); empty
        ///     when the grid has no tabs. The tab buttons are looked up once per grid, their selected byte every time.
        /// </summary>
        private string GridTab(in UiElementReader.ElementInfo info)
        {
            // "No tabs" is never kept as the answer for long, and tab buttons that no longer read are looked for again.
            var now = Environment.TickCount64;
            var lost = this.gridTabs.Tabs.Count == 0 ? now - this.gridTabs.At >= GridTabRetryMs : this.gridTabs.Tabs.Any(tab => !this.elements.IsElement(tab));
            if (info.Address != this.gridTabs.Grid || lost)
            {
                this.gridTabs = (info.Address, this.FindGridTabs(info), now);
            }

            var at = this.TabSelectedOffset(this.gridTabs.Tabs);
            foreach (var button in this.gridTabs.Tabs)
            {
                if (this.IsTabSelected(button, at) && this.elements.TryReadElement(button, out var tab))
                {
                    return Announcements.TabTitle(this.FindDescendantText(tab, CaptionSearchDepth + 1, null));
                }
            }

            return string.Empty;
        }

        /// <summary>Tab buttons of the nearest ancestor holding a pages container; its pages are not searched.</summary>
        private List<IntPtr> FindGridTabs(in UiElementReader.ElementInfo info)
        {
            var tabs = new List<IntPtr>();
            foreach (var ancestor in this.elements.ReadAncestors(info, GridTabHops))
            {
                var children = this.elements.ReadChildren(ancestor);
                if (!children.Any(child => this.elements.TryReadElement(child, out var page) && page.Name == GameLayout.TabButton.PagesContainerName))
                {
                    continue;
                }

                var queue = new Queue<IntPtr>(children);
                var budget = MaxTabSearchNodes;
                while (queue.Count > 0 && budget-- > 0)
                {
                    if (!this.elements.TryReadElement(queue.Dequeue(), out var node) || !node.IsVisibleFlag ||
                        node.Name == GameLayout.TabButton.PagesContainerName)
                    {
                        continue;
                    }

                    if (node.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && this.elements.DetectRoleBySound(node.Address) == UiRole.Tab)
                    {
                        tabs.Add(node.Address);
                        continue;
                    }

                    foreach (var child in this.elements.ReadChildren(node))
                    {
                        queue.Enqueue(child);
                    }
                }

                break;
            }

            return tabs;
        }
    }
}
