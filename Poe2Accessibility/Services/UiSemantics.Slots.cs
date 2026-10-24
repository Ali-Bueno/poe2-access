namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
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

        private static readonly Regex WordStart = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

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
                // A cell of the skill picker (Y on a bind screen slot: every skill and action that can go there,
                // 2026-10-24): no glyph, the skill or action it holds names it.
                var held = skill.Length > 0 ? skill : bound;
                return held.Length == 0 || !info.HasFlagBit(GameLayout.Element.NavigableFlagBit) ? null : new AccessibleUiNode
                {
                    Address = info.Address,
                    Name = info.Name,
                    Role = UiRole.Panel,
                    Text = held,
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

        /// <summary>"Art/…/SkillIcons/GamepadActions/DodgeRoll.dds|0,0,0,0|0" → "Dodge Roll".</summary>
        private static string IconName(string key)
        {
            var file = key[(key.LastIndexOf('/', key.IndexOf('|') is var bar and >= 0 ? bar : key.Length - 1) + 1)..];
            var stem = file[..Math.Max(0, file.IndexOf('.'))];
            return WordStart.Replace(stem, " ");
        }
    }
}
