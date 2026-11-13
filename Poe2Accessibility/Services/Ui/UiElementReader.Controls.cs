namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using Poe2Accessibility.Models;

    /// <summary>Control state reading of <see cref="UiElementReader"/>: roles by sound, visibility, ancestors, sliders, combo boxes, check boxes and section rows.</summary>
    public sealed partial class UiElementReader
    {
        /// <summary>
        ///     Widget kind from the click-sound asset the element carries (validated on the options menu:
        ///     FourCheckBoxPress / FourSliderPress / FourComboBoxPress / FourRegularButtonPress / FourTabButtonPress
        ///     / FourPanelButtonPress). The sound lives at subclass-specific offsets, so the block is scanned.
        /// </summary>
        public UiRole DetectRoleBySound(IntPtr address)
        {
            var block = this.ReadBlock(address, GameLayout.Element.ProbeBytes);
            for (var offset = BaseSize; offset + WStringDecoder.Size <= block.Length; offset += IntPtr.Size)
            {
                if (WStringDecoder.IsHeap(block, offset) == false && block[offset] == 0)
                {
                    continue;
                }

                var text = WStringDecoder.TryDecode(this.reader, block, offset, MaxTextLength, allowEmpty: false);
                if (text == null || !text.Contains("Press.ogg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (text.Contains("CheckBox", StringComparison.OrdinalIgnoreCase))
                {
                    return UiRole.CheckBox;
                }

                if (text.Contains("Slider", StringComparison.OrdinalIgnoreCase))
                {
                    return UiRole.Slider;
                }

                if (text.Contains("ComboBox", StringComparison.OrdinalIgnoreCase))
                {
                    return UiRole.ComboBox;
                }

                if (text.Contains("TabButton", StringComparison.OrdinalIgnoreCase))
                {
                    return UiRole.Tab;
                }

                return UiRole.Button;
            }

            return UiRole.Unknown;
        }

        /// <summary>True when the element and every ancestor carry the visible flag.</summary>
        public bool IsVisible(in ElementInfo info)
        {
            var current = info;
            for (var depth = 0; depth < MaxParentDepth; depth++)
            {
                if (!current.IsVisibleFlag)
                {
                    return false;
                }

                if (current.Raw.ParentPtr == IntPtr.Zero)
                {
                    return true;
                }

                if (!this.TryReadElement(current.Raw.ParentPtr, out current))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Ancestors from the parent upwards, at most <paramref name="maxHops"/>.</summary>
        public List<ElementInfo> ReadAncestors(in ElementInfo info, int maxHops)
        {
            var chain = new List<ElementInfo>();
            var current = info;
            while (chain.Count < maxHops && current.Raw.ParentPtr != IntPtr.Zero && this.TryReadElement(current.Raw.ParentPtr, out current))
            {
                chain.Add(current);
            }

            return chain;
        }

        /// <summary>Numeric state of a slider (GameLayout.Slider); null when unreadable or nonsensical.</summary>
        public (int Value, int Min, int Max)? ReadSliderState(IntPtr address)
        {
            // A slider at its minimum proves nothing (zeros fit nearly anywhere), so it fails the check below; while no
            // slider has proved the fields, the search then took the first place nearby holding a value above a minimum
            // and that number was read (a music volume at 0 focused first). Where the fields were measured it still
            // reads as a slider at its minimum: read there, and nothing is searched or learnt from it.
            if (this.Controls is { } controls && !controls.IsKnown(ControlGroup.Slider) &&
                this.SliderAt(address, controls.Current(ControlGroup.Slider)) is { } resting && resting.Value == resting.Min)
            {
                return resting;
            }

            return this.SliderAt(address, this.Controls?.Shift(ControlGroup.Slider, address, shift => this.SliderAt(address, shift) is { } state && state.Value > state.Min) ?? 0);
        }

        /// <summary>Selected and highlighted indices plus raw 16-byte entries of a combo box (GameLayout.ComboBox); null when the shape is wrong.</summary>
        public (int SelectedIndex, int HighlightIndex, byte[][] Entries, byte[][] ParallelEntries)? ReadComboBoxState(IntPtr address) =>
            this.ComboBoxAt(address, this.ComboBoxShift(address));

        /// <summary>Whether the combo box's list is open (GameLayout.ComboBox.OpenFlagOffset, moved with the other combo fields).</summary>
        public bool IsComboBoxOpen(IntPtr address) =>
            this.reader.TryRead<byte>(address + GameLayout.ComboBox.OpenFlagOffset + this.ComboBoxShift(address), out var open) && open == 1;

        /// <summary>A combo box by its parts, named or not: the drop button, the list's scrollbar and the value field.</summary>
        public bool HasComboBoxParts(in ElementInfo info)
        {
            if (info.Raw.ChildrensPtr.Last.ToInt64() - info.Raw.ChildrensPtr.First.ToInt64() > MaxComboBoxChildren * IntPtr.Size)
            {
                return false;
            }

            var found = 0;
            foreach (var child in this.ReadChildren(info))
            {
                var part = this.TryReadElement(child, out var childInfo) ? Array.IndexOf(ComboBoxParts, childInfo.Name) : -1;
                found |= part >= 0 ? 1 << part : 0;
            }

            return found == (1 << ComboBoxParts.Length) - 1;
        }

        private int? SectionRowAt(IntPtr section, int shift) =>
            this.reader.TryRead<byte>(section + GameLayout.OptionsSection.ActiveOffset + shift, out var active) && active == 1 &&
            this.reader.TryRead<long>(section + GameLayout.OptionsSection.RowIndexOffset + shift, out var row) && row is >= 0 and < MaxSectionRows
                ? (int)row
                : null;

        /// <summary>
        ///     Where this combo's fields are: the group's shift, or the one found on it for a subclass that keeps more fields
        ///     before them (the chat's channel list, 0xC8 further in 0.5.5e). Kept per class once a shift reads sensibly.
        /// </summary>
        private int ComboBoxShift(IntPtr address)
        {
            if (!this.reader.TryRead<IntPtr>(address, out var type) || type == IntPtr.Zero)
            {
                return this.Controls?.Current(ControlGroup.ComboBox) ?? 0;
            }

            if (this.comboBoxShifts.TryGetValue(type, out var known))
            {
                return known;
            }

            var shift = this.Controls?.Shift(ControlGroup.ComboBox, address, at => this.ComboBoxFits(address, at)) ?? 0;
            var fitting = this.ComboBoxFits(address, shift)
                ? shift
                : OffsetSearch.First(shift, SubclassFieldRadius, IntPtr.Size, at => this.ComboBoxFits(address, at), shift - SubclassFieldRadius);
            if (fitting is not { } proven)
            {
                return shift;
            }

            this.comboBoxShifts[type] = proven;
            return proven;
        }

        private (int Value, int Min, int Max)? SliderAt(IntPtr address, int shift)
        {
            if (!this.reader.TryRead<int>(address + GameLayout.Slider.ValueOffset + shift, out var value) ||
                !this.reader.TryRead<int>(address + GameLayout.Slider.MinOffset + shift, out var min) ||
                !this.reader.TryRead<int>(address + GameLayout.Slider.MaxOffset + shift, out var max) ||
                max <= min || value < min || value > max || Math.Abs(min) > MaxSliderMagnitude || Math.Abs(max) > MaxSliderMagnitude)
            {
                return null;
            }

            return (value, min, max);
        }

        private (int SelectedIndex, int HighlightIndex, byte[][] Entries, byte[][] ParallelEntries)? ComboBoxAt(IntPtr address, int shift)
        {
            if (!this.reader.TryRead<long>(address + GameLayout.ComboBox.SelectedIndexOffset + shift, out var index) || index < 0 || index > int.MaxValue)
            {
                return null;
            }

            var entries = this.ReadEntries(address + GameLayout.ComboBox.ItemsVectorOffset + shift);
            if (entries.Length == 0 || index >= entries.Length)
            {
                return null;
            }

            var highlight = this.reader.TryRead<long>(address + GameLayout.ComboBox.HighlightIndexOffset + shift, out var h) && h >= 0 && h < entries.Length
                ? (int)h
                : (int)index;
            var parallel = this.ReadEntries(address + GameLayout.ComboBox.ParallelVectorOffset + shift);
            return ((int)index, highlight, entries, parallel);
        }

        /// <summary>
        ///     A combo box reads sensibly with its fields moved by the shift: the selected entry reads as text and, while it
        ///     is closed, the highlight rests on it. The entries vector has a copy right after it, so the indices are what
        ///     tell the true layout from one moved by a vector.
        /// </summary>
        private bool ComboBoxFits(IntPtr address, int shift) =>
            this.ComboBoxAt(address, shift) is { } state && this.EntryReads(state.Entries[state.SelectedIndex]) &&
            this.reader.TryRead<byte>(address + GameLayout.ComboBox.OpenFlagOffset + shift, out var open) && open <= 1 &&
            (open == 1 || (this.reader.TryRead<long>(address + GameLayout.ComboBox.HighlightIndexOffset + shift, out var highlight) && highlight == state.SelectedIndex));

        /// <summary>One of the entry's two qwords points to a text: a std::wstring or a label (the shapes ComboBoxReader decodes).</summary>
        private bool EntryReads(byte[] entry)
        {
            for (var qword = 0; qword + IntPtr.Size <= entry.Length; qword += IntPtr.Size)
            {
                var pointer = MemoryMarshal.Read<IntPtr>(entry.AsSpan(qword));
                if (pointer != IntPtr.Zero && (!string.IsNullOrWhiteSpace(this.ReadWStringStructAt(pointer)) ||
                    (this.IsElement(pointer) && this.TryReadText(pointer, out var text) && text.Length > 0)))
                {
                    return true;
                }
            }

            return false;
        }

        private byte[][] ReadEntries(IntPtr vectorAddress)
        {
            if (!this.reader.TryRead<GameOffsets.Natives.StdVector>(vectorAddress, out var vector))
            {
                return Array.Empty<byte[]>();
            }

            var bytes = this.reader.ReadStdVector<byte>(vector, GameLayout.ComboBox.MaxEntries * GameLayout.ComboBox.EntryStride);
            if (bytes.Length == 0 || bytes.Length % GameLayout.ComboBox.EntryStride != 0)
            {
                return Array.Empty<byte[]>();
            }

            var entries = new byte[bytes.Length / GameLayout.ComboBox.EntryStride][];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = bytes.AsSpan(i * GameLayout.ComboBox.EntryStride, GameLayout.ComboBox.EntryStride).ToArray();
            }

            return entries;
        }

        /// <summary>Ticked state of a check-box element (GameLayout.Element.CheckBoxStateOffset); null when unreadable.</summary>
        public bool? ReadCheckBoxState(IntPtr address)
        {
            var at = GameLayout.Element.CheckBoxStateOffset + (this.Controls?.Changing(ControlGroup.CheckBox, address, GameLayout.Element.CheckBoxStateOffset, 1,
                (offset, length) => this.reader.ReadBytes(address + offset, length), value => value is 0 or 1) ?? 0);
            return this.reader.TryRead<byte>(address + at, out var state) ? state != 0 : null;
        }

        /// <summary>
        ///     The row an options section highlights while it is its page's active section (GameLayout.OptionsSection);
        ///     null otherwise. Disabled rows are highlighted there too, though the game never focuses them.
        /// </summary>
        public int? ReadSectionRow(IntPtr section) => this.SectionRowAt(section, this.Controls?.Current(ControlGroup.OptionsSection) ?? 0);

        /// <summary>Whether <paramref name="section"/> is an active options section highlighting <paramref name="row"/>, a row the game focused.</summary>
        public bool IsSectionRow(IntPtr section, int row) =>
            this.SectionRowAt(section, this.Controls?.Shift(ControlGroup.OptionsSection, section, at => this.SectionRowAt(section, at) == row) ?? 0) == row;

        /// <summary>Whether the quest map canvas shows the surface (GameLayout.WorldMap); null when unreadable.</summary>
        public bool? ReadSurfaceShown(IntPtr canvas)
        {
            var at = GameLayout.WorldMap.SurfaceShownOffset + (this.Controls?.Changing(ControlGroup.MapDepth, canvas, GameLayout.WorldMap.SurfaceShownOffset, 1,
                (offset, length) => this.reader.ReadBytes(canvas + offset, length), value => value is 0 or 1) ?? 0);
            return this.reader.TryRead<byte>(canvas + at, out var shown) && shown <= 1 ? shown == 1 : null;
        }

        /// <summary>Whether one of the element's sound paths contains <paramref name="part"/> (the quest map's "DepthToggle").</summary>
        public bool HasSound(IntPtr address, string part)
        {
            var block = this.ReadBlock(address, GameLayout.Element.ProbeBytes);
            for (var offset = BaseSize; offset + WStringDecoder.Size <= block.Length; offset += IntPtr.Size)
            {
                if (WStringDecoder.IsHeap(block, offset) == false && block[offset] == 0)
                {
                    continue;
                }

                if (WStringDecoder.TryDecode(this.reader, block, offset, MaxTextLength, allowEmpty: false) is { } text &&
                    text.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) && text.Contains(part, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
