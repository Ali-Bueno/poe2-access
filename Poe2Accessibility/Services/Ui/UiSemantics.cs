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

        /// <summary>The frame of the login's news banner, a picture: only its tooltip ("Opens in a web browser") is text.</summary>
        private const string NewsBannerFrame = "motd_frame";

        /// <summary>A named container right above an unnamed widget names it (news panel inside motd_frame).</summary>
        private const int NamedAncestorHops = 2;

        /// <summary>A focused row reads at most this many cells, from at most this deep (stat rows: cell → text).</summary>
        private const int MaxRowCells = 6;

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
            this.TreeFields = new PassiveTreeFields(reader, elements, record => this.PassiveRow(record) != null, this.GraphIdsOf)
            {
                FileIds = () => PassiveTreeCatalog.Shared.GraphIds,
            };
        }

        /// <summary>The passive tree's state and graph fields, found again after a game update.</summary>
        public PassiveTreeFields TreeFields { get; }

        /// <summary>In game: inventory archetypes and panel titles apply (their fields mean something else pre-game).</summary>
        public bool InGame { get; set; }

        /// <summary>The element whose children are the screen's layers: labels are never looked for past it.</summary>
        public IntPtr LayerContainer { get; set; }

        public AccessibleUiNode? Describe(IntPtr address, bool hovered = false) =>
            this.DescribeElement(address, hovered) is { } node ? this.WithBuildGear(this.WithBuildGems(node, hovered), hovered) : null;

        private AccessibleUiNode? DescribeElement(IntPtr address, bool hovered)
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

            if (this.DescribeEndgameNode(info, hovered) is { } endgameNode)
            {
                return endgameNode;
            }

            if (this.DescribeLink(info) is { } linkNode)
            {
                return linkNode;
            }

            if (this.DescribeSelectableList(info) is { } listNode)
            {
                return listNode;
            }

            if (this.DescribeButtonMenu(info) is { } menuNode)
            {
                return menuNode;
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
            var cardDetails = new List<string>();
            var shopPosition = string.Empty;
            var longestCell = int.MaxValue;
            var cellRow = false;
            string label;
            switch (role)
            {
                case UiRole.ComboBox:
                    value = this.ComboBoxValue(info, ownText);
                    valuePosition = this.ComboValuePosition(info, value);
                    expanded = this.elements.IsComboBoxOpen(info.Address);
                    label = Humanise(info.Name);
                    break;
                case UiRole.TextField:
                    value = ownText.Length > 0 ? ownText : this.FindDescendantText(info, CaptionSearchDepth, null);
                    if (this.catalog.TryGet(info.Name, out var field) && field.IsPassword)
                    {
                        value = Announcements.PasswordLength(value.Length); // never the contents
                    }

                    label = this.FindRowLabel(info);
                    if (label.Length == 0)
                    {
                        // A list right before the field picks where it goes (the chat's channel: "Global", then "#hello").
                        label = this.PrecedingComboValue(info);
                    }

                    if (label.Length == 0)
                    {
                        label = this.Placeholder(input != IntPtr.Zero ? input : address);
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
                    if (this.InGame && this.ShopEntryOf(info) is { } entry)
                    {
                        (label, value, shopPosition) = entry;
                        break;
                    }

                    if (ownText.Length == 0 && this.InGame && !info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && this.CardOf(info) is { } card)
                    {
                        // A trade listing: one label said the seller's status alone ("Jugador desconectado", 2026-10-28).
                        (label, cardDetails) = card;
                        break;
                    }

                    label = ownText.Length > 0 ? ownText : this.RowText(info, out longestCell, out cellRow);
                    if (role == UiRole.Button && label.Length > 0 && this.ButtonRowLabel(info) is { Length: > 0 } rowLabel &&
                        !string.Equals(rowLabel, label, StringComparison.OrdinalIgnoreCase))
                    {
                        // A settings row's button says what it acts on: "Popup Notifications, Edit Settings", not "Edit Settings" alone.
                        label = Announcements.Cells(new[] { rowLabel, label });
                    }

                    if (label.Length == 0 && role == UiRole.Button && this.FollowsTextField(info))
                    {
                        // The icon button at the end of a search box (Cosmetics' "x"): it empties the box.
                        label = Announcements.ClearSearch;
                    }

                    if (label.Length == 0)
                    {
                        label = Humanise(info.Name);
                        namedById = label.Length > 0;
                    }

                    break;
            }

            if (this.InGame && role == UiRole.Button && value.Length == 0)
            {
                value = this.MapDepthValue(info);
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

            if (this.InGame && value.Length == 0 && this.elements.ReadTextureKey(info) is { } art && QuestRewards.FromTextureKey(art) is { } reward)
            {
                // A quest's reward icon inspected with R3: "Recompensa:" labels it, its art says what it is (the crafting
                // bench's RewardCraftingBench.dds, 2026-10-26).
                value = reward;
            }
            else if (this.InGame && value.Length == 0 && this.TrialIcon(info) is { Length: > 0 } trialIcon)
            {
                value = trialIcon; // an affliction, boon or modifier icon of the trials
            }

            if (role == UiRole.Button && ownText.Length == 0 && this.elements.ReadAncestors(info, NamedAncestorHops).Any(ancestor => ancestor.Name == NewsBannerFrame))
            {
                (label, namedById) = (Announcements.NewsBanner, false);
            }

            var tooltip = cardDetails.Count > 0 ? cardDetails : this.TooltipLines(info);
            var context = this.InGame ? this.PanelTitle(info) : string.Empty;
            var sheet = this.InGame && value.Length == 0 && role is UiRole.Panel or UiRole.Unknown ? this.SheetValue(info) : null;
            if (sheet is { } stat)
            {
                value = stat.Value;
            }

            if (sheet?.Attribute is { } attribute)
            {
                (label, namedById) = (Announcements.AttributeName(attribute), false);
            }
            else if ((namedById || (hovered && !cellRow) || label.Length == 0 || (label == context && !cellRow)) && tooltip.Count > 0)
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

            if (label.Length == 0 && this.InGame)
            {
                // Nothing names it and it has no tooltip: an icon-only control showing an item (a picker once a currency is
                // chosen) is that item (docs/research/ui-unseen-panels.md).
                label = this.ArtCaption(info);
            }

            if (this.InGame && role == UiRole.Panel && tooltip.Count > 0 && BareNumber.IsMatch(label))
            {
                // A sheet widget whose caption the game hides in a 5:4 window shows only its number ("513"): its hidden
                // caption names it as at other sizes, else the tooltip's title.
                var caption = this.HiddenCaption(info);
                label = Announcements.LabelValue(caption.Length > 0 ? caption : tooltip[0], label);
                if (caption.Length == 0 || caption == tooltip[0])
                {
                    tooltip.RemoveAt(0);
                }
            }

            var group = string.Empty;
            var (position, listItems) = hovered ? (string.Empty, Array.Empty<string>()) : shopPosition.Length > 0 ? (shopPosition, Array.Empty<string>()) : role is UiRole.Button or UiRole.CheckBox or UiRole.Slider or UiRole.ComboBox or UiRole.TextField or UiRole.Tab
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

            if (role == UiRole.Button && this.InGame && this.TrialRoomPlace(info) is { Length: > 0 } room)
            {
                (label, position) = (label.Length > 0 ? label : Announcements.HiddenRoom, room);
            }
            else if (role == UiRole.Panel && label.Length > MaxCellText && longestCell > MaxCellText)
            {
                // A card of short cells keeps its place: the spirit tab's "Herald of Thunder, …, 30 spirit" is 82 long (2026-11-08).
                position = string.Empty; // a paragraph is no cell: the Trial map's tutorial was read with "columna 1, fila 1" inside (2026-11-07)
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
                PanelNotes = this.InGame && context.Length > 0 && !hovered ? this.PanelNotes(info) : Array.Empty<string>(),
                NamedByControlId = namedById,
                Position = position,
                ListItems = listItems,
                Group = group,
                Tooltip = tooltip,
                IsHovered = hovered,
                OpensWithA = input != IntPtr.Zero,
                IsEnabled = !info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) || !FlagBits.EnabledTrusted || info.HasFlagBit(GameLayout.Element.EnabledFlagBit),
            };
        }

        /// <summary>The longest caption a grid's cell has.</summary>
        private const int MaxCellText = 80;

        /// <summary>Visible text first (login gateway combo shows it in a text field); otherwise the selected entry from the object.</summary>
        private string ComboBoxValue(in UiElementReader.ElementInfo info, string ownText)
        {
            var combo = this.comboBoxes.Read(info.Address);
            var open = this.elements.IsComboBoxOpen(info.Address);
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

        private string PrecedingComboValue(in UiElementReader.ElementInfo field)
        {
            if (!this.elements.TryReadElement(field.Raw.ParentPtr, out var parent))
            {
                return string.Empty;
            }

            var siblings = this.elements.ReadChildren(parent);
            var index = Array.IndexOf(siblings, field.Address);
            return index > 0 && this.elements.TryReadElement(siblings[index - 1], out var before) && before.IsVisibleFlag &&
                this.InferRole(before, false) == UiRole.ComboBox
                ? this.ComboBoxValue(before, string.Empty)
                : string.Empty;
        }

        /// <summary>Whether the sibling right before <paramref name="control"/> is a visible text input.</summary>
        private bool FollowsTextField(in UiElementReader.ElementInfo control)
        {
            if (!this.elements.TryReadElement(control.Raw.ParentPtr, out var parent))
            {
                return false;
            }

            var siblings = this.elements.ReadChildren(parent);
            var index = Array.IndexOf(siblings, control.Address);
            return index > 0 && this.elements.TryReadElement(siblings[index - 1], out var before) && before.IsVisibleFlag &&
                   before.HasFlagBit(GameLayout.Element.TextInputFlagBit);
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

            if (this.elements.HasComboBoxParts(info))
            {
                return UiRole.ComboBox; // unnamed, with a button's click sound: the chat's channel list
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

        /// <summary>Breadth-first search for the first descendant with display text (optionally restricted by control name).</summary>
        private const string PopupSuffix = "_popup";
    }
}
