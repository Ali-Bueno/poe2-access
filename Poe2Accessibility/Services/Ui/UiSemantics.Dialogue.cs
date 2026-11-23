namespace Poe2Accessibility.Services
{
    using System;

    /// <summary>NPC dialogues of <see cref="UiSemantics"/>: the name drawn over the line.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Who speaks in a layer (<see cref="GameLayout.InGameUi.DialogueNamePanel"/>), empty when it is no dialogue.</summary>
        public string SpeakerIn(IntPtr layer)
        {
            if (!this.InGame || layer == IntPtr.Zero || !this.elements.TryReadElement(layer, out var info) ||
                this.elements.ReadChildren(info) is not [var first, ..] || !this.elements.TryReadElement(first, out var panel) ||
                !panel.IsVisibleFlag || panel.Name != GameLayout.InGameUi.DialogueNamePanel)
            {
                return string.Empty;
            }

            foreach (var child in this.elements.ReadChildren(panel))
            {
                if (this.elements.TryReadElement(child, out var label) && label.IsVisibleFlag && label.Name == GameLayout.InGameUi.DialogueNameLabel &&
                    this.elements.TryReadSpokenText(child, out var name))
                {
                    return name;
                }
            }

            return string.Empty;
        }
    }
}
