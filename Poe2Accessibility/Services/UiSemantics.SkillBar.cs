namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>The HUD skill bar read on request (D2R skill bar readout) for <see cref="UiSemantics"/>.</summary>
    public sealed partial class UiSemantics
    {
        /// <summary>The bind screen nests its bars a few levels deep; its glyph sits within a slot's grandparent.</summary>
        private const int BindSearchDepth = 12;

        private const int MaxBindNodes = 4000;

        private const int MaxGlyphNodes = 64;

        private const int MaxSkillNameChars = 128;

        /// <summary>Binding index → button and set index → set title, from the hidden bind screen.</summary>
        private (IntPtr Display, Dictionary<int, string> Buttons, string[] Titles)? bindTable;

        /// <summary>One set of the bar in screen order: its title and each binding's button and what it fires ("" = empty).</summary>
        public sealed record SkillSet(string Title, IReadOnlyList<(string Button, string Bound)> Bindings);

        /// <summary>The bar's primary and secondary sets; empty when the in-game bar is not found.</summary>
        public List<SkillSet> ReadSkillBar(IntPtr layerContainer)
        {
            var sets = new List<SkillSet>();
            if (!this.InGameElement(layerContainer, InGameField.SkillBar, out var bar))
            {
                return sets;
            }

            // The slot class is taken from the first slot at run time (its vtable), never hard-coded.
            var slots = this.elements.ReadChildren(bar).Skip(1).Take(GameLayout.SkillSlot.ButtonsPerSet).ToList();
            var first = slots.Count > 0 && this.elements.TryReadElement(slots[0], out var firstFrame)
                ? this.elements.ReadChildren(firstFrame).FirstOrDefault()
                : IntPtr.Zero;
            if (first == IntPtr.Zero || !this.reader.TryRead<IntPtr>(first, out var slotClass) || slotClass == IntPtr.Zero)
            {
                return sets;
            }

            var slotElements = slots.SelectMany(frame => this.elements.TryReadElement(frame, out var info) ? this.elements.ReadChildren(info) : Array.Empty<IntPtr>())
                .Where(slot => this.reader.TryRead<IntPtr>(slot, out var type) && type == slotClass)
                .ToList();
            var bindingAt = this.BindingOffset(slotElements);
            var table = this.BindTable(layerContainer, slotClass, false);
            var bindings = new List<(int Set, string Button, string Bound)>();
            foreach (var slotAddress in slotElements)
            {
                if (!this.elements.TryReadElement(slotAddress, out var slot) ||
                    !this.reader.TryRead<int>(slotAddress + bindingAt, out var index) ||
                    index < 0 || index >= 2 * GameLayout.SkillSlot.ButtonsPerSet)
                {
                    continue;
                }

                if (table != null && !table.Value.Buttons.ContainsKey(index))
                {
                    table = this.BindTable(layerContainer, slotClass, true); // rebuilt once (pad family changed)
                }

                if (table?.Buttons.GetValueOrDefault(index) is { Length: > 0 } button)
                {
                    bindings.Add((index / GameLayout.SkillSlot.ButtonsPerSet, button, this.BoundName(slot)));
                }
            }

            for (var set = 0; set < 2; set++)
            {
                var inSet = bindings.Where(binding => binding.Set == set).Select(binding => (binding.Button, binding.Bound)).ToList();
                if (inSet.Count > 0)
                {
                    var title = table?.Titles[set] is { Length: > 0 } shown ? shown : Announcements.SkillSetTitle(set);
                    sets.Add(new SkillSet(title, inSet));
                }
            }

            return sets;
        }

        /// <summary>What a slot fires: the skill's name, else the action's tooltip title, else its icon's name; "" when empty.</summary>
        private string BoundName(in UiElementReader.ElementInfo slot)
        {
            if (this.SkillName(slot) is { Length: > 0 } skill)
            {
                return skill;
            }

            // Actions (dodge, flasks, interact) carry no skill; their tooltip is built without a hover.
            if (this.TooltipLines(slot) is { Count: > 0 } tooltip)
            {
                return tooltip[0];
            }

            return this.elements.ReadTextureKey(slot) is { } key && key.Contains(SkillIconFolder, StringComparison.Ordinal)
                ? IconName(key)
                : string.Empty;
        }

        /// <summary>
        ///     The localized name of the skill on a slot, from its icon's ActiveSkillDetails through three dat rows,
        ///     each trusted only when its key names the expected table (skill tooltips stay empty until hovered).
        /// </summary>
        private string SkillName(in UiElementReader.ElementInfo slot)
        {
            foreach (var child in this.elements.ReadChildren(slot))
            {
                if (child != IntPtr.Zero && this.SkillLevelRow(child, this.SkillDetailsShift(child)) is { } level &&
                    this.DatRow(level + GameLayout.SkillSlot.EffectKeyOffset, GameLayout.SkillSlot.EffectTable) is { } effect &&
                    this.DatRow(effect + GameLayout.SkillSlot.SkillKeyOffset, GameLayout.SkillSlot.SkillTable) is { } skill &&
                    this.reader.TryRead<IntPtr>(skill + GameLayout.SkillSlot.DisplayedNameOffset, out var name) &&
                    this.elements.ReadUtf16BufferAt(name, MaxSkillNameChars) is { Length: > 0 } text)
                {
                    return text;
                }
            }

            return string.Empty;
        }

        /// <summary>The binding index field, judged on the skill bar's slots: both sets' indices, each once.</summary>
        private int BindingOffset(IReadOnlyList<IntPtr> slots)
        {
            bool Distinct(int shift)
            {
                var seen = new HashSet<int>();
                return slots.All(slot => this.reader.TryRead<int>(slot + GameLayout.SkillSlot.BindingIndexOffset + shift, out var index) &&
                    index >= 0 && index < 2 * GameLayout.SkillSlot.ButtonsPerSet && seen.Add(index));
            }

            var shift = slots.Count >= GameLayout.SkillSlot.ButtonsPerSet
                ? this.elements.Controls?.Shift(ControlGroup.SkillBinding, slots[0], Distinct)
                : this.elements.Controls?.Current(ControlGroup.SkillBinding);
            return GameLayout.SkillSlot.BindingIndexOffset + (shift ?? 0);
        }

        /// <summary>The skill details field of a slot's icon, judged where it leads to a skill level row (an empty slot proves nothing).</summary>
        private int SkillDetailsShift(IntPtr icon) =>
            this.elements.Controls?.Shift(ControlGroup.SkillDetails, icon, shift => this.SkillLevelRow(icon, shift) != null) ?? 0;

        private IntPtr? SkillLevelRow(IntPtr icon, int shift) =>
            this.reader.TryRead<IntPtr>(icon + GameLayout.SkillSlot.SkillDetailsOffset + shift, out var details) && details != IntPtr.Zero
                ? this.DatRow(details + GameLayout.SkillSlot.LevelKeyOffset, GameLayout.SkillSlot.LevelTable)
                : null;

        /// <summary>The row of a dat foreign key (row, file), only when the file is the expected table.</summary>
        private IntPtr? DatRow(IntPtr key, string table) =>
            this.reader.TryRead<IntPtr>(key, out var row) && row != IntPtr.Zero &&
            this.reader.TryRead<IntPtr>(key + GameLayout.SkillSlot.KeyFileOffset, out var file) && file != IntPtr.Zero &&
            string.Equals(this.elements.ReadWStringStructAt(file + GameLayout.SkillSlot.FileNameOffset), table, StringComparison.OrdinalIgnoreCase)
                ? row
                : null;

        /// <summary>
        ///     Each binding's button from the hidden bind screen: a slot element there (same class, same index) has its
        ///     button glyph within its grandparent, and its bar's first child holds the set's title. Cached per screen.
        /// </summary>
        private (IntPtr Display, Dictionary<int, string> Buttons, string[] Titles)? BindTable(IntPtr layerContainer, IntPtr slotClass, bool rebuild)
        {
            if (!this.InGameElement(layerContainer, InGameField.BindDisplay, out var display))
            {
                return null;
            }

            if (!rebuild && this.bindTable is { } cached && cached.Display == display.Address)
            {
                return cached;
            }

            var buttons = new Dictionary<int, string>();
            var titles = new string[2];
            var queue = new Queue<(UiElementReader.ElementInfo Node, int Depth)>();
            queue.Enqueue((display, 0));
            var budget = MaxBindNodes;
            while (queue.Count > 0 && budget-- > 0)
            {
                var (node, depth) = queue.Dequeue();
                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (!this.elements.TryReadElement(childAddress, out var child))
                    {
                        continue;
                    }

                    if (this.reader.TryRead<IntPtr>(childAddress, out var type) && type == slotClass)
                    {
                        this.ReadBindSlot(child, buttons, titles);
                    }
                    else if (depth < BindSearchDepth)
                    {
                        queue.Enqueue((child, depth + 1));
                    }
                }
            }

            this.bindTable = (display.Address, buttons, titles);
            return this.bindTable;
        }

        private void ReadBindSlot(in UiElementReader.ElementInfo slot, Dictionary<int, string> buttons, string[] titles)
        {
            if (!this.reader.TryRead<int>(slot.Address + GameLayout.SkillSlot.BindingIndexOffset + (this.elements.Controls?.Current(ControlGroup.SkillBinding) ?? 0), out var index) ||
                index < 0 || index >= 2 * GameLayout.SkillSlot.ButtonsPerSet ||
                !this.elements.TryReadElement(slot.Raw.ParentPtr, out var parent) ||
                !this.elements.TryReadElement(parent.Raw.ParentPtr, out var frame))
            {
                return;
            }

            var stack = new Stack<UiElementReader.ElementInfo>();
            stack.Push(frame);
            var budget = MaxGlyphNodes;
            while (stack.Count > 0 && budget-- > 0)
            {
                var node = stack.Pop();
                if (this.elements.ReadTextureKey(node) is { } key && ControllerGlyphs.FromTextureKey(key) is { Length: > 0 } glyph)
                {
                    buttons[index] = glyph;
                    break;
                }

                foreach (var childAddress in this.elements.ReadChildren(node))
                {
                    if (this.elements.TryReadElement(childAddress, out var child))
                    {
                        stack.Push(child);
                    }
                }
            }

            var set = index / GameLayout.SkillSlot.ButtonsPerSet;
            if (string.IsNullOrEmpty(titles[set]) && this.elements.TryReadElement(frame.Raw.ParentPtr, out var bar) &&
                this.elements.ReadChildren(bar).FirstOrDefault() is var titleAddress && titleAddress != IntPtr.Zero &&
                this.elements.TryReadElement(titleAddress, out var title))
            {
                titles[set] = this.FindDescendantText(title, CaptionSearchDepth, null);
            }
        }

        /// <summary>An in-game element the layer container points to by field, trusted only by its control name.</summary>
        /// <summary>The in-game layers by what they hold (set by the plugin).</summary>
        public InGameFields? Fields { get; set; }

        private bool InGameElement(IntPtr layerContainer, InGameField field, out UiElementReader.ElementInfo info)
        {
            info = default;
            return this.Fields?.Find(layerContainer, field) is { } address && address != IntPtr.Zero && this.elements.TryReadElement(address, out info);
        }
    }
}
