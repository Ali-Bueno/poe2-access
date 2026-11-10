namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>How <see cref="UiSemantics"/> finds a control's label: its own text, a row or wrapper beside it, a named ancestor, else its id made readable.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>A row label is a plain label (or a thin wrapper around one) in the sibling before our branch.</summary>
        private const int SiblingSearchDepth = 2;

        /// <summary>Last words of layout ids; "_display" holds a whole screen (the bind screen's "action_binding_display").</summary>
        private static readonly string[] StructuralWords = { "parent", "container", "layout", "background", "bg", "overlay", "display", "scrollable_frame" };

        private string FindDescendantText(in UiElementReader.ElementInfo root, int maxDepth, string? requiredName, string? skippedSuffix = null)
        {
            var queue = new Queue<(UiElementReader.ElementInfo Info, int Depth)>();
            queue.Enqueue((root, 0));
            while (queue.Count > 0)
            {
                var (current, depth) = queue.Dequeue();
                if (depth >= maxDepth)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(current))
                {
                    if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag ||
                        (skippedSuffix != null && child.Name.EndsWith(skippedSuffix, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var nameOk = requiredName == null || child.Name.Equals(requiredName, StringComparison.OrdinalIgnoreCase);
                    if (nameOk && this.elements.TryReadSpokenText(childAddress, out var text) && text.Length > 0)
                    {
                        return text;
                    }

                    queue.Enqueue((child, depth + 1));
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     Climbs the ancestors; at each level looks at the siblings that precede the branch we came from
        ///     (nearest first) for a text — that is the label column of a settings row or the caption above a field.
        /// </summary>
        private string FindRowLabel(in UiElementReader.ElementInfo info)
        {
            var child = info;
            foreach (var parent in this.elements.ReadAncestors(info, RowSearchHops))
            {
                if (parent.Address == this.LayerContainer)
                {
                    break; // other layers (a hidden "Inspect Mode" beside the chat) label nothing in this one
                }

                var siblings = this.elements.ReadChildren(parent);
                var index = Array.IndexOf(siblings, child.Address);
                for (var i = index - 1; i >= 0; i--)
                {
                    // Another widget (combo box, button) is never our label, whatever text it shows.
                    if (this.elements.TryReadElement(siblings[i], out var sibling) &&
                        !sibling.HasFlagBit(GameLayout.Element.InteractiveFlagBit) &&
                        sibling.Name.Length == 0)
                    {
                        var text = this.elements.TryReadSpokenText(sibling.Address, out var own) && own.Length > 0
                            ? own
                            : this.FindLabelInThinWrapper(sibling, SiblingSearchDepth);
                        if (text.Length > 0)
                        {
                            return text;
                        }
                    }
                }

                child = parent;
            }

            return string.Empty;
        }

        /// <summary>
        ///     The label column of a settings row whose control column holds this button ("Popup Notifications" before "Edit
        ///     Settings", 2026-11-08): a row of exactly two columns, the label a thin wrapper first, the button inside the second.
        /// </summary>
        private string ButtonRowLabel(in UiElementReader.ElementInfo info)
        {
            var child = info;
            foreach (var parent in this.elements.ReadAncestors(info, RowSearchHops))
            {
                if (parent.Address == this.LayerContainer)
                {
                    break;
                }

                var columns = this.elements.ReadChildren(parent);
                if (columns.Length == 2 && columns[1] == child.Address)
                {
                    if (!this.elements.TryReadElement(columns[0], out var column) || !column.IsVisibleFlag || column.Name.Length > 0 ||
                        column.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                    {
                        return string.Empty;
                    }

                    return this.elements.TryReadSpokenText(column.Address, out var own) && own.Length > 0 ? own : this.FindLabelInThinWrapper(column, SiblingSearchDepth);
                }

                if (columns.Length != 1)
                {
                    return string.Empty; // the button shares its column with other things: not a settings row
                }

                child = parent;
            }

            return string.Empty;
        }

        /// <summary>Label columns are a chain of single-child wrappers ending in a text leaf; anything wider is content, not a label.</summary>
        private string FindLabelInThinWrapper(in UiElementReader.ElementInfo wrapper, int maxDepth)
        {
            var current = wrapper;
            for (var depth = 0; depth < maxDepth; depth++)
            {
                var children = this.elements.ReadChildren(current);
                if (children.Length != 1 || !this.elements.TryReadElement(children[0], out var only) || only.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
                {
                    return string.Empty;
                }

                if (this.elements.TryReadSpokenText(only.Address, out var text) && text.Length > 0)
                {
                    return text;
                }

                current = only;
            }

            return string.Empty;
        }

        /// <summary>Value labels ("15%", "+ 0.0") are the sibling right after a slider inside the same container.</summary>
        private string FindFollowingSiblingText(in UiElementReader.ElementInfo info)
        {
            var child = info;
            foreach (var parent in this.elements.ReadAncestors(info, 2))
            {
                var siblings = this.elements.ReadChildren(parent);
                var index = Array.IndexOf(siblings, child.Address);
                for (var i = index + 1; i >= 0 && i < siblings.Length; i++)
                {
                    // Another control is never the value: the chat size sliders' "Reset" button beside them (2026-11-08).
                    if (this.elements.TryReadElement(siblings[i], out var sibling) && this.elements.DetectRoleBySound(sibling.Address) == UiRole.Unknown)
                    {
                        var text = this.elements.TryReadSpokenText(sibling.Address, out var own) && own.Length > 0
                            ? own
                            : this.FindDescendantText(sibling, CaptionSearchDepth, null);
                        if (text.Length > 0)
                        {
                            return text;
                        }
                    }
                }

                child = parent;
            }

            return string.Empty;
        }

        private string NearestNamedAncestor(in UiElementReader.ElementInfo info, int maxHops)
        {
            foreach (var ancestor in this.elements.ReadAncestors(info, maxHops))
            {
                if (ancestor.Address == this.LayerContainer)
                {
                    break;
                }

                if (ancestor.Name.Length > 0 && !IsStructuralName(ancestor.Name))
                {
                    return ancestor.Name;
                }
            }

            return string.Empty;
        }

        /// <summary>The direct child that is a text input; zero when none.</summary>
        private IntPtr TextInputChild(in UiElementReader.ElementInfo info)
        {
            foreach (var childAddress in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.HasFlagBit(GameLayout.Element.TextInputFlagBit))
                {
                    return childAddress;
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>Layout ids that only group other elements ("panels_parent", "container") and numeric ids (a spirit row's "2231762944"): they name nothing.</summary>
        private static bool IsStructuralName(string controlName) =>
            !controlName.Any(char.IsLetter) ||
            StructuralWords.Any(word => controlName.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                                        controlName.EndsWith("_" + word, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        ///     A text field's hint ("Type your character name here..."), judged where it reads as words other than the
        ///     field's font face, which the field also holds 0x50 before the hint (0.5.5e).
        /// </summary>
        private string Placeholder(IntPtr field)
        {
            string? HintAt(int shift) => this.elements.ReadWStringStructAt(field + GameLayout.Element.PlaceholderOffset + shift)?.Trim();
            var font = this.elements.ReadWStringStructAt(field + GameLayout.Element.FontOffset)?.Trim();
            var shift = this.elements.Controls?.Shift(ControlGroup.Placeholder, field, at => HintAt(at) is { Length: > 0 } hint && hint.Any(char.IsLetter) && hint != font) ?? 0;
            return HintAt(shift) ?? string.Empty;
        }

        /// <summary>"patch_notes_button" → "patch notes"; "label_(button)" → "label"; a structural id → "".</summary>
        private static string Humanise(string controlName)
        {
            if (IsStructuralName(controlName))
            {
                return string.Empty;
            }

            var name = controlName.Replace("_(button)", string.Empty, StringComparison.Ordinal);
            foreach (var suffix in new[] { "_button", "_combobox", "_checkbox", "_slider", "_label", "_frame" })
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    name = name[..^suffix.Length];
                    break;
                }
            }

            return Announcements.ControlLabel(name.Replace('_', ' ').Trim());
        }
    }
}
