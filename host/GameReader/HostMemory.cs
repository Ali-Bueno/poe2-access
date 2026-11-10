// <copyright file="HostMemory.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader
{
    using System;
    using GameOffsets.Natives;
    using GameReader.Utils;

    /// <summary>
    ///     The host's read-only access to the game's memory, for the plugin: the same handle and checks the host reads with,
    ///     and struct reads that follow the host's remap (<see cref="StructRemap{T}"/>) when a patch moved their fields.
    /// </summary>
    public static class HostMemory
    {
        /// <summary>Whether <paramref name="address"/> could back an allocation of the game (a range check, no read).</summary>
        public static bool IsValidAddress(IntPtr address) => SafeMemoryHandle.IsValidAddress(address);

        /// <summary>Exactly <paramref name="count"/> bytes at <paramref name="address"/>; empty when no game is open or any byte cannot be read.</summary>
        public static byte[] ReadBytes(IntPtr address, int count) =>
            Handle() is { } handle ? handle.ReadMemoryArray<byte>(address, count) : Array.Empty<byte>();

        /// <summary>
        ///     Fills <paramref name="destination"/> from <paramref name="address"/>, with no array of its own; false when no game
        ///     is open or any byte cannot be read, and the destination then holds nothing to use.
        /// </summary>
        public static bool TryReadBytes(IntPtr address, Span<byte> destination) =>
            Handle() is { } handle && handle.TryReadArray(address, destination);

        /// <summary>Exactly <paramref name="count"/> elements at <paramref name="address"/>, as the game lays them out; empty when any cannot be read.</summary>
        public static T[] ReadArray<T>(IntPtr address, int count)
            where T : unmanaged
        {
            if (count <= 0 || Handle() is not { } handle)
            {
                return Array.Empty<T>();
            }

            var elements = new T[count];
            return handle.TryReadArray<T>(address, elements) ? elements : Array.Empty<T>();
        }

        /// <summary>A struct at <paramref name="address"/>, read as the host reads it; false when it cannot be read.</summary>
        public static bool TryRead<T>(IntPtr address, out T value)
            where T : unmanaged
        {
            if (Handle() is { } handle)
            {
                return handle.TryReadMemory(address, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        ///     The elements of a game vector, at most <paramref name="maxElements"/>, as the host reads them: one per element size
        ///     the game uses now, through the remap; empty when it cannot be read or holds more.
        /// </summary>
        public static T[] ReadStdVector<T>(StdVector vector, int maxElements)
            where T : unmanaged
        {
            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            return bytes / HostStructs.Stride<T>() > maxElements ? Array.Empty<T>() : Handle()?.ReadStdVector<T>(vector) ?? Array.Empty<T>();
        }

        /// <summary>
        ///     The same elements as <see cref="ReadStdVector{T}(StdVector, int)"/>, in the caller's <paramref name="buffer"/> (a
        ///     bigger one takes its place when they do not fit): how many were read, 0 where that returns an empty array.
        ///     What lies past them in the buffer is an earlier read's.
        /// </summary>
        public static int ReadStdVector<T>(StdVector vector, int maxElements, ref T[] buffer)
            where T : unmanaged
        {
            var stride = HostStructs.Stride<T>();
            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            if (bytes <= 0 || bytes % stride != 0 || bytes > SafeMemoryHandle.MaxVectorBytes || bytes / stride > maxElements || Handle() is not { } handle)
            {
                return 0;
            }

            if (!HostStructs.AsCompiled<T>())
            {
                // An element a patch changed is rebuilt from the game's bytes: the array that needs is the price of a healed layout.
                var rebuilt = handle.ReadStdVector<T>(vector);
                Fit(ref buffer, rebuilt.Length);
                rebuilt.CopyTo(buffer, 0);
                return rebuilt.Length;
            }

            var count = (int)(bytes / stride);
            Fit(ref buffer, count);
            return handle.TryReadArray(vector.First, buffer.AsSpan(0, count)) ? count : 0;
        }

        /// <summary>The text of a game string; empty when it cannot be read.</summary>
        public static string ReadStdWString(StdWString value) => Handle()?.ReadStdWString(value) ?? string.Empty;

        private static void Fit<T>(ref T[] buffer, int count)
        {
            if (buffer.Length < count)
            {
                buffer = new T[count];
            }
        }

        private static SafeMemoryHandle? Handle() => Core.Process.Handle is { IsClosed: false, IsInvalid: false } handle ? handle : null;
    }
}
