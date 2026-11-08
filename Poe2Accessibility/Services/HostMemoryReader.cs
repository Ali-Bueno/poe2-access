namespace Poe2Accessibility.Services
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameReader;
    using GameOffsets.Natives;

    /// <summary>
    ///     Read-only access to the game's memory through the host's own reader (<see cref="HostMemory"/>): one handle in the
    ///     process, and host structs read through the host's remap. Every method fails soft: a bad pointer yields false or
    ///     empty, never an exception.
    /// </summary>
    public sealed class HostMemoryReader
    {
        private readonly PluginLog log;

        public HostMemoryReader(PluginLog log)
        {
            this.log = log;
        }

        public bool IsValidAddress(IntPtr address) => HostMemory.IsValidAddress(address);

        /// <summary>Reads exactly <paramref name="count"/> bytes; empty array on any failure (host reads are all-or-nothing).</summary>
        public byte[] ReadBytes(IntPtr address, int count)
        {
            if (count <= 0 || !this.IsValidAddress(address))
            {
                return Array.Empty<byte>();
            }

            try
            {
                return HostMemory.ReadBytes(address, count);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                this.log.Debug($"ReadBytes at 0x{address.ToInt64():X} failed: {ex.GetType().Name}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>Reads a struct as the host does: through the host's remap when a game patch moved its fields.</summary>
        public bool TryRead<T>(IntPtr address, out T value)
            where T : unmanaged
        {
            try
            {
                return HostMemory.TryRead(address, out value);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                value = default;
                return false;
            }
        }

        public T[] ReadArray<T>(IntPtr address, int count)
            where T : unmanaged
        {
            if (count <= 0 || count > int.MaxValue / Unsafe.SizeOf<T>())
            {
                return Array.Empty<T>();
            }

            var bytes = this.ReadBytes(address, count * Unsafe.SizeOf<T>());
            return bytes.Length == 0 ? Array.Empty<T>() : MemoryMarshal.Cast<byte, T>(bytes).ToArray();
        }

        public T[] ReadStdVector<T>(StdVector vector, int maxElements)
            where T : unmanaged
        {
            // As the host reads it: an entry a patch grew or rearranged is read where the host's searches found its fields.
            try
            {
                return HostMemory.ReadStdVector<T>(vector, maxElements);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                return Array.Empty<T>();
            }
        }

        public string ReadStdWString(StdWString value)
        {
            try
            {
                return HostMemory.ReadStdWString(value);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                return string.Empty;
            }
        }
    }
}
