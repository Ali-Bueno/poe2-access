namespace Poe2Accessibility.Models
{
    using System;

    public enum UiRole
    {
        Unknown,
        Panel,
        Label,
        Button,
        ComboBox,
        TextField,
        CheckBox,
        Slider,
        Tab,

        /// <summary>Inventory grid: the item under the cursor and its cell, which change without the focus moving.</summary>
        GridCell,

        /// <summary>Equipment or flask slot holding an item.</summary>
        ItemSlot,

        /// <summary>Selected entry of a list that keeps the focus on itself (character selection).</summary>
        ListItem,

        /// <summary>Keyword link inside a text (inspect mode: "[Flask|Flasks]"); A expands its definition.</summary>
        Link,
    }

    /// <summary>
    ///     Semantic view of one UI element, the only thing the speech layer consumes. Built by
    ///     UiSemantics from raw reads; pointers never leak past this type.
    /// </summary>
    public sealed class AccessibleUiNode
    {
        private string text = string.Empty;
        private System.Collections.Generic.IReadOnlyList<string> panelNotes = Array.Empty<string>();
        private System.Collections.Generic.IReadOnlyList<string> tooltip = Array.Empty<string>();

        public IntPtr Address { get; init; }

        /// <summary>The same node with another label, tooltip and panel notes (what a reader adds to a node an archetype built).</summary>
        public AccessibleUiNode With(string label, System.Collections.Generic.IReadOnlyList<string> lines, System.Collections.Generic.IReadOnlyList<string> notes)
        {
            var copy = (AccessibleUiNode)this.MemberwiseClone();
            (copy.text, copy.tooltip, copy.panelNotes) = (label, lines, notes);
            return copy;
        }

        /// <summary>
        ///     The same node with panel notes that stand until they change (the build's summary of the worn gear): said on
        ///     entering the panel only when they differ from those said last (docs/research/sweep-2026-11-13.md).
        /// </summary>
        /// <param name="of">Whose they are (a character): another's are news.</param>
        public AccessibleUiNode WithStandingNotes(System.Collections.Generic.IReadOnlyList<string> notes, string of)
        {
            var copy = (AccessibleUiNode)this.MemberwiseClone();
            var all = new System.Collections.Generic.List<string>(this.panelNotes);
            all.AddRange(notes);
            (copy.panelNotes, copy.StandingNotes, copy.StandingNotesOf) = (all, notes, of);
            return copy;
        }

        /// <summary>Those of <see cref="PanelNotes"/> that stand until they change.</summary>
        public System.Collections.Generic.IReadOnlyList<string> StandingNotes { get; private set; } = Array.Empty<string>();

        public string StandingNotesOf { get; private set; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public UiRole Role { get; init; }

        /// <summary>Spoken label: own text, else the caption of a child label, else the humanised control name.</summary>
        public string Text { get => this.text; init => this.text = value; }

        /// <summary>Current value for value-bearing controls (combo box selection, text field contents).</summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>Where the value stands among its choices (combo boxes: "3 de 4"), said when the value changes.</summary>
        public string ValuePosition { get; init; } = string.Empty;

        /// <summary>The frame around a text box: A opens it, and until then keys type nothing.</summary>
        public bool OpensWithA { get; init; }

        /// <summary>A combo box whose choices are open (in game: stepped in place with the D-pad), a collapsible row shown open.</summary>
        public bool IsExpanded { get; init; }

        /// <summary>
        ///     The few lines the focus's panel shows outside the focused part ("0 de espíritu" over the Skills list): said
        ///     once as the focus enters the panel.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> PanelNotes { get => this.panelNotes; init => this.panelNotes = value; }

        /// <summary>A row that A opens and closes (a Challenges category): its state is said with it.</summary>
        public bool IsCollapsible { get; init; }

        public bool IsVisible { get; init; }

        /// <summary>"3 of 6" in the list of sibling controls; empty when the control is alone.</summary>
        public string Position { get; init; } = string.Empty;

        /// <summary>Under the free cursor rather than focused (passive tree node): its tooltip is all there is to read.</summary>
        public bool IsHovered { get; init; }

        /// <summary>A node of the passive tree: the D-pad is the finder's there.</summary>
        public bool IsPassiveNode { get; init; }

        /// <summary>Lines of the element's tooltip (short ones are read with the focus, long ones on request).</summary>
        public System.Collections.Generic.IReadOnlyList<string> Tooltip { get => this.tooltip; init => this.tooltip = value; }

        /// <summary>
        ///     Lines of <see cref="Tooltip"/> that report a change elsewhere on the screen (the passive tree's count of
        ///     unconfirmed changes): not said again when the screen's own reading has just said them.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> TooltipNews { get; init; } = System.Array.Empty<string>();

        /// <summary>The game has not built the tooltip that holds this element's stats yet (passive node, a few frames).</summary>
        public bool TooltipPending { get; init; }

        /// <summary>An item's tooltip, shown for the first time, may lack lines the game is still adding: not said until it holds still.</summary>
        public bool TooltipFilling { get; init; }

        /// <summary>A grid cell with no item on it.</summary>
        public bool IsEmptyCell { get; init; }

        /// <summary>Every line, when <see cref="Tooltip"/> holds fewer to be said at once (a passive node while a guide leads); the details read these.</summary>
        public System.Collections.Generic.IReadOnlyList<string>? Details { get; init; }

        /// <summary>Title of the section holding the list ("UI Scaling"); spoken when the focus enters it.</summary>
        public string Group { get; init; } = string.Empty;

        /// <summary>Captions of the other items of the same list (a popup's message must not repeat them).</summary>
        public System.Collections.Generic.IReadOnlyList<string> ListItems { get; init; } = Array.Empty<string>();

        /// <summary>
        ///     Buttons that act on a choice beside the list without going to it ("Comprar o vender objetos, X" next to an
        ///     NPC's topics): said once as the panel opens, after the focus.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> Shortcuts { get; init; } = Array.Empty<string>();

        /// <summary>True when the only name found was the control id ("layout"): nothing a player sees.</summary>
        public bool NamedByControlId { get; init; }

        /// <summary>Title of the panel holding the element in game ("Inventory"); empty when unknown.</summary>
        public string Context { get; init; } = string.Empty;

        /// <summary>False for a control that is shown but cannot be used yet (locked button).</summary>
        public bool IsEnabled { get; init; } = true;
    }
}
