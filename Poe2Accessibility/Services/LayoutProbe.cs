namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    ///     Finds the memory layout a game update can move (docs/compatibility.md). Each field is first tried where it was
    ///     measured and only looked for around it when it no longer holds what it must; fields of one structure that
    ///     move together follow their anchor (element base fields follow the control name, label fields the text,
    ///     manager fields the root pointer). Outcomes go to the <see cref="CompatibilityReport"/>.
    /// </summary>
    public sealed class LayoutProbe
    {
        /// <summary>Fields are pointer-aligned.</summary>
        private const int Step = 8;

        /// <summary>How far from where it was measured a field is looked for (a class gaining or losing a few fields).</summary>
        private const int Radius = 0x100;

        private const int ManagerScanBytes = 0x1000;
        private const int MaxLength = 256;

        /// <summary>A text layout is trusted once this many labels read through it.</summary>
        private const int MinTextHits = 5;

        private const int MaxWalkNodes = 3000;
        private const int MaxLeaves = 400;
        private const int MaxNamedNodes = 200;

        /// <summary>Entity names are judged on this many monsters, characters and players; half must read.</summary>
        private const int MinRenders = 15;

        /// <summary>An inconclusive check (a screen without texts, a loading root) is tried again after this long.</summary>
        private const int RetryMs = 10000;

        /// <summary>Control names the game's roots and in-game layers carry (login and in-game dumps, 2026-09/10).</summary>
        private static readonly string[] KnownLayerNames =
        {
            "root_control", "HUD", "map_overlay", "world_overlay", "world_ui_layer_parent", "message_label", "no_controller_overlay", "death_screen",
        };

        /// <summary>
        ///     Subclass fields with nothing to check them by: they sit after the base control, so when it grows they move as
        ///     far as the label text (the groups that can be checked are in <see cref="ControlFields"/>).
        /// </summary>
        private static readonly (int Measured, Action<int> Set)[] TextFollowers =
        {
            (GameLayout.Element.CheckBoxStateOffset, at => GameLayout.Element.CheckBoxStateOffset = at),
            (GameLayout.Element.PanelTitleOffset, at => GameLayout.Element.PanelTitleOffset = at),
            (GameLayout.Element.PlaceholderOffset, at => GameLayout.Element.PlaceholderOffset = at),
            (GameLayout.TabButton.SelectedOffset, at => GameLayout.TabButton.SelectedOffset = at),
            (GameLayout.SelectableList.SelectedIndexOffset, at => GameLayout.SelectableList.SelectedIndexOffset = at),
            (GameLayout.ClassSelector.ClassIdOffset, at => GameLayout.ClassSelector.ClassIdOffset = at),
            (GameLayout.ClassSelector.IndexOffset, at => GameLayout.ClassSelector.IndexOffset = at),
            (GameLayout.SkillSlot.BindingIndexOffset, at => GameLayout.SkillSlot.BindingIndexOffset = at),
            (GameLayout.SkillSlot.SkillDetailsOffset, at => GameLayout.SkillSlot.SkillDetailsOffset = at),
            (GameLayout.PassiveTree.NodeRecordOffset, at => GameLayout.PassiveTree.NodeRecordOffset = at),
            (GameLayout.PassiveTree.CanvasStateOffset, at => GameLayout.PassiveTree.CanvasStateOffset = at),
        };

        private readonly HostMemoryReader reader;
        private readonly UiElementReader elements;
        private readonly CompatibilityReport report;

        // Where each anchor was measured: the shift of an anchor moves its whole group.
        private readonly int measuredName = GameLayout.Element.NameOffset;
        private readonly int measuredFont = GameLayout.Element.FontOffset;
        private readonly int measuredManager = GameLayout.Element.ManagerOffset;
        private readonly int measuredTooltips = GameLayout.Element.TooltipsOffset;
        private readonly int measuredTagged = GameLayout.Element.TextWithTagsOffset;
        private readonly int measuredText = GameLayout.Element.TextOffset;
        private readonly int measuredCopy = GameLayout.Element.TextCopyOffset;
        private readonly int measuredColor = GameLayout.Element.TextColorOffset;
        private readonly int measuredRoot = GameLayout.UiManager.RootOffset;
        private readonly int measuredCurrent = GameLayout.UiManager.CurrentOffset;
        private readonly int measuredLastFocused = GameLayout.UiManager.LastFocusedOffset;
        private readonly int measuredHovered = GameLayout.UiManager.HoveredOffset;
        private readonly int measuredPad = GameLayout.UiManager.PadStateOffset;
        private readonly int measuredRenderName = GameLayout.RenderComponent.NameOffset;

        private bool namesKnown;
        private bool textKnown;
        private bool managerKnown;
        private bool renderKnown;
        private long namesTriedAt = long.MinValue / 2;
        private long textTriedAt = long.MinValue / 2;

        public LayoutProbe(HostMemoryReader reader, UiElementReader elements, CompatibilityReport report)
        {
            this.reader = reader;
            this.elements = elements;
            this.report = report;
        }

        /// <summary>Control names: the root or its layers must carry a known name (<paramref name="conclusive"/>: in game, where the HUD always does).</summary>
        public void ProbeNames(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.namesKnown || root == IntPtr.Zero || now - this.namesTriedAt < RetryMs)
            {
                return;
            }

            this.namesTriedAt = now;
            if (OffsetSearch.First(this.measuredName, Radius, Step, this.NameFits(root), Step) is not { } found)
            {
                if (conclusive)
                {
                    this.report.Missing("element names");
                }

                return;
            }

            var shift = found - this.measuredName;
            GameLayout.Element.NameOffset = found;
            GameLayout.Element.FontOffset = this.measuredFont + shift;
            GameLayout.Element.ManagerOffset = this.measuredManager + shift;
            GameLayout.Element.TooltipsOffset = this.measuredTooltips + shift;
            this.namesKnown = true;
            this.Record("element names", this.measuredName, found);
        }

        /// <summary>Label texts: the tagged text and two equal stripped copies (<see cref="UiElementReader.TryReadText(IntPtr, out string)"/>).</summary>
        public void ProbeText(IntPtr root, bool conclusive)
        {
            var now = Environment.TickCount64;
            if (this.textKnown || root == IntPtr.Zero || now - this.textTriedAt < RetryMs)
            {
                return;
            }

            this.textTriedAt = now;
            if (this.Leaves(root).Where(leaf => this.elements.TryReadText(leaf, out var text) && text.Length > 0).Take(MinTextHits).Count() >= MinTextHits)
            {
                this.textKnown = true;
                this.report.Found("element texts");
                return;
            }

            if (OffsetSearch.First(0, Radius, Step, this.TextFits(root), -Radius) is not { } found)
            {
                if (conclusive)
                {
                    this.report.Missing("element texts");
                }

                return;
            }

            GameLayout.Element.TextWithTagsOffset = this.measuredTagged + found;
            GameLayout.Element.TextOffset = this.measuredText + found;
            GameLayout.Element.TextCopyOffset = this.measuredCopy + found;
            GameLayout.Element.TextColorOffset = this.measuredColor + found;
            foreach (var (measured, set) in TextFollowers)
            {
                set(measured + found);
            }

            this.textKnown = true;
            this.Record("element texts", this.measuredText, this.measuredText + found);
        }

        /// <summary>
        ///     A UI manager holds <paramref name="root"/> (the root of elements that point at it) where measured, or the slot
        ///     that holds it is adopted and the focus, hover and pad fields follow it.
        /// </summary>
        public bool AdoptManager(IntPtr manager, IntPtr root)
        {
            if (root == IntPtr.Zero || !this.reader.IsValidAddress(manager) || this.elements.IsElement(manager))
            {
                return false;
            }

            if (this.reader.TryRead<IntPtr>(manager + GameLayout.UiManager.RootOffset, out var held) && held == root)
            {
                if (!this.managerKnown)
                {
                    this.managerKnown = true;
                    this.report.Found("UI manager");
                }

                return true;
            }

            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            if (OffsetSearch.First(this.measuredRoot, ManagerScanBytes, Step, offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root) is not { } found)
            {
                return false;
            }

            var shift = found - this.measuredRoot;
            GameLayout.UiManager.RootOffset = found;
            GameLayout.UiManager.CurrentOffset = this.measuredCurrent + shift;
            GameLayout.UiManager.LastFocusedOffset = this.measuredLastFocused + shift;
            GameLayout.UiManager.HoveredOffset = this.measuredHovered + shift;
            GameLayout.UiManager.PadStateOffset = this.measuredPad + shift;
            this.managerKnown = true;
            this.report.Moved("UI manager", this.measuredRoot, found);
            return true;
        }

        /// <summary>Entity names (Render component): judged on monsters, characters and players, half of which must read.</summary>
        public void ProbeRenderNames(IReadOnlyList<IntPtr> renders)
        {
            if (this.renderKnown || renders.Count < MinRenders)
            {
                return;
            }

            int Named(int offset) => renders.Count(render => this.elements.ReadWStringStructAt(render + offset) is { Length: > 0 } name && name.Any(char.IsLetter));
            bool Fits(int offset) => Named(offset) * 2 >= renders.Count;
            this.renderKnown = true; // judged once per game: a wrong guess must not flap
            if (OffsetSearch.First(this.measuredRenderName, Radius, Step, Fits, Step) is not { } found)
            {
                this.report.Missing("entity names");
                return;
            }

            GameLayout.RenderComponent.NameOffset = found;
            this.Record("entity names", this.measuredRenderName, found);
        }

        /// <summary>A new game process: everything is checked again from the measured layout.</summary>
        /// <summary>How far the label text fields are from where they were measured (the base control's growth, most likely).</summary>
        public int TextShift => GameLayout.Element.TextOffset - this.measuredText;

        public void Reset()
        {
            (this.namesKnown, this.textKnown, this.managerKnown, this.renderKnown) = (false, false, false, false);
            this.namesTriedAt = this.textTriedAt = long.MinValue / 2;
        }

        /// <summary>
        ///     Research (dev command "layout"): every search started off target (a field moved by <see cref="SelfTestShift"/>)
        ///     must come back to where the layout is now; "FAIL" names a search that would not survive such an update.
        /// </summary>
        public string SelfTest(IntPtr root, IntPtr manager)
        {
            var names = OffsetSearch.First(GameLayout.Element.NameOffset + SelfTestShift, Radius, Step, this.NameFits(root), Step);
            var textShift = GameLayout.Element.TextOffset - this.measuredText;
            var text = OffsetSearch.First(textShift + SelfTestShift, Radius, Step, this.TextFits(root), -Radius);
            var slots = this.reader.ReadArray<IntPtr>(manager, ManagerScanBytes / IntPtr.Size);
            var rootSlot = OffsetSearch.First(GameLayout.UiManager.RootOffset + SelfTestShift, ManagerScanBytes, Step,
                offset => offset / IntPtr.Size < slots.Length && slots[offset / IntPtr.Size] == root);
            return string.Join("; ", SelfTestLine("element names", GameLayout.Element.NameOffset, names), SelfTestLine("element texts", textShift, text),
                SelfTestLine("UI manager", GameLayout.UiManager.RootOffset, rootSlot));
        }

        /// <summary>How far a field is pretended to have moved in <see cref="SelfTest"/>.</summary>
        public const int SelfTestShift = 0x18;

        public static string SelfTestLine(string part, int now, int? found) =>
            found == now ? $"{part} ok" : $"{part} FAIL (found {(found is { } at ? $"0x{at:X}" : "nothing")}, is 0x{now:X})";

        /// <summary>The root or one of its layers carries a known control name at the offset.</summary>
        private Func<int, bool> NameFits(IntPtr root)
        {
            // In game the named layers sit one level lower than at login (root, layer container, layers).
            var nodes = new List<IntPtr> { root };
            if (this.elements.TryReadElement(root, out var rootInfo))
            {
                foreach (var child in this.elements.ReadChildren(rootInfo))
                {
                    nodes.Add(child);
                    if (nodes.Count < MaxNamedNodes && this.elements.TryReadElement(child, out var childInfo))
                    {
                        nodes.AddRange(this.elements.ReadChildren(childInfo).Take(MaxNamedNodes - nodes.Count));
                    }
                }
            }

            var blocks = nodes.Select(node => this.reader.ReadBytes(node, this.measuredName + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return offset => blocks.Any(block => WStringDecoder.TryDecode(this.reader, block, offset, MaxLength, false) is { } name && KnownLayerNames.Contains(name));
        }

        /// <summary>Enough labels under the root read through the text fields moved by the shift.</summary>
        private Func<int, bool> TextFits(IntPtr root)
        {
            var blocks = this.Leaves(root).Take(MaxLeaves).Select(leaf => this.reader.ReadBytes(leaf, this.measuredCopy + Radius + WStringDecoder.Size)).Where(block => block.Length > 0).ToList();
            return shift => blocks.Count(block => this.TextAt(block, shift)) >= MinTextHits;
        }

        private bool TextAt(byte[] block, int shift)
        {
            var tagged = WStringDecoder.TryDecode(this.reader, block, this.measuredTagged + shift, MaxLength, true);
            var text = WStringDecoder.TryDecode(this.reader, block, this.measuredText + shift, MaxLength, false);
            return tagged != null && text != null && text == WStringDecoder.TryDecode(this.reader, block, this.measuredCopy + shift, MaxLength, false);
        }

        /// <summary>Childless elements under <paramref name="root"/>, breadth first and lazily, hidden ones too (their texts stay).</summary>
        private IEnumerable<IntPtr> Leaves(IntPtr root)
        {
            var queue = new Queue<IntPtr>();
            queue.Enqueue(root);
            for (var walked = 0; queue.Count > 0 && walked < MaxWalkNodes; walked++)
            {
                if (!this.elements.TryReadElement(queue.Dequeue(), out var info))
                {
                    continue;
                }

                var children = this.elements.ReadChildren(info);
                if (children.Length == 0)
                {
                    yield return info.Address;
                }

                foreach (var child in children)
                {
                    queue.Enqueue(child);
                }
            }
        }

        private void Record(string part, int measured, int found)
        {
            if (found == measured)
            {
                this.report.Found(part);
            }
            else
            {
                this.report.Moved(part, measured, found);
            }
        }
    }
}
