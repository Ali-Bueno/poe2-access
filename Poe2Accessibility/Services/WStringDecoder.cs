namespace Poe2Accessibility.Services
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameOffsets.Natives;

    /// <summary>
    ///     Decodes MSVC std::wstring fields found inside a memory block of the game (one shared
    ///     implementation for the research probe and the live element reader).
    ///     Layout (GameOffsets.Natives.StdWString): 16-byte inline buffer / heap pointer, 64-bit size, 64-bit
    ///     capacity. Capacity reads exactly 7 while the characters are inline; padding ints are always zero.
    /// </summary>
    public static class WStringDecoder
    {
        public const int InlineCapacity = 7;

        public static readonly int Size = Unsafe.SizeOf<StdWString>();

        private const int InlineChars = 2 * 8 / sizeof(char);

        /// <summary>Shape test only (no memory access). Empty strings are accepted when <paramref name="allowEmpty"/>.</summary>
        public static bool LooksLikeString(in StdWString value, int maxLength, bool allowEmpty)
        {
            if (value.PAD_14 != 0 || value.PAD_1C != 0 || value.Length < 0 || value.Length > maxLength)
            {
                return false;
            }

            if (value.Length == 0)
            {
                return allowEmpty && value.Capacity >= InlineCapacity;
            }

            return value.Capacity == InlineCapacity
                ? value.Length <= InlineCapacity
                : value.Capacity > InlineCapacity && value.Capacity >= value.Length;
        }

        /// <summary>
        ///     Reads the std::wstring at <paramref name="offset"/> of <paramref name="block"/>. Inline text is
        ///     decoded from the block; heap text costs one read. Both must be NUL-terminated. Null when malformed.
        /// </summary>
        public static string? TryDecode(HostMemoryReader reader, byte[] block, int offset, int maxLength, bool allowEmpty)
        {
            if (offset < 0 || offset + Size > block.Length)
            {
                return null;
            }

            var value = MemoryMarshal.Read<StdWString>(block.AsSpan(offset));
            if (!LooksLikeString(value, maxLength, allowEmpty))
            {
                return null;
            }

            if (value.Length == 0)
            {
                return string.Empty;
            }

            if (value.Capacity == InlineCapacity)
            {
                var chars = MemoryMarshal.Cast<byte, char>(block.AsSpan(offset, InlineChars * sizeof(char)));
                return chars[value.Length] == '\0' ? new string(chars[..value.Length]) : null;
            }

            if (!reader.IsValidAddress(value.Buffer))
            {
                return null;
            }

            var heap = reader.ReadArray<char>(value.Buffer, value.Length + 1);
            return heap.Length == value.Length + 1 && heap[value.Length] == '\0'
                ? new string(heap, 0, value.Length)
                : null;
        }

        public static bool IsHeap(byte[] block, int offset) =>
            offset + Size <= block.Length && MemoryMarshal.Read<StdWString>(block.AsSpan(offset)).Capacity != InlineCapacity;
    }
}
