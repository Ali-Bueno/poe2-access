namespace GameReader.Utils
{
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    using GameOffsets.Natives;

    /// <summary>The game's strings decoded from bytes already read, without an array per string.</summary>
    public static class NativeText
    {
        /// <summary>The bytes a std::string or std::wstring keeps inside itself: its buffer pointer and the reserved word.</summary>
        public const int InlineBytes = 2 * sizeof(long);

        /// <summary>The text of a std::string short enough to live inside the struct; empty when its length does not fit there.</summary>
        public static string InlineAscii(in StdString value)
        {
            Span<byte> bytes = stackalloc byte[InlineBytes];
            Inline(value.Buffer, value.ReservedBytes, bytes);
            return value.Length > 0 && value.Length < InlineBytes ? Encoding.ASCII.GetString(bytes[..value.Length]) : string.Empty;
        }

        /// <summary>The text of a std::wstring short enough to live inside the struct; empty when its length does not fit there.</summary>
        public static string InlineWide(in StdWString value)
        {
            Span<byte> bytes = stackalloc byte[InlineBytes];
            Inline(value.Buffer, value.ReservedBytes, bytes);

            // Each half on its own, as they were always decoded: a surrogate pair across the two is two unknown characters.
            Span<char> chars = stackalloc char[InlineBytes];
            var count = Encoding.Unicode.GetChars(bytes[..sizeof(long)], chars);
            count += Encoding.Unicode.GetChars(bytes[sizeof(long)..], chars[count..]);
            return value.Length > 0 && value.Length < count ? new string(chars[..value.Length]) : string.Empty;
        }

        /// <summary>The ASCII text up to the first zero byte; empty when there is none or it comes first.</summary>
        public static string AsciiZ(ReadOnlySpan<byte> buffer)
        {
            var count = buffer.IndexOf((byte)0);
            return count > 0 ? Encoding.ASCII.GetString(buffer[..count]) : string.Empty;
        }

        /// <summary>The UTF-16 text up to its terminator (three zero bytes in a row); empty when there is none or it comes first.</summary>
        public static string WideZ(ReadOnlySpan<byte> buffer)
        {
            for (var i = 0; i < buffer.Length - 2; i++)
            {
                if (buffer[i] == 0 && buffer[i + 1] == 0 && buffer[i + 2] == 0)
                {
                    var count = i % 2 == 0 ? i : i + 1;
                    return count == 0 ? string.Empty : Encoding.Unicode.GetString(buffer[..count]);
                }
            }

            return string.Empty;
        }

        private static void Inline(IntPtr buffer, IntPtr reserved, Span<byte> bytes)
        {
            var first = buffer.ToInt64();
            var second = reserved.ToInt64();
            MemoryMarshal.Write(bytes, in first);
            MemoryMarshal.Write(bytes[sizeof(long)..], in second);
        }
    }
}
