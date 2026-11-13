namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using GameReader.Plugin;
    using Poe2Accessibility.Models;

    /// <summary>Layer capture of <see cref="ScreenWatcher"/>: walking one layer of the element tree into texts, and the checks the walk needs.</summary>
    public sealed partial class ScreenWatcher
    {
        private ScreenLayer CaptureLayer(UiElementReader.ElementInfo layerInfo, int index, RectangleF screen, PluginUiElementReflection.RectCache rects, ref int budget)
        {
            // The walk's working lists serve every layer of every snapshot: the layer returned holds a copy of its texts.
            var (nodes, texts, glyphs, units, setIcons, stack) = (this.walkNodes, this.walkTexts, this.walkGlyphs, this.walkUnits, this.walkSetIcons, this.walkStack);
            nodes.Clear();
            texts.Clear();
            glyphs.Clear();
            units.Clear();
            setIcons.Clear();
            stack.Clear();
            var hasControls = false;
            var (tabBars, tabBarEnds) = (this.walkTabBars, this.walkTabBarEnds);
            tabBars.Clear();
            tabBarEnds.Clear();
            bool isHintBar;
            stack.Push((layerInfo, -1, 0, -1));
            byte[]? walked = null;
            while (stack.Count > 0 && budget-- > 0)
            {
                // The element before this one is done with: its text was decoded into strings, its block reads no more.
                this.blocks.Return(walked);
                var (info, parent, childIndex, control) = stack.Pop();
                walked = info.Block;
                var above = parent >= 0 ? nodes[parent] : new WalkNode(-1, 0, -1, 0, 0, -1, true);
                var self = nodes.Count;
                var enabled = above.Enabled;
                // A control slid out of view does not make its layer one to use: the login screen's "Deal of the Day"
                // panel waits off screen with its "buy now!" button and hid the screen in front (fresh install, 2026-10-24).
                if (info.HasFlagBit(GameLayout.Element.InteractiveFlagBit) && !IsOffScreen(info.Address, screen, rects))
                {
                    hasControls = true;
                    control = self;
                    enabled = !FlagBits.EnabledTrusted || info.HasFlagBit(GameLayout.Element.EnabledFlagBit);
                }

                if (parent >= 0 && TabBar.IsPart(info.Name, childIndex, tabBars.Contains(parent)))
                {
                    tabBars.Add(parent); // once for its bar, once for its pages: a tab control, whose later children hold its end buttons
                }

                var position = info.Raw.RelativePosition;
                nodes.Add(new WalkNode(parent, childIndex, above.Depth + 1, above.X + position.X, above.Y + position.Y, control, enabled));
                var children = this.elements.ReadChildren(info, ref this.walkChildren);
                var hasText = this.elements.TryReadText(info, out var text, out var tagged);
                if (!hasText && this.capturedLongText == IntPtr.Zero && info.Raw.UnscaledSize.Y >= LongTextMinHeight && this.elements.IsLongText(info.Address))
                {
                    this.capturedLongText = info.Address; // read a paragraph at a time as it scrolls, not with the screen
                }
                if (hasText && ControllerGlyphs.ForOtherPlayer(tagged))
                {
                    continue; // a second controller's prompt and its children ("P2")
                }

                var markupButton = hasText ? ControllerGlyphs.FromMarkup(tagged) : string.Empty;
                // A chord's icons joined by a plus ("<<rt>>+<<lstick>>", stripped " + ") are an icon too: the label beside
                // them is the legend (it was read as content and repeated the inventory's focus, 2026-10-24).
                var glyphsOnly = markupButton.Length > 0 && text.Trim().Trim('+').Trim().Length == 0;
                if (hasText && text.Length > 0 && IsOffScreen(info.Address, screen, rects))
                {
                    continue; // a panel slid out of view keeps its visible flag (the world map's quest list)
                }

                if (hasText && markupButton.Length > 0 && tagged.TrimStart().StartsWith("<<", StringComparison.Ordinal) &&
                    ControllerGlyphs.OnlyLeadingButtons(tagged) && text.Trim().TrimStart('+').Trim() is { Length: > 0 } legendLabel)
                {
                    // A text that starts with its buttons is a legend ("<<rt>> + <<lstick>> Move" beside the character
                    // preview): label and buttons, read with the hints, not as content of the panel.
                    texts.Add((self, new ScreenText(info.Address, Announcements.Flatten(legendLabel), info.Name, false, true, markupButton)));
                }
                else if (hasText && text.Length > 0 && !glyphsOnly)
                {
                    // Glyphs inside a sentence are read in place; a lone trailing glyph ("continue {A}") the same way.
                    var spoken = Announcements.Flatten(markupButton.Length > 0 ? ControllerGlyphs.Render(tagged) : text);
                    var isCaption = control >= 0 && nodes[self].Depth - nodes[control].Depth <= ControlCaptionHops;
                    texts.Add((self, new ScreenText(info.Address, spoken, info.Name, isCaption, !isCaption || enabled)));
                }
                else if (markupButton.Length > 0)
                {
                    // A text holding only glyph markup is an icon (panel legends): it labels its neighbour.
                    glyphs.Add((self, markupButton));
                }
                else if (this.TextureKeyOf(info) is { } key)
                {
                    if (ControllerGlyphs.FromTextureKey(key) is { } end && parent >= 0 && TabBar.IsControl(tabBars.Count(seen => seen == nodes[parent].Parent)))
                    {
                        // The buttons at the two ends of a tab bar switch tabs: a legend of their own, not the label of
                        // the tab or heading nearest in the tree ("Entrada, RB" was said of the Options, 2026-11-10).
                        var bar = nodes[parent].Parent;
                        texts.Add((self, new ScreenText(info.Address, TabBar.EndLegend(tabBarEnds.Count(seen => seen == bar)), info.Name, false, true, end)));
                        tabBarEnds.Add(bar);
                    }
                    else if (ControllerGlyphs.FromTextureKey(key) is { } glyph)
                    {
                        glyphs.Add((self, this.HasChildNamed(this.walkChildren, children, GameLayout.InGameUi.HoldProgressName) ? Announcements.HoldButton(glyph) : glyph));
                    }
                    else if (WeaponSetRows.FromTextureKey(key) is { } set)
                    {
                        setIcons.Add((self, set));
                    }
                    else if (CurrencyIcons.FromTextureKey(key) is { } unit)
                    {
                        units.Add((self, unit));
                    }
                    else if (QuestRewards.FromTextureKey(key) is { } reward && !IsOffScreen(info.Address, screen, rects))
                    {
                        // An icon that says what a quest gives ("Recompensa:" then a support gem and gold).
                        texts.Add((self, new ScreenText(info.Address, reward, info.Name, false, true)));
                    }
                }

                if (nodes[self].Depth >= MaxDepth)
                {
                    continue;
                }

                for (var i = children - 1; i >= 0; i--)
                {
                    if (!this.elements.TryReadElement(this.walkChildren[i], this.blocks, out var child))
                    {
                        continue;
                    }

                    if (child.IsVisibleFlag && !IsIgnored(child.Name))
                    {
                        stack.Push((child, self, i, control));
                    }
                    else
                    {
                        this.blocks.Return(child.Block);
                    }
                }
            }

            // What the budget left on the stack keeps its blocks: lost to the collector, never handed out twice.
            this.blocks.Return(walked);
            PairGlyphs(nodes, texts, glyphs);
            PairUnits(nodes, texts, units);
            PairWeaponSets(nodes, texts, setIcons);
            MergeRows(nodes, texts);
            // A legend layer holds nothing but legends; a menu layer may carry its own hint bar (game menu).
            isHintBar = texts.Count > 0 && texts.TrueForAll(text => IsLegend(text.Text));
            return new ScreenLayer(layerInfo.Address, index, texts.Select(text => text.Text).ToList(), hasControls, isHintBar, Name: layerInfo.Name);
        }

        private static bool IsOffScreen(IntPtr element, RectangleF screen, PluginUiElementReflection.RectCache rects) =>
            !screen.IsEmpty && PluginUiElementReflection.TryGetAbsoluteRect(element, rects, out var position, out var size) &&
            !screen.IntersectsWith(new RectangleF(position.X, position.Y, size.X, size.Y));

        /// <summary>A hint-bar entry label or a text paired with its button glyph.</summary>
        public static bool IsLegend(ScreenText text) => text.Name == HintEntryName || text.Button.Length > 0;

        /// <summary>Art key of an image element; texture keys are cached (shared resources).</summary>
        private bool HasChildNamed(IntPtr[] children, int count, string name)
        {
            for (var i = 0; i < count; i++)
            {
                if (this.elements.TryReadElement(children[i], this.blocks, out var info))
                {
                    this.blocks.Return(info.Block); // only its name is looked at
                    if (info.Name == name)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string? TextureKeyOf(in UiElementReader.ElementInfo info)
        {
            if (info.Texture == IntPtr.Zero)
            {
                return null;
            }

            var now = Environment.TickCount64;
            if (!this.textureKeys.TryGetValue(info.Texture, out var known) || now - known.At >= TextureKeyKeepMs)
            {
                known = (this.elements.ReadTextureKey(info.Texture), now);
                this.textureKeys[info.Texture] = known;
            }

            return known.Key;
        }
    }
}
