namespace Poe2Accessibility.Services
{
    using System;
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
            this.elements.ReadChildren(window).Any(child => this.elements.TryReadElement(child, out var button) &&
                button.Name is GameLayout.InGameUi.RitualTributeButtonName or GameLayout.InGameUi.RitualDeferButtonName);
    }
}
