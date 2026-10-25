namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.UiElement;
    using Poe2Accessibility.Models;

    /// <summary>
    ///     Cheap, validated reads of single UI elements for live use (focus tracking), as opposed to the
    ///     research-oriented UiTreeReader that probes whole trees. Offsets come from GameLayout.
    /// </summary>
    public sealed class UiElementReader
    {
        private const int MaxChildrenPerNode = 4096;
        private const int MaxTextLength = 1024;

        /// <summary>The longest text read whole on request (the terms of use hold about 60 000 characters).</summary>
        private const int MaxLongTextLength = 1 << 18;
        private const int MaxParentDepth = 64;

        /// <summary>
        ///     Option sliders run 0..1000 at most (2026-10). A slider resting at its minimum proves nothing about where its
        ///     fields are: next to it, zeros followed by a positive int read as a slider at its minimum too.
        /// </summary>
        private const int MaxSliderMagnitude = 1000000;

        /// <summary>Warning red is a strong red channel with little green and blue (the price red is E4,00,00).</summary>
        private const uint MinWarningRed = 0xC0;
        private const uint MaxWarningGreenBlue = 0x40;

        /// <summary>
        ///     Grey text: a heard dialogue topic is FF606060, FF909090 while focused; new topics are beige FFE3D7A6 and
        ///     white FFFFFFF9 focused (Una, 2026-10-24).
        /// </summary>
        private const uint MaxGrey = 0xA0;

        private const uint MinGrey = 0x20;

        private const int GreyTolerance = 4;

        /// <summary>Host reads are all-or-nothing; objects are read in page-sized chunks so a short tail is tolerated.</summary>
        private const int ChunkBytes = 0x100;

        /// <summary>How far a combo subclass's fields are looked for from the group's (the chat's channel list: 0xC8).</summary>
        private const int SubclassFieldRadius = 0x100;

        /// <summary>More rows than an options section holds (Gráficos' sections have 12 at most, 2026-10-25).</summary>
        private const int MaxSectionRows = 256;

        /// <summary>A combo box holds its parts and a label or two (four children seen).</summary>
        private const int MaxComboBoxChildren = 8;

        /// <summary>Control names of a combo box's parts (gateway and language lists, the chat's channel list; 0.5.5e).</summary>
        private static readonly string[] ComboBoxParts = { "button", "scrollbar", "textField" };

        private static readonly int BaseSize = Unsafe.SizeOf<UiElementBaseOffset>();

        /// <summary>Enough bytes to cover the text fields of the label subclass (0x538 + 32 as measured; they can move).</summary>
        private static int TextBlockSize => GameLayout.Element.TextCopyOffset + WStringDecoder.Size;

        /// <summary>The base struct, and the control name wherever it lies now.</summary>
        private static int ElementBlockSize => Math.Max(Math.Max(BaseSize, GameLayout.Element.NameOffset + WStringDecoder.Size),
            Math.Max(GameLayout.Image.TextureOffset, GameLayout.Image.AltTextureOffset) + IntPtr.Size);

        private readonly HostMemoryReader reader;

        /// <summary>Combo field shift per element class (vtable), once proven on one of its controls.</summary>
        private readonly Dictionary<IntPtr, int> comboBoxShifts = new();

        public UiElementReader(HostMemoryReader reader)
        {
            this.reader = reader;
        }

        /// <summary>Where slider and combo box fields are after a game update; as measured when null.</summary>
        public ControlFields? Controls { get; set; }

        /// <param name="Texture">Texture resource of image elements (GameLayout.Image), read with the base block.</param>
        public readonly record struct ElementInfo(IntPtr Address, UiElementBaseOffset Raw, string Name, IntPtr Texture = default, IntPtr AltTexture = default)
        {
            public bool IsVisibleFlag => UiElementBaseFuncs.IsVisibleChecker(this.Raw.Flags);

            public bool HasFlagBit(int bit) => (this.Raw.Flags & (1u << bit)) != 0;
        }

        /// <summary>Reads the base struct plus the control name; false unless the self pointer confirms an element.</summary>
        public bool TryReadElement(IntPtr address, out ElementInfo info)
        {
            info = default;
            var block = this.reader.ReadBytes(address, ElementBlockSize);
            if (block.Length == 0)
            {
                return false;
            }

            var raw = MemoryMarshal.Read<UiElementBaseOffset>(block);
            if (raw.Self != address && raw.Self != IntPtr.Zero)
            {
                return false;
            }

            var name = WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.NameOffset, MaxTextLength, allowEmpty: true) ?? string.Empty;
            var texture = block.Length >= GameLayout.Image.TextureOffset + IntPtr.Size
                ? MemoryMarshal.Read<IntPtr>(block.AsSpan(GameLayout.Image.TextureOffset))
                : IntPtr.Zero;
            var altTexture = block.Length >= GameLayout.Image.AltTextureOffset + IntPtr.Size
                ? MemoryMarshal.Read<IntPtr>(block.AsSpan(GameLayout.Image.AltTextureOffset))
                : IntPtr.Zero;
            info = new ElementInfo(address, raw, name, texture, altTexture);
            return true;
        }

        /// <summary>Strict test used on untrusted pointers: real elements always store their own address in Self.</summary>
        public bool IsElement(IntPtr address) =>
            this.reader.IsValidAddress(address) &&
            this.reader.TryRead<UiElementBaseOffset>(address, out var raw) &&
            raw.Self == address;

        public IntPtr[] ReadChildren(in ElementInfo info) =>
            this.reader.ReadStdVector<IntPtr>(info.Raw.ChildrensPtr, MaxChildrenPerNode);

        /// <summary>
        ///     Display text of a label / text-field element. Only the 0x700-byte text subclass has these
        ///     fields; on smaller elements the offsets fall into the next heap object, so the read is accepted
        ///     only when the tagged text and both stripped copies are all well-formed and the copies agree.
        /// </summary>
        public bool TryReadText(IntPtr address, out string text) => this.TryReadText(address, out text, out _);

        /// <summary>Same, also returning the text with markup (glyph tokens such as &lt;&lt;xbox_button_a&gt;&gt;).</summary>
        public bool TryReadText(IntPtr address, out string text, out string tagged)
        {
            text = string.Empty;
            tagged = string.Empty;
            var block = this.reader.ReadBytes(address, TextBlockSize);
            if (block.Length == 0)
            {
                return false;
            }

            var taggedText = WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.TextWithTagsOffset, MaxTextLength, allowEmpty: true);
            var stripped = WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.TextOffset, MaxTextLength, allowEmpty: true);
            var copy = WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.TextCopyOffset, MaxTextLength, allowEmpty: true);
            if (taggedText == null || stripped == null || copy == null || stripped != copy)
            {
                return false;
            }

            // The stripped copy leaves a gap where inline item art was ("Identificar   x 1"): the item is named there.
            text = ControllerGlyphs.HasUnnamedItemArt(taggedText) ? ControllerGlyphs.Plain(taggedText) : stripped.Trim();
            tagged = taggedText;
            return true;
        }

        /// <summary>True when the element holds a text too long for <see cref="TryReadText"/>; only its header is read.</summary>
        public bool IsLongText(IntPtr address) =>
            this.reader.TryRead<GameOffsets.Natives.StdWString>(address + GameLayout.Element.TextOffset, out var value) &&
            value.Length > MaxTextLength && WStringDecoder.LooksLikeString(value, MaxLongTextLength, allowEmpty: false);

        /// <summary>The whole text of an element holding a long one (<see cref="IsLongText"/>), markup stripped.</summary>
        public bool TryReadLongText(IntPtr address, out string text)
        {
            var block = this.reader.ReadBytes(address, TextBlockSize);
            text = block.Length == 0 ? string.Empty : WStringDecoder.TryDecode(this.reader, block, GameLayout.Element.TextOffset, MaxLongTextLength, allowEmpty: false) ?? string.Empty;
            return text.Length > MaxTextLength;
        }

        /// <summary>
        ///     The text as it is heard, as the screen watcher speaks it: glyphs the plain text drops are named in place
        ///     (the two "Encaje de objetos" options differ only by their stick and D-pad glyphs) and wrapped lines are
        ///     joined ("Detección automática de" / "método de entrada", one label on two lines).
        /// </summary>
        public bool TryReadSpokenText(IntPtr address, out string spoken)
        {
            spoken = string.Empty;
            if (!this.TryReadText(address, out var text, out var tagged))
            {
                return false;
            }

            spoken = text.Length == 0 ? text : Announcements.Flatten(ControllerGlyphs.FromMarkup(tagged).Length > 0 ? ControllerGlyphs.Render(tagged) : text);
            return true;
        }

        /// <summary>
        ///     A label drawn in the game's warning red (a price the player cannot pay); call only on elements whose text
        ///     read succeeded, the colour field belongs to the label subclass.
        /// </summary>
        public bool IsTextRed(IntPtr label) =>
            this.reader.TryRead<uint>(label + GameLayout.Element.TextColorOffset, out var argb) &&
            ((argb >> 16) & 0xFF) >= MinWarningRed && ((argb >> 8) & 0xFF) <= MaxWarningGreenBlue && (argb & 0xFF) <= MaxWarningGreenBlue;

        /// <summary>A label drawn grey (a heard dialogue topic); call only on elements whose text read succeeded.</summary>
        public bool IsTextGrey(IntPtr label)
        {
            if (!this.reader.TryRead<uint>(label + GameLayout.Element.TextColorOffset, out var argb))
            {
                return false;
            }

            int red = (int)((argb >> 16) & 0xFF), green = (int)((argb >> 8) & 0xFF), blue = (int)(argb & 0xFF);
            return red is >= (int)MinGrey and <= (int)MaxGrey && Math.Abs(red - green) <= GreyTolerance && Math.Abs(green - blue) <= GreyTolerance;
        }

        /// <summary>Art key of an image element, whichever image class it is.</summary>
        public string? ReadTextureKey(in ElementInfo info) => this.ReadTextureKey(info.Texture) ?? this.ReadTextureKey(info.AltTexture);

        public string? ReadTextureKey(IntPtr texture)
        {
            if (!this.reader.IsValidAddress(texture))
            {
                return null;
            }

            var key = this.ReadWStringStructAt(texture + GameLayout.Image.TextureKeyOffset);
            return key != null && key.StartsWith(GameLayout.Image.ArtPrefix, StringComparison.Ordinal) &&
                key.Contains(GameLayout.Image.TextureMarker, StringComparison.Ordinal) ? key : null;
        }

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
        public (int Value, int Min, int Max)? ReadSliderState(IntPtr address) =>
            this.SliderAt(address, this.Controls?.Shift(ControlGroup.Slider, address, shift => this.SliderAt(address, shift) is { } state && state.Value > state.Min) ?? 0);

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

        /// <summary>Reads a std::wstring struct located at an arbitrary address; null when malformed.</summary>
        public string? ReadWStringStructAt(IntPtr address)
        {
            var block = this.reader.ReadBytes(address, WStringDecoder.Size);
            return block.Length == 0 ? null : WStringDecoder.TryDecode(this.reader, block, 0, MaxTextLength, allowEmpty: true);
        }

        /// <summary>Reads a NUL-terminated UTF-16 buffer at an arbitrary address; null when not printable text.</summary>
        public string? ReadUtf16BufferAt(IntPtr address, int maxChars)
        {
            var chars = this.reader.ReadArray<char>(address, maxChars);
            if (chars.Length == 0)
            {
                return null;
            }

            var end = Array.IndexOf(chars, '\0');
            if (end <= 0)
            {
                return null;
            }

            for (var i = 0; i < end; i++)
            {
                if (char.IsControl(chars[i]) || char.IsSurrogate(chars[i]))
                {
                    return null;
                }
            }

            return new string(chars, 0, end);
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

        public bool IsReadable(IntPtr address) => this.reader.IsValidAddress(address) && this.reader.ReadBytes(address, IntPtr.Size).Length > 0;

        /// <summary>Decodes a std::vector&lt;std::wstring&gt;; entries that are not well-formed strings are skipped.</summary>
        public List<string> ReadStringVector(GameOffsets.Natives.StdVector vector)
        {
            var items = new List<string>();
            var bytes = this.reader.ReadStdVector<byte>(vector, MaxTextLength * WStringDecoder.Size);
            for (var offset = 0; offset + WStringDecoder.Size <= bytes.Length; offset += WStringDecoder.Size)
            {
                var text = WStringDecoder.TryDecode(this.reader, bytes, offset, MaxTextLength, allowEmpty: true);
                if (text != null)
                {
                    items.Add(text);
                }
            }

            return items;
        }

        /// <summary>Raw bytes of an object, chunked; the returned length is the readable prefix.</summary>
        public byte[] ReadBlock(IntPtr address, int bytes)
        {
            var result = new byte[bytes];
            var readable = 0;
            for (var chunk = 0; chunk < bytes; chunk += ChunkBytes)
            {
                var part = this.reader.ReadBytes(address + chunk, Math.Min(ChunkBytes, bytes - chunk));
                if (part.Length == 0)
                {
                    break;
                }

                Array.Copy(part, 0, result, chunk, part.Length);
                readable += part.Length;
            }

            return readable == bytes ? result : result[..readable];
        }
    }
}
