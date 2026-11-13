namespace Poe2Accessibility.Services
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.UiElement;
    using Poe2Accessibility.Models;

    /// <summary>Text and texture reading of <see cref="UiElementReader"/>: labels, long and spoken text, colours and texture keys.</summary>
    public sealed partial class UiElementReader
    {
        /// <summary>
        ///     Display text of a label / text-field element. Only the 0x700-byte text subclass has these
        ///     fields; on smaller elements the offsets fall into the next heap object, so the read is accepted
        ///     only when the tagged text and both stripped copies are all well-formed and the copies agree.
        /// </summary>
        public bool TryReadText(IntPtr address, out string text) => this.TryReadText(address, out text, out _);

        /// <summary>Same, also returning the text with markup (glyph tokens such as &lt;&lt;xbox_button_a&gt;&gt;).</summary>
        public bool TryReadText(IntPtr address, out string text, out string tagged)
        {
            var block = this.reader.ReadBytes(address, TextBlockSize);
            return this.DecodeText(block, out text, out tagged);
        }

        /// <summary>Same, from the bytes <see cref="TryReadElement"/> already read when they cover the text fields.</summary>
        public bool TryReadText(in ElementInfo info, out string text, out string tagged) =>
            info.Block is { } block && block.Length >= TextBlockSize
                ? this.DecodeText(block, out text, out tagged)
                : this.TryReadText(info.Address, out text, out tagged);

        private bool DecodeText(byte[] block, out string text, out string tagged)
        {
            text = string.Empty;
            tagged = string.Empty;
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

            // The stripped copy leaves a gap where inline item art was ("Identificar   x 1"): the item is named there; so is
            // a picture that means a word ("<<village_gold>> 500" → "500 oro", InlineIcons).
            text = ControllerGlyphs.HasUnnamedItemArt(taggedText) || InlineIcons.HasNamed(taggedText) ? ControllerGlyphs.Plain(taggedText) : stripped.Trim();
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

        /// <summary>The texture's key whatever it is (a downloaded shop image's file path too), or null.</summary>
        public string? ReadAnyTextureKey(in ElementInfo info)
        {
            foreach (var texture in new[] { info.Texture, info.AltTexture })
            {
                if (this.reader.IsValidAddress(texture) && this.ReadWStringStructAt(texture + GameLayout.Image.TextureKeyOffset) is { Length: > 0 } key &&
                    key.Contains('/', StringComparison.Ordinal))
                {
                    return key;
                }
            }

            return null;
        }

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
    }
}
