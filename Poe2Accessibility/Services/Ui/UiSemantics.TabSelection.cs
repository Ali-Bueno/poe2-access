namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>Tab selection of <see cref="UiSemantics"/>: which tab of a layer is selected.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Label of the selected tab button in a layer; empty when the layer has no tab bar.</summary>
        public string SelectedTab(IntPtr layer)
        {
            if (!this.elements.TryReadElement(layer, out var root))
            {
                return string.Empty;
            }

            // Until a bar proves the selected byte every control's click sound is read: a layer found without tabs is
            // then only read through the byte (the cheap way) until the bar is proved.
            var proved = this.elements.Controls?.IsKnown(ControlGroup.Tabs) ?? true;
            if (!proved && this.tablessLayers.Contains(layer))
            {
                proved = true;
            }

            var queue = new Queue<UiElementReader.ElementInfo>();
            queue.Enqueue(root);
            var bars = new HashSet<IntPtr>();
            var budget = MaxTabSearchNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                // Once the selected byte is proved, it is read before the costlier click-sound scan.
                var node = queue.Dequeue();
                if (node.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && !bars.Contains(node.Raw.ParentPtr) &&
                    (!proved || this.IsTabSelected(node.Address, this.TabSelectedOffset(null))) &&
                    this.elements.DetectRoleBySound(node.Address) == UiRole.Tab && this.SiblingTabs(node) is { Count: >= 1 } tabs &&
                    (tabs.Count >= 2 || proved))
                {
                    // A bar of one tab (Zarka's stock, "-1-") counts once the selected byte is proved, which only a bar of several does.
                    bars.Add(node.Raw.ParentPtr);
                    var at = this.TabSelectedOffset(tabs);
                    if (tabs.FirstOrDefault(tab => this.IsTabSelected(tab, at)) is var selected && selected != IntPtr.Zero &&
                        this.elements.TryReadElement(selected, out var tab))
                    {
                        return this.FindDescendantText(tab, CaptionSearchDepth + 1, null);
                    }
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            if (!proved && bars.Count == 0)
            {
                if (this.tablessLayers.Count >= MaxTablessLayers)
                {
                    this.tablessLayers.Clear();
                }

                this.tablessLayers.Add(layer);
            }

            return string.Empty;
        }

        /// <summary>The tabs of a tab's bar, itself included; a lone control with the tab click sound is something else.</summary>
        private List<IntPtr> SiblingTabs(in UiElementReader.ElementInfo tab)
        {
            var tabs = new List<IntPtr>();
            var own = tab.Address;
            if (this.elements.TryReadElement(tab.Raw.ParentPtr, out var parent))
            {
                tabs.AddRange(this.elements.ReadChildren(parent).Where(sibling => sibling == own || this.elements.DetectRoleBySound(sibling) == UiRole.Tab));
            }

            return tabs;
        }

        /// <summary>The tab buttons' selected byte, judged on a bar of them when one is given (exactly one tab is set).</summary>
        private int TabSelectedOffset(IReadOnlyList<IntPtr>? tabs)
        {
            var shift = tabs is { Count: >= MinJudgedTabs }
                ? this.elements.Controls?.Shift(ControlGroup.Tabs, tabs[0], at => this.OneTabSelected(tabs, GameLayout.TabButton.SelectedOffset + at))
                : this.elements.Controls?.Current(ControlGroup.Tabs);
            return GameLayout.TabButton.SelectedOffset + (shift ?? 0);
        }

        private bool OneTabSelected(IReadOnlyList<IntPtr> tabs, int offset)
        {
            var selected = 0;
            foreach (var tab in tabs)
            {
                if (!this.reader.TryRead<byte>(tab + offset, out var value) || value > 1)
                {
                    return false;
                }

                selected += value;
            }

            return selected == 1;
        }

        private bool IsTabSelected(IntPtr tab, int offset) => this.reader.TryRead<byte>(tab + offset, out var selected) && selected != 0;
    }
}
