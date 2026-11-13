namespace Poe2Accessibility.Services
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using GameOffsets.Objects.UiElement;
    using Poe2Accessibility.Models;

    /// <summary>Raw memory reading of <see cref="UiElementReader"/>: strings, pointers, buffers and blocks at an address.</summary>
    public sealed partial class UiElementReader
    {
        /// <summary>Reads a std::wstring struct located at an arbitrary address; null when malformed.</summary>
        public string? ReadWStringStructAt(IntPtr address)
        {
            // The decoder returns a string and keeps no bytes, so the same array serves every read of this thread.
            var block = wideStringScratch ??= new byte[WStringDecoder.Size];
            return this.reader.TryReadBytes(address, block) ? WStringDecoder.TryDecode(this.reader, block, 0, MaxTextLength, allowEmpty: true) : null;
        }

        /// <summary>A pointer stored at an address, false when unreadable or not an address of the game's.</summary>
        public bool TryReadPointer(IntPtr address, out IntPtr value) =>
            this.reader.TryRead(address, out value) && this.reader.IsValidAddress(value);

        /// <summary>The pointer-sized value stored at an address, whatever it is (null too); false when unreadable.</summary>
        public bool TryReadWord(IntPtr address, out IntPtr value) => this.reader.TryRead(address, out value);

        /// <summary>Bytes at an address, fewer than asked (none) when unreadable.</summary>
        public byte[] ReadBytesAt(IntPtr address, int count) =>
            count > 0 && this.reader.IsValidAddress(address) ? this.reader.ReadBytes(address, count) : Array.Empty<byte>();

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
