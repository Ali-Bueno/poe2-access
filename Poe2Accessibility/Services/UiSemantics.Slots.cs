namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using Poe2Accessibility.Models;

    /// <summary>Slots bound to a controller button (skill bar binding) for <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>Slot → frame → glyph holder → glyph image; the skill icon sits at the same depth.</summary>
        private const int SlotSearchDepth = 4;

        private const int MaxSlotNodes = 64;

        /// <summary>Folder of skill and action icons ("…/SkillIcons/GamepadActions/DodgeRoll.dds|…").</summary>
        private const string SkillIconFolder = "/SkillIcons/";

        /// <summary>The skill picker's first cell, a red cross with no text or tooltip: it empties the button (2026-10-24).</summary>
        private const string ClearBindingIcon = "/UIImages/InGame/Cross.dds";

        private static readonly Regex WordStart = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        /// <summary>The numeral the cosmetics' weapon-set toggles draw instead of a caption ("…/Cosmetics/WeaponSetIcon2.dds|…").</summary>
        private static readonly Regex WeaponSetIcon = new(@"/WeaponSetIcon(\d)\.dds", RegexOptions.Compiled);

        /// <summary>
        ///     An in-game control with no caption that shows a controller glyph: the button it binds, then what is
        ///     bound — the skill's name from its dat rows, else the title of the icon's tooltip (built for actions;
        ///     for skills only once hovered), else the icon's file name, else empty ("Set Skill Bind" bar, 2026-10-23).
        /// </summary>
        private AccessibleUiNode? DescribeButtonSlot(in UiElementReader.ElementInfo info)
        {
            if (!this.InGame || this.FindDescendantText(info, CaptionSearchDepth + 1, null).Length > 0)
            {
                return null;
            }

            var button = string.Empty;
            var bound = string.Empty;
            var skill = string.Empty;
            var icon = string.Empty;
            var clears = false;
            var details = new List<string>();
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((info, 0));
            var budget = MaxSlotNodes;
            while (queue.Count > 0 && budget-- > 0 && (button.Length == 0 || (bound.Length == 0 && skill.Length == 0)))
            {
                var (node, depth) = queue.Dequeue();
                if (skill.Length == 0)
                {
                    skill = this.SkillName(node);
                }

                if (this.elements.ReadTextureKey(node) is { } key)
                {
                    if (button.Length == 0 && ControllerGlyphs.FromTextureKey(key) is { Length: > 0 } glyph)
                    {
                        button = glyph;
                    }
                    else if (icon.Length == 0 && key.Contains(SkillIconFolder, StringComparison.Ordinal))
                    {
                        icon = IconName(key);
                    }
                    else
                    {
                        clears |= key.Contains(ClearBindingIcon, StringComparison.Ordinal);
                    }
                }

                if (bound.Length == 0 && this.TooltipLines(node) is { Count: > 0 } tooltip)
                {
                    bound = tooltip[0];
                    details = tooltip.GetRange(1, tooltip.Count - 1);
                }

                if (depth == SlotSearchDepth)
                {
                    continue;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            if (button.Length == 0)
            {
                // A cell of the skill picker (A on a Bind Screen button: every skill and action that can go there,
                // 2026-10-24): no glyph, the skill or action it holds names it. The options' combo boxes are navigable
                // too, with their description as tooltip: they carry the interactive bit, the picker's cells do not.
                // A cell nothing names fell back to a far container's id, "action binding display" (2026-10-24): its icon or
                // "unnamed" with its place is all a sighted player has too.
                var held = skill.Length > 0 ? skill : bound.Length > 0 ? bound : clears ? Announcements.ClearBinding : string.Empty;
                if (held.Length == 0 && this.NearestNamedAncestor(info, RowSearchHops) == GameLayout.InGameUi.SkillPickerName)
                {
                    held = icon.Length > 0 ? icon : Announcements.UnnamedElement;
                }

                return held.Length == 0 || !info.HasFlagBit(GameLayout.Element.NavigableFlagBit) || info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) ||
                       !this.AmongCellsOnly(info)
                    ? null
                    : new AccessibleUiNode
                {
                    Address = info.Address,
                    Name = info.Name,
                    Role = UiRole.Panel,
                    Text = held,
                    Position = this.PickerPosition(info),
                    IsVisible = this.elements.IsVisible(info),
                    Context = this.PanelTitle(info),
                    Tooltip = details,
                };
            }

            return new AccessibleUiNode
            {
                Address = info.Address,
                Name = info.Name,
                Role = UiRole.Panel,
                Text = button,
                Value = skill.Length > 0 ? skill : bound.Length > 0 ? bound : icon.Length > 0 ? icon : Announcements.Empty, // binding reads just the new skill
                IsVisible = this.elements.IsVisible(info),
                Context = this.PanelTitle(info),
                Group = this.SlotRowCaption(info),
                Tooltip = details,
            };
        }

        /// <summary>
        ///     The caption beside a row of slots ("Set de habilidades principal" before the twelve slots of the skill bind
        ///     screen, 2026-10-24): a text among the slot's siblings. Said when the cursor moves into that row.
        /// </summary>
        private string SlotRowCaption(in UiElementReader.ElementInfo slot)
        {
            if (!this.elements.TryReadElement(slot.Raw.ParentPtr, out var row))
            {
                return string.Empty;
            }

            foreach (var sibling in this.elements.ReadChildren(row))
            {
                if (sibling != slot.Address && this.elements.TryReadElement(sibling, out var info) && info.IsVisibleFlag &&
                    this.elements.TryReadSpokenText(sibling, out var caption) && caption.Length > 0)
                {
                    return caption;
                }
            }

            return string.Empty;
        }

        /// <summary>
        ///     A picker cell's column and row (2026-10-24: a first row with the cross and the skills in use, then every
        ///     other skill and action seven to a row, each row group a child of the grid): from the cells' positions.
        /// </summary>
        private string PickerPosition(in UiElementReader.ElementInfo cell)
        {
            if (!this.elements.TryReadElement(cell.Raw.ParentPtr, out var group) || !this.elements.TryReadElement(group.Raw.ParentPtr, out var grid))
            {
                return string.Empty;
            }

            var cells = new List<(IntPtr Address, float X, float Y)>();
            foreach (var groupAddress in this.elements.ReadChildren(grid))
            {
                if (!this.elements.TryReadElement(groupAddress, out var rows) || !rows.IsVisibleFlag)
                {
                    continue;
                }

                foreach (var cellAddress in this.elements.ReadChildren(rows))
                {
                    if (this.elements.TryReadElement(cellAddress, out var other) && other.IsVisibleFlag && other.HasFlagBit(GameLayout.Element.NavigableFlagBit))
                    {
                        cells.Add((cellAddress, MathF.Round(rows.Raw.RelativePosition.X + other.Raw.RelativePosition.X),
                            MathF.Round(rows.Raw.RelativePosition.Y + other.Raw.RelativePosition.Y)));
                    }
                }
            }

            var address = cell.Address;
            var index = cells.FindIndex(other => other.Address == address);
            if (index < 0)
            {
                return string.Empty;
            }

            var (_, x, y) = cells[index];
            var row = cells.Select(other => other.Y).Distinct().Count(top => top < y);
            var column = cells.Count(other => other.Y == y && other.X < x);
            return Announcements.GridPosition(column, row);
        }

        /// <summary>
        ///     A picker's row group holds only cells; the character panel's stat icons sit among texts ("Monje",
        ///     "Resistencias"), which they are not cells of (a resistance read "column 1, row 4" without its value, 2026-10-24).
        /// </summary>
        private bool AmongCellsOnly(in UiElementReader.ElementInfo cell)
        {
            if (!this.elements.TryReadElement(cell.Raw.ParentPtr, out var group))
            {
                return false;
            }

            foreach (var siblingAddress in this.elements.ReadChildren(group))
            {
                if (siblingAddress != cell.Address && this.elements.TryReadElement(siblingAddress, out var sibling) && sibling.IsVisibleFlag &&
                    this.elements.TryReadText(siblingAddress, out var text) && text.Length > 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A caption drawn as an image among the control's children: the weapon-set numeral (2026-10-24).</summary>
        private string DrawnCaption(in UiElementReader.ElementInfo info)
        {
            foreach (var childAddress in this.elements.ReadChildren(info))
            {
                if (this.elements.TryReadElement(childAddress, out var child) && child.IsVisibleFlag &&
                    this.elements.ReadTextureKey(child) is { } key && WeaponSetIcon.Match(key) is { Success: true } match)
                {
                    return Announcements.WeaponSet(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            return string.Empty;
        }

        /// <summary>"Art/…/SkillIcons/GamepadActions/DodgeRoll.dds|0,0,0,0|0" → "Dodge Roll".</summary>
        private static string IconName(string key)
        {
            var file = key[(key.LastIndexOf('/', key.IndexOf('|') is var bar and >= 0 ? bar : key.Length - 1) + 1)..];
            var stem = file[..Math.Max(0, file.IndexOf('.'))];
            return WordStart.Replace(stem, " ");
        }
    }
}
