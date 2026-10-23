namespace Poe2Accessibility.Services
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using GameHelper;
    using GameOffsets.Natives;

    /// <summary>
    ///     Read-only access to the game's memory through GameHelper2's own reader
    ///     (GameProcess.Handle : SafeMemoryHandle, both internal). Reflection keeps a single memory path
    ///     in the process (no second ReadProcessMemory handle), the pattern StashUtility validated in production.
    ///     Reflection cost is paid once per handle instance (delegates are cached); after that every read is
    ///     a direct call. Every method fails soft: a bad pointer yields false / empty, never an exception.
    /// </summary>
    public sealed class HostMemoryReader
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private readonly PluginLog log;
        private readonly PropertyInfo handleProperty;
        private readonly Func<GameProcess, object?> getHandle;
        private readonly MethodInfo readBytesMethod;
        private readonly MethodInfo readStdWStringMethod;
        private readonly Func<IntPtr, bool> isValidAddress;

        private object? boundHandle;
        private ReadBytesFn? readBytes;
        private ReadWStringFn? readWString;

        private delegate byte[] ReadBytesFn(IntPtr address, int count);

        private delegate string ReadWStringFn(StdWString value);

        public HostMemoryReader(PluginLog log)
        {
            this.log = log;
            this.handleProperty = Require(typeof(GameProcess).GetProperty("Handle", AnyInstance), "GameProcess.Handle");
            this.getHandle = Require(this.handleProperty.GetGetMethod(true), "GameProcess.Handle getter").CreateDelegate<Func<GameProcess, object?>>();
            var handleType = this.handleProperty.PropertyType;
            this.readBytesMethod = Require(handleType.GetMethod("ReadMemoryArray", AnyInstance), "SafeMemoryHandle.ReadMemoryArray")
                .MakeGenericMethod(typeof(byte));
            this.readStdWStringMethod = Require(handleType.GetMethod("ReadStdWString", AnyInstance), "SafeMemoryHandle.ReadStdWString");
            var isValid = Require(handleType.GetMethod("IsValidAddress", AnyStatic), "SafeMemoryHandle.IsValidAddress");
            this.isValidAddress = isValid.CreateDelegate<Func<IntPtr, bool>>();
        }

        public bool IsValidAddress(IntPtr address) => this.isValidAddress(address);

        /// <summary>Reads exactly <paramref name="count"/> bytes; empty array on any failure (host reads are all-or-nothing).</summary>
        public byte[] ReadBytes(IntPtr address, int count)
        {
            if (count <= 0 || !this.IsValidAddress(address) || !this.EnsureBound())
            {
                return Array.Empty<byte>();
            }

            try
            {
                return this.readBytes!(address, count);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                this.log.Debug($"ReadBytes at 0x{address.ToInt64():X} failed: {ex.GetType().Name}");
                return Array.Empty<byte>();
            }
        }

        public bool TryRead<T>(IntPtr address, out T value)
            where T : unmanaged
        {
            var bytes = this.ReadBytes(address, Unsafe.SizeOf<T>());
            if (bytes.Length == 0)
            {
                value = default;
                return false;
            }

            value = MemoryMarshal.Read<T>(bytes);
            return true;
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
            var elementSize = Unsafe.SizeOf<T>();
            var bytes = vector.Last.ToInt64() - vector.First.ToInt64();
            if (bytes <= 0 || bytes % elementSize != 0)
            {
                return Array.Empty<T>();
            }

            var count = bytes / elementSize;
            return count > maxElements ? Array.Empty<T>() : this.ReadArray<T>(vector.First, (int)count);
        }

        public string ReadStdWString(StdWString value)
        {
            if (!this.EnsureBound())
            {
                return string.Empty;
            }

            try
            {
                return this.readWString!(value) ?? string.Empty;
            }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException)
            {
                return string.Empty;
            }
        }

        /// <summary>Binds the read delegates to the host's current handle; false when no usable handle exists.</summary>
        private bool EnsureBound()
        {
            var handle = this.getHandle(Core.Process);
            if (handle is not SafeHandle safeHandle || safeHandle.IsClosed || safeHandle.IsInvalid)
            {
                this.boundHandle = null;
                return false;
            }

            if (!ReferenceEquals(handle, this.boundHandle))
            {
                this.readBytes = this.readBytesMethod.CreateDelegate<ReadBytesFn>(handle);
                this.readWString = this.readStdWStringMethod.CreateDelegate<ReadWStringFn>(handle);
                this.boundHandle = handle;
                this.log.Debug("Memory reader bound to a new host handle");
            }

            return true;
        }

        private static T Require<T>(T? member, string name)
            where T : MemberInfo =>
            member ?? throw new MissingMemberException($"GameHelper2 internal member not found: {name} (host version changed?)");
    }
}
