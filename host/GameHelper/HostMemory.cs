// <copyright file="HostMemory.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper
{
    using System;
    using GameOffsets.Natives;
    using GameHelper.Utils;

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

        /// <summary>The text of a game string; empty when it cannot be read.</summary>
        public static string ReadStdWString(StdWString value) => Handle()?.ReadStdWString(value) ?? string.Empty;

        private static SafeMemoryHandle? Handle() => Core.Process.Handle is { IsClosed: false, IsInvalid: false } handle ? handle : null;
    }
}
