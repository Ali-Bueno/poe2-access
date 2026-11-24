namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The Ritual's favours window in <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>From the tribute's label up: its holder (an icon, then the label), then the window.</summary>
        private const int TributeLabelHops = 2;

        /// <summary>The favours window's "1.265 de tributo": an unnamed label after its icon, in the window its named buttons tell.</summary>
        public bool IsTributeLabel(IntPtr text) =>
            this.elements.TryReadElement(text, out var info) && info.Name.Length == 0 &&
            this.elements.ReadAncestors(info, TributeLabelHops) is [var holder, var window] &&
            this.elements.ReadChildren(holder) is [_, var label] && label == text &&
            this.IsFavoursWindow(window);

        /// <summary>Whether a control lies in the favours window.</summary>
        public bool InFavoursWindow(IntPtr control) =>
            this.elements.TryReadElement(control, out var info) && this.elements.ReadAncestors(info, PanelTitleHops).Any(ancestor => this.IsFavoursWindow(ancestor));

        /// <summary>The favours window by its own name ("Favores"), as its moves name it (<see cref="SidePanelTitle"/>); empty when the panel is no such window.</summary>
        /// <param name="panel">The side panel the focus is in.</param>
        /// <param name="chain">The focus's ancestors.</param>
        private string FavoursTitle(IntPtr panel, IEnumerable<UiElementReader.ElementInfo> chain)
        {
            if (this.favoursTitle.Panel == panel)
            {
                return this.favoursTitle.Name;
            }

            var name = chain.Any(ancestor => this.IsFavoursWindow(ancestor)) && this.elements.TryReadElement(panel, out var info) ? this.FirstLabel(info) : string.Empty;
            if (name.Length > 0)
            {
                this.favoursTitle = (panel, name);
            }

            return name;
        }

        /// <summary>The favours window's name, once read: its panel keeps it.</summary>
        private (IntPtr Panel, string Name) favoursTitle;

        private bool IsFavoursWindow(in UiElementReader.ElementInfo window) =>
            this.elements.ReadChildren(window).Any(child => this.elements.TryReadElement(child, out var button) &&
                button.Name is GameLayout.InGameUi.RitualTributeButtonName or GameLayout.InGameUi.RitualDeferButtonName);
    }
}
