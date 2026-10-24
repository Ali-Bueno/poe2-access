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
        public IntPtr Address { get; init; }

        public string Name { get; init; } = string.Empty;

        public UiRole Role { get; init; }

        /// <summary>Spoken label: own text, else the caption of a child label, else the humanised control name.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>Current value for value-bearing controls (combo box selection, text field contents).</summary>
        public string Value { get; init; } = string.Empty;

        /// <summary>Where the value stands among its choices (combo boxes: "3 de 4"), said when the value changes.</summary>
        public string ValuePosition { get; init; } = string.Empty;

        /// <summary>A combo box whose choices are open (in game: stepped in place with the D-pad).</summary>
        public bool IsExpanded { get; init; }

        public bool IsVisible { get; init; }

        /// <summary>"3 of 6" in the list of sibling controls; empty when the control is alone.</summary>
        public string Position { get; init; } = string.Empty;

        /// <summary>Under the free cursor rather than focused (passive tree node): its tooltip is all there is to read.</summary>
        public bool IsHovered { get; init; }

        /// <summary>Lines of the element's tooltip (short ones are read with the focus, long ones on request).</summary>
        public System.Collections.Generic.IReadOnlyList<string> Tooltip { get; init; } = Array.Empty<string>();

        /// <summary>Title of the section holding the list ("UI Scaling"); spoken when the focus enters it.</summary>
        public string Group { get; init; } = string.Empty;

        /// <summary>Captions of the other items of the same list (a popup's message must not repeat them).</summary>
        public System.Collections.Generic.IReadOnlyList<string> ListItems { get; init; } = Array.Empty<string>();

        /// <summary>True when the only name found was the control id ("layout"): nothing a player sees.</summary>
        public bool NamedByControlId { get; init; }

        /// <summary>Title of the panel holding the element in game ("Inventory"); empty when unknown.</summary>
        public string Context { get; init; } = string.Empty;

        /// <summary>False for a control that is shown but cannot be used yet (locked button).</summary>
        public bool IsEnabled { get; init; } = true;
    }
}
