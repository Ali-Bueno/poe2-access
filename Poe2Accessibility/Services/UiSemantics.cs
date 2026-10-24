namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Turns a raw element into an AccessibleUiNode: role from the layout control name, the click-sound
    ///     asset and flags; spoken label from the element, its caption child, or the label column of the
    ///     form row it sits in; value from its text child or the value label that follows it.
    ///     Widget classification, never screen classification (generic UI strategy).
    /// </summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Captions sit one or two levels below a button / combo box.</summary>
        private const int CaptionSearchDepth = 2;

        /// <summary>
        ///     Form rows (label column + control column) are within this many ancestors of the control (options rows: 3;
        ///     the FPS limit's number field, wrapped beside its check box: 5).
        /// </summary>
        private const int RowSearchHops = 5;

        /// <summary>A named container right above an unnamed widget names it (news panel inside motd_frame).</summary>
        private const int NamedAncestorHops = 2;

        /// <summary>A row label is a plain label (or a thin wrapper around one) in the sibling before our branch.</summary>
        private const int SiblingSearchDepth = 2;

        /// <summary>A focused row reads at most this many cells, from at most this deep (stat rows: cell → text).</summary>
        private const int MaxRowCells = 6;

        private const int RowCellDepth = 2;

        private static readonly string[] StructuralSuffixes = { "_parent", "_container", "_layout", "_background", "_bg", "_overlay" };

        private readonly UiElementReader elements;
        private readonly UiCatalog catalog;
        private readonly ComboBoxReader comboBoxes;

        private readonly HostMemoryReader reader;
        private readonly InventoryReader inventory;

        public UiSemantics(HostMemoryReader reader, UiElementReader elements, UiCatalog catalog, ComboBoxReader comboBoxes, InventoryReader inventory)
        {
            this.reader = reader;
            this.inventory = inventory;
            this.elements = elements;
            this.catalog = catalog;
            this.comboBoxes = comboBoxes;
        }

        /// <summary>In game: inventory archetypes and panel titles apply (their fields mean something else pre-game).</summary>
        public bool InGame { get; set; }

        public AccessibleUiNode? Describe(IntPtr address, bool hovered = false)
        {
            if (!this.elements.TryReadElement(address, out var info))
            {
                return null;
            }

            if (this.InGame && this.DescribeInventory(info) is { } inventoryNode)
            {
                return inventoryNode;
            }

            if (this.DescribePassiveNode(info, hovered) is { } passiveNode)
            {
                return passiveNode;
            }

            if (this.DescribeLink(info) is { } linkNode)
            {
                return linkNode;
            }

            if (this.DescribeSelectableList(info) is { } listNode)
            {
                return listNode;
            }

            if (this.DescribeOptionList(info) is { } optionNode)
            {
                return optionNode;
            }

            if (this.DescribeMarkedRow(info) is { } markedNode)
            {
                return markedNode;
            }

            if (this.DescribeButtonSlot(info) is { } slotNode)
            {
                return slotNode;
            }

            if (this.DescribeMapMarker(info) is { } markerNode)
            {
                return markerNode;
            }

            var ownText = this.elements.TryReadSpokenText(address, out var text) ? text : string.Empty;
            // A text box wrapped in a bare frame (social filter): the frame takes the focus, its child is the input.
            var input = ownText.Length == 0 && !info.HasFlagBit(GameLayout.Element.TextInputFlagBit) ? this.TextInputChild(info) : IntPtr.Zero;
            var role = input != IntPtr.Zero ? UiRole.TextField : this.InferRole(info, ownText.Length > 0);
            var value = string.Empty;
            var valuePosition = string.Empty;
            var expanded = false;
            var namedById = false;
            string label;
            switch (role)
            {
                case UiRole.ComboBox:
                    value = this.ComboBoxValue(info, ownText);
                    valuePosition = this.ComboValuePosition(info, value);
                    expanded = this.reader.TryRead<byte>(info.Address + this.elements.ComboBoxOpenFlagOffset, out var open) && open == 1;
                    label = Humanise(info.Name);
                    break;
                case UiRole.TextField:
                    value = ownText.Length > 0 ? ownText : this.FindDescendantText(info, CaptionSearchDepth, null);
                    label = this.FindRowLabel(info);
                    if (label.Length == 0)
                    {
                        label = this.elements.ReadWStringStructAt((input != IntPtr.Zero ? input : address) + GameLayout.Element.PlaceholderOffset)?.Trim() ?? string.Empty;
                    }

                    if (label.Length == 0)
                    {
                        label = Humanise(info.Name);
                    }

                    break;
                case UiRole.Slider:
                    value = this.SliderValue(info);
                    label = Humanise(info.Name);
                    break;
                case UiRole.CheckBox:
                    value = this.elements.ReadCheckBoxState(address) switch
                    {
                        true => Announcements.Checked,
                        false => Announcements.Unchecked,
                        null => string.Empty,
                    };
                    label = ownText.Length > 0 ? ownText : Humanise(info.Name);
                    if (label.Length == 0)
                    {
                        // A toggle drawn as a captioned button (cosmetics categories: "Character", "Extras").
                        label = this.FindDescendantText(info, CaptionSearchDepth, null);
                    }

                    if (label.Length == 0)
                    {
                        label = this.DrawnCaption(info);
                    }

                    break;
                default:
                    label = ownText.Length > 0 ? ownText : this.RowText(info);
                    if (label.Length == 0)
                    {
                        label = Humanise(info.Name);
                        namedById = label.Length > 0;
                    }

                    break;
            }

            if (label.Length == 0)
            {
                // Unnamed widget inside a named container (news panel, form): the container names it.
                label = Humanise(this.NearestNamedAncestor(info, NamedAncestorHops));
                namedById = label.Length > 0;
            }

            if (label.Length == 0)
            {
                // Settings rows, form fields: the label lives in the sibling column before the control.
                label = this.FindRowLabel(info);
            }

            if (label.Length == 0)
            {
                label = Humanise(this.NearestNamedAncestor(info, RowSearchHops));
                namedById = label.Length > 0;
            }

            var tooltip = this.TooltipLines(info);
            var context = this.InGame ? this.PanelTitle(info) : string.Empty;
            if ((namedById || hovered || label.Length == 0 || label == context) && tooltip.Count > 0)
            {
                // Only a control id named it, nothing did (cosmetic slots: an icon), the label found is the panel's
                // title (the quest map's underground toggle), or the free cursor rests on it (passive node): the
                // tooltip's title is what a sighted player reads ("Support Gem Slot", "Weapon Effect").
                label = tooltip[0];
                tooltip.RemoveAt(0);
                namedById = false;
            }
            else if (tooltip.Count > 0 && (label == tooltip[0] || label.StartsWith(tooltip[0] + ", ", StringComparison.Ordinal)))
            {
                // The tooltip's title repeats what the widget shows ("Mana, 64" → title "Mana").
                tooltip.RemoveAt(0);
            }

            if (this.InGame && value.Length == 0 && tooltip.Count > 0 && role is UiRole.Panel or UiRole.Unknown && !label.Any(char.IsDigit))
            {
                // An inspectable widget (character panel stat): its number is drawn by a sibling, unless its own caption
                // holds it ("Armour, 0%").
                value = this.OverlayValue(info, label);
            }

            var group = string.Empty;
            var (position, listItems) = hovered ? (string.Empty, Array.Empty<string>()) : role is UiRole.Button or UiRole.CheckBox or UiRole.Slider or UiRole.ComboBox or UiRole.TextField or UiRole.Tab
                ? this.ListPosition(info)
                : (string.Empty, Array.Empty<string>());
            if (position.Length == 0 && this.InGame && !hovered && role is not UiRole.Button and not UiRole.Tab)
            {
                // In game controls carry no interactive flag, but list items and option rows are siblings of one class.
                (position, listItems, group) = this.SameClassPosition(info);
            }
            if (namedById && this.InGame && position.Length > 0 && this.FindDescendantText(info, CaptionSearchDepth + 1, null).Length == 0)
            {
                // A row of a list whose other rows have captions but this one shows nothing (empty skill slot below
                // "LVL: 1, Punch"): it is empty, not the list's control id.
                (label, namedById) = (Announcements.Empty, false);
            }

            return new AccessibleUiNode
            {
                Address = address,
                Name = info.Name,
                Role = role,
                Text = label,
                Value = value,
                ValuePosition = valuePosition,
                IsExpanded = expanded,
                IsVisible = this.elements.IsVisible(info),
                Context = context,
                NamedByControlId = namedById,
                Position = position,
                ListItems = listItems,
                Group = group,
                Tooltip = tooltip,
                IsHovered = hovered,
                IsEnabled = !info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) || info.HasFlagBit(GameLayout.Element.EnabledFlagBit),
            };
        }

        /// <summary>Visible text first (login gateway combo shows it in a text field); otherwise the selected entry from the object.</summary>
        private string ComboBoxValue(in UiElementReader.ElementInfo info, string ownText)
        {
            var combo = this.comboBoxes.Read(info.Address);
            var open = this.reader.TryRead<byte>(info.Address + this.elements.ComboBoxOpenFlagOffset, out var flag) && flag == 1;
            if (open && combo.IsOpenOnOtherEntry && combo.Highlighted.Length > 0)
            {
                // List open and the user is on another entry: that entry is what matters now (the gateway list keeps
                // showing the committed server while the D-pad moves, 2026-10-24).
                return combo.Highlighted;
            }

            // The league selector's "league_combobox_popup" shows the league's description beside it while focused:
            // a tooltip, not the value.
            var shown = ownText.Length > 0 ? ownText : this.FindDescendantText(info, CaptionSearchDepth, null, PopupSuffix);
            if (shown.Length > 0)
            {
                return shown;
            }

            if (combo.Selected.Length > 0)
            {
                return combo.Selected;
            }

            return combo.Count > 0 ? Announcements.ComboPosition(combo.SelectedIndex + 1, combo.Count) : string.Empty;
        }

        /// <summary>
        ///     The shown value's place among the combo's entries. In-game option combos open in place: A, then the D-pad
        ///     steps the value itself (no list on screen), so the position is all that tells how many choices are left.
        /// </summary>
        private string ComboValuePosition(in UiElementReader.ElementInfo info, string value)
        {
            var combo = this.comboBoxes.Read(info.Address);
            if (combo.Count < 2 || value.Length == 0)
            {
                return string.Empty;
            }

            var index = value == combo.Highlighted ? combo.HighlightIndex : value == combo.Selected ? combo.SelectedIndex : -1;
            return index >= 0 && index < combo.Count ? Announcements.ListPosition(index + 1, combo.Count) : string.Empty;
        }

        /// <summary>The game shows a value label next to some sliders; the rest only have the number in memory.</summary>
        private string SliderValue(in UiElementReader.ElementInfo info)
        {
            // Read even when a label shows the value, so the slider fields are checked on the first slider seen.
            var state = this.elements.ReadSliderState(info.Address);
            var shown = this.FindFollowingSiblingText(info);
            if (shown.Length > 0)
            {
                return shown;
            }

            return state == null ? string.Empty : Announcements.SliderValue(state.Value.Value, state.Value.Min, state.Value.Max);
        }

        /// <summary>Live signals first (name convention, click sound, flags); the offline catalog is only a last reference.</summary>
        private UiRole InferRole(in UiElementReader.ElementInfo info, bool hasOwnText)
        {
            var name = info.Name.ToLowerInvariant();
            if (name.Contains("combobox"))
            {
                return UiRole.ComboBox;
            }

            if (name.Contains("textfield") || name.Contains("text_field"))
            {
                return UiRole.TextField;
            }

            if (name.Contains("checkbox"))
            {
                return UiRole.CheckBox;
            }

            if (name.Contains("slider"))
            {
                return UiRole.Slider;
            }

            if (name.EndsWith("button", StringComparison.Ordinal))
            {
                return UiRole.Button;
            }

            var bySound = this.elements.DetectRoleBySound(info.Address);
            if (bySound != UiRole.Unknown)
            {
                return bySound;
            }

            if (info.HasFlagBit(GameLayout.Element.TextInputFlagBit))
            {
                return UiRole.TextField;
            }

            if (info.Name.Length > 0 && this.catalog.TryGet(info.Name, out var entry) && entry.Role != UiRole.Unknown)
            {
                return entry.Role;
            }

            if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return UiRole.Button;
            }

            return hasOwnText ? UiRole.Label : UiRole.Panel;
        }

        /// <summary>The caption of a control, or every cell of a focused table row ("Maximum Life, 42").</summary>
        private string RowText(in UiElementReader.ElementInfo info)
        {
            if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit))
            {
                return this.FindDescendantText(info, CaptionSearchDepth, null);
            }

            var cells = new List<string>();
            this.CollectTexts(info, RowCellDepth, cells);
            return cells.Count is > 0 and <= MaxRowCells ? Announcements.Cells(cells) : this.FindDescendantText(info, CaptionSearchDepth, null);
        }

        private void CollectTexts(in UiElementReader.ElementInfo node, int depth, List<string> found)
        {
            if (depth < 0 || found.Count > MaxRowCells)
            {
                return;
            }

            foreach (var childAddress in this.elements.ReadChildren(node))
            {
                if (!this.elements.TryReadElement(childAddress, out var child) || !child.IsVisibleFlag)
                {
                    continue;
                }

                if (this.elements.TryReadSpokenText(childAddress, out var text) && text.Length > 0)
                {
                    found.Add(text);
                }

                this.CollectTexts(child, depth - 1, found);
            }
        }

        /// <summary>Breadth-first search for the first descendant with display text (optionally restricted by control name).</summary>
        private const string PopupSuffix = "_popup";

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
                    if (this.elements.TryReadElement(siblings[i], out var sibling))
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

        /// <summary>Layout ids that only group other elements ("panels_parent", "tabs_container"): they name nothing.</summary>
        private static bool IsStructuralName(string controlName) =>
            StructuralSuffixes.Any(suffix => controlName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

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
