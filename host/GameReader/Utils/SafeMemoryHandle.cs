// <copyright file="SafeMemoryHandle.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameReader.Utils
{
    using System;
    using System.Buffers;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using GameOffsets.Natives;
    using Microsoft.Win32.SafeHandles;
    using ProcessMemoryUtilities.Managed;
    using ProcessMemoryUtilities.Native;

    /// <summary>
    ///     Handle to a process.
    /// </summary>
    internal class SafeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private static readonly ReadFailureLog FailedReads = new(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(30)); // a burst: the game closing, an area loading

        private static void WriteLeftOut()
        {
            if (FailedReads.TakeLeftOut() is > 0 and var count)
            {
                Console.WriteLine($"{count} more failed reads in the same burst left out");
            }
        }

        /// <summary>
        ///     Max valid user-mode address on 64-bit Windows (48-bit addressing).
        /// </summary>
        private const long MaxUserModeAddress = 0x7FFFFFFFFFFF;

        /// <summary>
        ///     Lowest address that can ever back a real allocation. The first 64 KiB is the
        ///     reserved null-pointer partition on Windows x64, and allocation granularity is
        ///     64 KiB, so anything below this is never a valid pointer. Game pointers live far
        ///     above this (module base 0x140000000+, heap in the TiB range), so this floor never
        ///     rejects a legitimate read — it only catches data values (floats, small ints,
        ///     string fragments) that a torn read fed in where a pointer was expected.
        /// </summary>
        private const long MinValidAddress = 0x10000;

        /// <summary>A std::vector holding more bytes than this is a torn read, not a vector.</summary>
        internal const long MaxVectorBytes = 50_000_000;

        /// <summary>A read this small goes to the stack; a bigger one to an array of the shared pool.</summary>
        private const int MaxStackBytes = 2048;

        /// <summary>The longest std::string or std::wstring read, in characters.</summary>
        private const int MaxStdStringLength = 1000;

        /// <summary>The bytes read for a string of unknown length: ASCII, then UTF-16.</summary>
        private const int AsciiZBytes = 128;
        private const int WideZBytes = 256;

        /// <summary>Gets the reads of the game's memory made so far (each a system call), by the type read: the host logs them per frame.</summary>
        internal static IEnumerable<KeyValuePair<string, long>> ReadsByType =>
            counters.Select(pair => new KeyValuePair<string, long>(pair.Key, Interlocked.Read(ref pair.Value.Value)));

        private static readonly ConcurrentDictionary<string, ReadCounter> counters = new();

        internal sealed class ReadCounter
        {
            public long Value;
        }

        /// <summary>One counter per type read, found without a lookup on the hot path.</summary>
        private static class ReadCount<T>
        {
            public static readonly ReadCounter Of = counters.GetOrAdd(typeof(T).Name, _ => new ReadCounter());
        }

        /// <summary>
        ///     Required by SafeHandle infrastructure for finalizer / marshaling support.
        ///     Private to prevent callers accidentally constructing a zombie handle
        ///     without a PID — see audit F-034. Real construction must go through
        ///     the <see cref="SafeMemoryHandle(int)"/> ctor.
        /// </summary>
        private SafeMemoryHandle()
            : base(true)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="SafeMemoryHandle" /> class.
        /// </summary>
        /// <param name="processId">processId you want to access.</param>
        internal SafeMemoryHandle(int processId)
            : base(true)
        {
            var handle = NativeWrapper.OpenProcess(ProcessAccessFlags.VirtualMemoryRead, processId);
            if (NativeWrapper.HasError)
            {
                Console.WriteLine($"Failed to open a new handle 0x{handle:X}" +
                                  $" due to ErrorNo: {NativeWrapper.LastError}");
            }
            else
            {
                Console.WriteLine($"Opened a new handle using IntPtr 0x{handle:X}");
            }

            this.SetHandle(handle);
        }

        /// <summary>
        ///     Reads the process memory as type T.
        /// </summary>
        /// <typeparam name="T">type of data structure to read.</typeparam>
        /// <param name="address">address to read the data from.</param>
        /// <returns>data from the process in T format.</returns>
        internal T ReadMemory<T>(IntPtr address)
            where T : unmanaged
        {
            if (this.TryReadMemory<T>(address, out var result))
            {
                return result;
            }

            // Only a genuine read failure on a plausible address warrants a console error;
            // an out-of-range address is silently skipped (historical behaviour). Both cases
            // are still captured by the diagnostics window via TryReadMemory.
            if (!this.IsInvalid && IsValidAddress(address) && FailedReads.Report(DateTime.UtcNow))
            {
                WriteLeftOut();
                Console.WriteLine("ERROR: Failed To Read the Memory (T)" +
                                  $" due to Error Number: 0x{NativeWrapper.LastError:X} on " +
                                  $"adress 0x{address.ToInt64():X} for type {typeof(T).Name}");
            }

            return default;
        }

        /// <summary>
        ///     Reads the process memory as type T without logging on failure. Returns false
        ///     (and <paramref name="result"/> = default) when the handle is invalid, the
        ///     address fails the <see cref="IsValidAddress"/> sanity check, or the underlying
        ///     read fails. Use this on hot, inherently-racy paths (e.g. walking a live,
        ///     concurrently-mutated container) where torn reads are expected and recoverable,
        ///     so they don't flood the log. Use <see cref="ReadMemory{T}"/> elsewhere so a
        ///     genuine offset/layout breakage still surfaces.
        /// </summary>
        /// <typeparam name="T">type of data structure to read.</typeparam>
        /// <param name="address">address to read the data from.</param>
        /// <param name="result">data read from the process, or default on failure.</param>
        /// <returns>true if the read succeeded; otherwise false.</returns>
        internal bool TryReadMemory<T>(IntPtr address, out T result)
            where T : unmanaged
        {
            result = default;
            if (this.IsInvalid || !IsValidAddress(address))
            {
                return false;
            }

            if (StructRemap<T>.Segments is { } moved)
            {
                return this.TryReadMoved(address, moved, out result);
            }

            try
            {
                Interlocked.Increment(ref ReadCount<T>.Of.Value);
                if (!NativeWrapper.ReadProcessMemory(this.handle, address, ref result))
                {
                    result = default;
                    return false;
                }

                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }

        /// <summary>Reads a struct whose fields a game patch moved (<see cref="StructRemap{T}"/>).</summary>
        private bool TryReadMoved<T>(IntPtr address, RemapSegment[] moved, out T result)
            where T : unmanaged
        {
            result = default;
            var target = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref result, 1));
            var size = StructRemapper.ReadSize(target.Length, moved);
            var rented = size > MaxStackBytes ? ArrayPool<byte>.Shared.Rent(size) : null;
            try
            {
                Span<byte> raw = rented == null ? stackalloc byte[size] : rented.AsSpan(0, size);
                if (!this.TryReadArray(address, raw))
                {
                    return false;
                }

                StructRemapper.Apply(raw, target, moved);
                return true;
            }
            finally
            {
                if (rented != null)
                {
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        ///     Cheap sanity check that an address could plausibly back a real allocation in
        ///     the target process. Rejects the reserved low memory range and addresses beyond
        ///     the 48-bit user-mode limit. Does not guarantee the address is currently mapped.
        /// </summary>
        /// <param name="address">address to check.</param>
        /// <returns>true if the address is within the plausible user-mode range.</returns>
        internal static bool IsValidAddress(IntPtr address)
        {
            var addr = address.ToInt64();
            return addr >= MinValidAddress && addr <= MaxUserModeAddress;
        }

        /// <summary>
        ///     Reads the std::vector into an array.
        /// </summary>
        /// <typeparam name="T">Object type to read.</typeparam>
        /// <param name="nativeContainer">StdVector address to read from.</param>
        /// <returns>An array of elements of type T.</returns>
        internal T[] ReadStdVector<T>(StdVector nativeContainer)
            where T : unmanaged
        {
            var typeSize = HostStructs.Stride<T>();
            var length = nativeContainer.Last.ToInt64() - nativeContainer.First.ToInt64();
            if (length <= 0 || length % typeSize != 0 || length > MaxVectorBytes)
            {
                return Array.Empty<T>();
            }

            // An entry a patch changed (a field moved, or the entry grew) is rebuilt from the game's bytes.
            return HostStructs.AsCompiled<T>()
                ? this.ReadMemoryArray<T>(nativeContainer.First, (int)length / typeSize)
                : HostStructs.ReadElements<T>(this.ReadMemoryArray<byte>(nativeContainer.First, (int)length));
        }

        /// <summary>
        ///     Reads the std::vector into an array of the shared pool, for a reader that is done with the elements before it
        ///     returns: as <see cref="ReadStdVector{T}"/>, with no elements where that returns an empty array.
        /// </summary>
        internal PooledArray<T> RentStdVector<T>(StdVector nativeContainer)
            where T : unmanaged
        {
            var typeSize = HostStructs.Stride<T>();
            var length = nativeContainer.Last.ToInt64() - nativeContainer.First.ToInt64();
            if (length <= 0 || length % typeSize != 0 || length > MaxVectorBytes)
            {
                return default;
            }

            if (!HostStructs.AsCompiled<T>())
            {
                var rebuilt = this.ReadStdVector<T>(nativeContainer);
                return new PooledArray<T>(rebuilt, rebuilt.Length, false);
            }

            var count = (int)length / typeSize;
            var array = ArrayPool<T>.Shared.Rent(count);
            if (this.TryReadArray(nativeContainer.First, array.AsSpan(0, count)))
            {
                return new PooledArray<T>(array, count, true);
            }

            ArrayPool<T>.Shared.Return(array);
            return default;
        }

        /// <summary>
        ///     Fills <paramref name="destination"/> from the game's memory, with no array of its own. All or nothing, as
        ///     <see cref="ReadMemoryArray{T}"/>: false on a short or failed read, and the destination then holds nothing to use.
        /// </summary>
        internal unsafe bool TryReadArray<T>(IntPtr address, Span<T> destination)
            where T : unmanaged
        {
            if (this.IsInvalid || !IsValidAddress(address) || destination.IsEmpty)
            {
                return false;
            }

            try
            {
                var bytes = (long)destination.Length * sizeof(T);
                Interlocked.Increment(ref ReadCount<T[]>.Of.Value);
                fixed (T* target = destination)
                {
                    return NativeWrapper.ReadProcessMemory(this.handle, address, (IntPtr)target, (IntPtr)bytes, out var read) &&
                           read.ToInt64() >= bytes;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Reads the process memory as an array.
        /// </summary>
        /// <typeparam name="T">Array type to read.</typeparam>
        /// <param name="address">memory address to read from.</param>
        /// <param name="nsize">total array elements to read.</param>
        /// <returns>
        ///     An array of type T and of size nsize. In case or any error it returns empty array.
        /// </returns>
        internal T[] ReadMemoryArray<T>(IntPtr address, int nsize)
            where T : unmanaged
        {
            if (this.IsInvalid || !IsValidAddress(address) || nsize <= 0)
            {
                return Array.Empty<T>();
            }

            var buffer = new T[nsize];
            try
            {
                // Array/blob/string reads are inherently speculative — the source pointer comes
                // from a container (std::vector/string) that may be torn or stale, so failures
                // and short reads are routine and recoverable. Record them for the diagnostics
                // window but keep the console clean, matching TryReadMemory (audit: torn-read noise).
                var expectedBytes = (long)nsize * Marshal.SizeOf<T>();
                Interlocked.Increment(ref ReadCount<T[]>.Of.Value);
                if (!NativeWrapper.ReadProcessMemoryArray(this.handle, address, buffer, out var numBytesRead) ||
                    numBytesRead.ToInt64() < expectedBytes)
                {
                    return Array.Empty<T>();
                }

                return buffer;
            }
            catch
            {
                return Array.Empty<T>();
            }
        }

        /// <summary>
        ///     Reads the std::string. String read is in ASCII format.
        /// </summary>
        /// <param name="nativecontainer">native object of std::string.</param>
        /// <returns>string.</returns>
        internal string ReadStdString(StdString nativecontainer)
        {
            if (nativecontainer.Length <= 0 ||
                nativecontainer.Length > MaxStdStringLength ||
                nativecontainer.Capacity <= 0 ||
                nativecontainer.Capacity > MaxStdStringLength)
            {
                return string.Empty;
            }

            if (nativecontainer.Capacity <= 15)
            {
                return NativeText.InlineAscii(nativecontainer);
            }

            Span<byte> buffer = stackalloc byte[nativecontainer.Length];
            return this.TryReadArray(nativecontainer.Buffer, buffer) ? Encoding.ASCII.GetString(buffer) : string.Empty;
        }

        /// <summary>
        ///     Reads the std::wstring. String read is in unicode format.
        /// </summary>
        /// <param name="nativecontainer">native object of std::wstring.</param>
        /// <returns>string.</returns>
        internal string ReadStdWString(StdWString nativecontainer)
        {
            if (nativecontainer.Length <= 0 ||
                nativecontainer.Length > MaxStdStringLength ||
                nativecontainer.Capacity <= 0 ||
                nativecontainer.Capacity > MaxStdStringLength)
            {
                return string.Empty;
            }

            if (nativecontainer.Capacity <= 8)
            {
                return NativeText.InlineWide(nativecontainer);
            }

            Span<byte> buffer = stackalloc byte[nativecontainer.Length * 2];
            return this.TryReadArray(nativecontainer.Buffer, buffer) ? Encoding.Unicode.GetString(buffer) : string.Empty;
        }

        /// <summary>
        ///     Reads the string.
        /// </summary>
        /// <param name="address">pointer to the string.</param>
        /// <returns>string read.</returns>
        internal string ReadString(IntPtr address)
        {
            Span<byte> buffer = stackalloc byte[AsciiZBytes];
            return this.TryReadArray(address, buffer) ? NativeText.AsciiZ(buffer) : string.Empty;
        }

        /// <summary>
        ///     Reads Unicode string when string length isn't know.
        ///     Use  <see cref="ReadStdWString" /> if string length is known.
        /// </summary>
        /// <param name="address">points to the Unicode string pointer.</param>
        /// <returns>string read from the memory.</returns>
        internal string ReadUnicodeString(IntPtr address)
        {
            Span<byte> buffer = stackalloc byte[WideZBytes];
            return this.TryReadArray(address, buffer) ? NativeText.WideZ(buffer) : string.Empty;
        }

        /// <summary>
        ///     Walks a std::map, calling <paramref name="onEachNotNullNode"/> on each node read (concurrently for big maps); with
        ///     <paramref name="keyLimit"/> the subtrees holding only keys at or above it may be left unread (<see cref="StdMapWalk{TKey, TValue}"/>).
        /// </summary>
        /// <returns>the nodes read, and whether the walk reached all of them.</returns>
        internal StdMapWalkResult ReadStdMap<TKey, TValue>(
            StdMap nativeContainer,
            int maxSizeAllowed,
            Func<TKey, TValue, bool> onEachNotNullNode,
            StdMapKeyLimit<TKey>? keyLimit = null)
            where TKey : unmanaged
            where TValue : unmanaged
        {
            return StdMapWalk<TKey, TValue>.Run(
                nativeContainer,
                maxSizeAllowed,
                this.TryReadMemory<StdMapNode<TKey, TValue>>,
                onEachNotNullNode,
                keyLimit,
                Core.GHSettings.EntityReaderMaxDegreeOfParallelism);
        }

        /// <summary>
        ///     Reads the std::bucket into a array.
        /// </summary>
        /// <typeparam name="TValue">value type that the std bucket contains.</typeparam>
        /// <param name="nativeContainer">native object of the std::bucket.</param>
        /// <returns>a array containing all the valid values found in std::bucket.</returns>
        internal TValue[] ReadStdBucket<TValue>(StdBucket nativeContainer)
            where TValue : unmanaged
        {
            if (nativeContainer.Data.First == IntPtr.Zero ||
                nativeContainer.Capacity <= 0x00)
            {
                return Array.Empty<TValue>();
            }

            return this.ReadStdVector<TValue>(nativeContainer.Data);
        }

        /// <summary>
        ///     When overridden in a derived class, executes the code required to free the handle.
        /// </summary>
        /// <returns>
        ///     true if the handle is released successfully; otherwise, in the event of a catastrophic failure, false.
        ///     In this case, it generates a releaseHandleFailed MDA Managed Debugging Assistant.
        /// </returns>
        protected override bool ReleaseHandle()
        {
            WriteLeftOut();
            Console.WriteLine($"Releasing handle on 0x{this.handle:X}\n");
            return NativeWrapper.CloseHandle(this.handle);
        }
    }
}
